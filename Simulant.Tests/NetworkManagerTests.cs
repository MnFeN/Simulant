using Simulant.Core.Entity;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Tests
{
    internal static class NetworkManagerTests
    {
        public static void NetworkPlayersAndNpcsSharePollingAndPreserveKinds()
        {
            var spawner = new EntitySpawner();
            Exception failure = null;
            var calls = 0;
            var playerCalls = 0;
            var npcCalls = 0;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
            {
                var manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                try
                {
                    Action<SimActor, ObjectKind, Job> ready = (actor, kind, job) =>
                    {
                        Assert(scheduler.IsSchedulerThread && actor.IsActive,
                            "玩家与 NPC 必须在同一个会话调度线程交付活动角色。");
                        Assert(spawner.World.Objects[actor.Entity.Identity.Slot].Value.ObjectKind == kind &&
                            actor.Entity.Job == job, "混合生成不得把玩家登记为 NPC，或丢失指定职业。");
                        if (kind == ObjectKind.Pc) playerCalls++; else npcCalls++;
                        if (++calls == 3) scheduler.ScheduleAfter(0.04, finished.Set);
                    };
                    manager.SpawnNetworkPlayer((Job)19, actor => ready(actor, ObjectKind.Pc, (Job)19));
                    manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), actor => ready(actor, ObjectKind.BattleNpc, Job.ADV));
                    manager.SpawnNetworkPlayer(new NetworkPlayerSpawnSpec { Job = (Job)24 },
                        actor => ready(actor, ObjectKind.Pc, (Job)24));
                    Assert(scheduler.PendingCount == 1 && spawner.NetworkRequests.Select(request => request.EntityId).Distinct().Count() == 3,
                        "玩家和 NPC 必须使用独立请求身份，并共用一个轮询节点。");
                    scheduler.ScheduleAfter(0.05, () =>
                    {
                        Assert(calls == 0 && spawner.NetworkPollCount >= 3 && scheduler.PendingCount == 1,
                            "混合请求等待就绪期间不得提前交付，也不得各自积累轮询节点。");
                        spawner.NetworkReady = true;
                    });
                    scheduler.Start();
                    Assert(finished.Wait(5000), "混合网络生成测试未结束。");
                    scheduler.Dispose();
                    Assert(failure == null && playerCalls == 2 && npcCalls == 1 && scheduler.PendingCount == 0,
                        "两个玩家与一个 NPC 必须各交付一次，随后停止轮询：" + failure);
                    manager.Clear();
                    Assert(spawner.Deleted.Count == 3, "混合生成的所有角色必须由会话分别回收。");
                }
                finally { scheduler.Dispose(); manager.Clear(); }
            }
        }

        public static void NetworkPlayersValidateBeforeSpawningAndCancelWithNpcs()
        {
            var spawner = new EntitySpawner();
            using (var scheduler = new SimScheduler())
            {
                var manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                try
                {
                    Action<SimActor> invalid = async _ => await Task.Yield();
                    Throws<ArgumentNullException>(() => manager.SpawnNetworkPlayer((NetworkPlayerSpawnSpec)null, _ => { }));
                    Throws<ArgumentNullException>(() => manager.SpawnNetworkPlayer((Job)19, null));
                    Throws<ArgumentException>(() => manager.SpawnNetworkPlayer((Job)19, invalid));
                    Throws<ArgumentOutOfRangeException>(() => manager.SpawnNetworkPlayer((Job)19, _ => { }, 0));
                    Assert(spawner.NetworkRequests.Count == 0, "无效玩家回调必须在原生创建前被拒绝。");
                    manager.SpawnNetworkPlayer((Job)19, _ => { throw new Exception("已取消玩家不得交付。"); });
                    manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ => { throw new Exception("已取消 NPC 不得交付。"); });
                    manager.Clear();
                    Assert(spawner.CancelledNetworkRequests.Count == 2 && scheduler.PendingCount == 0 &&
                        spawner.NetworkRequests.All(request => request.Entity == null),
                        "清理必须一起取消尚未就绪的玩家和 NPC，且不得创建替代角色。");
                }
                finally { manager.Clear(); }
            }
        }

        public static void NetworkBatchSharesPollingAndReadyRunsOnce()
        {
            var spawner = new EntitySpawner();
            Exception failure = null;
            var calls = 0;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
            {
                var manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                try
                {
                    for (var i = 0; i < 3; i++)
                        manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), actor =>
                        {
                            Assert(scheduler.IsSchedulerThread && actor.IsActive,
                                "网络实体就绪回调必须在所属调度线程上接收活动角色。");
                            if (++calls == 3) scheduler.ScheduleAfter(0.04, finished.Set);
                        });
                    Assert(scheduler.PendingCount == 1,
                        "多个网络生成请求必须共享同一个轮询节点。");
                    scheduler.ScheduleAfter(0.05, () =>
                    {
                        Assert(calls == 0 && spawner.NetworkPollCount >= 3,
                            "原生实体尚未就绪时必须持续查询，并且不得提前调用就绪回调。");
                        Assert(scheduler.PendingCount == 1,
                            "等待多个网络实体时不得积累多个轮询节点。");
                        spawner.NetworkReady = true;
                    });
                    scheduler.Start();
                    Assert(finished.Wait(5000), "批量网络生成测试未结束。");
                    scheduler.Dispose();
                    Assert(failure == null && calls == 3,
                        "每个网络生成请求必须只交付一次角色：" + failure);
                    Assert(scheduler.PendingCount == 0,
                        "全部网络实体就绪后不得残留轮询节点。");
                    manager.Clear();
                    Assert(spawner.Deleted.Count == 3,
                        "交付给会话的网络实体必须全部取得并履行回收责任。");
                }
                finally { scheduler.Dispose(); manager.Clear(); }
            }
        }

        public static void ClearCancelsPendingNetworkSpawnsAndRejectsReuse()
        {
            var spawner = new EntitySpawner();
            var calls = 0;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler())
            {
                var manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                try
                {
                    for (var i = 0; i < 3; i++)
                        manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ => calls++);
                    manager.Clear();
                    Assert(spawner.CancelledNetworkRequests.Count == 3 && scheduler.PendingCount == 0,
                        "清理必须同时取消尚未就绪的原生生成请求和共享轮询。");
                    spawner.NetworkReady = true;
                    scheduler.ScheduleAfter(0.03, finished.Set);
                    scheduler.Start();
                    Assert(finished.Wait(5000), "网络生成取消测试未结束。");
                    scheduler.Dispose();
                    Assert(calls == 0 && spawner.NetworkRequests.All(request => request.Entity == null),
                        "已取消请求即使后来满足就绪条件，也不得创建或交付角色。");
                    Throws<InvalidOperationException>(() =>
                        manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ => calls++));
                    Assert(spawner.NetworkRequests.Count == 3,
                        "停止的会话必须在接触原生生成入口前拒绝新请求。");
                }
                finally { scheduler.Dispose(); manager.Clear(); }
            }
        }

        public static void NetworkCleanupRetriesFailedCancellationWithoutDeliveringOldCallbacks()
        {
            var spawner = new EntitySpawner();
            var failedAttempts = 0;
            var successfulAttempts = 0;
            var errors = 0;
            var oldCalls = 0;
            var newCalls = 0;
            Exception failure = null;
            using (var oldQueueDrained = new ManualResetEventSlim())
            using (var ready = new ManualResetEventSlim())
            using (var firstScheduler = new SimScheduler((error, _) => { failure = error; oldQueueDrained.Set(); }))
            using (var secondScheduler = new SimScheduler((error, _) => { failure = error; ready.Set(); }))
            {
                var manager = new SimEntityManager(spawner, _ => errors++) { Scheduler = firstScheduler };
                try
                {
                    manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ => oldCalls++);
                    manager.SpawnNetworkPlayer((Job)19, _ => oldCalls++);
                    var failed = spawner.NetworkRequests[0];
                    spawner.BeforeNetworkCancel = request =>
                    {
                        if (ReferenceEquals(request, failed))
                        {
                            if (++failedAttempts == 1) throw new InvalidOperationException("首次取消失败");
                        }
                        else successfulAttempts++;
                    };
                    manager.Clear();
                    Assert(errors == 1 && !failed.Cancelled && spawner.CancelledNetworkRequests.Count == 1 &&
                        firstScheduler.PendingCount == 0, "取消一个等待请求失败时仍须取消其余请求和共享轮询。");
                    Assert(manager.HasPendingCleanup, "取消失败的请求必须保留在待清理状态中。");
                    Throws<InvalidOperationException>(() => manager.Scheduler = secondScheduler);
                    Throws<InvalidOperationException>(() => manager.SpawnNetworkPlayer((Job)19, _ => oldCalls++));
                    spawner.NetworkReady = true;
                    firstScheduler.Schedule(0, oldQueueDrained.Set);
                    firstScheduler.Start();
                    Assert(oldQueueDrained.Wait(5000), "旧调度队列未完成同步节点。");
                    firstScheduler.Dispose();
                    Assert(failure == null && oldCalls == 0 && spawner.NetworkPollCount == 0,
                        "失败清理记录只用于回收重试，不得恢复查询或交付旧回调。");
                    manager.Clear();
                    Assert(errors == 1 && failedAttempts == 2 && successfulAttempts == 1 &&
                        spawner.CancelledNetworkRequests.Count == 2 && spawner.NetworkRequests.All(request => request.Entity == null),
                        "再次清理必须只重试失败请求，不重复取消已成功请求，也不能补生成旧实体。");
                    Assert(!manager.HasPendingCleanup, "取消重试成功后不得遗留虚假的待清理状态。");
                    manager.Clear();
                    Assert(failedAttempts == 2 && successfulAttempts == 1, "全部取消成功后的清理不能再次提交取消。");
                    manager.Scheduler = secondScheduler;
                    manager.SpawnNetworkPlayer((Job)19, _ => { newCalls++; ready.Set(); });
                    secondScheduler.Start();
                    Assert(ready.Wait(5000), "清理重试成功后，新会话玩家没有交付。");
                    secondScheduler.Dispose();
                    Assert(failure == null && oldCalls == 0 && newCalls == 1,
                        "清理完成后只能交付新会话请求：" + failure);
                }
                finally
                {
                    firstScheduler.Dispose();
                    secondScheduler.Dispose();
                    spawner.BeforeNetworkCancel = null;
                    manager.Clear();
                }
            }
        }

        public static void FirstReadyCallbackCanClearPendingBatch()
        {
            var spawner = new EntitySpawner { NetworkReady = true };
            var calls = 0;
            Exception failure = null;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
            {
                var manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                try
                {
                    for (var i = 0; i < 3; i++)
                        manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ =>
                        {
                            calls++;
                            manager.Clear();
                            finished.Set();
                        });
                    scheduler.Start();
                    Assert(finished.Wait(5000), "就绪回调内清理会话的测试未结束。");
                    scheduler.Dispose();
                    Assert(failure == null && calls == 1 && spawner.Deleted.Count == 3,
                        "首个就绪回调清理会话后，同批未领取回调必须作废，已生成角色必须全部回收：" + failure);
                    Assert(scheduler.PendingCount == 0, "回调内清理不得遗留共享轮询。");
                }
                finally { scheduler.Dispose(); manager.Clear(); }
            }
        }

        public static void NetworkReadyCallbackCanCancelScheduler()
        {
            var spawner = new EntitySpawner { NetworkReady = true };
            var calls = 0;
            Exception failure = null;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
            {
                var manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                try
                {
                    for (var i = 0; i < 3; i++)
                        manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ =>
                        {
                            calls++;
                            scheduler.Cancel();
                            finished.Set();
                        });
                    scheduler.Start();
                    Assert(finished.Wait(5000), "就绪回调取消调度器的测试未结束。");
                    scheduler.Dispose();
                    Assert(failure == null && calls == 1 && scheduler.PendingCount == 0,
                        "调度器取消后，同批尚未领取的网络就绪回调不得继续执行：" + failure);
                }
                finally { scheduler.Dispose(); manager.Clear(); }
            }
        }

        public static void NetworkSpawnTimeoutCancelsRequest()
        {
            var spawner = new EntitySpawner();
            Exception failure = null;
            var calls = 0;
            SimEntityManager manager = null;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) =>
            {
                failure = error;
                manager.Clear();
                finished.Set();
            }))
            {
                manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                try
                {
                    manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ => calls++, timeoutSeconds: 0.03);
                    scheduler.Start();
                    Assert(finished.Wait(5000), "始终未就绪的网络请求没有超时。");
                    scheduler.Dispose();
                    Assert(failure is TimeoutException && calls == 0 &&
                        spawner.CancelledNetworkRequests.Count == 1 && scheduler.PendingCount == 0,
                        "网络生成超时必须取消原生请求，停止轮询，并由会话异常处理完成清理：" + failure);
                }
                finally { scheduler.Dispose(); manager.Clear(); }
            }
        }

        public static void NetworkReadyFailureClearsWholeBatch()
        {
            var spawner = new EntitySpawner { NetworkReady = true };
            var expected = new InvalidOperationException("就绪回调失败");
            Exception failure = null;
            var calls = 0;
            SimEntityManager manager = null;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) =>
            {
                failure = error;
                manager.Clear();
                finished.Set();
            }))
            {
                manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                try
                {
                    for (var i = 0; i < 3; i++)
                        manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ =>
                        {
                            calls++;
                            throw expected;
                        });
                    scheduler.Start();
                    Assert(finished.Wait(5000), "就绪回调失败后未执行会话清理。");
                    scheduler.Dispose();
                    Assert(ReferenceEquals(failure, expected) && calls == 1 && !scheduler.IsRunning &&
                        scheduler.PendingCount == 0,
                        "首个就绪回调失败后必须停止调度，保留原始异常，并取消同批其余回调。");
                    Assert(spawner.Deleted.Count == 3 && spawner.CancelledNetworkRequests.Count == 0 &&
                        spawner.NetworkRequests.All(request => request.Entity != null && !request.Entity.IsAlive),
                        "已完成生成但尚未交付的同批角色也必须回收，不能遗漏在等待记录中。");
                }
                finally { scheduler.Dispose(); manager.Clear(); }
            }
        }

        public static void NetworkTimeoutClearsReadyAndWaitingBatch()
        {
            var spawner = new EntitySpawner();
            Exception failure = null;
            var calls = 0;
            SimEntityManager manager = null;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) =>
            {
                failure = error;
                manager.Clear();
                finished.Set();
            }))
            {
                manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                try
                {
                    manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ => calls++);
                    spawner.NetworkRequests[0].Ready = true;
                    // 首次共享检查前即到期，保证同一批内先取得角色、再遇到超时。
                    manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ => calls++, timeoutSeconds: 0.001);
                    manager.SpawnNetworkPlayer((Job)19, _ => calls++);
                    scheduler.Start();
                    Assert(finished.Wait(5000), "混合就绪与等待请求的超时未触发会话清理。");
                    scheduler.Dispose();
                    Assert(failure is TimeoutException && calls == 0 && !scheduler.IsRunning &&
                        scheduler.PendingCount == 0,
                        "同批检查发生超时后必须先停止会话，不得交付该批尚未领取的就绪结果。");
                    Assert(spawner.Deleted.Count == 1 &&
                        ReferenceEquals(spawner.Deleted[0], spawner.NetworkRequests[0].Entity) &&
                        !spawner.NetworkRequests[0].Entity.IsAlive,
                        "超时前已经完成生成的角色必须按实体回收。");
                    Assert(spawner.CancelledNetworkRequests.Count == 2 &&
                        spawner.NetworkRequests.Skip(1).All(request => request.Cancelled && request.Entity == null),
                        "超时请求和其余等待请求必须各取消一次，不能继续创建实体。");
                }
                finally { scheduler.Dispose(); manager.Clear(); }
            }
        }

        public static void StoppedSchedulerRejectsNewNetworkRequestAndRollsItBack()
        {
            foreach (var hasPending in new[] { false, true })
            {
                var spawner = new EntitySpawner();
                using (var scheduler = new SimScheduler())
                {
                    var manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                    try
                    {
                        if (hasPending)
                            manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ => { });
                        scheduler.Cancel();
                        Throws<InvalidOperationException>(() =>
                            manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ => { }));
                        Assert(spawner.CancelledNetworkRequests.Count == 1 &&
                            ReferenceEquals(spawner.CancelledNetworkRequests[0], spawner.NetworkRequests.Last()),
                            "无法安排轮询时，刚提交的原生生成请求必须回滚，不能被已取消的共享句柄掩盖。");
                        manager.Clear();
                        Assert(spawner.CancelledNetworkRequests.Count == spawner.NetworkRequests.Count,
                            "排队失败后的清理仍须取消所有更早的未完成请求。");
                    }
                    finally { manager.Clear(); }
                }
            }
        }

        public static void FailedNetworkQueueRollbackRetainsRequestForCleanup()
        {
            var spawner = new EntitySpawner();
            var cancelAttempts = 0;
            var callbacks = 0;
            var expected = new InvalidOperationException("排队回滚时首次取消失败");
            using (var scheduler = new SimScheduler())
            {
                var manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                try
                {
                    scheduler.Dispose();
                    spawner.BeforeNetworkCancel = _ =>
                    {
                        if (++cancelAttempts == 1) throw expected;
                    };
                    AggregateException failure = null;
                    try { manager.SpawnNetworkPlayer((Job)19, _ => callbacks++); }
                    catch (AggregateException error) { failure = error; }
                    Assert(failure != null && failure.InnerExceptions.Count == 2 &&
                        failure.InnerExceptions[0] is InvalidOperationException &&
                        ReferenceEquals(failure.InnerExceptions[1], expected),
                        "排队与回滚同时失败时必须报告两个原因，不能用取消异常覆盖原始排队错误。");
                    Assert(manager.HasPendingCleanup && cancelAttempts == 1 && spawner.NetworkRequests.Count == 1 &&
                        spawner.CancelledNetworkRequests.Count == 0,
                        "回滚失败的请求必须保留清理记录，不能在取消成功前移除。");
                    Throws<InvalidOperationException>(() => manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ => callbacks++));
                    Throws<InvalidOperationException>(() => manager.SpawnNetworkPlayer((Job)19, _ => callbacks++));
                    Assert(spawner.NetworkRequests.Count == 1 && cancelAttempts == 1 && spawner.NetworkPollCount == 0,
                        "回滚失败后的新生成必须在接触原生入口前被拒绝，不能重新安排旧请求的就绪回调。");
                    spawner.NetworkReady = true;
                    manager.Clear();
                    Assert(!manager.HasPendingCleanup && cancelAttempts == 2 && spawner.CancelledNetworkRequests.Count == 1 &&
                        spawner.NetworkRequests[0].Entity == null && callbacks == 0 && spawner.NetworkPollCount == 0,
                        "后续清理必须重试取消回滚失败的请求，不能查询或交付旧生成结果。");
                    manager.Clear();
                    Assert(cancelAttempts == 2, "取消成功后的重复清理不得再次提交回滚。");
                }
                finally { spawner.BeforeNetworkCancel = null; manager.Clear(); }
            }
        }

        public static void NetworkDispatchAndRollbackFailureRemainOwnedBySession()
        {
            var spawner = new EntitySpawner();
            var dispatchFailure = new InvalidOperationException("生成包提交失败");
            var cancelFailure = new InvalidOperationException("生成包回滚失败");
            var failedAttempts = 0;
            var previousAttempts = 0;
            var callbacks = 0;
            NetworkCharacterSpawnRequest failedRequest = null;
            using (var scheduler = new SimScheduler())
            {
                var manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                try
                {
                    manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ => callbacks++);
                    var previous = spawner.NetworkRequests[0];
                    spawner.AfterNetworkReserved = request => { failedRequest = request; throw dispatchFailure; };
                    spawner.BeforeNetworkCancel = request =>
                    {
                        if (ReferenceEquals(request, failedRequest) && ++failedAttempts == 1) throw cancelFailure;
                        if (ReferenceEquals(request, previous)) previousAttempts++;
                    };
                    AggregateException failure = null;
                    try { manager.SpawnNetworkPlayer((Job)19, _ => callbacks++); }
                    catch (AggregateException error) { failure = error; }
                    Assert(failure != null && failure.InnerExceptions.Count == 2 &&
                        ReferenceEquals(failure.InnerExceptions[0], dispatchFailure) &&
                        ReferenceEquals(failure.InnerExceptions[1], cancelFailure),
                        "发包和内部回滚同时失败时必须保留两项原始异常。");
                    Assert(failedRequest != null && !failedRequest.Released && manager.HasPendingCleanup &&
                        scheduler.PendingCount == 0, "已预留但发包回滚失败的请求必须交给会话持有，并取消已有共享轮询。");
                    Throws<InvalidOperationException>(() => manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ => callbacks++));
                    Throws<InvalidOperationException>(() => manager.SpawnNetworkPlayer((Job)19, _ => callbacks++));
                    Assert(spawner.NetworkRequests.Count == 2 && failedAttempts == 1,
                        "存在发包回滚失败记录时，新生成必须在原生预留前被拒绝。");
                    spawner.NetworkReady = true;
                    manager.Clear();
                    Assert(!manager.HasPendingCleanup && failedRequest.Released && previous.Released &&
                        failedAttempts == 2 && previousAttempts == 1 && spawner.CancelledNetworkRequests.Count == 2 &&
                        spawner.NetworkRequests.All(request => request.Entity == null) && callbacks == 0 && spawner.NetworkPollCount == 0,
                        "会话清理必须重试发包内部取消失败的请求，同时回收先前请求，不得交付任一旧回调。");
                    manager.Clear();
                    Assert(failedAttempts == 2 && previousAttempts == 1, "取消完成后不得再次处理发包失败记录。");
                }
                finally { spawner.BeforeNetworkCancel = null; spawner.AfterNetworkReserved = null; manager.Clear(); }
            }
        }

        public static void NetworkDispatchRollbackSuccessLeavesNoPendingRequest()
        {
            var spawner = new EntitySpawner();
            var expected = new InvalidOperationException("发包失败但回滚成功");
            var cancelAttempts = 0;
            using (var scheduler = new SimScheduler())
            {
                var manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                try
                {
                    spawner.AfterNetworkReserved = _ => { throw expected; };
                    spawner.BeforeNetworkCancel = _ => cancelAttempts++;
                    Exception failure = null;
                    try { manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ => { }); }
                    catch (Exception error) { failure = error; }
                    Assert(ReferenceEquals(failure, expected) && spawner.NetworkRequests.Count == 1 &&
                        spawner.NetworkRequests[0].Released && !manager.HasPendingCleanup && scheduler.PendingCount == 0,
                        "已在发包入口成功回滚的请求不能再次交给会话清理，且应保留原始发包异常。");
                    spawner.AfterNetworkReserved = null;
                    manager.SpawnNetworkPlayer((Job)19, _ => { });
                    Assert(spawner.NetworkRequests.Count == 2 && scheduler.PendingCount == 1,
                        "已完整回滚的失败不得阻止后续有效请求或占用共享轮询。");
                    manager.Clear();
                    Assert(cancelAttempts == 2 && spawner.CancelledNetworkRequests.Count == 2 && !manager.HasPendingCleanup,
                        "后续清理只能取消新请求，不得再次取消已在发包入口释放的请求。");
                }
                finally { spawner.AfterNetworkReserved = null; spawner.BeforeNetworkCancel = null; manager.Clear(); }
            }
        }

        public static void NetworkPreflightFailureCreatesNoCleanupRecord()
        {
            var spawner = new EntitySpawner();
            var expected = new ArgumentException("原生预留前的参数检查失败");
            using (var scheduler = new SimScheduler())
            {
                var manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                try
                {
                    spawner.BeforeNetworkBegin = _ => { throw expected; };
                    spawner.BeforeNetworkPlayerBegin = _ => { throw expected; };
                    foreach (var player in new[] { false, true })
                    {
                        Exception failure = null;
                        try
                        {
                            if (player) manager.SpawnNetworkPlayer((Job)19, _ => { });
                            else manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ => { });
                        }
                        catch (Exception error) { failure = error; }
                        Assert(ReferenceEquals(failure, expected) && !manager.HasPendingCleanup &&
                            spawner.NetworkRequests.Count == 0 && spawner.CancelledNetworkRequests.Count == 0 && scheduler.PendingCount == 0,
                            "尚未预留资源的前置错误必须保持原异常，不能凭空生成清理记录或调用取消。");
                    }
                    spawner.BeforeNetworkBegin = null;
                    spawner.BeforeNetworkPlayerBegin = null;
                    manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ => { });
                    Assert(spawner.NetworkRequests.Count == 1 && scheduler.PendingCount == 1,
                        "前置检查失败不得使原本有效的会话失去生成能力。");
                }
                finally { spawner.BeforeNetworkBegin = null; spawner.BeforeNetworkPlayerBegin = null; manager.Clear(); }
            }
        }

        public static void InvalidNetworkCallbacksAndTimeoutsDoNotBeginSpawning()
        {
            var spawner = new EntitySpawner();
            using (var scheduler = new SimScheduler())
            {
                var manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                try
                {
                    Action<SimActor> invalid = async _ => await Task.Yield();
                    Action<SimActor> multicast = _ => { };
                    multicast += invalid;
                    Throws<ArgumentException>(() => manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), invalid));
                    Throws<ArgumentException>(() => manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), multicast));
                    Throws<ArgumentNullException>(() => manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), null));
                    foreach (var timeout in new[] { 0d, -1d, double.NaN, double.PositiveInfinity })
                        Throws<ArgumentOutOfRangeException>(() =>
                            manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ => { }, timeout));
                    Assert(spawner.NetworkRequests.Count == 0 && scheduler.PendingCount == 0,
                        "无效的回调或等待时长必须在生成原生资源前被拒绝。");
                    manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ => { });
                    Throws<ArgumentException>(() => manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), invalid));
                    Assert(spawner.NetworkRequests.Count == 1 && scheduler.PendingCount == 1,
                        "后续无效请求不得改变已提交的有效请求或轮询节点。");
                }
                finally { manager.Clear(); }
            }
        }

        public static void NetworkReadyCallbacksRunOutsideManagerLock()
        {
            var spawner = new EntitySpawner { NetworkReady = true };
            Exception failure = null;
            Exception producerFailure = null;
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var produced = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; entered.Set(); }))
            {
                var manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                Thread producer = null;
                try
                {
                    manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ =>
                    {
                        entered.Set();
                        Assert(release.Wait(5000), "锁外回调测试未能释放等待屏障。");
                    });
                    scheduler.Start();
                    Assert(entered.Wait(5000), "网络就绪回调没有开始。");
                    producer = new Thread(() =>
                    {
                        try { manager.SpawnBNpc(1); }
                        catch (Exception error) { producerFailure = error; }
                        finally { produced.Set(); }
                    }) { IsBackground = true };
                    producer.Start();
                    Assert(produced.Wait(5000), "网络就绪回调持有会话管理锁，阻塞了其他线程。");
                    release.Set();
                    producer.Join();
                    scheduler.Dispose();
                    Assert(failure == null && producerFailure == null,
                        "网络就绪回调期间的独立实体操作失败：" + failure + producerFailure);
                }
                finally
                {
                    release.Set();
                    scheduler.Dispose();
                    producer?.Join();
                    manager.Clear();
                }
            }
        }

        public static void RestartDoesNotDeliverOldNetworkRequests()
        {
            var spawner = new EntitySpawner();
            var oldCalls = 0;
            var newCalls = 0;
            Exception failure = null;
            using (var finished = new ManualResetEventSlim())
            using (var firstScheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
            using (var secondScheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
            {
                var manager = new SimEntityManager(spawner) { Scheduler = firstScheduler };
                try
                {
                    manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), _ => oldCalls++);
                    Throws<InvalidOperationException>(() => manager.Scheduler = secondScheduler);
                    manager.Clear();
                    manager.Scheduler = secondScheduler;
                    manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec(), actor =>
                    {
                        Assert(secondScheduler.IsSchedulerThread && actor.IsActive,
                            "重启后的网络请求必须由新会话调度器交付。");
                        newCalls++;
                        secondScheduler.ScheduleAfter(0.04, finished.Set);
                    });
                    spawner.NetworkReady = true;
                    firstScheduler.Start();
                    secondScheduler.Start();
                    Assert(finished.Wait(5000), "更换调度器后的网络生成没有完成。");
                    firstScheduler.Dispose();
                    secondScheduler.Dispose();
                    Assert(failure == null && oldCalls == 0 && newCalls == 1 &&
                        spawner.NetworkRequests[0].Entity == null && spawner.CancelledNetworkRequests.Count == 1,
                        "旧请求被取消后不得因新会话就绪而再次创建实体或执行回调：" + failure);
                    manager.Clear();
                    Assert(spawner.Deleted.Count == 1,
                        "重启测试应只生成并回收新会话实际领取的角色。");
                }
                finally { firstScheduler.Dispose(); secondScheduler.Dispose(); manager.Clear(); }
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new Exception("预期抛出 " + typeof(T).Name + "。");
        }
    }
}
