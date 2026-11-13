using Simulant.Core.Zone;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Simulant.Tests
{
    internal static class ZonePhaseTests
    {
        public static void PhaseCancellationStopsPendingApplication()
        {
            var application = new PhaseApplication((_, token) => Task.Delay(Timeout.Infinite, token));
            var prepared = 0;
            var applied = 0;
            var pending = application.ApplyAsync(_ => prepared++, _ => applied++, true);
            application.Cancel();
            Assert(pending.Wait(1000) && !pending.Result,
                "退出或世界失效应立即结束阶段等待，并返回取消结果。");
            Assert(prepared == 1 && applied == 0, "取消后的阶段不能写入新世界的环境。");
            Assert(application.ApplyAsync(_ => { }, _ => applied++, false).GetAwaiter().GetResult(),
                "取消结束后应允许应用新阶段。");
            Assert(applied == 1, "新阶段应只应用一次。");
        }

        public static void PhaseReplacementCannotResumeStaleApplication()
        {
            var delay = new ControlledDelay();
            var application = new PhaseApplication(delay.Wait);
            var oldWrites = 0;
            var newWrites = 0;
            var old = application.ApplyAsync(_ => { }, _ => oldWrites++, true);
            var replacement = application.ApplyAsync(_ => { }, _ => newWrites++, false);
            delay.Complete(0); // 模拟旧延迟已经完成、恢复执行晚于取消的情况。
            Assert(!old.GetAwaiter().GetResult() && replacement.GetAwaiter().GetResult(),
                "新阶段应替换旧阶段，而不是与旧等待同时生效。");
            Assert(oldWrites == 0 && newWrites == 1, "恢复后的旧阶段不能覆盖新阶段。");
        }

        public static void PhaseCancellationBetweenPassesPreventsSecondWrite()
        {
            var delay = new ControlledDelay();
            var application = new PhaseApplication(delay.Wait);
            var writes = 0;
            var pending = application.ApplyAsync(_ => { }, _ => writes++, true);
            delay.Complete(0);
            Assert(writes == 1 && delay.Count == 2, "第一次应用完成后应等待第二次应用。");
            application.Cancel();
            delay.Complete(1);
            Assert(!pending.GetAwaiter().GetResult() && writes == 1,
                "两次应用之间退出时，不得执行第二次环境写入。");
        }

        public static void PhaseApplicationRetainsCallingContextAndRecoversFromFailure()
        {
            var previousContext = SynchronizationContext.Current;
            var context = new PumpContext();
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                var callerThread = Thread.CurrentThread.ManagedThreadId;
                var delay = new ControlledDelay();
                var application = new PhaseApplication(delay.Wait);
                var writes = 0;
                var pending = application.ApplyAsync(_ => { }, _ =>
                {
                    Assert(Thread.CurrentThread.ManagedThreadId == callerThread,
                        "延迟结束后的原生操作必须留在调用方同步上下文。");
                    writes++;
                }, true);
                Task.Run(() => delay.Complete(0)).GetAwaiter().GetResult();
                context.PumpOne();
                Task.Run(() => delay.Complete(1)).GetAwaiter().GetResult();
                context.PumpOne();
                Assert(pending.GetAwaiter().GetResult() && writes == 2,
                    "正常阶段应在同一上下文完成两次环境应用。");

                var expected = new InvalidOperationException("模拟环境写入失败");
                var failed = application.ApplyAsync(_ => { }, _ => { throw expected; }, false);
                try
                {
                    failed.GetAwaiter().GetResult();
                    throw new Exception("阶段写入异常不应被当作取消吞掉。");
                }
                catch (InvalidOperationException actual)
                {
                    Assert(ReferenceEquals(actual, expected), "应保留原始环境写入错误。");
                }
                application.Cancel();
                Assert(application.ApplyAsync(_ => { }, _ => { }, false).GetAwaiter().GetResult(),
                    "失败的阶段不应阻止后续重试。");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private sealed class ControlledDelay
        {
            private readonly List<TaskCompletionSource<bool>> _waits = new List<TaskCompletionSource<bool>>();
            internal int Count => _waits.Count;

            internal Task Wait(int milliseconds, CancellationToken token)
            {
                var completion = new TaskCompletionSource<bool>();
                _waits.Add(completion);
                return completion.Task;
            }

            internal void Complete(int index) => _waits[index].SetResult(true);
        }

        private sealed class PumpContext : SynchronizationContext
        {
            private readonly ConcurrentQueue<Action> _callbacks = new ConcurrentQueue<Action>();

            public override void Post(SendOrPostCallback callback, object state)
                => _callbacks.Enqueue(() => callback(state));

            internal void PumpOne()
            {
                Assert(_callbacks.TryDequeue(out var callback), "延迟完成后应回到调用方上下文。");
                callback();
            }
        }
    }
}
