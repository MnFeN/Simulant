using Simulant.Content.U7b.Presets;
using Simulant.Core.Entity;
using Simulant.Game.ExtractedCsv;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using ActionRow = Simulant.Game.ExtractedCsv.Rows.Action;

namespace Simulant.Tests
{
    internal static class ForsakenMechanicTests
    {
        internal static void RandomForsakenPlansResolveThroughProductionMechanics()
        {
            using (var rows = new ForsakenActionRows())
            using (var scene = new Scene())
            {
                for (var seed = 0; seed < 64; seed++)
                {
                    var plan = ForsakenPlan.Create(new Random(seed));
                    foreach (var round in plan.Rounds)
                    {
                        scene.Place(round);
                        var description = $"随机种子 {seed}，第 {round.Number} 轮：";
                        var towers = scene.Resolve(new MechanicGroup(CreateTowers(round)));
                        Assert(towers.Success, description + "实际塔判定失败：" + towers.Reason);
                        for (var tower = 0; tower < 2; tower++)
                            AssertSlots(towers.Children[tower].Participants,
                                Enumerable.Range(0, 8).Where(slot => round.TowerBySlot[slot] == tower),
                                description + "双人塔应命中对应两名成员。");

                        var fanTargets = new Dictionary<int, int>();
                        var spells = scene.Resolve(new MechanicGroup(CreateSpells(scene, round, fanTargets)));
                        Assert(spells.Success, description + "实际点名判定失败：" + spells.Reason);
                        var hits = new int[8];
                        for (var index = 0; index < round.Soakers.Length; index++)
                        {
                            var source = round.Soakers[index];
                            var result = spells.Children[index];
                            var expected = ExpectedSpellParticipants(round, source).ToArray();
                            AssertSlots(result.Participants, expected, description + "点名的实际受击成员不符合站位职责。");
                            if (round.MarkersBefore[source] == ForsakenMarker.Fan)
                            {
                                Assert(fanTargets[source] == expected.Single(), description + "扇形应朝向最近的其他成员。");
                                Assert(result.Participants.All(member => member.Actor != scene.Party[source]), description + "扇形不能把来源自己算作受击者。");
                            }
                            foreach (var participant in result.Participants) hits[participant.Slot]++;
                        }

                        if (round.Number % 2 == 0)
                        {
                            var nearest = PartySnapshot.Capture(scene.Party).Nearest(ForsakenPlan.Center, 4);
                            AssertSlots(nearest, Enumerable.Range(0, 8).Where(slot => round.TowerBySlot[slot] < 0),
                                description + "偶数轮四名闲人应取得四个近点圆。");
                            var jumps = scene.Resolve(new MechanicGroup(nearest.Select(target => new AreaMechanic
                            {
                                Area = ActionArea.FromAction(0xBAD6), RequiredPlayers = 1,
                                CenterAtResolve = snapshot => snapshot.Get(target.Slot).Position,
                            }).ToArray()));
                            Assert(jumps.Success, description + "近点圆应只命中各自的引导成员。");
                            for (var index = 0; index < nearest.Count; index++)
                            {
                                AssertSlots(jumps.Children[index].Participants, new[] { nearest[index].Slot }, description + "近点圆不得重叠。");
                                foreach (var participant in jumps.Children[index].Participants) hits[participant.Slot]++;
                            }
                        }
                        Assert(hits.All(count => count == 1), description + "钢铁、扇形、分摊和近点圆合计应使每名成员恰好受击一次。");
                    }
                }
            }
        }

        internal static void ForsakenMechanicsUseActualPositionsWhenThePlanIsBroken()
        {
            using (var rows = new ForsakenActionRows())
            using (var scene = new Scene())
            {
                var round = ForsakenPlan.Create(new Random(42)).Rounds[0];
                scene.Place(round);
                var intruder = Enumerable.Range(0, 2).Single(slot => round.TowerBySlot[slot] < 0);
                scene.Party[intruder].Pos = round.TowerCenters[0];
                var remainingStacks = (int[])round.StacksBefore.Clone();
                remainingStacks[intruder] = 0;
                var towerMechanics = CreateTowers(round);
                foreach (var tower in towerMechanics)
                    tower.Check = members => members.Any(member => remainingStacks[member.Slot] == 0) ? "踩塔者没有剩余层数。" : null;
                var towers = scene.Resolve(new MechanicGroup(towerMechanics));
                Assert(!towers.Success && !towers.Children[0].Success && towers.Children[1].Success,
                    "无层数闲人误入应使对应塔失败，不能按计划分工忽略实际入塔者。");
                AssertSlots(towers.Children[0].Participants,
                    Enumerable.Range(0, 8).Where(slot => round.TowerBySlot[slot] == 0).Concat(new[] { intruder }),
                    "塔失败结果必须保留全部三名实际入塔者。");

                scene.Place(round);
                var fan = round.Soakers.Single(slot => round.MarkersBefore[slot] == ForsakenMarker.Fan);
                var originalBait = ExpectedSpellParticipants(round, fan).Single();
                var sourcePosition = round.Positions[fan];
                scene.Party[intruder].Pos = sourcePosition + Vector2.Normalize(round.Positions[originalBait] - sourcePosition);
                var fanTargets = new Dictionary<int, int>();
                var fanMechanic = CreateSpell(scene, round, fan, fanTargets);
                var fanResult = scene.Resolve(new MechanicGroup(fanMechanic)).Children.Single();
                Assert(fanTargets[fan] == intruder, "更近的现场成员应抢走扇形诱导，不能继续朝计划目标判定。");
                Assert(!fanResult.Success, "同方向的两名受击者应触发扇形人数失败。");
                AssertSlots(fanResult.Participants, new[] { intruder, originalBait },
                    "扇形必须命中实际方向上的两人，并排除作为顶点的来源自己。");
            }
        }

        private static AreaMechanic[] CreateTowers(ForsakenRoundPlan round)
            => Enumerable.Range(0, 2).Select(tower => new AreaMechanic
            {
                Area = ActionArea.FromAction(0xBABE), Center = round.TowerCenters[tower], RequiredPlayers = 2,
                Check = members => members.Any(member => round.StacksBefore[member.Slot] == 0) ? "踩塔者没有剩余层数。" : null,
            }).ToArray();

        private static AreaMechanic[] CreateSpells(Scene scene, ForsakenRoundPlan round, Dictionary<int, int> fanTargets)
            => round.Soakers.Select(source => CreateSpell(scene, round, source, fanTargets)).ToArray();

        private static AreaMechanic CreateSpell(Scene scene, ForsakenRoundPlan round, int source, Dictionary<int, int> fanTargets)
        {
            var marker = round.MarkersBefore[source];
            var fan = marker == ForsakenMarker.Fan;
            var action = marker == ForsakenMarker.Stack ? 0xBAC0u : fan ? 0xBAC2u : 0xBAC1u;
            return new AreaMechanic
            {
                Area = ActionArea.FromAction(action, coneAngleDegrees: fan ? 90f : (float?)null),
                CenterAtResolve = snapshot => snapshot.Get(source).Position,
                HeadingAtResolve = snapshot =>
                {
                    if (!fan) return 0;
                    var position = snapshot.Get(source).Position;
                    var target = snapshot.Nearest(position, scene.Party[source]) ?? throw new Exception("扇形没有可引导的成员。");
                    fanTargets[source] = target.Slot;
                    return (float)Math.Atan2(target.Position.X - position.X, target.Position.Y - position.Y);
                },
                Exclude = fan ? scene.Party[source] : null,
                RequiredPlayers = marker == ForsakenMarker.Stack ? 3 : 1,
            };
        }

        // 预期集合依据解法中的角色职责给出；不再实现另一套圆或扇形数学判定。
        private static IEnumerable<int> ExpectedSpellParticipants(ForsakenRoundPlan round, int source)
        {
            var marker = round.MarkersBefore[source];
            var helpers = Enumerable.Range(0, 8).Where(slot => round.TowerBySlot[slot] < 0).ToArray();
            if (marker == ForsakenMarker.Fan)
                return new[] { round.Number % 2 == 0
                    ? round.Soakers.Single(slot => slot != source && round.MarkersBefore[slot] == ForsakenMarker.Fan)
                    : helpers.Single(slot => slot / 2 == 1) };
            if (marker == ForsakenMarker.Circle) return new[] { source };
            if (round.TowerBySlot[source] == 0)
                return new[] { source, round.Soakers.Single(slot => round.MarkersBefore[slot] == ForsakenMarker.Fan), helpers.Single(slot => slot / 2 == 0) };
            return new[] { source, helpers.Single(slot => slot / 2 == 2), helpers.Single(slot => slot / 2 == 3) };
        }

        private sealed class Scene : IDisposable
        {
            private readonly ConcurrentQueue<Exception> _errors = new ConcurrentQueue<Exception>();
            private readonly SimScheduler _scheduler;
            private readonly SimEntityManager _manager;
            internal readonly SimActor[] Party;

            internal Scene()
            {
                _scheduler = new SimScheduler((error, name) => _errors.Enqueue(error));
                _manager = new SimEntityManager(new EntitySpawner()) { Scheduler = _scheduler };
                // 通用测试世界只有四个槽位；本测试使用同一个正式注册表提供完整八人身份。
                var observations = Enumerable.Range(0, 8).Select(slot => (EntityObservation?)new EntityObservation(
                    new IntPtr(1000 + slot), (uint)(100 + slot), ObjectKind.Pc)).ToArray();
                var registry = new EntityRegistry(slot => observations[slot], observations.Length);
                Party = Enumerable.Range(0, 8).Select(slot => _manager.Attach((Character)registry.Get((ushort)slot))).ToArray();
                _manager.Party.AddRange(Party);
                _scheduler.Start();
            }

            internal void Place(ForsakenRoundPlan round)
            {
                for (var slot = 0; slot < Party.Length; slot++) Party[slot].Pos = round.Positions[slot];
            }

            internal MechanicResult Resolve(MechanicGroup group)
            {
                using (var finished = new ManualResetEventSlim())
                {
                    _manager.RunMechanic(group);
                    // 后排入的零延迟节点等待正式判定及清理全部完成，避免在回调中提前更换下一轮站位。
                    _scheduler.ScheduleAfter(0, finished.Set, "等待遗弃末世集成测试判定");
                    Assert(finished.Wait(3000), "等待遗弃末世集成测试判定超时。");
                    if (!_errors.IsEmpty) throw new AggregateException("遗弃末世集成测试收到调度异常。", _errors);
                    return group.Result ?? throw new Exception("机制未生成判定结果。");
                }
            }

            public void Dispose() { _scheduler.Dispose(); _manager.Clear(); }
        }

        private sealed class ForsakenActionRows : IDisposable
        {
            private readonly Dictionary<int, ActionRow> _previous = new Dictionary<int, ActionRow>();

            internal ForsakenActionRows()
            {
                // 对应项目现有 Action.csv 的 CastType/EffectRange；这些点名技能没有标准预兆名。
                Add(0xBABE, 2, 4);
                Add(0xBAC0, 2, 5);
                Add(0xBAC1, 2, 5);
                Add(0xBAC2, 13, 40);
                Add(0xBAD6, 2, 5);
            }

            private void Add(int action, byte shape, float range)
            {
                var rows = CsvManager.Instance.Get<ActionRow>();
                if (rows.TryGetValue(action, out var previous)) _previous.Add(action, previous);
                rows[action] = new ActionRow { ShapeType = shape, ScaleY = range };
            }

            public void Dispose()
            {
                var rows = CsvManager.Instance.Get<ActionRow>();
                foreach (var action in new[] { 0xBABE, 0xBAC0, 0xBAC1, 0xBAC2, 0xBAD6 })
                {
                    if (_previous.TryGetValue(action, out var previous)) rows[action] = previous;
                    else rows.Remove(action);
                }
            }
        }

        private static void AssertSlots(IEnumerable<PartyMemberSnapshot> actual, IEnumerable<int> expected, string message)
            => Assert(actual.Select(member => member.Slot).OrderBy(slot => slot).SequenceEqual(expected.OrderBy(slot => slot)), message);

        private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
