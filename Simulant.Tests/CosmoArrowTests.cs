using Simulant.Content.U6b.Presets;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace Simulant.Tests
{
    internal static class CosmoArrowTests
    {
        private static readonly ArrowMode[] Modes = { ArrowMode.InsideFirst, ArrowMode.OutsideFirst };
        private static readonly ArrowOccurrence[] Occurrences = { ArrowOccurrence.First, ArrowOccurrence.Second };
        private static readonly ActionArea Tank = ActionArea.Circle(8), Stack = ActionArea.Circle(6), Beam = ActionArea.Rectangle(100, 8);
        private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static ActionArea Arrow(CosmoArrowPlan.Shot shot) => ActionArea.Rectangle(shot.Action == 0x7BA3 ? 40 : 100, shot.Action == 0x7BA3 ? 10 : 5);

        public static void RoutesMatchRolesAndAvoidEveryArrow()
        {
            Assert(CosmoArrowPlan.InitialPosition(ArrowOccurrence.First, 0) == new Vector2(92.5f, 92.5f), "MT 应从左上起步。");
            Assert(CosmoArrowPlan.InitialPosition(ArrowOccurrence.First, 1) == new Vector2(107.5f, 92.5f), "ST 应从右上起步。");
            Assert(Enumerable.Range(2, 6).All(slot => CosmoArrowPlan.InitialPosition(ArrowOccurrence.First, slot) == new Vector2(92.5f, 107.5f)), "第一次治疗和 DPS 应在左下起步。");
            foreach (var mode in Modes)
            foreach (var occurrence in Occurrences)
            {
                Assert(Enumerable.Range(0, 8).Sum(wave => CosmoArrowPlan.Wave(mode, wave).Length) == 42, "两种天箭均应结算 42 条范围。");
                var random = new Random(731);
                var definitions = Enumerable.Range(0, 8).Select(slot => CosmoArrowPlan.Route(mode, occurrence, slot)).ToArray();
                for (var sample = 0; sample < 1000; sample++)
                {
                    var routes = definitions.Select(route => route.Select(move => new CosmoArrowPlan.Move(move.Start, move.Arrival, move.Area.Sample(random))).ToArray()).ToArray();
                    var spawn = Enumerable.Range(0, 8).Select(_ => CosmoArrowPlan.SpawnArea.Sample(random)).ToArray();
                    Assert(spawn.All(point => Vector2.Distance(point, CosmoArrowPlan.Center) <= 8.0001), "初始队友必须在场中八米圆内。");
                    foreach (var delay in new[] { 0.0, 0.3 })
                    {
                        for (var wave = 0; wave < 8; wave++)
                        {
                            var positions = CosmoArrowPlan.PositionsAt(routes, CosmoArrowPlan.Hit(wave), delay);
                            foreach (var shot in CosmoArrowPlan.Wave(mode, wave))
                            for (var slot = 0; slot < 8; slot++)
                                Assert(!Arrow(shot).Contains(positions[slot], shot.Position, shot.Heading),
                                    $"{mode} {occurrence} 样本 {sample} 延迟 {delay} 第 {wave + 1} 次天箭击中 {CosmoArrowPlan.Roles[slot]}：{positions[slot]}。");
                        }
                        for (var slot = 0; slot < 8; slot++)
                        foreach (var move in routes[slot])
                        {
                            var previous = move.Start == 0 ? spawn[slot] : CosmoArrowPlan.PositionsAt(routes, move.Start, delay)[slot];
                            var travel = Vector2.Distance(previous, move.Position) / 6 + 0.4;
                            Assert(travel <= move.Arrival - move.Start, $"{mode} {occurrence} {CosmoArrowPlan.Roles[slot]} 在 {move.Start:0.00} 秒移动窗口不足：需要 {travel:0.00} 秒，实际 {move.Arrival - move.Start:0.00} 秒。");
                        }
                        var final = CosmoArrowPlan.PositionsAt(routes, CosmoArrowPlan.FollowupHit(occurrence), delay);
                        Assert(occurrence == ArrowOccurrence.First ? CosmoArrowPlan.ValidDive(final, Tank, Stack)
                            : Enumerable.Range(0, 8).All(target => CosmoArrowPlan.ValidCharge(final, target, Beam)), "随机集合点不能破坏双 T 死刑或人群分摊。");
                    }
                    if (occurrence == ArrowOccurrence.Second)
                        for (var wave = 0; wave < 2; wave++)
                        {
                            var fast = CosmoArrowPlan.PositionsAt(routes, CosmoArrowPlan.BeamHit(wave), 0);
                            var slow = CosmoArrowPlan.PositionsAt(routes, CosmoArrowPlan.BeamHit(wave), 0.3);
                            foreach (var source in new[] { fast, slow })
                            foreach (var victims in new[] { fast, slow })
                            for (var target = 0; target < 8; target++)
                            for (var slot = 0; slot < 8; slot++)
                                if (slot != target)
                                    Assert(!Beam.Contains(victims[slot], CosmoArrowPlan.Center, CosmoArrowPlan.TargetHeading(source, target)),
                                        $"{mode} 样本 {sample} 第 {wave + 1} 次波动炮 {CosmoArrowPlan.Roles[target]} 波及 {CosmoArrowPlan.Roles[slot]}：源 {source[target]}，目标 {victims[slot]}。");
                        }
                }
                var beforeGather = CosmoArrowPlan.PositionsAt(mode, occurrence, CosmoArrowPlan.Hit(6) + 0.01);
                if (occurrence == ArrowOccurrence.First)
                {
                    Assert(beforeGather[0] == new Vector2(100, 92.5f) && beforeGather[1] == new Vector2(107.5f, 100), "第一次双 T 应由左上、右上分别转到正上、正右。");
                    Assert(beforeGather[2] == new Vector2(92.5f, 100) && beforeGather[3] == beforeGather[2], "第一次治疗应走 TH 路线到左侧，再回人群。");
                }
            }
        }

        public static void FollowupsUseActualTargetsAndOnlyAttributeUnsafePositions()
        {
            foreach (var mode in Modes)
            {
                var dive = CosmoArrowPlan.PositionsAt(mode, ArrowOccurrence.First, CosmoArrowPlan.FollowupHit(ArrowOccurrence.First));
                Assert(CosmoArrowPlan.ValidDive(dive, Tank, Stack), "第一次最终站位必须满足双 T 单吃、其余六人远点分摊。");
                for (var wave = 0; wave < 2; wave++)
                {
                    var spread = CosmoArrowPlan.PositionsAt(mode, ArrowOccurrence.Second, CosmoArrowPlan.BeamHit(wave));
                    Assert(CosmoArrowPlan.ValidBeams(spread, Enumerable.Range(0, 8).ToArray(), Beam), $"{mode} 第 {wave + 1} 组波动炮的任意四人点名均不能波及队友。");
                }
                var charge = CosmoArrowPlan.PositionsAt(mode, ArrowOccurrence.Second, CosmoArrowPlan.FollowupHit(ArrowOccurrence.Second));
                for (var target = 0; target < 8; target++) Assert(CosmoArrowPlan.ValidCharge(charge, target, Beam), "任意目标的最终波动炮均应八人分摊，双 T 在前。");
                var actual = (Vector2[])dive.Clone();
                actual[4] += new Vector2(-2, 2);
                Assert(CosmoArrowPlan.FindOffenders(actual, dive, positions => CosmoArrowPlan.ValidDive(positions, Tank, Stack)).Length == 0,
                    "在有效分摊范围内移动超过半米，不应因偏离导航点而画叉。");
                actual[4] = new Vector2(100, 100);
                Assert(CosmoArrowPlan.DiveTargets(actual).Contains(4), "D1 走入场中后必须按实际距离误引导死刑。");
                Assert(CosmoArrowPlan.FindOffenders(actual, dive, positions => CosmoArrowPlan.ValidDive(positions, Tank, Stack)).SequenceEqual(new[] { 4 }),
                    "D1 误引导死刑时不应把被波及的玩家或队友一起归责。");
            }
        }

        public static void NetworkMovementCompletesBothRoutesWithoutHittingArrows()
        {
            Exception failure = null;
            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => { failure = error; finished.Set(); }))
            using (var movement = new SimMovementController(scheduler))
            using (var casts = new SimCastController(scheduler))
            using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
            {
                foreach (var mode in Modes)
                foreach (var occurrence in Occurrences)
                {
                    var actors = Enumerable.Range(0, 8).Select(slot =>
                    {
                        var world = new EntityTestWorld();
                        var actor = new SimActor(world.CreateCharacter(2), casts, movement);
                        actor.Entity.Pos = CosmoArrowPlan.SpawnArea.Sample(new Random(731 + slot));
                        actor.Entity.OnActorMove = (position, _, __) => actor.Entity.Pos3D = position;
                        behavior.Add(actor, new SimActorBehaviorOptions { AttackTarget = false, IdleRadius = 0 });
                        actor.MoveForMechanic(actor.Pos, 1, 30, urgent: true);
                        foreach (var move in CosmoArrowPlan.Route(mode, occurrence, slot))
                            scheduler.Schedule(move.Start, () => actor.MoveForMechanic(move.Area, move.Arrival, 30, urgent: true));
                        return actor;
                    }).ToArray();
                    for (var wave = 0; wave < 8; wave++)
                    {
                        var index = wave;
                        scheduler.Schedule(CosmoArrowPlan.Hit(wave), () =>
                        {
                            foreach (var shot in CosmoArrowPlan.Wave(mode, index))
                            for (var slot = 0; slot < 8; slot++)
                                Assert(!Arrow(shot).Contains(actors[slot].Pos, shot.Position, shot.Heading), $"真实移动控制器：{mode} {occurrence} 第 {index + 1} 次天箭击中 {CosmoArrowPlan.Roles[slot]}：{actors[slot].Pos}。");
                        });
                    }
                    if (occurrence == ArrowOccurrence.Second)
                        for (var wave = 0; wave < 2; wave++)
                            scheduler.Schedule(CosmoArrowPlan.BeamHit(wave), () => Assert(CosmoArrowPlan.ValidBeams(actors.Select(actor => actor.Pos).ToArray(), Enumerable.Range(0, 8).ToArray(), Beam), "真实移动控制器：波动炮分散仍在跑位且波及队友。"));
                    scheduler.Schedule(CosmoArrowPlan.FollowupHit(occurrence), () =>
                    {
                        var positions = actors.Select(actor => actor.Pos).ToArray();
                        Assert(occurrence == ArrowOccurrence.First ? CosmoArrowPlan.ValidDive(positions, Tank, Stack) : Enumerable.Range(0, 8).All(target => CosmoArrowPlan.ValidCharge(positions, target, Beam)), "真实移动控制器：后续分摊未及时到位。");
                    });
                }
                scheduler.Schedule(26, () => finished.Set());
                scheduler.Start();
                Assert(finished.Wait(30000), "天箭完整移动检查超时。");
                scheduler.Cancel();
                Assert(failure == null, "天箭完整移动检查失败：" + failure);
            }
        }
    }
}
