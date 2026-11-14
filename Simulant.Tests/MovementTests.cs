using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Simulant.Tests
{
    internal static class MovementTests
    {
        public static void MovementSharesOneTickAndSchedulerThread()
        {
            var world = new EntityTestWorld();
            var firstTarget = new Vector2(1, 0);
            var secondTarget = new Vector2(0, 1);
            Exception failure = null;
            using (var arrived = new CountdownEvent(2))
            using (var scheduler = new SimScheduler((error, _) => failure = error))
            using (var movement = new SimMovementController(scheduler))
            {
                var first = CreateActor(world, movement, scheduler, 0);
                var second = CreateActor(world, movement, scheduler, 1);
                var workerThread = 0;
                first.Entity.OnPositionWritten = value => { if (value == firstTarget) arrived.Signal(); };
                second.Entity.OnPositionWritten = value => { if (value == secondTarget) arrived.Signal(); };
                movement.MoveTo(first, firstTarget, 100);
                movement.MoveTo(second, secondTarget, 100, faceMovement: false);
                for (var i = 0; i < 20; i++) movement.MoveTo(first, firstTarget, 100);
                Assert(scheduler.PendingCount == 1,
                    "多个模拟角色及目标变更必须共享一个待执行的移动更新节点。");
                Assert(first.Entity.PositionWrites.Count == 0 && second.Entity.PositionWrites.Count == 0,
                    "MoveTo 必须只将任务加入队列，不得在调用线程上写入位置。");
                scheduler.Schedule(0, () => workerThread = Thread.CurrentThread.ManagedThreadId);
                scheduler.Start();
                Assert(arrived.Wait(5000), "两个模拟角色未全部到达目标位置。");
                scheduler.Dispose();
                Assert(failure == null, "移动导致工作线程出错：" + failure);
                Assert(!movement.IsMoving(first) && !movement.IsMoving(second), "到达目标后必须移除已完成的移动。");
                Assert(first.Entity.Pos == firstTarget && second.Entity.Pos == secondTarget,
                    "到达时必须精确落在目标位置，不得越过目标。");
                Assert(first.Entity.WriteThreads.Concat(second.Entity.WriteThreads).All(id => id == workerThread),
                    "所有插值位置写入都必须在共享调度器的工作线程上执行。");
                Assert(second.Entity.Heading == 0, "faceMovement=false 时必须保留模拟角色的朝向。");
            }
        }

        public static void MovementRetargetsTheCurrentPosition()
        {
            var world = new EntityTestWorld();
            var target = new Vector2(-1, 2);
            var retargeted = false;
            var retargetOrigin = Vector2.Zero;
            Exception failure = null;
            using (var arrived = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => failure = error))
            using (var movement = new SimMovementController(scheduler))
            {
                var actor = CreateActor(world, movement, scheduler, 0);
                actor.Entity.OnPositionWritten = position =>
                {
                    if (!retargeted) return;
                    Assert(position.X >= target.X && position.X <= retargetOrigin.X &&
                        position.Y >= retargetOrigin.Y && position.Y <= target.Y,
                        "更换目标后的移动必须位于当前位置与新目标之间。");
                    if (position == target) arrived.Set();
                };
                movement.MoveTo(actor, new Vector2(1000, 0), 1);
                scheduler.Schedule(0.03, () =>
                {
                    retargetOrigin = actor.Entity.Pos;
                    retargeted = true;
                    movement.MoveTo(actor, target, 100);
                    Assert(scheduler.PendingCount == 1, "更换目标必须替换原移动，不得增加第二个更新节点。");
                });
                scheduler.Start();
                Assert(arrived.Wait(5000), "模拟角色未到达更换后的目标位置。");
                scheduler.Dispose();
                Assert(failure == null && actor.Entity.Pos == target && !movement.IsMoving(actor),
                    "模拟角色必须到达新目标，且工作线程不得出错：" + failure);
            }
        }

        public static void MovementStopAndDisposeCancelTheTick()
        {
            var world = new EntityTestWorld();
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler())
            using (var movement = new SimMovementController(scheduler))
            {
                var first = CreateActor(world, movement, scheduler, 0);
                var second = CreateActor(world, movement, scheduler, 1);
                movement.MoveTo(first, Vector2.One);
                movement.MoveTo(second, Vector2.One);
                movement.Stop(first);
                Assert(!movement.IsMoving(first) && movement.IsMoving(second) && scheduler.PendingCount == 1,
                    "停止一个模拟角色后，必须保留另一角色使用的共享更新节点。");
                movement.Stop(second);
                Assert(scheduler.PendingCount == 0, "停止最后一个移动后，必须移除空闲的更新节点。");
                movement.MoveTo(first, Vector2.One);
                movement.Dispose();
                movement.Dispose();
                Assert(scheduler.PendingCount == 0 && !movement.IsMoving(first),
                    "释放必须取消待执行更新节点，并恰好清除所有活动移动一次。");
                scheduler.Schedule(0.05, finished.Set);
                scheduler.Start();
                Assert(finished.Wait(5000), "调度器未运行至被取消的移动截止时间之后。");
                scheduler.Dispose();
                Assert(first.Entity.PositionWrites.Count == 0 && second.Entity.PositionWrites.Count == 0,
                    "已取消或释放的移动不得在之后写入任何位置。");
            }
        }

        public static void MovementDropsDetachedAndRecycledEntities()
        {
            var world = new EntityTestWorld();
            Exception failure = null;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => failure = error))
            using (var movement = new SimMovementController(scheduler))
            {
                var detached = CreateActor(world, movement, scheduler, 0);
                var stale = CreateActor(world, movement, scheduler, 1);
                movement.MoveTo(detached, Vector2.One);
                movement.MoveTo(stale, Vector2.One);
                detached.Dispose();
                world.Registry.RegisterCreated(1);
                scheduler.Schedule(0.05, finished.Set);
                scheduler.Start();
                Assert(finished.Wait(5000), "失效移动阻止了调度器执行下一个节点。");
                scheduler.Dispose();
                Assert(failure == null, "写入原生字段前必须跳过非活动角色：" + failure);
                Assert(!movement.IsMoving(detached) && !movement.IsMoving(stale),
                    "必须从移动控制器中移除已解除关联或被回收的实体。");
                Assert(detached.Entity.PositionWrites.Count == 0 && stale.Entity.PositionWrites.Count == 0,
                    "失效引用不得移动新生命周期中的实体。");
            }
        }

        public static void MovementRejectsRestartWhenItsQueuedTickWasCancelled()
        {
            foreach (var stopFirst in new[] { false, true })
            {
                var world = new EntityTestWorld();
                using (var scheduler = new SimScheduler())
                using (var movement = new SimMovementController(scheduler))
                {
                    var actor = CreateActor(world, movement, scheduler, 0);
                    actor.MoveTo(Vector2.One);
                    scheduler.Cancel();
                    if (stopFirst) actor.StopMoving();
                    Throws<InvalidOperationException>(() => actor.MoveTo(new Vector2(2, 2)));
                    Assert(!actor.IsMoving && scheduler.PendingCount == 0 && actor.Entity.PositionWrites.Count == 0,
                        "共享更新句柄已被调度器取消后，新移动必须回滚，不能留下永远不会更新的移动状态。");
                }
            }
        }

        public static void MovementRejectsInvalidInputWithoutReplacingMove()
        {
            var world = new EntityTestWorld();
            var target = Vector2.One;
            using (var arrived = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler())
            using (var movement = new SimMovementController(scheduler))
            {
                var actor = CreateActor(world, movement, scheduler, 0);
                actor.Entity.OnPositionWritten = value => { if (value == target) arrived.Set(); };
                movement.MoveTo(actor, target, 100);
                foreach (var speed in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                    Throws<ArgumentOutOfRangeException>(() => movement.MoveTo(actor, new Vector2(5, 5), speed));
                foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    Throws<ArgumentOutOfRangeException>(() => movement.MoveTo(actor, new Vector2(invalid, 5)));
                    Throws<ArgumentOutOfRangeException>(() => movement.MoveTo(actor, new Vector2(5, invalid)));
                }
                Assert(movement.IsMoving(actor) && scheduler.PendingCount == 1,
                    "拒绝请求后必须保留原目标及其唯一更新节点。");
                scheduler.Start();
                Assert(arrived.Wait(5000), "拒绝请求后丢失了原先有效的目标位置。");
                scheduler.Dispose();
                Assert(actor.Entity.Pos == target, "无效请求不得替换有效的移动。");
            }
        }

        public static void MovementAdvanceClampsLongFramesAndPreservesSpeed()
        {
            var advanced = SimMovementController.Advance(Vector2.Zero, new Vector2(3, 4), 2, 0.5, out var arrived);
            Near(new Vector2(0.6f, 0.8f), advanced);
            Assert(!arrived, "仅完成部分移动时不得将角色标记为已到达。");
            var target = new Vector2(3, 4);
            advanced = SimMovementController.Advance(advanced, target, 2, 60, out arrived);
            Assert(arrived && advanced == target, "长帧移动必须限制在目标位置，不得越过目标。");
            advanced = SimMovementController.Advance(target, target, 2, 0, out arrived);
            Assert(arrived && advanced == target, "已在目标位置的角色必须结束移动，不得对零向量进行归一化。");
            advanced = SimMovementController.Advance(Vector2.Zero, target, 2, 0, out arrived);
            Assert(!arrived && advanced == Vector2.Zero, "经过时间为零时不得移动角色。");
        }

        public static void ArrivalResetsAnimationBeforeOneSchedulerCallback()
        {
            var world = new EntityTestWorld();
            Exception failure = null;
            var calls = 0;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
            using (var movement = new SimMovementController(scheduler))
            {
                var actor = CreateActor(world, movement, scheduler, 0);
                actor.MoveTo(Vector2.One, 100, animationId: 13, onArrived: () =>
                {
                    calls++;
                    Assert(scheduler.IsSchedulerThread, "到达回调必须在所属调度线程执行。");
                    Assert(!actor.IsMoving && actor.Pos == Vector2.One,
                        "到达回调执行前，角色必须位于终点且不再标记为移动中。");
                    Assert(actor.Entity.BaseTimelineId == 0 && actor.Entity.BaseTimelineResetCount == 1,
                        "到达回调执行前必须先停止本次移动拥有的动画。");
                    scheduler.ScheduleAfter(0.04, finished.Set);
                });
                Assert(actor.Entity.PlayedBaseTimelines.Count == 0,
                    "排队移动不得提前在调用线程播放动画。");
                scheduler.Start();
                Assert(finished.Wait(5000), "未执行到达回调。");
                scheduler.Dispose();
                Assert(failure == null, "到达处理失败：" + failure);
                Assert(calls == 1 && actor.Entity.PlayedBaseTimelines.SequenceEqual(new ushort[] { 13 }),
                    "一次移动应只开始一次动画，并恰好调用一次到达回调。");
            }
        }

        public static void StopRetargetDeleteAndDetachDiscardOldArrival()
        {
            var spawner = new Simulant.Core.Entity.EntitySpawner();
            Exception failure = null;
            var obsoleteCalls = 0;
            var newCalls = 0;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
            {
                var manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                try
                {
                    var stopped = manager.SpawnBNpc(1);
                    var replaced = manager.SpawnBNpc(2);
                    var deleted = manager.SpawnBNpc(3);
                    var detached = manager.Attach(spawner.World.CreateCharacter(0));
                    var actors = new[] { stopped, replaced, deleted, detached };
                    foreach (var actor in actors)
                        actor.MoveTo(new Vector2(100, 0), animationId: 13,
                            onArrived: () => obsoleteCalls++);
                    scheduler.Schedule(0.04, () =>
                    {
                        Assert(actors.All(actor => actor.Entity.PlayedBaseTimelines.Count == 1),
                            "取消测试必须先让所有角色真正开始移动动画。");
                        stopped.StopMoving();
                        replaced.MoveTo(Vector2.One, 100, animationId: 14, onArrived: () =>
                        {
                            newCalls++;
                            scheduler.ScheduleAfter(0.04, finished.Set);
                        });
                        manager.Delete(deleted);
                        manager.Detach(detached);
                        Assert(!stopped.IsMoving && stopped.Entity.BaseTimelineId == 0,
                            "停止移动必须立即回收自己的动画。");
                        Assert(!deleted.IsActive && !deleted.Entity.IsAlive &&
                            !detached.IsActive && detached.Entity.IsAlive,
                            "删除应释放实体，解除关联应保留实体，二者都应停止移动。");
                    });
                    scheduler.Start();
                    Assert(finished.Wait(5000), "更换后的移动未完成。");
                    scheduler.Dispose();
                    Assert(failure == null, "取消或替换移动失败：" + failure);
                    Assert(obsoleteCalls == 0 && newCalls == 1 && replaced.Pos == Vector2.One,
                        "停止、更换目标、删除和解除关联不得触发旧到达回调，新移动必须正常完成。");
                }
                finally { manager.Clear(); }
            }
        }

        public static void ArrivalCallbackCanChainSelfAndRetargetAnotherActor()
        {
            var world = new EntityTestWorld();
            Exception failure = null;
            var firstCalls = 0;
            var nextCalls = 0;
            var peerCalls = 0;
            using (var finished = new CountdownEvent(2))
            using (var scheduler = new SimScheduler((error, _) => failure = error))
            using (var movement = new SimMovementController(scheduler))
            {
                var actor = CreateActor(world, movement, scheduler, 0);
                var peer = CreateActor(world, movement, scheduler, 1);
                peer.MoveTo(new Vector2(100, 0), onArrived: () =>
                    { throw new Exception("被替换的另一角色移动不得触发旧到达回调。"); });
                actor.MoveTo(Vector2.One, 100, animationId: 13, onArrived: () =>
                {
                    firstCalls++;
                    actor.MoveTo(new Vector2(-1, 0), 100, animationId: 14, onArrived: () =>
                    {
                        nextCalls++;
                        finished.Signal();
                    });
                    peer.MoveTo(new Vector2(0, -1), 100, animationId: 15, onArrived: () =>
                    {
                        peerCalls++;
                        finished.Signal();
                    });
                });
                scheduler.Start();
                Assert(finished.Wait(5000), "到达回调中新建的自身移动或另一角色移动没有完成：" + failure);
                scheduler.Dispose();
                Assert(failure == null && firstCalls == 1 && nextCalls == 1 && peerCalls == 1,
                    "回调重入不得丢失或重复执行新一段移动：" + failure);
                Assert(actor.Pos == new Vector2(-1, 0) && peer.Pos == new Vector2(0, -1) &&
                    !actor.IsMoving && !peer.IsMoving && scheduler.PendingCount == 0,
                    "连续移动完成后必须精确到达各自新终点，且没有遗留更新节点。");
                Assert(actor.Entity.BaseTimelineId == 0 && peer.Entity.BaseTimelineId == 0,
                    "连续移动不能遗留任何一段拥有的动画。");
            }
        }

        public static void ArrivalCallbackCanCancelOtherCompletedArrivals()
        {
            foreach (var cancellation in new[] { 0, 1, 2 })
            {
                var world = new EntityTestWorld();
                var calls = 0;
                Exception failure = null;
                using (var finished = new ManualResetEventSlim())
                using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
                using (var movement = new SimMovementController(scheduler))
                {
                    var actors = Enumerable.Range(0, 3)
                        .Select(index => CreateActor(world, movement, scheduler, (ushort)index)).ToArray();
                    foreach (var actor in actors)
                        actor.MoveTo(Vector2.Zero, onArrived: () =>
                        {
                            calls++;
                            foreach (var other in actors)
                            {
                                if (ReferenceEquals(actor, other)) continue;
                                if (cancellation == 0) other.StopMoving();
                                else if (cancellation == 1) other.MoveTo(new Vector2(100, 0));
                                else other.Dispose();
                            }
                            scheduler.ScheduleAfter(0.04, finished.Set);
                        });
                    scheduler.Start();
                    Assert(finished.Wait(5000), "同批到达回调取消测试未结束。");
                    scheduler.Dispose();
                    Assert(failure == null && calls == 1,
                        "前一个到达回调停止、替换或解除同批角色关联时，尚未调用的旧回调必须作废：" + failure);
                }
            }
        }

        public static void ArrivalCallbackCanCancelItsScheduler()
        {
            var world = new EntityTestWorld();
            var calls = 0;
            Exception failure = null;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
            using (var movement = new SimMovementController(scheduler))
            {
                for (ushort index = 0; index < 3; index++)
                    CreateActor(world, movement, scheduler, index).MoveTo(Vector2.Zero, onArrived: () =>
                    {
                        calls++;
                        scheduler.Cancel();
                        finished.Set();
                    });
                scheduler.Start();
                Assert(finished.Wait(5000), "到达回调没有执行调度器取消。");
                scheduler.Dispose();
                Assert(failure == null && calls == 1 && scheduler.PendingCount == 0,
                    "到达回调取消所属调度器后，同批尚未执行的回调必须停止：" + failure);
            }
        }

        public static void ArrivalCallbackDoesNotHoldTheMovementLock()
        {
            var world = new EntityTestWorld();
            Exception failure = null;
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var stopped = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => failure = error))
            using (var movement = new SimMovementController(scheduler))
            {
                var peer = CreateActor(world, movement, scheduler, 0);
                var actor = CreateActor(world, movement, scheduler, 1);
                peer.MoveTo(new Vector2(100, 0));
                actor.MoveTo(Vector2.Zero, onArrived: () =>
                {
                    entered.Set();
                    Assert(release.Wait(5000), "未释放到达回调等待屏障。");
                });
                var stopThread = new Thread(() =>
                {
                    try { peer.StopMoving(); }
                    catch (Exception error) { failure = error; }
                    finally { stopped.Set(); }
                }) { IsBackground = true };
                scheduler.Start();
                Assert(entered.Wait(5000), "到达回调没有进入等待屏障。");
                var stoppedBeforeRelease = false;
                try
                {
                    stopThread.Start();
                    stoppedBeforeRelease = stopped.Wait(1000);
                }
                finally { release.Set(); }
                scheduler.Dispose();
                Assert(stopThread.Join(5000), "停止移动的辅助线程未退出。");
                Assert(stoppedBeforeRelease && failure == null && !peer.IsMoving,
                    "到达回调等待期间，其他线程应能停止角色，回调不得持有移动锁：" + failure);
            }
        }

        public static void InvalidArrivalCallbackPreservesTheCurrentMovement()
        {
            var world = new EntityTestWorld();
            var calls = 0;
            var invalidRan = false;
            Exception failure = null;
            Action invalid = async () => { invalidRan = true; await Task.Yield(); };
            Action multicast = () => { invalidRan = true; };
            multicast += invalid;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
            using (var movement = new SimMovementController(scheduler))
            {
                var actor = CreateActor(world, movement, scheduler, 0);
                actor.MoveTo(Vector2.One, 100, animationId: 13, onArrived: () =>
                {
                    calls++;
                    finished.Set();
                });
                Throws<ArgumentException>(() => actor.MoveTo(new Vector2(2, 2), onArrived: invalid));
                Throws<ArgumentException>(() => actor.MoveTo(new Vector2(2, 2), onArrived: multicast));
                Throws<ArgumentOutOfRangeException>(() => actor.MoveTo(new Vector2(2, 2), animationId: 0));
                Assert(actor.IsMoving && scheduler.PendingCount == 1,
                    "无效回调或动画编号不得破坏当前移动的目标、回调和唯一更新节点。");
                scheduler.Start();
                Assert(finished.Wait(5000), "拒绝无效请求后，原移动未正常到达。");
                scheduler.Dispose();
                Assert(failure == null && calls == 1 && !invalidRan && actor.Pos == Vector2.One,
                    "拒绝请求必须保留原到达回调，且不得执行无效回调：" + failure);
            }
        }

        public static void MovementCleanupPreservesExternalAnimationEvenWithTheSameId()
        {
            foreach (ushort externalAnimation in new ushort[] { 13, 14 })
            {
                var world = new EntityTestWorld();
                Exception failure = null;
                using (var finished = new ManualResetEventSlim())
                using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
                using (var movement = new SimMovementController(scheduler))
                {
                    var actor = CreateActor(world, movement, scheduler, 0);
                    actor.MoveTo(new Vector2(100, 0), animationId: 13);
                    scheduler.Schedule(0.04, () =>
                    {
                        Assert(actor.Entity.BaseTimelineId == 13,
                            "外部动画接管测试必须先启动移动动画。");
                        actor.Entity.PlayBaseTimeline(externalAnimation);
                        actor.StopMoving();
                        Assert(actor.Entity.BaseTimelineId == externalAnimation &&
                            actor.Entity.BaseTimelineResetCount == 0,
                            "旧移动收尾不得重置后来播放的动画，即使两次动画编号相同。");
                        finished.Set();
                    });
                    scheduler.Start();
                    Assert(finished.Wait(5000), "外部动画接管测试未完成。");
                    scheduler.Dispose();
                    Assert(failure == null, "外部动画被移动收尾覆盖：" + failure);
                }
            }
        }

        public static void MovementYieldsAnimationToCastingWithoutRestartingIt()
        {
            var world = new EntityTestWorld();
            Exception failure = null;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
            using (var movement = new SimMovementController(scheduler))
            {
                var alreadyCasting = CreateActor(world, movement, scheduler, 0);
                var laterCasting = CreateActor(world, movement, scheduler, 1);
                alreadyCasting.Entity.PlayBaseTimeline(21);
                alreadyCasting.Entity.SetCastInfo(1, 1);
                alreadyCasting.MoveTo(new Vector2(100, 0), animationId: 13);
                laterCasting.MoveTo(new Vector2(100, 0), animationId: 13);
                scheduler.Schedule(0.04, () =>
                {
                    Assert(alreadyCasting.Entity.BaseTimelineId == 21 &&
                        alreadyCasting.Entity.PlayedBaseTimelines.Count == 1,
                        "开始移动时已经读条，不得用移动动画覆盖原动画。");
                    Assert(laterCasting.Entity.BaseTimelineId == 13,
                        "中途施法测试必须先启动移动动画。");
                    laterCasting.Entity.SetCastInfo(2, 1);
                    scheduler.ScheduleAfter(0.05, () =>
                    {
                        Assert(laterCasting.IsMoving && laterCasting.Entity.BaseTimelineId == 0 &&
                            laterCasting.Entity.CastingActionId == 2,
                            "移动途中读条必须让出自己的动画，保留位移和施法状态。");
                        alreadyCasting.Entity.CastingActionId = 0;
                        laterCasting.Entity.CastingActionId = 0;
                        scheduler.ScheduleAfter(0.04, () =>
                        {
                            Assert(alreadyCasting.Entity.PlayedBaseTimelines.Count == 1 &&
                                laterCasting.Entity.PlayedBaseTimelines.Count == 1,
                                "读条结束后旧移动不得自行恢复已经让出的动画。");
                            alreadyCasting.StopMoving();
                            laterCasting.StopMoving();
                            Assert(alreadyCasting.Entity.BaseTimelineId == 21 &&
                                laterCasting.Entity.BaseTimelineResetCount == 1,
                                "结束移动只能清理本次真正拥有的动画，且不得重复清理。");
                            finished.Set();
                        });
                    });
                });
                scheduler.Start();
                Assert(finished.Wait(5000), "移动与读条衔接测试未完成。");
                scheduler.Dispose();
                Assert(failure == null, "移动与读条动画衔接失败：" + failure);
            }
        }

        public static void ActorDetachStopsBehaviorEvenWhenCastCleanupFails()
        {
            var world = new EntityTestWorld();
            var entity = world.CreateCharacter(0);
            var expected = new InvalidOperationException("读条清理失败");
            Simulant.ACT.TriggernometryInterop.Reset();
            Simulant.Game.Data.GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Action>()[1] =
                new Lumina.Excel.Sheets.Action
                {
                    RowId = 1, Cast100ms = 10, CastType = 2, EffectRange = 5,
                    Omen = new Lumina.Excel.RowRef<Lumina.Excel.Sheets.Omen> { RowId = 1, ValueNullable = new Lumina.Excel.Sheets.Omen { Path = "test_omen" } }
                };
            using (var scheduler = new SimScheduler())
            using (var casts = new SimCastController(scheduler))
            using (var movement = new SimMovementController(scheduler))
            {
                var actor = new SimActor(entity, casts, movement);
                actor.Cast(1);
                actor.MoveTo(Vector2.One);
                Assert(actor.IsActive && actor.IsMoving, "新关联的模拟角色必须能够控制自身移动。");
                Simulant.ACT.TriggernometryInterop.OnCommand = command =>
                {
                    if (command.StartsWith("Action: Remove")) throw expected;
                };
                try
                {
                    try { actor.Dispose(); throw new Exception("预期抛出原始的读条清理错误。"); }
                    catch (InvalidOperationException error)
                    {
                        Assert(ReferenceEquals(error, expected), "释放模拟角色时必须保留原始清理错误。");
                    }
                }
                finally { Simulant.ACT.TriggernometryInterop.OnCommand = null; }
                Assert(!actor.IsActive && !actor.IsMoving && scheduler.PendingCount == 0,
                    "即使其他行为清理失败，Detach 也必须停止移动。");
                Assert(entity.IsAlive, "解除模拟角色关联不得销毁底层游戏实体或使其失效。");
                actor.Dispose();
                Assert(entity.FinalizedCasts.Count == 1 &&
                    Simulant.ACT.TriggernometryInterop.Commands.Count(command => command.StartsWith("Action: Remove")) == 1,
                    "重复释放不得重复清理行为。");
                Throws<InvalidOperationException>(() => actor.Pos = Vector2.One);
                actor.MoveTo(Vector2.One);
                Assert(scheduler.PendingCount == 0 && entity.PositionWrites.Count == 0,
                    "旧模拟角色不得立即写入位置，也不得重新启动延迟移动。");
            }
        }

        public static void NetworkMovementSharesPollingAndUsesActualArrival()
        {
            Exception failure = null;
            using (var arrived = new CountdownEvent(2))
            using (var scheduler = new SimScheduler((error, _) => { failure = error; }))
            using (var movement = new SimMovementController(scheduler))
            {
                var first = CreateActor(new EntityTestWorld(), movement, scheduler, 2);
                var second = CreateActor(new EntityTestWorld(), movement, scheduler, 2);
                var local = CreateActor(new EntityTestWorld(), movement, scheduler, 0);
                first.Entity.Pos3D = new Vector3(0, 0, 7);
                var writesBefore = first.Entity.PositionWrites.Count;
                var callbacks = 0;
                Action callback = () =>
                {
                    Assert(scheduler.IsSchedulerThread, "网络到达回调必须在共享调度线程执行。");
                    callbacks++;
                    arrived.Signal();
                };
                first.MoveNetworkTo(new Vector2(3, 4), onArrived: () =>
                {
                    Assert(scheduler.ElapsedSeconds >= 5.0 / 3, "即使原生位置提前抵达，也不能跳过按速度计算的移动时间。");
                    callback();
                });
                second.MoveNetworkTo(new Vector2(-4, 0), speedParam: 40, onArrived: () =>
                {
                    Assert(scheduler.ElapsedSeconds >= 1, "网络移动到达不能早于自身路程所需时间。");
                    callback();
                });
                local.MoveTo(new Vector2(100, 0));
                Assert(scheduler.PendingCount == 1 && first.Entity.NetworkMoves.Count == 0,
                    "本地和多个网络角色应共享一个节点，调用线程不能提前发送移动包。");
                scheduler.Schedule(0.32, () =>
                {
                    Assert(first.IsMoving && second.IsMoving && callbacks == 0,
                        "只发送移动包不能视作到达，必须等待实际坐标接近目标。");
                    Assert(first.Entity.NetworkMoves.Count >= 2 && first.Entity.NetworkMoves.Count <= 4 &&
                        second.Entity.NetworkMoves.Count >= 2 && second.Entity.NetworkMoves.Count <= 4,
                        "网络角色应按低频共享节拍发送沿途位置，不应随本地插值每帧发包。");
                    Assert(first.Entity.PositionReads <= 5 && second.Entity.PositionReads <= 5 && local.Entity.PositionWrites.Count >= 5,
                        "本地高频插值不能增加网络角色跨进程位置读取频率。");
                    Assert(first.Entity.PositionWrites.Count == writesBefore && first.Entity.PlayedBaseTimelines.Count == 0,
                        "网络移动不得直接插值位置或强制覆盖移动动画。");
                    first.Entity.Pos = new Vector2(3.02f, 4.01f);
                    second.Entity.Pos = new Vector2(-4.02f, 0);
                    local.StopMoving();
                });
                scheduler.Start();
                Assert(arrived.Wait(5000), "两个网络角色未按实际位置完成到达回调：" + failure);
                scheduler.Dispose();
                Assert(failure == null && callbacks == 2 && !first.IsMoving && !second.IsMoving,
                    "网络到达应各执行一次回调并移除移动状态：" + failure);
                var packet = first.Entity.NetworkMoves.First();
                Assert(packet.Position == new Vector3(0, 0, 7) && packet.SpeedParam == 30 &&
                    Math.Abs(packet.Heading - Math.Atan2(3, 4)) < 0.00001,
                    "首包应报告出发点、保留 ACT 高度，按目标计算面向并采用默认网络速度参数。");
                Assert(first.Entity.NetworkMoves.Last().Position == new Vector3(3, 4, 7) &&
                    first.Entity.NetworkMoves.Count(move => move.Position == new Vector3(3, 4, 7)) == 1 &&
                    second.Entity.NetworkMoves.Last().Position == new Vector3(-4, 0, 0),
                    "终点应精确补发一次，正常到达不应额外发停止包改变角色朝向。");
            }
        }

        public static void NetworkMovementStreamsElapsedPositionsWithoutEarlyCompletion()
        {
            Exception failure = null;
            var samples = new List<Tuple<double, Vector3>>();
            var callbacks = 0;
            using (var finished = new ManualResetEventSlim())
            using (var alreadyThere = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); alreadyThere.Set(); }))
            using (var movement = new SimMovementController(scheduler))
            {
                var actor = CreateActor(new EntityTestWorld(), movement, scheduler, 2);
                var target = new Vector2(2.22f, 0);
                actor.Entity.OnActorMove = (position, _, __) => samples.Add(Tuple.Create(scheduler.ElapsedSeconds, position));
                actor.MoveNetworkTo(target, speedParam: 60, onArrived: () => { callbacks++; finished.Set(); });
                scheduler.Schedule(0.12, () => Thread.Sleep(220));
                scheduler.Schedule(0.65, () =>
                {
                    Assert(actor.IsMoving && callbacks == 0, "路径时间走完仍须等待实际位置，不能只按预估时间回调。");
                    Assert(samples.Count(sample => sample.Item2.X == target.X) == 1,
                        "最后一个不足节拍的分段必须补发，等待实际到达期间不能重发同一终点。");
                    actor.Entity.Pos = target;
                });
                scheduler.Start();
                Assert(finished.Wait(5000), "分段网络移动未完成：" + failure);
                Assert(failure == null && callbacks == 1, "分段网络移动不应提前完成或报告异常：" + failure);
                var packetCount = samples.Count;
                actor.MoveNetworkTo(target, speedParam: 60, onArrived: alreadyThere.Set);
                Assert(alreadyThere.Wait(5000), "已经在目标点的网络移动没有完成。");
                scheduler.Dispose();
                Assert(failure == null && samples.Count == packetCount, "已在目标点时不能再发送移动包：" + failure);

                var start = samples[0].Item1;
                Assert(samples.Count >= 3 && samples.Count <= 5, "掉帧后应跳过过时的中间包，不得补发一串追赶帧。");
                for (var index = 1; index < samples.Count; index++)
                {
                    var expected = Math.Min(target.X, 6 * (samples[index].Item1 - start));
                    Assert(Math.Abs(samples[index].Item2.X - expected) < 0.05,
                        "网络包位置应根据真实经过时间计算，既不能提前发送未来位置，也不能按固定帧数慢慢追赶。");
                    Assert(samples[index].Item2.X > samples[index - 1].Item2.X, "途中位置必须前进，不能反复发送同一坐标。");
                }
                Assert(samples.Last().Item1 - start >= target.X / 6 - 0.005,
                    "最终目的地不得在计划路程完成前发送。");
                Assert(samples.Last().Item1 - start < 0.45,
                    "尾段不能为了等下一个完整轮询间隔而推迟到位。");
            }
        }

        public static void NetworkAndLocalMovementReplaceEachOtherAndChainCallbacks()
        {
            Exception failure = null;
            var obsoleteCalls = 0;
            var localCalls = 0;
            var finalCalls = 0;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
            using (var movement = new SimMovementController(scheduler))
            {
                var actor = CreateActor(new EntityTestWorld(), movement, scheduler, 2);
                actor.MoveNetworkTo(new Vector2(10, 0), onArrived: () => obsoleteCalls++);
                scheduler.Schedule(0.03, () =>
                {
                    actor.Entity.Pos3D = new Vector3(1, 2, 3);
                    actor.Entity.Heading = 0.7f;
                    actor.MoveNetworkTo(new Vector2(20, 0), onArrived: () => obsoleteCalls++);
                    var stop = actor.Entity.NetworkMoves.Last();
                    Assert(stop.Position == new Vector3(1, 2, 3) && stop.Heading == 0.7f && stop.SpeedParam == 30,
                        "更换网络目标应先向实际当前位置发送停止包，保留原移动速度和当前面向。");
                });
                scheduler.Schedule(0.06, () =>
                {
                    actor.MoveTo(new Vector2(1, 2), animationId: 13, onArrived: () =>
                    {
                        localCalls++;
                        Assert(actor.Entity.BaseTimelineId == 0 && !actor.IsMoving,
                            "从本地到达回调切回网络移动前必须结束旧移动和动画。");
                        actor.MoveNetworkTo(new Vector2(2, 2), onArrived: () =>
                        {
                            finalCalls++;
                            finished.Set();
                        });
                    });
                    Assert(actor.Entity.NetworkMoves.Last().Position == new Vector3(1, 2, 3),
                        "本地移动替换网络移动时也必须先停止旧的原生移动。");
                });
                scheduler.Schedule(0.15, () => actor.Entity.Pos = new Vector2(2, 2));
                scheduler.Start();
                Assert(finished.Wait(5000), "本地与网络移动的到达链未完成。");
                scheduler.Dispose();
                Assert(failure == null && obsoleteCalls == 0 && localCalls == 1 && finalCalls == 1 && !actor.IsMoving,
                    "两种移动必须互斥，取消旧回调，并允许到达回调启动下一段：" + failure);
                Assert(actor.Entity.NetworkMoves.Count >= 6 && actor.Entity.NetworkMoves.Last().Position == new Vector3(2, 2, 3) &&
                    actor.Entity.NetworkMoves.All(move => move.Position.X < 3),
                    "替换后只能推进当前路线，已取消的旧远端目标不能迟到发出。");
            }
        }

        public static void NetworkMovementStopFailuresRemainRetryable()
        {
            foreach (var mode in new[] { "停止", "解除关联", "释放控制器" })
            {
                var callbackCalls = 0;
                using (var sent = new ManualResetEventSlim())
                using (var scheduler = new SimScheduler())
                using (var movement = new SimMovementController(scheduler))
                {
                    var actor = CreateActor(new EntityTestWorld(), movement, scheduler, 2);
                    actor.Entity.OnActorMove = (_, __, ___) => sent.Set();
                    actor.MoveNetworkTo(new Vector2(10, 0), onArrived: () => callbackCalls++);
                    scheduler.Start();
                    Assert(sent.Wait(5000), "网络移动没有发出首包。");
                    scheduler.Cancel();
                    actor.Entity.OnActorMove = (_, __, ___) => { throw new InvalidOperationException("模拟原生停止失败。"); };
                    Action stop = mode == "停止" ? (Action)actor.StopMoving :
                        mode == "解除关联" ? actor.Dispose : (Action)movement.Dispose;
                    if (mode == "释放控制器") Throws<AggregateException>(stop);
                    else Throws<InvalidOperationException>(stop);
                    Assert(actor.Entity.NetworkMoves.Count == 2, "停止失败后应保留已经尝试发送停止包的证据。");
                    actor.Entity.OnActorMove = null;
                    stop();
                    Assert(actor.Entity.NetworkMoves.Count == 3 && actor.Entity.NetworkMoves.Last().Position == Vector3.Zero,
                        mode + "失败后必须仍能重试原移动的停止，不能丢失记录。");
                    stop();
                    Assert(actor.Entity.NetworkMoves.Count == 3 && callbackCalls == 0 && !actor.IsMoving,
                        "成功停止后不得重复发包或补执行已取消的到达回调。");
                }
            }
        }

        public static void NetworkMovementTimeoutStopsWithoutArriving()
        {
            Exception failure = null;
            var callbackCalls = 0;
            using (var failed = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; failed.Set(); }))
            using (var movement = new SimMovementController(scheduler, networkTimeoutSeconds: 0.15))
            {
                var actor = CreateActor(new EntityTestWorld(), movement, scheduler, 2);
                actor.MoveNetworkTo(new Vector2(10, 0), speedParam: 40, onArrived: () => callbackCalls++);
                scheduler.Start();
                Assert(failed.Wait(5000), "没有实际移动的角色应在截止时间后明确报告失败。");
                scheduler.Dispose();
                Assert(failure is TimeoutException && callbackCalls == 0 && !actor.IsMoving && !scheduler.IsRunning,
                    "超时应结束移动并交给调度器失败路径，不能冒充到达。");
                Assert(actor.Entity.NetworkMoves.Count >= 2 && actor.Entity.NetworkMoves.Last().Position == Vector3.Zero &&
                    actor.Entity.NetworkMoves.Last().SpeedParam == 40 &&
                    actor.Entity.NetworkMoves.Count(move => move.Position == Vector3.Zero) == 2,
                    "超时必须用原速度参数向实际当前位置发送一次停止包。");
            }
        }

        public static void NetworkMovementRejectsInvalidOrCancelledRequestsBeforeDispatch()
        {
            var world = new EntityTestWorld();
            using (var scheduler = new SimScheduler())
            using (var movement = new SimMovementController(scheduler))
            {
                var actor = CreateActor(world, movement, scheduler, 2);
                actor.MoveNetworkTo(new Vector2(10, 0));
                Action asynchronous = async () => await Task.Yield();
                Throws<ArgumentException>(() => actor.MoveNetworkTo(new Vector2(20, 0), onArrived: asynchronous));
                Throws<ArgumentOutOfRangeException>(() => actor.MoveNetworkTo(new Vector2(float.NaN, 0)));
                Throws<ArgumentOutOfRangeException>(() => actor.MoveNetworkTo(new Vector2(1001, 0)));
                Throws<ArgumentOutOfRangeException>(() => actor.MoveNetworkTo(Vector2.One, speedParam: 0));
                foreach (ushort slot in new ushort[] { 0, 1, 3 })
                {
                    var unsuitable = CreateActor(world, movement, scheduler, slot);
                    unsuitable.MoveTo(Vector2.One);
                    Throws<InvalidOperationException>(() => unsuitable.MoveNetworkTo(Vector2.One));
                    Assert(unsuitable.IsMoving, "拒绝非网络表角色时不得取消其已有的本地移动。");
                    unsuitable.StopMoving();
                }
                var localRegistry = new Simulant.Core.Entity.EntityRegistry(slot => slot == 200 ?
                    new Simulant.Core.Entity.EntityObservation(new IntPtr(2000), 2000,
                        Simulant.Game.FFCS.Client.Game.Object.ObjectKind.BattleNpc) :
                    (Simulant.Core.Entity.EntityObservation?)null, 201);
                var localEntity = (Simulant.Core.Entity.Character)localRegistry.Get(200);
                var localActor = new SimActor(localEntity, new SimCastController(scheduler), movement);
                localActor.MoveTo(Vector2.One);
                Throws<InvalidOperationException>(() => localActor.MoveNetworkTo(Vector2.One));
                Assert(localActor.IsMoving && localEntity.NetworkMoves.Count == 0,
                    "本地角色表的偶数槽位也必须明确拒绝网络移动，同时保留旧本地移动。");
                localActor.StopMoving();
                Assert(actor.IsMoving && scheduler.PendingCount == 1 && actor.Entity.NetworkMoves.Count == 0,
                    "无效参数不得替换原网络移动或提前发包。");
                actor.StopMoving();
                Assert(scheduler.PendingCount == 0 && actor.Entity.NetworkMoves.Count == 0,
                    "首包发送前停止不得向客户端发送多余停止包。");
                actor.MoveNetworkTo(new Vector2(10, 0));
                scheduler.Cancel();
                Throws<InvalidOperationException>(() => actor.MoveNetworkTo(new Vector2(20, 0)));
                Assert(!actor.IsMoving && actor.Entity.NetworkMoves.Count == 0 && scheduler.PendingCount == 0,
                    "已取消调度器不能留下无法执行的新移动或发送原生包。");
            }
        }

        public static void ManagerRetainsFailedBorrowedNetworkMovementCleanup()
        {
            var spawner = new Simulant.Core.Entity.EntitySpawner();
            var cleanupErrors = 0;
            using (var sent = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler())
            using (var replacementScheduler = new SimScheduler())
            {
                var manager = new SimEntityManager(spawner, _ => cleanupErrors++) { Scheduler = scheduler };
                var actor = manager.Attach(spawner.World.CreateCharacter(2));
                actor.Entity.OnActorMove = (_, __, ___) => sent.Set();
                actor.MoveNetworkTo(new Vector2(10, 0));
                Assert(!manager.HasPendingCleanup, "正常借用角色的移动不能误报失败清理待办。");
                scheduler.Start();
                Assert(sent.Wait(5000), "借用网络角色没有发出首包。");
                scheduler.Cancel();
                actor.Entity.OnActorMove = (_, __, ___) => { throw new InvalidOperationException("模拟借用角色停止失败。"); };
                Throws<InvalidOperationException>(() => manager.Detach(actor));
                manager.Clear();
                Assert(manager.HasPendingCleanup && cleanupErrors > 0 && actor.Entity.IsAlive,
                    "解除关联后借用实体仍然有效，原生停止失败必须保留到后续 Clear 重试。");
                Throws<InvalidOperationException>(() => manager.Scheduler = replacementScheduler);
                actor.Entity.OnActorMove = null;
                var failedAttempts = actor.Entity.NetworkMoves.Count;
                manager.Clear();
                Assert(!manager.HasPendingCleanup && actor.Entity.NetworkMoves.Count == failedAttempts + 1 && actor.Entity.IsAlive,
                    "停止恢复后 Clear 必须完成一次原生停止而不删除借用实体，并解除重启限制。");
                manager.Clear();
                Assert(actor.Entity.NetworkMoves.Count == failedAttempts + 1, "成功停止后重复清理不能再次发包。");
                manager.Scheduler = replacementScheduler;
                manager.Clear();
            }
        }

        public static void NetworkMovementDetachingDuringDispatchStillStopsTheOwnedMove()
        {
            Exception failure = null;
            Exception detachFailure = null;
            using (var failed = new ManualResetEventSlim())
            using (var detached = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; failed.Set(); }))
            using (var movement = new SimMovementController(scheduler))
            {
                var actor = CreateActor(new EntityTestWorld(), movement, scheduler, 2);
                var expected = new InvalidOperationException("模拟移动包部分成功后失败。");
                actor.Entity.OnActorMove = (_, __, ___) =>
                {
                    if (actor.Entity.NetworkMoves.Count != 1) return;
                    var thread = new Thread(() =>
                    {
                        try { actor.Dispose(); }
                        catch (Exception error) { detachFailure = error; }
                        finally { detached.Set(); }
                    }) { IsBackground = true };
                    thread.Start();
                    Assert(SpinWait.SpinUntil(() => !actor.IsActive, 5000), "解除关联线程没有进入停止路径。");
                    throw expected;
                };
                actor.MoveNetworkTo(new Vector2(10, 0));
                scheduler.Start();
                Assert(failed.Wait(5000) && detached.Wait(5000), "发包与并发解除关联没有按期结束。");
                scheduler.Dispose();
                Assert(ReferenceEquals(failure, expected) && detachFailure == null && actor.Entity.IsAlive,
                    "发包失败必须报告原异常，解除关联应保留原生实体。");
                Assert(actor.Entity.NetworkMoves.Count == 2 && actor.Entity.NetworkMoves[1].Position == Vector3.Zero,
                    "actor 已解除关联但实体仍活着时，不能误当世界失效而丢弃停止责任。");
                Assert(!movement.HasPendingCleanup && !actor.IsMoving, "成功停止后不应留下清理待办。");
            }
        }

        public static void NetworkMovementNeverStopsARecycledEntity()
        {
            var world = new EntityTestWorld();
            using (var sent = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler())
            using (var movement = new SimMovementController(scheduler))
            {
                var actor = CreateActor(world, movement, scheduler, 2);
                actor.Entity.OnActorMove = (_, __, ___) => sent.Set();
                actor.MoveNetworkTo(new Vector2(10, 0));
                scheduler.Start();
                Assert(sent.Wait(5000), "网络移动没有发出首包。");
                scheduler.Cancel();
                world.Registry.RegisterCreated(2);
                actor.StopMoving();
                actor.Dispose();
                movement.Dispose();
                Assert(actor.Entity.NetworkMoves.Count == 1 && !actor.IsMoving,
                    "原实体已经换代时，停止、解除关联和释放都不得向复用槽位的新实体发包。");
            }
        }

        private static SimActor CreateActor(EntityTestWorld world, SimMovementController movement, SimScheduler scheduler, ushort slot)
            => new SimActor(world.CreateCharacter(slot), new SimCastController(scheduler), movement);

        private static void Near(Vector2 expected, Vector2 actual)
            => Assert(Vector2.Distance(expected, actual) < 0.00001f, "移动距离不等于速度乘以经过时间。");

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
