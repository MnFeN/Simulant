using Simulant.Content.U7a.Presets;
using Simulant.Simulation.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Tests
{
    internal static class U7aEarlyExerciseTests
    {
        private static readonly int[] Slots = Enumerable.Range(0, 8).ToArray();

        public static void DiamondDustLeavesSafeKnockbackLandingsForBothKicksAndAllCircleAxes()
        {
            var needle = ActionArea.Cross(40, 5);
            var icicle = ActionArea.Circle(10);
            var marker = ActionArea.Circle(5);
            var random = new Random(0x9D06);
            foreach (var axe in new[] { false, true })
                foreach (var markersOnDamageDealers in new[] { false, true })
                    for (var axis = 0; axis < 4; axis++)
                    {
                        var plan = new P21钻石星尘Plan(axe, false, markersOnDamageDealers, axis, 0, 1);
                        Check(plan.IcicleDirections.Distinct().Count() == 8, "三批冰柱应覆盖正斜八方各一次。");
                        var firstIcicles = plan.IcicleDirections.Take(2).Select(direction => P21钻石星尘Plan.At(direction, 16)).ToArray();
                        var secondIcicles = plan.IcicleDirections.Skip(2).Take(4).Select(direction => P21钻石星尘Plan.At(direction, 16)).ToArray();
                        var kick = axe ? ActionArea.Circle(16) : ActionArea.Donut(4, 20);

                        for (var sample = 0; sample < 128; sample++)
                        {
                            var initial = Slots.Select(slot => Jitter(plan.InitialBait(slot, false), 0.12f, random)).ToArray();
                            Check(initial.All(point => !kick.Contains(point, Vector2.Zero, 0) && point.Length() < 20), "扇形与冰石的初始站位应躲开回旋踢和电网。");
                            var progress = Slots.Select(slot => Jitter(plan.Marked.Contains(slot) ? plan.InitialBait(slot, true)
                                : Vector2.Normalize(plan.InitialBait(slot, false)) * (axe ? 4.75f : 1.7f), 0.12f, random)).ToArray();
                            Check(progress.All(point => !firstIcicles.Any(circle => icicle.Contains(point, circle, 0))), "首批两颗冰柱结算时，全队应已离开冰柱范围。");

                            foreach (var target in plan.Marked)
                                Check(Slots.Where(slot => marker.Contains(progress[slot], progress[target], 0)).SequenceEqual(new[] { target }),
                                    "四颗冰石必须分别落在点名者位置且互不重叠。");

                            for (var group = 0; group < 2; group++)
                            {
                                var start = Jitter(plan.KnockbackStart(group), 0.1f, random);
                                var landing = start + Vector2.Normalize(start) * 12;
                                Check(secondIcicles.All(circle => !icicle.Contains(start, circle, 0)), "第二批冰柱应在击退位移前躲开。");
                                Check(landing.Length() < 20, "十二米击退的落点不能触及电网。");
                                foreach (var target in plan.Marked)
                                    Check(!marker.Contains(landing, progress[target], 0) && !needle.Contains(landing, progress[target], 0)
                                        && !needle.Contains(landing, progress[target], -(float)Math.PI / 4),
                                        "落点必须躲开四个冰花的圆和两组十字。");
                            }
                        }
                    }
        }

        public static void DiamondDustStackRoutesLeaveBothSwordSidesAndAvoidTheLastIcicles()
        {
            var circle = ActionArea.Circle(10);
            for (var first = 0; first < 4; first++)
                for (var reflection = 0; reflection < 8; reflection++)
                {
                    var plan = new P21钻石星尘Plan(false, false, false, first, reflection, 0);
                    var last = plan.IcicleDirections.Skip(6).Select(dir => P21钻石星尘Plan.At(dir, 16)).ToArray();
                    Check(plan.StartsBehind(0) != plan.StartsBehind(1), "两组放圈结束后应分别在镜像的前方及后方准备滑冰。");
                    for (var group = 0; group < 2; group++)
                    {
                        var firstStack = plan.StackOffset(group, 0);
                        Check(last.All(center => !circle.Contains(firstStack, center, 0)), "末批冰柱不能覆盖首次罪神圣分摊点。");
                        Check(Vector2.Distance(plan.KnockbackStart(group) + P21钻石星尘Plan.At(first + group * 4, 12), firstStack) < 9,
                            "冰花结算后，第一段放圈走位应来得及。");
                        for (var wave = 1; wave < 4; wave++)
                        {
                            var step = Vector2.Distance(plan.StackOffset(group, wave - 1), plan.StackOffset(group, wave));
                            Check(step > 6 && step < 9.5f, "前一圈应在下次分摊前移出，下一点应在一轮分摊间隔内可达。");
                        }
                    }
                    for (var slot = 0; slot < 8; slot++)
                    {
                        var slide = Vector2.Distance(plan.IceOffset(slot, false), plan.IceOffset(slot, true));
                        Check(slide > 31 && slide < 33, "双剑切换安全侧应使用回放中的长滑距离。");
                    }
                }
        }

        public static void LightRampantSupportsEveryHammerPairAndBothOrbOrders()
        {
            var orb = ActionArea.Circle(11);
            var actualPairs = new HashSet<int>();
            var actualParities = new HashSet<int>();
            var random = new Random(0x9D14);
            for (var sample = 0; sample < 2048; sample++)
            {
                var plan = new P23光之失控Plan(random);
                actualPairs.Add((1 << plan.Baits[0]) | (1 << plan.Baits[1]));
                actualParities.Add(plan.FirstOrbParity);
                Check(plan.Baits.Length == 2 && plan.Chain.Length == 6 && plan.Chain.Intersect(plan.Baits).Count() == 0,
                    "两位光锤与其他六位连线应覆盖全队。");
                Check(plan.TowerPlayers.OrderBy(slot => slot).SequenceEqual(plan.Chain.OrderBy(slot => slot)),
                    "外圈六塔必须分给六名连线玩家。");
                Check(Slots.Count(slot => plan.Side(slot) == 0) == 4 && Slots.Count(slot => plan.Side(slot) == 1) == 4,
                    "强光两侧各由三名连线玩家和一名光锤组成。");
                for (var i = 0; i < 6; i++)
                    Check(plan.Side(plan.Chain[i]) != plan.Side(plan.Chain[(i + 1) % 6]), "灰九的连线在两侧之间交替。");
                Check(plan.Weights.All(plan.Chain.Contains) && plan.Side(plan.Weights[0]) == 0 && plan.Side(plan.Weights[1]) == 1,
                    "两名强光之兆必须各落在一边。");
                Check(plan.InitialStacks.Sum() == 6 && plan.InitialStacks.All(stacks => stacks <= 2) && plan.CenterPlayers.Length == 4,
                    "初始六层以及前两轮加层应产生恰好四名两层踩塔玩家。");
                foreach (var slot in Slots)
                {
                    var beforeCenter = plan.InitialStacks[slot] + (plan.Chain.Contains(slot) ? 1 : 0) + 1;
                    Check(beforeCenter + (plan.CenterPlayers.Contains(slot) ? 1 : 0) == 3, "中央塔后每人都应恰好三层。");
                }

                foreach (var afterFirst in new[] { false, true })
                    for (var side = 0; side < 2; side++)
                    {
                        var parity = afterFirst ? 1 - plan.FirstOrbParity : plan.FirstOrbParity;
                        var point = Jitter(plan.OrbSafeOffset(side, afterFirst), 0.24f, random);
                        Check(point.Length() < 20, "光球安全点必须留在电网内。");
                        for (var tower = parity; tower < 6; tower += 2)
                            Check(!orb.Contains(point, P23光之失控Plan.TowerOffset(tower), 0), "安全点必须躲开各轮的三颗光球。");
                    }
            }
            Check(actualPairs.Count == 28 && actualParities.Count == 2, "应覆盖所有光锤组合与两种光球顺序。");
        }

        private static Vector2 Jitter(Vector2 center, float radius, Random random)
            => SimPosition.Ellipse(center, radius).Sample(random);

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
