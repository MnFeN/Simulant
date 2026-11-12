using Simulant.Content.U7b.Presets;
using System;
using System.Linq;
using System.Numerics;

namespace Simulant.Tests
{
    internal static class ForsakenPlanTests
    {
        internal static void InitialPositionsSeparateRolePairsInFourCorners()
        {
            var groups = new[] { new[] { 0, 2 }, new[] { 1, 3 }, new[] { 4, 6 }, new[] { 5, 7 } };
            for (var group = 0; group < groups.Length; group++)
            {
                var positions = groups[group].Select(ForsakenPlan.InitialPosition).ToArray();
                foreach (var position in positions)
                {
                    var offset = position - ForsakenPlan.Center;
                    Assert((offset.X < 0) == (group % 2 == 0) && (offset.Y < 0) == (group < 2),
                        "开场应为 MT/H1 左前、ST/H2 右前、D1/D3 左后、D2/D4 右后。");
                }
                Assert(Vector2.Distance(positions[0], positions[1]) >= 2, "同组队员须留出间距看清点名。");
            }
            Assert(Enumerable.Range(0, 8).Select(ForsakenPlan.InitialPosition).Distinct().Count() == 8,
                "八名队员的开场位置不能重叠。");
        }

        internal static void InitialAssignmentsKeepRoleDistributionAndPartnerGroups()
        {
            var partners = new[] { 2, 3, 0, 1, 6, 7, 4, 5 };
            var initialPatterns = new System.Collections.Generic.HashSet<string>();
            for (var seed = 0; seed < 256; seed++)
            {
                var plan = ForsakenPlan.Create(new Random(seed));
                var markers = plan.InitialMarkers;
                Assert(markers.Count(marker => marker == ForsakenMarker.Fan) == 3, "初始应有三人扇形。");
                Assert(markers.Count(marker => marker == ForsakenMarker.Circle) == 3, "初始应有三人钢铁。");
                Assert(markers.Take(4).Count(marker => marker == ForsakenMarker.Stack) == 1, "辅助应恰有一个分摊。");
                Assert(markers.Skip(4).Count(marker => marker == ForsakenMarker.Stack) == 1, "输出应恰有一个分摊。");
                Assert(plan.GroupA.Count(value => value) == 4, "A 组必须为四人。");
                for (var slot = 0; slot < 8; slot++)
                    Assert(plan.GroupA[slot] == (markers[slot] == ForsakenMarker.Stack || markers[partners[slot]] == ForsakenMarker.Stack),
                        "初始分摊及其固定搭档必须进入 A 组。");
                initialPatterns.Add(string.Join(",", markers.Select(marker => (int)marker)));
            }
            Assert(initialPatterns.Count == 32, "随机测试必须覆盖辅助/输出钢扇互换和全部分摊选择。");
        }

        internal static void EveryRandomRoundHasTwoSoakersAndOneSafeSpellPerMember()
        {
            for (var seed = 0; seed < 512; seed++)
            {
                var plan = ForsakenPlan.Create(new Random(seed));
                foreach (var round in plan.Rounds)
                {
                    Assert(round.Positions.All(position => Vector2.Distance(position, ForsakenPlan.Center) < 20), "全部站位必须留在场地内。");
                    for (var tower = 0; tower < 2; tower++)
                    {
                        var actual = Enumerable.Range(0, 8)
                            .Where(slot => Vector2.Distance(round.Positions[slot], round.TowerCenters[tower]) <= 4).ToArray();
                        var assigned = Enumerable.Range(0, 8).Where(slot => round.TowerBySlot[slot] == tower).ToArray();
                        Assert(actual.Length == 2 && actual.SequenceEqual(assigned), "每个塔必须恰有两名指定成员，闲人不得误踩。");
                    }

                    var hits = new int[8];
                    foreach (var source in round.Soakers)
                    {
                        var marker = round.MarkersBefore[source];
                        int[] targets;
                        if (marker == ForsakenMarker.Fan)
                        {
                            var nearest = Enumerable.Range(0, 8).Where(slot => slot != source)
                                .OrderBy(slot => Vector2.DistanceSquared(round.Positions[source], round.Positions[slot])).First();
                            var direction = Vector2.Normalize(round.Positions[nearest] - round.Positions[source]);
                            targets = Enumerable.Range(0, 8).Where(slot => slot != source
                                && Vector2.Distance(round.Positions[source], round.Positions[slot]) <= 40
                                && Vector2.Dot(direction, Vector2.Normalize(round.Positions[slot] - round.Positions[source])) >= Math.Sqrt(0.5)).ToArray();
                            Assert(targets.Length == 1, "扇形必须只击中一名引导者。");
                        }
                        else
                        {
                            targets = Enumerable.Range(0, 8)
                                .Where(slot => Vector2.Distance(round.Positions[source], round.Positions[slot]) <= 5).ToArray();
                            Assert(targets.Length == (marker == ForsakenMarker.Stack ? 3 : 1), "分摊必须三人，钢铁不得碰到队友。");
                        }
                        foreach (var target in targets) hits[target]++;
                    }

                    if (round.Number % 2 == 0)
                    {
                        var near = Enumerable.Range(0, 8).OrderBy(slot => Vector2.DistanceSquared(round.Positions[slot], ForsakenPlan.Center)).Take(4).ToArray();
                        Assert(near.All(slot => round.TowerBySlot[slot] == -1), "偶数轮最近四人必须全为闲人。");
                        foreach (var target in near)
                            foreach (var slot in Enumerable.Range(0, 8).Where(slot => Vector2.Distance(round.Positions[slot], round.Positions[target]) <= 5))
                                hits[slot]++;
                    }
                    Assert(hits.All(count => count == 1), "每人应只受一次魔法判定，不能重叠吃易伤伤害。");
                }
            }
        }

        internal static void GroupsClearAllStacksAndPreservePreviousLeftRightPriority()
        {
            var aRounds = new[] { 1, 2, 3, 8 };
            for (var seed = 0; seed < 256; seed++)
            {
                var plan = ForsakenPlan.Create(new Random(seed));
                foreach (var round in plan.Rounds)
                {
                    var activeA = aRounds.Contains(round.Number);
                    Assert(round.Soakers.All(slot => plan.GroupA[slot] == activeA), "必须按附件的 A 组 1238、B 组 4567 踩塔。");
                    for (var slot = 0; slot < 8; slot++)
                    {
                        var delta = round.Soakers.Contains(slot) ? 1 : 0;
                        Assert(round.StacksAfter[slot] == round.StacksBefore[slot] - delta, "每踩一次塔只消一层。");
                        Assert(round.StacksAfter[slot] >= 0, "剩余层数不能为负。");
                        Assert((round.MarkersAfter[slot] == ForsakenMarker.None) == (round.StacksAfter[slot] == 0), "层数耗尽后不得再留头标。");
                    }

                    var previous = plan.Rounds.Take(round.Number - 1).LastOrDefault(candidate => aRounds.Contains(candidate.Number) == activeA);
                    if (previous == null) continue;
                    var angle = previous.NorthDirection * Math.PI / 4;
                    var right = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                    foreach (var duplicate in round.Soakers.GroupBy(slot => round.MarkersBefore[slot]).Where(group => group.Count() > 1))
                    {
                        var slots = duplicate.ToArray();
                        var leftProjection = Vector2.Dot(previous.Positions[slots[0]] - ForsakenPlan.Center, right);
                        var rightProjection = Vector2.Dot(previous.Positions[slots[1]] - ForsakenPlan.Center, right);
                        Assert(leftProjection <= rightProjection, "同类新头标必须按上次踩塔时的左右排序，不能重回职业优先级。");
                    }
                }
                Assert(plan.Rounds[7].StacksAfter.All(value => value == 0), "八轮结束必须清空全员四层状态。");
            }
        }

        internal static void MapEffectsAndCleaveBaitsMatchTheirWorldPositions()
        {
            for (var seed = 0; seed < 256; seed++)
            {
                var plan = ForsakenPlan.Create(new Random(seed));
                foreach (var round in plan.Rounds)
                {
                    for (var tower = 0; tower < 2; tower++)
                    {
                        var angle = (round.TowerMapIndices[tower] - 1) * Math.PI / 4;
                        var expected = ForsakenPlan.Center + new Vector2((float)Math.Sin(angle) * 8, -(float)Math.Cos(angle) * 8);
                        Assert(Vector2.Distance(expected, round.TowerCenters[tower]) < 0.001f, "地图塔索引必须与几何判定的塔心一致。");
                    }
                    if (round.Number % 2 != 0) continue;
                    Assert(Math.Abs(Vector2.Distance(round.BaitPosition, ForsakenPlan.Center) - 8) < 0.001f, "集合引导位置必须在半径八米处。");
                    if (round.Number == 8)
                    {
                        Assert(round.BaitPosition == new Vector2(100, 92), "末次必须固定正北集合。");
                        Assert(round.FinalDodgePosition == (round.FutureEnd ? new Vector2(100, 108) : new Vector2(100, 92)), "末次未来穿背，过去原地。");
                        AssertCleaveSafe(round, new[] { round.FinalDodgePosition });
                        continue;
                    }

                    var next = plan.Rounds[round.Number];
                    var towerDirection = Vector2.Normalize((next.TowerCenters[0] + next.TowerCenters[1]) * 0.5f - ForsakenPlan.Center);
                    var baitDirection = Vector2.Normalize(round.BaitPosition - ForsakenPlan.Center);
                    Assert(Vector2.Dot(towerDirection, baitDirection) * (round.FutureEnd ? -1 : 1) > 0.999f,
                        "前三次过去在下一组两塔间，未来在下一组塔对面集合。");
                    AssertCleaveSafe(round, next.Positions);
                }
            }
        }

        private static void AssertCleaveSafe(ForsakenRoundPlan round, Vector2[] safePositions)
        {
            // 日志中的逻辑源位于场中；运行时仍须读取原生实体的实际位置。
            AssertCleaveSafeFromOrigin(round, safePositions, ForsakenPlan.Center);
            var targets = round.Positions.OrderBy(position => Vector2.DistanceSquared(position, ForsakenPlan.Center)).Take(4);
            foreach (var target in targets)
            {
                // 额外检验一种小幅偏移的容差。目标前四米仅是外部模拟器尚未验证的假设，不能据此设置实体位置。
                var offset = target - ForsakenPlan.Center;
                var distance = offset.Length();
                var origin = ForsakenPlan.Center + offset * Math.Max(0, distance - 4) / distance;
                AssertCleaveSafeFromOrigin(round, safePositions, origin);
            }
        }

        private static void AssertCleaveSafeFromOrigin(ForsakenRoundPlan round, Vector2[] safePositions, Vector2 origin)
        {
            var facing = Vector2.Normalize(round.BaitPosition - origin) * (round.FutureEnd ? 1 : -1);
            foreach (var safe in safePositions)
                Assert(Vector2.Dot(facing, safe - origin) < 0, "四个半场刀都必须避开后续塔位或末次安全点。");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
