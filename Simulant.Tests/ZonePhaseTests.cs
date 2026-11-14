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
            var pending = application.ApplyAsync(_ => prepared++, _ => applied++, _ => false);
            application.Cancel();
            Assert(pending.Wait(1000) && !pending.Result,
                "退出或世界失效应立即结束阶段等待，并返回取消结果。");
            Assert(prepared == 1 && applied == 0, "取消后的阶段不能写入新世界的环境。");

            Assert(application.ApplyAsync(_ => { }, _ => applied++, _ => true).GetAwaiter().GetResult(),
                "取消结束后应允许应用新阶段。");

            Assert(applied == 1, "新阶段应只应用一次。");
        }

        public static void PhaseReplacementCannotResumeStaleApplication()
        {
            var delay = new ControlledDelay();
            var application = new PhaseApplication(delay.Wait);
            var oldWrites = 0;
            var newWrites = 0;
            var probes = 0;
            var old = application.ApplyAsync(_ => { }, _ => oldWrites++, _ =>
            {
                probes++;
                return false;
            });
            var replacement = application.ApplyAsync(_ => { }, _ => newWrites++, _ => true);
            delay.Complete(0); // 模拟旧延迟已经完成、恢复执行晚于取消的情况。
            Assert(!old.GetAwaiter().GetResult() && replacement.GetAwaiter().GetResult(),
                "新阶段应替换旧阶段，而不是与旧等待同时生效。");
            Assert(oldWrites == 0 && newWrites == 1, "恢复后的旧阶段不能覆盖新阶段。");
            Assert(probes == 1, "替换后连旧地图的就绪检查也不能继续执行。");
        }

        public static void PhaseWaitsUntilReadyAndAppliesOnlyOnce()
        {
            var delay = new ControlledDelay();
            var application = new PhaseApplication(delay.Wait);
            var writes = 0;
            var ready = false;
            var pending = application.ApplyAsync(_ => { }, _ => writes++, _ => ready);
            delay.Complete(0);
            Assert(writes == 0 && delay.Count == 2, "地图尚未就绪时只能继续检查，不能提前设置环境。");
            ready = true;
            delay.Complete(1);
            Assert(pending.GetAwaiter().GetResult() && writes == 1 && delay.Count == 2,
                "地图就绪后应立即设置一次环境，不得再定时重发。");
        }

        public static void ReadyPhaseDoesNotWait()
        {
            var application = new PhaseApplication((_, token) =>
            {
                throw new Exception("就绪后不应等待。");
            });
            var prepared = false;
            var applied = 0;
            var pending = application.ApplyAsync(_ => prepared = true, _ => applied++, _ =>
            {
                Assert(prepared, "出生点设置应在等待地图就绪之前完成。");
                return true;
            });
            Assert(pending.IsCompleted && pending.Result && applied == 1,
                "已就绪的地图切换阶段应立即生效，只提交一次。");
        }

        public static void CancellationDuringReadyCheckPreventsApplication()
        {
            var application = new PhaseApplication();
            var writes = 0;
            var pending = application.ApplyAsync(_ => { }, _ => writes++, _ =>
            {
                application.Cancel();
                return true;
            });
            Assert(!pending.GetAwaiter().GetResult() && writes == 0,
                "就绪检查期间退出，不能因为检查返回 true 就继续写入旧阶段。");
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
                var ready = false;
                var pending = application.ApplyAsync(_ => { }, _ =>
                {
                    Assert(Thread.CurrentThread.ManagedThreadId == callerThread,
                        "延迟结束后的原生操作必须留在调用方同步上下文。");
                    writes++;
                }, _ => ready);
                ready = true;
                Task.Run(() => delay.Complete(0)).GetAwaiter().GetResult();
                context.PumpOne();
                Assert(pending.GetAwaiter().GetResult() && writes == 1,
                    "正常阶段应在同一上下文完成一次环境应用。");

                var expected = new InvalidOperationException("模拟环境写入失败");
                var failed = application.ApplyAsync(_ => { }, _ =>
                {
                    throw expected;
                }, _ => true);

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

                Assert(application.ApplyAsync(_ => { }, _ => { }, _ => true).GetAwaiter().GetResult(),
                    "失败的阶段不应阻止后续重试。");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
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
