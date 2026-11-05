using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace Simulant.Tests
{
    internal static class MovementTests
    {
        internal static void MovementSharesOneTickAndSchedulerThread()
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

        internal static void MovementRetargetsTheCurrentPosition()
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

        internal static void MovementStopAndDisposeCancelTheTick()
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

        internal static void MovementDropsDetachedAndRecycledEntities()
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

        internal static void MovementRejectsInvalidInputWithoutReplacingMove()
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

        internal static void MovementAdvanceClampsLongFramesAndPreservesSpeed()
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

        internal static void ActorDetachStopsBehaviorEvenWhenCastCleanupFails()
        {
            var world = new EntityTestWorld();
            var entity = world.CreateCharacter(0);
            var expected = new InvalidOperationException("读条清理失败");
            Simulant.ACT.TriggernometryInterop.Reset();
            Simulant.Game.ExtractedCsv.CsvManager.Instance.Get<Simulant.Game.ExtractedCsv.Rows.Action>()[1] =
                new Simulant.Game.ExtractedCsv.Rows.Action
                {
                    Index = 1, CastTime = 1, OmenId = 1, ShapeType = 2, ScaleY = 5,
                    Omen = new Simulant.Game.ExtractedCsv.Rows.Omen { Name = "test_omen" }
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
