using Simulant.Simulation.Geometry;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using Simulant.Game.ExtractedCsv;
using Simulant.Game.FFCS.Client.Game.Character;
using System;
using System.Numerics;
using System.Threading;
using ActionRow = Simulant.Game.ExtractedCsv.Rows.Action;

namespace Simulant.Tests
{
    internal static class BehaviorMovementTests
    {
        internal static void EntityChoosesDepartureAndHonorsDeadlineAndUrgency()
        {
            Exception failure = null;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
            using (var movement = new SimMovementController(scheduler))
            using (var casts = new SimCastController(scheduler))
            using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
            {
                var normal = CreateActor(behavior, movement, casts);
                var urgent = CreateActor(behavior, movement, casts);
                var immediate = CreateActor(behavior, movement, casts, delayArrival: false);
                urgent.ConfigureBehavior(new SimActorBehaviorOptions { RouteAngleDegrees = 90 });
                immediate.ConfigureBehavior(new SimActorBehaviorOptions { DelayArrival = false, RouteAngleDegrees = 90 });
                var destination = new Vector2(3, 0);
                normal.MoveForMechanic(destination, 3, holdUntil: 4);
                urgent.MoveForMechanic(destination, 3, holdUntil: 4, urgent: true);
                immediate.MoveForMechanic(destination, 3, holdUntil: 4);
                scheduler.Schedule(1.4, () =>
                {
                    Assert(Vector2.Distance(urgent.Pos, destination) < 0.025 && !urgent.IsMoving,
                        "紧急意图应立即按正常跑速到达，不等到目标时间才行动。");
                    Assert(normal.Pos == Vector2.Zero && !normal.IsMoving,
                        "有余裕的普通意图应由实体安排稍后出发，不能收到指令就强制移动。");
                    Assert(Vector2.Distance(immediate.Pos, destination) < 0.025 && !immediate.IsMoving,
                        "关闭延迟到达后，即使不是紧急意图也应尽早出发并到达。");
                    Assert(urgent.Entity.NetworkMoves.TrueForAll(move => move.Position.Y == 0) &&
                        immediate.Entity.NetworkMoves.TrueForAll(move => move.Position.Y == 0),
                        "紧急意图或关闭延迟到达时，即使配置允许绕路也必须沿直线直接就位。");
                });
                scheduler.Schedule(3, () =>
                {
                    Assert(Vector2.Distance(normal.Pos, destination) < 0.025 && !normal.IsMoving,
                        "只给目标位置和时间，实体也应在截止前完成路线。");
                });
                scheduler.Schedule(3.3, () =>
                {
                    Assert(normal.Pos == destination && urgent.Pos == destination && immediate.Pos == destination,
                        "提前到达后必须保持站位，直到机制结束。");
                    finished.Set();
                });
                scheduler.Start();
                Assert(finished.Wait(6000), "实体自主安排移动的检查未完成。");
                scheduler.Dispose();
                Assert(failure == null, "实体自主移动失败：" + failure);
            }
        }

        internal static void SmallCorrectionsReachTheRequestedRange()
        {
            Exception failure = null;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
            using (var movement = new SimMovementController(scheduler))
            using (var casts = new SimCastController(scheduler))
            using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
            {
                var actor = CreateActor(behavior, movement, casts);
                var area = SimPosition.Ellipse(new Vector2(0.08f, 0), 0.01f);
                actor.MoveForMechanic(area, 1, urgent: true);
                scheduler.Schedule(0.6, () =>
                {
                    Assert(area.Contains(actor.Pos) && !actor.IsMoving && actor.Entity.NetworkMoves.Count > 0,
                        "离终点不到十厘米但仍在容差外时，也必须通过移动包完成修正。");
                    finished.Set();
                });
                scheduler.Start();
                Assert(finished.Wait(5000), "小距离机制移动的检查未完成。");
                scheduler.Dispose();
                Assert(failure == null, "小距离机制移动失败：" + failure);
            }
        }

        internal static void ImmediateArrivalInterruptsAutomaticCasting()
        {
            var actions = CsvManager.Instance.Get<ActionRow>();
            actions.TryGetValue(25859, out var original);
            actions[25859] = new ActionRow { Index = 25859, CastTime = 5, Range = 25 };
            try
            {
                Exception failure = null;
                using (var finished = new ManualResetEventSlim())
                using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
                using (var movement = new SimMovementController(scheduler))
                using (var casts = new SimCastController(scheduler))
                using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
                {
                    var world = new EntityTestWorld();
                    var actor = new SimActor(world.CreateCharacter(2), casts, movement);
                    actor.Entity.Job = Job.WHM;
                    actor.Entity.OnActorMove = (position, _, __) => actor.Entity.Pos3D = position;
                    behavior.Add(actor, new SimActorBehaviorOptions { DelayArrival = false, IdleRadius = 0, RouteAngleDegrees = 90 });
                    var boss = new SimActor(world.CreateCharacter(0), casts, movement);
                    boss.Entity.Pos = new Vector2(0, 5);
                    behavior.SetTarget(boss);
                    scheduler.Schedule(2.3, () =>
                    {
                        Assert(casts.Remaining(actor) > 0, "检查前假玩家应已经开始自动读条。");
                        actor.MoveForMechanic(new Vector2(3, 0), 5, holdUntil: 6);
                        Assert(casts.Remaining(actor) == 0 && actor.Entity.CastingActionId == 0,
                            "关闭延迟后应立即取消演出读条，不能等到滑步窗口才动。");
                    });
                    scheduler.Schedule(3.5, () =>
                    {
                        Assert(actor.Pos == new Vector2(3, 0) && !actor.IsMoving,
                            "法系收到立即就位的意图后应连续跑到目标，不受之前读条影响。");
                        finished.Set();
                    });
                    scheduler.Start();
                    Assert(finished.Wait(6000), "立即就位取消读条的检查未完成。");
                    scheduler.Dispose();
                    Assert(failure == null, "立即就位取消读条失败：" + failure);
                }
            }
            finally
            {
                if (original == null) actions.Remove(25859);
                else actions[25859] = original;
            }
        }

        internal static void RoutesRespectAnglesAndScaleWithDistance()
        {
            var left = false;
            var right = false;
            foreach (var angle in new[] { 1d, 30d, 90d })
            foreach (var distance in new[] { 0.5f, 2f, 8f, 20f })
            for (var seed = 0; seed < 100; seed++)
            {
                var origin = new Vector2(31, -47);
                var forward = Vector2.Normalize(new Vector2(3, 4));
                var side = new Vector2(-forward.Y, forward.X);
                var destination = origin + forward * distance;
                var route = SimActorBehaviorController.CreateRoute(origin, destination, angle, new Random(seed));
                Assert(route[route.Length - 1] == destination, "绕路不能改变最终安全点。");
                var previous = origin;
                foreach (var point in route)
                {
                    var step = point - previous;
                    var ahead = Vector2.Dot(step, forward);
                    var sideways = Vector2.Dot(step, side);
                    var heading = Math.Atan2(Math.Abs(sideways), ahead) * 180 / Math.PI;
                    Assert(ahead >= -0.0001 && heading <= angle + 0.02,
                        "路线每一段都应向目标推进，且偏角不超过配置上限。");
                    Assert(Math.Abs(Vector2.Dot(point - origin, side)) <= distance * 0.3 * Math.Sin(angle * Math.PI / 180) + 0.0001,
                        "绕路偏移应随路程和角度缩小，不能让短距离移动绕出大圈。");
                    previous = point;
                }
                var firstSide = Vector2.Dot(route[0] - origin, side);
                left |= firstSide < 0;
                right |= firstSide > 0;
            }
            Assert(left && right, "绕路应能随机向左右两侧偏转。");
            Assert(SimActorBehaviorController.CreateRoute(Vector2.Zero, new Vector2(20, 0), 0, new Random(1)).Length == 1,
                "关闭绕路时应直接前往终点。");
            Assert(SimActorBehaviorController.CreateRoute(Vector2.Zero, new Vector2(0.1f, 0), 30, new Random(1)).Length == 1,
                "很小的站位修正不应额外插入拐点。");
        }

        internal static void IdleMeleeReturnsAfterTheHoldAndYieldsToNewMechanics()
        {
            Exception failure = null;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
            using (var movement = new SimMovementController(scheduler))
            using (var casts = new SimCastController(scheduler))
            using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
            {
                var melee = CreateActor(behavior, movement, casts, job: Job.VPR, position: new Vector2(10, 0));
                var immediate = CreateActor(behavior, movement, casts, delayArrival: false, job: Job.DRK, position: new Vector2(10, 0));
                var retarget = CreateActor(behavior, movement, casts, job: Job.MNK, position: new Vector2(20, 0));
                var ranged = CreateActor(behavior, movement, casts, job: Job.BRD, position: new Vector2(10, 0));
                var disabled = CreateActor(behavior, movement, casts, job: Job.VPR, position: new Vector2(10, 0));
                disabled.ConfigureBehavior(new SimActorBehaviorOptions { AttackTarget = false, IdleRadius = 0 });
                foreach (var actor in new[] { melee, immediate, retarget, ranged, disabled })
                    actor.MoveForMechanic(actor.Pos, 0.5, holdUntil: 1.5, urgent: true);
                var boss = new SimActor(new EntityTestWorld().CreateCharacter(0), casts, movement);
                boss.Entity.HitboxRadius = 2;
                behavior.SetTarget(boss);
                scheduler.Schedule(1.2, () =>
                {
                    Assert(melee.Pos.X == 10 && immediate.Pos.X == 10 && retarget.Pos.X == 20,
                        "机制判定要求保持站位时，近战不能提前离开远处的安全点。");
                });
                scheduler.Schedule(2, () =>
                {
                    Assert(retarget.IsMoving && retarget.Pos.X < 20, "机制结束后应主动返回近战，不等待空闲走动冷却。");
                    retarget.MoveForMechanic(new Vector2(22, 0), 3.7, holdUntil: 4.5, urgent: true);
                });
                scheduler.Schedule(3.8, () =>
                {
                    Assert(melee.Pos == new Vector2(4, 0) && immediate.Pos == new Vector2(4, 0),
                        "空闲近战应回到身位圈外两米，即使空闲半径为零或关闭延迟到达。");
                    Assert(retarget.Pos == new Vector2(22, 0) && !retarget.IsMoving,
                        "新的紧急站位应立即取代返回近战的路线。");
                    Assert(ranged.Pos.X == 10 && disabled.Pos.X == 10,
                        "远程与关闭攻击的角色不应主动贴近 Boss。");
                    finished.Set();
                });
                scheduler.Start();
                Assert(finished.Wait(6500), "机制结束返回近战的检查未完成。");
                scheduler.Dispose();
                Assert(failure == null, "机制结束返回近战失败：" + failure);
            }
        }

        internal static void MeleeUsesTheGapBeforeTheNextMechanicWithoutMissingItsDeadline()
        {
            Exception failure = null;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
            using (var movement = new SimMovementController(scheduler))
            using (var casts = new SimCastController(scheduler))
            using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
            {
                var normal = CreateActor(behavior, movement, casts, job: Job.VPR, position: new Vector2(10, 0));
                var tight = CreateActor(behavior, movement, casts, job: Job.VPR, position: new Vector2(10, 0));
                var urgent = CreateActor(behavior, movement, casts, job: Job.VPR, position: new Vector2(10, 0));
                var immediate = CreateActor(behavior, movement, casts, delayArrival: false, job: Job.VPR, position: new Vector2(10, 0));
                foreach (var actor in new[] { normal, tight, urgent, immediate })
                    actor.MoveForMechanic(actor.Pos, 0.5, holdUntil: 1, urgent: true);
                var boss = new SimActor(new EntityTestWorld().CreateCharacter(0), casts, movement);
                boss.Entity.HitboxRadius = 2;
                behavior.SetTarget(boss);
                scheduler.Schedule(1, () =>
                {
                    normal.MoveForMechanic(new Vector2(10, 0), 6, holdUntil: 7);
                    tight.MoveForMechanic(new Vector2(12, 0), 3, holdUntil: 7);
                    urgent.MoveForMechanic(new Vector2(12, 0), 6, holdUntil: 7, urgent: true);
                    immediate.MoveForMechanic(new Vector2(12, 0), 6, holdUntil: 7);
                });
                scheduler.Schedule(3.1, () =>
                {
                    Assert(normal.Pos == new Vector2(4, 0) && !normal.IsMoving,
                        "下一次仍使用原来的远处站位时，有足够空闲应先回到近战输出。");
                    foreach (var actor in new[] { tight, urgent, immediate })
                        Assert(actor.Pos == new Vector2(12, 0) && actor.Entity.NetworkMoves.TrueForAll(move => move.Position.X >= 10),
                            "时间紧张、紧急移动或关闭延迟到达时不能绕回近战。");
                });
                scheduler.Schedule(6.2, () =>
                {
                    Assert(normal.Pos == new Vector2(10, 0) && !normal.IsMoving,
                        "返回近战输出后仍应按时回到原定机制站位，并保持到判定结束。");
                    finished.Set();
                });
                scheduler.Start();
                Assert(finished.Wait(9000), "机制间隔返回近战的检查未完成。");
                scheduler.Dispose();
                Assert(failure == null, "机制间隔返回近战失败：" + failure);
            }
        }

        private static SimActor CreateActor(SimActorBehaviorController behavior, SimMovementController movement, SimCastController casts,
            bool delayArrival = true, Job job = Job.ADV, Vector2 position = default(Vector2))
        {
            var actor = new SimActor(new EntityTestWorld().CreateCharacter(2), casts, movement);
            actor.Entity.Job = job;
            actor.Entity.Pos = position;
            // 测试适配器模拟客户端收到 ActorMove 后的位置更新。
            actor.Entity.OnActorMove = (next, _, __) => actor.Entity.Pos3D = next;
            behavior.Add(actor, new SimActorBehaviorOptions { DelayArrival = delayArrival, RouteAngleDegrees = 0, IdleRadius = 0 });
            return actor;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
