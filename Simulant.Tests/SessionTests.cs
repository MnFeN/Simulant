using Simulant.ACT;
using Simulant.Core;
using Simulant.Simulation;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Simulant.Tests
{
    // These tests compile the real SimSession against the minimal fakes below. They exercise its
    // lifecycle and thread ordering without loading ACT, touching UI controls, or accessing game memory.
    internal static class SessionTests
    {
        private const int TimeoutMs = 5000;

        internal static void SessionsUseIndependentSnapshotsOfGlobalBehavior()
        {
            var host = new PluginHost();
            host.Configuration.SimulatedParty.AttackTarget = false;
            host.Configuration.SimulatedParty.IdleRadius = 1.7;
            using (var firstProbe = new SessionProbe())
            using (var secondProbe = new SessionProbe())
            using (var first = CreateSession(host, firstProbe))
            using (var second = CreateSession(host, secondProbe))
            {
                first.Start();
                second.Start();
                var firstOptions = firstProbe.Logic.EntityManager.PlayerBehavior;
                var secondOptions = secondProbe.Logic.EntityManager.PlayerBehavior;
                Assert(!firstOptions.AttackTarget && !secondOptions.AttackTarget && firstOptions.IdleRadius == 1.7 && secondOptions.IdleRadius == 1.7,
                    "不同预设的会话应读取同一份全局行为设置。");
                Assert(!ReferenceEquals(firstOptions, secondOptions) && !ReferenceEquals(firstOptions, host.Configuration.SimulatedParty),
                    "各会话必须保留独立快照，不能共享可变行为对象。");
                host.Configuration.SimulatedParty.IdleRadius = 2.1;
                Assert(firstOptions.IdleRadius == 1.7 && secondOptions.IdleRadius == 1.7, "更新全局设置不能改变已经开始的模拟。");
                first.Stop();
                first.Start();
                Assert(firstProbe.Logic.EntityManager.PlayerBehavior.IdleRadius == 2.1, "再次开始模拟应读取新保存的设置。");
            }
        }

        internal static void SessionFailurePreservesSceneUntilExplicitStop()
        {
            using (var probe = new SessionProbe())
            using (var displaying = new ManualResetEventSlim())
            {
                var host = new PluginHost();
                var laterNodes = 0;
                SimActor actor = null;
                probe.StartAction = () =>
                {
                    actor = probe.Logic.EntityManager.SpawnBNpc(1);
                    probe.Logic.Scheduler.ScheduleAfter(0, () => { throw new SimMechanicFailedException("塔内人数错误。"); });
                    probe.Logic.Scheduler.ScheduleAfter(0.05, () => laterNodes++);
                };
                using (var session = CreateSession(host, probe))
                {
                    TriggernometryInterop.OnChatCommand = _ => displaying.Set();
                    session.Start();
                    Wait(displaying, "机制失败的聊天提示");
                    Assert(!session.IsRunning && session.HasPendingCleanup && actor.Entity.IsAlive && probe.StopCount == 0,
                        "异常后应取消战斗时间轴，但保留实体让已提交的技能继续演出。");
                    Assert(probe.Logic.Scheduler.PendingCount == 0, "保留演出时不得继续执行后续战斗节点。");
                    Assert(TriggernometryInterop.ChatCommands.Count == 1 &&
                        TriggernometryInterop.ChatCommands[0] == "/e <se.11>\n塔内人数错误。",
                        "机制失败应通过 command 回调发送一条包含提示音、换行和原始原因的消息。");
                    Assert(!probe.Cleaned.Wait(150) && host.EntitySpawner.Deleted.Count == 0,
                        "中断后不得自动清场，必须等待显式停止。");
                    session.Stop();
                    Assert(laterNodes == 0 && probe.StopCount == 1 && !actor.Entity.IsAlive && !session.HasPendingCleanup,
                        "手动停止后必须只清理一次，期间不能放行后续技能。");
                }
            }
        }

        internal static void SessionExplicitStopClearsInterruptedScene()
            => RunBoundedSessionScenario("显式停止清理中断现场", () =>
            {
                foreach (var dispose in new[] { false, true })
                using (var probe = new SessionProbe())
                using (var displaying = new ManualResetEventSlim())
                {
                    var host = new PluginHost();
                    probe.StartAction = () =>
                    {
                        probe.Logic.EntityManager.SpawnBNpc(1);
                        throw new InvalidOperationException("演出中的异常。");
                    };
                    using (var session = CreateSession(host, probe))
                    {
                        TriggernometryInterop.OnChatCommand = _ => displaying.Set();
                        session.Start();
                        Wait(displaying, "等待显式停止的中断现场");
                        var elapsed = System.Diagnostics.Stopwatch.StartNew();
                        if (dispose) session.Dispose();
                        else session.Stop();
                        Assert(elapsed.Elapsed.TotalSeconds < 2,
                            "手动停止或禁用必须及时完成清理。");
                        Assert(probe.StopCount == 1 && !session.HasPendingCleanup && host.EntitySpawner.Deleted.Count == 1,
                            "显式停止后必须完成一次实体回收。");
                    }
                }
            });

        internal static void SessionInterruptedCleanupCanRetry()
        {
            using (var probe = new SessionProbe())
            using (var reported = new ManualResetEventSlim())
            {
                var host = new PluginHost();
                var failDeletion = true;
                host.EntitySpawner.BeforeDelete = actor =>
                {
                    if (failDeletion) throw new InvalidOperationException("暂时无法回收。");
                };
                probe.StartAction = () =>
                {
                    probe.Logic.EntityManager.SpawnBNpc(1);
                    throw new InvalidOperationException("技能结算异常。");
                };
                using (var session = CreateSession(host, probe))
                {
                    TriggernometryInterop.OnChatCommand = _ => reported.Set();
                    session.Start();
                    Wait(reported, "异常中断的聊天提示");
                    session.Stop();
                    Assert(session.HasPendingCleanup && host.EntitySpawner.Deleted.Count == 0,
                        "手动清理失败仍须保留实体及清理责任。");
                    failDeletion = false;
                    session.Stop();
                    Assert(!session.HasPendingCleanup && host.EntitySpawner.Deleted.Count == 1 && probe.StopCount == 1,
                        "再次停止只重试未完成回收，不能重复预设的停止逻辑。");
                }
            }
        }

        internal static void SessionPrepareFailureRollsBack()
        {
            using (var probe = new SessionProbe())
            {
                var expected = new InvalidOperationException("option preparation failed");
                probe.PrepareAction = () => { throw expected; };
                var host = new PluginHost();
                var callerThread = Thread.CurrentThread.ManagedThreadId;
                using (var session = CreateSession(host, probe))
                {
                    var actual = Throws<InvalidOperationException>(session.Start);
                    Assert(ReferenceEquals(actual, expected), "Start must propagate the original option error.");
                    Assert(probe.PrepareCount == 1 && probe.PrepareThreadId == callerThread,
                        "Options must be prepared once on the calling thread.");
                    Assert(probe.StartCount == 0, "Failed option preparation must not run initialization.");
                    Assert(probe.StopCount == 1 && probe.ClearCount == 1,
                        "Failed preparation must roll back exactly once.");
                    Assert(!session.IsRunning && probe.Logic.Scheduler.PendingCount == 0,
                        "A failed start must leave no running or queued work.");
                    Throws<ObjectDisposedException>(() => probe.Logic.Scheduler.Schedule(0, () => { }));
                    session.Stop();
                    Assert(probe.StopCount == 1 && host.Errors.IsEmpty,
                        "Stopping a rolled-back session must not repeat cleanup or report a worker error.");
                }
                Assert(probe.StopCount == 1, "Dispose must not repeat preparation rollback.");
            }
        }

        internal static void SessionInitializationFailureWaitsForExplicitCleanup()
        {
            using (var probe = new SessionProbe())
            using (var reported = new ManualResetEventSlim())
            {
                var host = new PluginHost();
                var callerThread = Thread.CurrentThread.ManagedThreadId;
                probe.StartAction = () => { throw new InvalidOperationException("initialization failed"); };
                using (var session = CreateSession(host, probe))
                {
                    TriggernometryInterop.OnChatCommand = _ => reported.Set();
                    session.Start();
                    Wait(reported, "初始化失败的聊天提示");
                    Assert(!session.IsRunning && session.HasPendingCleanup && probe.StopCount == 0,
                        "初始化失败即使没有生成实体，也要保留停止入口和清理责任。");
                    Assert(TriggernometryInterop.ChatCommands.Count == 1 &&
                        TriggernometryInterop.ChatCommands[0] == "/e <se.11>\n节点「初始化模拟」执行失败：initialization failed",
                        "非机制异常的聊天提示应包含节点名称和原始原因。");
                    session.Stop();
                    Assert(probe.StartCount == 1 && probe.StartThreadId != callerThread,
                        "Initialization must execute on the worker.");
                    Assert(probe.StopCount == 1 && probe.StopThreadId == callerThread,
                        "中断后的清理必须由显式停止调用一次。");
                    Assert(probe.ClearCount == 1 && !session.IsRunning,
                        "A worker failure must clear entities and stop the session.");
                    Assert(host.Errors.Count == 1 && host.Errors.TryPeek(out var error) &&
                        error.Contains("initialization failed"), "The original worker failure must be logged once.");
                }
                Assert(probe.StopCount == 1, "Disposing a failed session must not repeat cleanup.");
            }
        }

        internal static void SessionChatFailurePreservesSceneAndAllowsRestart()
        {
            using (var probe = new SessionProbe())
            using (var reported = new ManualResetEventSlim())
            using (var restarted = new ManualResetEventSlim())
            {
                var host = new PluginHost
                {
                    FirewallLog = message =>
                    {
                        if (message.StartsWith("模拟错误的聊天提示发送失败：")) reported.Set();
                    }
                };
                SimActor actor = null;
                probe.StartAction = () =>
                {
                    actor = probe.Logic.EntityManager.SpawnBNpc(1);
                    throw new SimMechanicFailedException("分摊人数错误。");
                };
                using (var session = CreateSession(host, probe))
                {
                    TriggernometryInterop.OnChatCommand = _ => { throw new InvalidOperationException("回调暂时不可用。"); };
                    session.Start();
                    Wait(reported, "聊天提示失败的日志");
                    Assert(session.HasPendingCleanup && actor.Entity.IsAlive && probe.StopCount == 0,
                        "聊天回调失败不能触发清场或丢失清理责任。");
                    probe.StartAction = () => restarted.Set();
                    session.Start();
                    Wait(restarted, "中断后的再次启动");
                    Assert(!actor.Entity.IsAlive && probe.StopCount == 1 && session.IsRunning,
                        "再次启动应先清理上次保留现场，再运行新时间轴。");
                    session.Stop();
                    Assert(probe.StopCount == 2 && !session.HasPendingCleanup,
                        "重新开始后的停止应完成自己的清理周期。");
                }
            }
        }

        internal static void SessionStopWaitsForInitializationBeforeCleanup()
        {
            using (var probe = new SessionProbe())
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var callbackExited = new ManualResetEventSlim())
            using (var stopReturned = new ManualResetEventSlim())
            {
                var host = new PluginHost();
                var lateCalls = 0;
                probe.StartAction = () =>
                {
                    entered.Set();
                    Wait(release, "initialization release");
                    callbackExited.Set();
                    // External Stop has disposed this scheduler. The resulting exception is normal
                    // shutdown, and must not trigger the failure cleanup path a second time.
                    probe.Logic.Scheduler.Schedule(0, () => Interlocked.Increment(ref lateCalls));
                };
                probe.StopAction = () => Assert(callbackExited.IsSet,
                    "Session cleanup must not run while initialization still owns entity state.");

                using (var session = CreateSession(host, probe))
                {
                    session.Start();
                    Wait(entered, "initialization entry");
                    Exception stopError = null;
                    var stopThread = new Thread(() =>
                    {
                        try { session.Stop(); }
                        catch (Exception error) { stopError = error; }
                        finally { stopReturned.Set(); }
                    }) { IsBackground = true };
                    stopThread.Start();
                    try
                    {
                        Assert(SpinWait.SpinUntil(() => !probe.Logic.Scheduler.IsRunning, TimeoutMs),
                            "Stop did not cancel the scheduler.");
                        Assert(!stopReturned.Wait(50) && probe.StopCount == 0,
                            "Stop must wait for initialization before returning or cleaning entities.");
                    }
                    finally
                    {
                        release.Set();
                        Join(stopThread);
                    }

                    Assert(stopError == null, "Stopping initialization failed: " + stopError);
                    Assert(probe.StopCount == 1 && probe.ClearCount == 1,
                        "Stop must clean up exactly once after initialization exits.");
                    Assert(probe.StopThreadId == stopThread.ManagedThreadId,
                        "Normal cleanup should run on the Stop caller after joining the worker.");
                    Assert(lateCalls == 0 && host.Errors.IsEmpty,
                        "Cancellation must prevent later work without reporting normal shutdown as a failure.");
                }
            }
        }

        internal static void SessionCanRestartAfterStop()
        {
            using (var probe = new SessionProbe())
            using (var initialized = new AutoResetEvent(false))
            {
                probe.StartAction = () => initialized.Set();
                using (var session = CreateSession(new PluginHost(), probe))
                {
                    session.Start();
                    Assert(initialized.WaitOne(TimeoutMs), "First initialization did not run.");
                    var firstScheduler = probe.Logic.Scheduler;
                    session.Stop();
                    Throws<ObjectDisposedException>(() => firstScheduler.Schedule(0, () => { }));

                    session.Start();
                    Assert(initialized.WaitOne(TimeoutMs), "Second initialization did not run.");
                    var secondScheduler = probe.Logic.Scheduler;
                    Assert(!ReferenceEquals(firstScheduler, secondScheduler) && session.IsRunning,
                        "A restarted session must receive a fresh running scheduler.");
                    Assert(ReferenceEquals(secondScheduler, probe.Logic.EntityManager.Scheduler),
                        "The logic and entity manager must share the new scheduler.");
                    session.Stop();
                    Assert(probe.PrepareCount == 2 && probe.StartCount == 2 && probe.StopCount == 2 &&
                        probe.ClearCount == 2,
                        "Each run must prepare, initialize, and clean up exactly once.");
                }
                Assert(probe.StopCount == 2, "Dispose must not repeat either completed run's cleanup.");
            }
        }

        internal static void SessionNormalTimelineCompletionDoesNotRetainFailureState()
        {
            using (var probe = new SessionProbe())
            using (var completed = new AutoResetEvent(false))
            {
                probe.StartAction = () =>
                {
                    probe.Logic.EntityManager.SpawnBNpc(1);
                    probe.Logic.EntityManager.Clear();
                    probe.Logic.Scheduler.Cancel();
                    completed.Set();
                };
                using (var session = CreateSession(new PluginHost(), probe))
                {
                    session.Start();
                    Assert(completed.WaitOne(TimeoutMs), "正常时间轴未完成。");
                    Assert(!session.IsRunning && !session.HasPendingCleanup,
                        "正常完成并清理的时间轴不得被标记为失败留场，否则开始按钮无法再次使用。");
                    session.Start();
                    Assert(completed.WaitOne(TimeoutMs) && probe.StartCount == 2 && !session.HasPendingCleanup,
                        "正常完成后应能直接再次开始，不必额外点击停止。");
                }
            }
        }

        internal static void SessionDisposeIsIdempotent()
        {
            using (var probe = new SessionProbe())
            {
                var session = CreateSession(new PluginHost(), probe);
                try
                {
                    session.Start();
                    Wait(probe.Started, "initialization before disposal");
                    session.Dispose();
                    session.Dispose();
                    session.Stop();
                    Assert(!session.IsRunning && probe.StopCount == 1 && probe.ClearCount == 1,
                        "Repeated Dispose and Stop must leave the session stopped with one cleanup.");
                    Throws<ObjectDisposedException>(session.Start);
                    Throws<ObjectDisposedException>(() => probe.Logic.Scheduler.Schedule(0, () => { }));
                }
                finally { session.Dispose(); }
            }

            using (var probe = new SessionProbe())
            {
                var neverStarted = CreateSession(new PluginHost(), probe);
                neverStarted.Dispose();
                neverStarted.Dispose();
                Assert(probe.StopCount == 0, "Disposing an unstarted session must not invent a cleanup cycle.");
                Throws<ObjectDisposedException>(neverStarted.Start);
            }
        }

        internal static void SessionRetainsFailedCleanupAndRejectsRestart()
        {
            using (var probe = new SessionProbe())
            using (var initialized = new AutoResetEvent(false))
            {
                var host = new PluginHost();
                var failDeletion = true;
                host.EntitySpawner.BeforeDelete = _ =>
                {
                    if (failDeletion) throw new InvalidOperationException("暂时无法回收实体");
                };
                probe.StartAction = () =>
                {
                    probe.Logic.EntityManager.SpawnBNpc(1);
                    initialized.Set();
                };
                using (var session = CreateSession(host, probe))
                {
                    try
                    {
                        session.Start();
                        Assert(initialized.WaitOne(TimeoutMs), "等待带实体的会话启动超时。");
                        var firstScheduler = probe.Logic.Scheduler;
                        session.Stop();
                        Assert(!session.IsRunning && session.HasPendingCleanup && probe.StopCount == 1 &&
                            host.EntitySpawner.Deleted.Count == 0,
                            "实体回收失败后必须保留清理状态，同时只调用一次停止逻辑。");

                        Throws<InvalidOperationException>(session.Start);
                        Assert(ReferenceEquals(firstScheduler, probe.Logic.Scheduler) &&
                            ReferenceEquals(firstScheduler, probe.Logic.EntityManager.Scheduler) &&
                            probe.PrepareCount == 1 && probe.StartCount == 1 && probe.StopCount == 1 &&
                            host.EntitySpawner.DeleteAttempts.Count == 2,
                            "重启前可以重试清理，但仍失败时不得替换调度器或重复执行预设逻辑。");

                        failDeletion = false;
                        session.Stop();
                        Assert(!session.HasPendingCleanup && probe.StopCount == 1 &&
                            host.EntitySpawner.Deleted.Count == 1 && host.EntitySpawner.DeleteAttempts.Count == 3,
                            "再次停止应只重试未完成的回收，并在成功后解除清理状态。");

                        session.Start();
                        Assert(initialized.WaitOne(TimeoutMs), "回收成功后会话未能重新启动。");
                        Assert(session.IsRunning && !ReferenceEquals(firstScheduler, probe.Logic.Scheduler),
                            "清理完成后重新开始应使用新调度器。");
                        session.Stop();
                        Assert(probe.StopCount == 2 && host.EntitySpawner.Deleted.Count == 2,
                            "重启后的停止逻辑与实体回收应各执行一次。");
                    }
                    finally { failDeletion = false; session.Stop(); }
                }
            }
        }

        internal static void SessionDisposeRetriesPendingNetworkCleanup()
        {
            using (var probe = new SessionProbe())
            using (var queued = new ManualResetEventSlim())
            {
                var host = new PluginHost();
                var failCancellation = true;
                var cancellationAttempts = 0;
                host.EntitySpawner.BeforeNetworkCancel = _ =>
                {
                    cancellationAttempts++;
                    if (failCancellation) throw new InvalidOperationException("暂时无法取消网络生成");
                };
                probe.StartAction = () =>
                {
                    probe.Logic.EntityManager.SpawnNetworkBNpc(new Core.Entity.NetworkNpcSpawnSpec(),
                        _ => { throw new Exception("尚未就绪的角色不得交付。"); });
                    queued.Set();
                };
                var session = CreateSession(host, probe);
                try
                {
                    session.Start();
                    Wait(queued, "网络生成请求排队");
                    session.Dispose();
                    Assert(session.HasPendingCleanup && !session.IsRunning && probe.StopCount == 1 &&
                        cancellationAttempts == 1 && host.EntitySpawner.CancelledNetworkRequests.Count == 0,
                        "Dispose 遇到网络取消失败必须保留请求，不得把会话当作已经清理完成。");
                    Throws<ObjectDisposedException>(session.Start);

                    failCancellation = false;
                    session.Dispose();
                    session.Stop();
                    session.Dispose();
                    Assert(!session.HasPendingCleanup && probe.StopCount == 1 && cancellationAttempts == 2 &&
                        host.EntitySpawner.CancelledNetworkRequests.Count == 1 &&
                        host.EntitySpawner.NetworkRequests[0].Entity == null,
                        "Dispose 之后仍应允许重试取消；成功后的重复停止不得重放预设停止逻辑或再次取消请求。");
                }
                finally { failCancellation = false; session.Dispose(); }
            }
        }

        internal static void SessionCallbackCanStopWhileExternalStopWaits()
            => VerifyConcurrentStopReentry("停止", session => session.Stop(), null, false);

        internal static void SessionCallbackCanDisposeWhileExternalStopWaits()
            => VerifyConcurrentStopReentry("释放", session => session.Dispose(), null, true);

        internal static void SessionCallbackCannotRestartWhileExternalStopWaits()
            => VerifyConcurrentStopReentry("重新开始", session => session.Start(), typeof(InvalidOperationException), false);

        internal static void SessionSynchronousCallbacksCannotRestart()
            => RunBoundedSessionScenario("准备及停止回调内重启", () =>
            {
                var probe = new SessionProbe();
                var host = new PluginHost();
                var prepareAttempts = 0;
                var stopAttempts = 0;
                Exception prepareError = null;
                Exception stopError = null;
                SimSession session = null;
                probe.PrepareAction = () =>
                {
                    if (Interlocked.Increment(ref prepareAttempts) != 1) return;
                    try { session.Start(); }
                    catch (Exception error) { prepareError = error; }
                };
                probe.StartAction = () => probe.Logic.EntityManager.SpawnBNpc(1);
                probe.StopAction = () =>
                {
                    if (Interlocked.Increment(ref stopAttempts) != 1) return;
                    try { session.Start(); }
                    catch (Exception error) { stopError = error; }
                };
                session = CreateSession(host, probe);
                session.Start();
                Assert(probe.Started.Wait(TimeoutMs), "准备阶段拒绝重入后，正常初始化未执行。");
                Assert(prepareError?.GetType() == typeof(InvalidOperationException),
                    "准备回调内再次开始必须明确抛出 InvalidOperationException：" + prepareError);
                var originalScheduler = probe.Logic.Scheduler;
                session.Stop();
                Assert(stopError?.GetType() == typeof(InvalidOperationException),
                    "外部停止触发的停止回调内再次开始必须明确抛出 InvalidOperationException：" + stopError);
                Assert(prepareAttempts == 1 && stopAttempts == 1 && probe.PrepareCount == 1 && probe.StartCount == 1 &&
                    probe.StopCount == 1 && probe.ClearCount == 1 && host.EntitySpawner.Deleted.Count == 1,
                    "同步重入不得重复准备、初始化、停止或回收实体。");
                Assert(!session.IsRunning && originalScheduler.PendingCount == 0 &&
                    ReferenceEquals(originalScheduler, probe.Logic.Scheduler) && host.Errors.IsEmpty,
                    "停止回调内重启被拒绝后，不得替换调度器或遗留运行中的会话。");
                session.Dispose();
                Assert(probe.StopCount == 1 && probe.ClearCount == 1, "最后释放不得重复已完成的清理。");
                probe.Dispose();
            });

        private static void VerifyConcurrentStopReentry(string operation, Action<SimSession> callback,
            Type expectedCallbackError, bool disposedByCallback)
        {
            foreach (var externalDispose in new[] { false, true })
                VerifyBoundedSessionReentry(operation, callback, expectedCallbackError, disposedByCallback, externalDispose);
        }

        private static void VerifyBoundedSessionReentry(string operation, Action<SimSession> callback,
            Type expectedCallbackError, bool disposedByCallback, bool externalDispose)
            => RunBoundedSessionScenario("外部" + (externalDispose ? "释放" : "停止") + "与节点内" + operation,
                () => RunConcurrentStopReentry(operation, callback, expectedCallbackError, disposedByCallback, externalDispose));

        private static void RunBoundedSessionScenario(string description, Action action)
        {
            Exception failure = null;
            var completed = new ManualResetEventSlim();
            var scenario = new Thread(() =>
            {
                try { action(); }
                catch (Exception error) { failure = error; }
                finally { completed.Set(); }
            }) { IsBackground = true, Name = "会话并发回归场景" };
            scenario.Start();
            // 场景本身也有超时，避免 Start 或完成后的状态检查出现新死锁时挂住整套测试。
            if (!completed.Wait(TimeoutMs * 3))
                throw new Exception("会话" + description + "场景未按期退出。");
            Assert(scenario.Join(TimeoutMs), "会话并发测试的场景线程未退出。");
            completed.Dispose();
            if (failure != null) throw new Exception("会话" + description + "验证失败。", failure);
        }

        private static void RunConcurrentStopReentry(string operation, Action<SimSession> callback,
            Type expectedCallbackError, bool disposedByCallback, bool externalDispose)
        {
            var probe = new SessionProbe();
            var entered = new ManualResetEventSlim();
            var callbackExited = new ManualResetEventSlim();
            var host = new PluginHost();
            var lateCalls = 0;
            Exception callbackError = null;
            Exception stopError = null;
            SimSession session = null;
            probe.StartAction = () =>
            {
                probe.Logic.EntityManager.SpawnBNpc(1);
                var scheduler = probe.Logic.Scheduler;
                scheduler.ScheduleAfter(0, () =>
                {
                    entered.Set();
                    try
                    {
                        Assert(SpinWait.SpinUntil(() => !scheduler.IsRunning, TimeoutMs),
                            "外部停止未在节点重入前取消调度器。");
                        callback(session);
                    }
                    catch (Exception error) { callbackError = error; }
                    finally { callbackExited.Set(); }
                }, "等待外部停止后在节点内" + operation);
                scheduler.ScheduleAfter(0, () => Interlocked.Increment(ref lateCalls), "停止后不得执行的节点");
            };
            session = CreateSession(host, probe);
            session.Start();
            Assert(entered.Wait(TimeoutMs), "并发测试节点未进入执行状态。");
            var originalScheduler = probe.Logic.Scheduler;
            var stopThread = new Thread(() =>
            {
                try
                {
                    if (externalDispose) session.Dispose();
                    else session.Stop();
                }
                catch (Exception error) { stopError = error; }
            }) { IsBackground = true, Name = "会话并发回归外部停止" };
            stopThread.Start();

            // 失败时不在 finally 中等待 Dispose，也不释放仍可能被后台节点使用的屏障。
            Assert(callbackExited.Wait(TimeoutMs), "节点内" + operation + "与外部停止互相等待，节点未退出。");
            Assert(stopThread.Join(TimeoutMs), "节点返回后外部停止仍未退出。");
            Assert(stopError == null, "外部停止发生异常：" + stopError);
            Assert(expectedCallbackError == null ? callbackError == null : callbackError?.GetType() == expectedCallbackError,
                "节点内" + operation + "的结果不符合约定：" + callbackError);
            Assert(!session.IsRunning && ReferenceEquals(originalScheduler, probe.Logic.Scheduler) &&
                originalScheduler.PendingCount == 0 && probe.PrepareCount == 1 && probe.StartCount == 1,
                "节点重入不得重建调度器、重复准备或遗留待执行节点。");
            Assert(probe.StopCount == 1 && probe.ClearCount == 1 &&
                host.EntitySpawner.Deleted.Count == 1 && host.EntitySpawner.DeleteAttempts.Count == 1,
                "并发停止必须只执行一次预设停止、清理及实体回收。");
            Assert(lateCalls == 0 && host.Errors.IsEmpty,
                "停止后的节点不得执行，正常关闭及明确拒绝重启不应被记录为执行失败。");
            if (disposedByCallback || externalDispose)
                Assert(Throws<ObjectDisposedException>(session.Start).GetType() == typeof(ObjectDisposedException),
                    "任一线程释放会话后，后续开始调用必须保持拒绝状态。");

            session.Dispose();
            session.Stop();
            Assert(probe.StopCount == 1 && probe.ClearCount == 1 && host.EntitySpawner.DeleteAttempts.Count == 1,
                "并发停止完成后重复释放和停止不得再次清理。");
            probe.Dispose();
            entered.Dispose();
            callbackExited.Dispose();
        }

        private static SimSession CreateSession(PluginHost host, SessionProbe probe)
        {
            TriggernometryInterop.ChatCommands.Clear();
            TriggernometryInterop.OnChatCommand = null;
            return new SimSession(host, new SessionTestPreset(probe));
        }

        internal static void Wait(ManualResetEventSlim signal, string description)
            => Assert(signal.Wait(TimeoutMs), "Timed out waiting for " + description + ".");

        private static void Join(Thread thread)
            => Assert(thread.Join(TimeoutMs), "A session test helper thread did not exit.");

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static T Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T exception) { return exception; }
            throw new Exception("Expected " + typeof(T).Name + ".");
        }
    }

    internal sealed class SessionProbe : IDisposable
    {
        internal Action PrepareAction;
        internal Action StartAction;
        internal Action StopAction;
        internal readonly ManualResetEventSlim Started = new ManualResetEventSlim();
        internal readonly ManualResetEventSlim Cleaned = new ManualResetEventSlim();
        internal SessionTestLogic Logic;
        internal int PrepareCount;
        internal int StartCount;
        internal int StopCount;
        internal int ClearCount;
        internal int PrepareThreadId;
        internal int StartThreadId;
        internal int StopThreadId;

        public void Dispose()
        {
            Started.Dispose();
            Cleaned.Dispose();
        }
    }

    internal sealed class SessionTestPreset : SimPresetBase
    {
        internal readonly SessionProbe Probe;
        internal SessionTestPreset(SessionProbe probe) { Probe = probe; }
        public override Type SimLogicType => typeof(SessionTestLogic);
    }

    internal sealed class SessionTestLogic : SimLogicBase
    {
        private readonly SessionProbe _probe;

        public SessionTestLogic(PluginHost host, SimPresetBase preset)
        {
            _probe = ((SessionTestPreset)preset).Probe;
            _probe.Logic = this;
        }

        internal override void Prepare()
        {
            _probe.PrepareThreadId = Thread.CurrentThread.ManagedThreadId;
            Interlocked.Increment(ref _probe.PrepareCount);
            _probe.PrepareAction?.Invoke();
        }

        internal override void Start()
        {
            _probe.StartThreadId = Thread.CurrentThread.ManagedThreadId;
            Interlocked.Increment(ref _probe.StartCount);
            _probe.Started.Set();
            _probe.StartAction?.Invoke();
        }

        internal override void Stop()
        {
            _probe.StopThreadId = Thread.CurrentThread.ManagedThreadId;
            Interlocked.Increment(ref _probe.StopCount);
            _probe.StopAction?.Invoke();
            EntityManager.Clear();
            Interlocked.Increment(ref _probe.ClearCount);
            _probe.Cleaned.Set();
        }
    }
}

// The host and preset logic are isolated from ACT/UI; session, entity manager and behavior
// controllers are the production source files linked by the test project.
namespace Simulant.Core
{
    public sealed class PluginHost
    {
        internal Configuration Configuration { get; set; } = new Configuration();
        internal readonly Entity.EntitySpawner EntitySpawner = new Entity.EntitySpawner();
        internal readonly ConcurrentQueue<string> Errors = new ConcurrentQueue<string>();
        internal void LogError(string message) => Errors.Enqueue(message);
        internal Action<string> FirewallLog { get; set; }
        internal void LogRuntime(string message) => FirewallLog?.Invoke(message);
        internal void LogWarning(string message) => FirewallLog?.Invoke(message);
        internal void LogVerbose(string message) => FirewallLog?.Invoke(message);
    }
}

namespace Simulant.Simulation
{
    public abstract class SimPresetBase
    {
        public abstract Type SimLogicType { get; }
    }

    public abstract class SimLogicBase
    {
        internal SimEntityManager EntityManager { get; set; }
        internal SimScheduler Scheduler { get; set; }
        internal abstract void Prepare();
        internal abstract void Start();
        internal abstract void Stop();
    }
}
