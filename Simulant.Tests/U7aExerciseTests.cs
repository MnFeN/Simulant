using Simulant.Game.FFCS.Client.Game.UI;
using Simulant.Content.U7a.Presets;
using Simulant.Simulation.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static Simulant.Utils.Maths.Angles;

namespace Simulant.Tests
{
    internal static class U7aExerciseTests
    {
        public static void RelativityAssignmentsAndFirePositionsWorkForAllOptions()
        {
            var random = new Random(5758);
            foreach (RelativitySolution solution in Enum.GetValues(typeof(RelativitySolution)))
                foreach (RelativityDebuff initial in Enum.GetValues(typeof(RelativityDebuff)))
                    for (var self = 0; self < 8; self++)
                        for (var sample = 0; sample < 20; sample++)
                        {
                            var plan = new RelativityPlan(self, initial, solution, ExerciseDirection.Random, random);
                            if (initial != RelativityDebuff.Random)
                                Check(RelativityPlan.Kind(plan.Codes[self]) == (int)initial, "指定点名必须落在自身。");
                            Check(plan.Codes.Distinct().Count() == 8 && plan.StackTargets.Distinct().Count() == 3, "点名分工与三轮神圣必须唯一。");
                            for (var wave = 0; wave < 3; wave++)
                            {
                                var points = Enumerable.Range(0, 8).Select(i => plan.FirePosition(i, wave).Sample(random)).ToArray();
                                foreach (var target in Enumerable.Range(0, 8).Where(i => plan.HasFire(i, wave)))
                                    Check(points.Where((_, i) => i != target).All(point => Vector2.Distance(point, points[target]) > 8), "火分散容差不得波及队友。");
                                var stack = plan.StackTargets[wave];
                                Check(!plan.HasFire(stack, wave), "分摊点名不能与同轮火重叠。");
                                Check(points.Count(point => Vector2.Distance(point, points[stack]) <= 6) >= 5, "神圣必须留有五人分摊。");
                                var baits = Enumerable.Range(0, 8).Select(i => plan.Between(i, wave).Sample(random)).ToArray();
                                foreach (var target in Enumerable.Range(0, 8).Where(i => plan.LaserWave(i) == wave))
                                {
                                    var glass = plan.Hourglass(plan.DirectionIndex(target));
                                    Check(Math.Abs(Vector2.Distance(glass, baits[target]) - 1) < 0.0001, "引导点必须距沙漏一米且没有容差。");
                                    Check(Enumerable.Range(0, 8).OrderBy(i => Vector2.DistanceSquared(glass, baits[i])).First() == target, "沙漏最近目标必须正确。");
                                }
                            }
                        }
        }

        public static void RelativityLastLaserRetreatStaysBehindBothRotatingLasers()
        {
            var random = new Random(20261117);
            foreach (RelativitySolution solution in Enum.GetValues(typeof(RelativitySolution)))
                for (var north = 0; north < 8; north++)
                    for (var sample = 0; sample < 32; sample++)
                    {
                        var plan = new RelativityPlan(0, RelativityDebuff.Random, solution, (ExerciseDirection)north, random);
                        var baiters = Enumerable.Range(0, 8).Where(slot => plan.LaserWave(slot) == 2).ToArray();
                        foreach (var slot in baiters)
                        {
                            var direction = plan.DirectionIndex(slot);
                            var area = plan.LastLaserRetreat(direction);
                            var center = area.Midpoint();
                            Check(area.DeviationRadius == 0.5f,
                                "最后一组沙漏内侧等待点使用半米容差。");
                            Check(Vector2.Distance(center, RelativityPlan.Center + 7 * RelativityPlan.Direction(plan.Angle(slot))) < 0.0001,
                                "引导后应退到自身沙漏向场中两米半的位置，不能回到场中。");
                            Check(Vector2.Distance(plan.LaserPoint(slot), center) + area.DeviationRadius < (49.0 - 47.9) * 6,
                                "最后一组应能在停手前以正常速度走完完整撤离距离。");
                            for (var edge = 0; edge < 16; edge++)
                            {
                                var point = center + 0.5f * RelativityPlan.Direction(Deg(edge * 22.5));
                                foreach (var caster in baiters)
                                    for (var step = 0; step <= 9; step++)
                                    {
                                        var casterDirection = plan.DirectionIndex(caster);
                                        var heading = plan.Angle(caster) + Deg(plan.Spins[casterDirection] * (step * 15 - 60));
                                        Check(!ActionArea.Rectangle(60, 5).Contains(point, plan.Hourglass(casterDirection), (float)heading),
                                            "沙漏内侧等待点的容差边缘应避开最后两座沙漏的全部旋转激光。");
                                    }
                            }
                        }
                    }
        }

        public static void RelativityFixedRepeatedDebuffsKeepTheirPartnersUniform()
        {
            foreach (var self in new[] { 1, 5 })
            {
                var random = new Random(110117 + self);
                var kind = self < 4 ? RelativityDebuff.LongFire : RelativityDebuff.ShortFire;
                var start = self < 4 ? 0 : 4;
                var counts = new int[8];
                const int samples = 6000;
                for (var sample = 0; sample < samples; sample++)
                {
                    var plan = new RelativityPlan(self, kind, RelativitySolution.CN, ExerciseDirection.N, random);
                    Check(RelativityPlan.Kind(plan.Codes[self]) == (int)kind, "修正偏差后仍必须满足指定自身点名。");
                    var partner = Enumerable.Range(start, 4).Single(slot => slot != self && RelativityPlan.Kind(plan.Codes[slot]) == (int)kind);
                    counts[partner]++;
                }
                foreach (var slot in Enumerable.Range(start, 4).Where(slot => slot != self))
                    Check(Math.Abs(counts[slot] / (double)samples - 1.0 / 3) < 0.03,
                        "指定重复点名后，同组队友应等概率成为同点名者，不能偏向下标靠后的玩家。");
            }
        }

        public static void ApocalypseEqualGroupsPreserveEveryPriorityAndFixedDebuff()
        {
            foreach (ApocalypseSolution solution in Enum.GetValues(typeof(ApocalypseSolution)))
            {
                var priority = solution == ApocalypseSolution.DoubleCN ? new[] { 2, 3, 0, 1, 4, 5, 6, 7 } :
                    solution == ApocalypseSolution.DoubleHTDH || solution == ApocalypseSolution.SingleHTDH ? new[] { 2, 0, 1, 4, 5, 6, 7, 3 } :
                    solution == ApocalypseSolution.DoubleNA || solution == ApocalypseSolution.SingleJP ? new[] { 3, 2, 1, 0, 4, 5, 6, 7 } :
                    solution == ApocalypseSolution.SingleReverseD ? new[] { 0, 1, 2, 3, 7, 6, 5, 4 } :
                    solution == ApocalypseSolution.SingleBalanced ? new[] { 0, 1, 4, 5, 2, 3, 6, 7 } : Enumerable.Range(0, 8).ToArray();
                for (var self = 0; self < 8; self++)
                    for (var initial = 0; initial < 4; initial++)
                    {
                        var seed = 20261117 + self * 10 + initial;
                        if (self == priority[0] || self == priority[7])
                        {
                            var original = new ApocalypsePlan(self, (ApocalypseDebuff)initial, solution, ApocalypseReference.GroupSafe,
                                ApocalypseHalf.NorthToSouthwest, ExerciseDirection.N, ExerciseTurn.Clockwise, new Random(seed), 5, equalGroups: false);
                            var enabled = new ApocalypsePlan(self, (ApocalypseDebuff)initial, solution, ApocalypseReference.GroupSafe,
                                ApocalypseHalf.NorthToSouthwest, ExerciseDirection.N, ExerciseTurn.Clockwise, new Random(seed), 5);
                            Check(original.Orders.SequenceEqual(enabled.Orders) && original.StackGroups.SequenceEqual(enabled.StackGroups),
                                "最高、最低优先级不应因开启等概率分组而改变抽样结果。");
                            continue;
                        }
                        for (var group = 0; group < 2; group++)
                        {
                            var random = new AssignmentRandomTests.GroupRandom(seed, group);
                            var plan = new ApocalypsePlan(self, (ApocalypseDebuff)initial, solution, ApocalypseReference.GroupSafe,
                                ApocalypseHalf.NorthToSouthwest, ExerciseDirection.N, ExerciseTurn.Clockwise, random, 5);
                            Check(random.GroupChosen && plan.StackGroups[self] == group && plan.Orders[self] == initial,
                                "均衡抽样应覆盖自身两组，且保留指定点名。");
                            for (var side = 0; side < 2; side++)
                            {
                                var members = plan.Group(side);
                                Check(members.Select(slot => plan.Orders[slot]).OrderBy(value => value).SequenceEqual(new[] { 0, 1, 2, 3 }),
                                    "各组应仍有无、短、中、长水各一人。");
                                if (solution == ApocalypseSolution.SingleBalanced)
                                    Check(members.Count(slot => slot < 2 || slot == 4 || slot == 5) == 2,
                                        "均衡自身组别不能破坏自动双近双远。");
                            }
                            if (solution != ApocalypseSolution.SingleBalanced)
                                for (var kind = 0; kind < 4; kind++)
                                {
                                    var pair = priority.Where(slot => plan.Orders[slot] == kind).ToArray();
                                    Check(plan.StackGroups[pair[0]] == 0 && plan.StackGroups[pair[1]] == 1,
                                        "均衡抽样不能改写攻略优先级，应从合法点名中选择。");
                                }
                        }
                    }
            }
        }

        public static void ApocalypseAllGroupProfilesKeepStacksAndSpreadClear()
        {
            var random = new Random(9327);
            foreach (ApocalypseSolution solution in Enum.GetValues(typeof(ApocalypseSolution)))
                foreach (ApocalypseReference reference in Enum.GetValues(typeof(ApocalypseReference)))
                    foreach (ApocalypseHalf half in Enum.GetValues(typeof(ApocalypseHalf)))
                        for (var direction = 0; direction < 8; direction++)
                            for (var turn = 1; turn <= 2; turn++)
                            {
                                var plan = new ApocalypsePlan(0, ApocalypseDebuff.Random, solution, reference, half,
                                    (ExerciseDirection)direction, (ExerciseTurn)turn, random, 5);
                                for (var group = 0; group < 2; group++)
                                {
                                    Check(plan.Group(group).Length == 4, "每组必须四人。");
                                    for (var wave = 0; wave < 3; wave++)
                                        Check(plan.StackGroups[plan.Target(wave, group)] == group, "每组每轮必须恰有一名水分摊。");
                                    if (solution == ApocalypseSolution.SingleBalanced)
                                        Check(plan.Group(group).Count(i => i < 2 || i == 4 || i == 5) == 2, "平衡组应双近双远。");
                                }
                                var points = Enumerable.Range(0, 8).Select(i => plan.Spread(i).Sample(random)).ToArray();
                                var initial = Enumerable.Range(0, 8).Select(plan.InitialStack).ToArray();
                                var jumps = Enumerable.Range(0, 8).Select(plan.FirstJump).ToArray();
                                var jumpPoints = plan.SampleFirstJump(random);
                                for (var i = 0; i < 8; i++)
                                {
                                    var stackCenter = ApocalypsePlan.Center + new Vector2(plan.StackGroups[i] == 0 ? -8 : 8, 0);
                                    var offset = initial[i].Midpoint() - stackCenter;
                                    var towardSpread = Vector2.Normalize(jumps[i].Midpoint() - stackCenter);
                                    Check(Math.Abs(offset.Length() - 1.5f) < 0.0001 && (plan.IsSingleGroup
                                        ? Math.Abs(Math.Abs(offset.X) - Math.Abs(offset.Y)) < 0.0001
                                        : Vector2.Dot(offset, towardSpread) > 1.4999f),
                                        "初始分摊应从正左右八米处预站一米半；单分组使用固定斜方向。");
                                    Check(Math.Abs(initial[i].DeviationRadius - (plan.IsSingleGroup ? 1 : 0.5)) < 0.0001,
                                        "单分组预站位使用一米容差，双分组保留半米容差。");
                                    Check(jumps[i].Contains(jumpPoints[i]), "协调后的随机分散落点必须仍在各自容差范围内。");
                                    Check(Vector2.Distance(jumps[i].Midpoint(), ApocalypsePlan.Center) + jumps[i].DeviationRadius < 20,
                                        "碎灵分散的完整容差范围不得越过电网。");
                                    for (var other = 0; other < 8; other++)
                                    {
                                        var distance = Vector2.Distance(initial[i].Midpoint(), initial[other].Midpoint());
                                        Check(plan.StackGroups[i] == plan.StackGroups[other]
                                            ? distance + initial[i].DeviationRadius + initial[other].DeviationRadius < 6
                                            : distance - initial[i].DeviationRadius - initial[other].DeviationRadius > 6,
                                            "初始分摊的完整容差范围必须保持同组四人，且不波及另一组。");
                                        if (other != i)
                                            Check(Vector2.Distance(jumpPoints[i], jumpPoints[other]) > 5.1f,
                                                "碎灵分散的随机落点不得互相波及，并应保留余量。");
                                    }
                                    Check(Vector2.Distance(initial[i].Midpoint(), jumps[i].Midpoint()) + initial[i].DeviationRadius + jumps[i].DeviationRadius < (17.52 - 15.61) * 6,
                                        "初始分摊后，应能以正常跑速在碎灵一击锁定前完成分散。");
                                    Check(Vector2.Distance(points[i], ApocalypsePlan.Center) < 20, "分散不可越过电网。");
                                    Check(points.Where((_, j) => i != j).All(point => Vector2.Distance(point, points[i]) > 6), "八人分散容差不可重叠。");
                                    foreach (var volley in new[] { 0, 1 })
                                        Check(plan.Explosions(volley).All(center => Vector2.Distance(points[i], center) > 9), "分散点必须避开前两轮地火。");
                                }
                            }
        }

        public static void DarklitAllStrategiesKeepTowersProteansAndWaterValid()
        {
            var random = new Random(110111);
            foreach (DarklitSolution solution in Enum.GetValues(typeof(DarklitSolution)))
                foreach (DiagonalSwap diagonal in Enum.GetValues(typeof(DiagonalSwap)))
                    foreach (SideSwap side in Enum.GetValues(typeof(SideSwap)))
                        for (var self = 0; self < 8; self++)
                            foreach (DarklitDebuff initial in Enum.GetValues(typeof(DarklitDebuff)))
                                for (var sample = 0; sample < 8; sample++)
                                {
                                    var plan = new DarklitPlan(self, initial, solution, diagonal, side, SomberDanceBait.MTThenST, WingSide.Random, random);
                                    Check(plan.Soakers.Concat(plan.Baiters).Distinct().Count() == 8, "双塔与四扇形必须覆盖八人且不重复。");
                                    Check(plan.Cycle.Count(i => i < 2) == 1 && plan.Cycle.Count(i => i >= 2 && i < 4) == 1, "连线固定一坦、一奶、两输出。");
                                    Check(plan.Cycle.Contains(plan.Waters[0]) && !plan.Cycle.Contains(plan.Waters[1]), "水必须一名连线、一名无线。");
                                    Check(plan.Quadrants[plan.Waters[0]] / 2 != plan.Quadrants[plan.Waters[1]] / 2, "两名水必须分属南北两组。");
                                    if (initial != DarklitDebuff.Random)
                                    {
                                        Check(plan.Cycle.Contains(self) == (initial == DarklitDebuff.Chain || initial == DarklitDebuff.ChainWater), "指定连线点名应生效。");
                                        Check(plan.Waters.Contains(self) == (initial == DarklitDebuff.ChainWater || initial == DarklitDebuff.ProteanWater), "指定水点名应生效。");
                                    }
                                    var points = Enumerable.Range(0, 8).Select(i => plan.TowerOrBait(i).Sample(random)).ToArray();
                                    for (var tower = 0; tower < 2; tower++)
                                        Check(points.Count(point => Vector2.Distance(point, plan.Tower(tower)) <= 4) == 2, "塔必须保持两人。");
                                    foreach (var bait in plan.Baiters)
                                    {
                                        var heading = (float)Math.Atan2(points[bait].X - 100, points[bait].Y - 100);
                                        Check(Enumerable.Range(0, 8).Count(i => ActionArea.Cone(60, 60).Contains(points[i], new Vector2(100, 100), heading)) == 1,
                                            "引导扇形不可覆盖队友或双塔。");
                                    }
                                    var spread = Enumerable.Range(0, 8).Select(i => plan.Spread(i).Sample(random)).ToArray();
                                    for (var i = 0; i < 8; i++)
                                    {
                                        Check(spread.Where((_, j) => i != j).All(point => Vector2.Distance(point, spread[i]) > 5), "跳跃分散不可重叠。");
                                        Check(Vector2.Distance(spread[i], U7aCrystal.Position) > 8.5f, "跳跃分散不可击中北侧水晶。");
                                    }
                                }
        }

        public static void ApocalypseEverySingleGroupKeepsTheCounterpartsPositions()
        {
            var random = new Random(2358);
            foreach (var solution in new[] { ApocalypseSolution.SingleTHD, ApocalypseSolution.SingleReverseD,
                ApocalypseSolution.SingleHTDH, ApocalypseSolution.SingleJP, ApocalypseSolution.SingleBalanced })
            {
                var priority = solution == ApocalypseSolution.SingleReverseD ? new[] { 0, 1, 2, 3, 7, 6, 5, 4 } :
                    solution == ApocalypseSolution.SingleHTDH ? new[] { 2, 0, 1, 4, 5, 6, 7, 3 } :
                    solution == ApocalypseSolution.SingleJP ? new[] { 3, 2, 1, 0, 4, 5, 6, 7 } : Enumerable.Range(0, 8).ToArray();
                var swapCounts = new HashSet<int>();
                var matchedTankAndHealerSwap = false;
                for (var sample = 0; sample < 256; sample++)
                {
                    var plan = new ApocalypsePlan(0, ApocalypseDebuff.Random, solution, ApocalypseReference.GroupSafe,
                        ApocalypseHalf.NorthToSouthwest, ExerciseDirection.N, ExerciseTurn.Clockwise, random, 5);
                    swapCounts.Add(Enumerable.Range(0, 8).Count(i => plan.PositionOwners[i] != i));
                    Check(plan.PositionOwners.Distinct().Count() == 8, "换位后仍需恰好占据八个原职能站位。");
                    if (solution != ApocalypseSolution.SingleBalanced)
                    {
                        var left = Enumerable.Range(0, 4).Where(slot => plan.StackGroups[slot] == 1).ToArray();
                        var right = Enumerable.Range(4, 4).Where(slot => plan.StackGroups[slot] == 0).ToArray();
                        for (var index = 0; index < left.Length; index++)
                            Check(plan.PositionOwners[left[index]] == right[index] && plan.PositionOwners[right[index]] == left[index],
                                "换组者应按固定的 T12 H12、D1234 顺序填对侧空位，不能沿用点名分组优先级或重新匹配近远程。");
                        if (left.SequenceEqual(new[] { 1, 3 }) && right.SequenceEqual(new[] { 5, 7 }))
                        {
                            matchedTankAndHealerSwap = true;
                            Check(plan.PositionOwners[1] == 5 && plan.PositionOwners[5] == 1 &&
                                plan.PositionOwners[3] == 7 && plan.PositionOwners[7] == 3,
                                "ST、H2 与 D2、D4 换组时，应为 ST 对 D2、H2 对 D4，不能互换近远程站位。");
                        }
                    }
                    for (var slot = 0; slot < 8; slot++)
                    {
                        var owner = plan.PositionOwners[slot];
                        Check(plan.StackGroups[slot] == owner / 4 && plan.SpreadGroups[slot] == owner / 4,
                            "所有单分组的分摊组与地火半场均应继承换位对象。");
                        if (solution == ApocalypseSolution.SingleBalanced)
                        {
                            Check((slot < 2 || slot == 4 || slot == 5) == (owner % 4 < 2),
                                "标点分组应由一二号站近战位，三四号站远程位，不再继承换位对象。");
                        }
                        else
                        {
                            Check(plan.PositionOwners[owner] == slot, "普通单分组的换位关系必须是双方互换，不能丢失对方身份。");
                            Check(plan.SpreadIndices[slot] == owner % 4, "普通单分组的具体分散点应继承换位对象，不能重新排序。");
                            var pair = priority.Where(i => plan.Orders[i] == plan.Orders[slot]).ToArray();
                            Check(plan.StackGroups[pair[0]] == 0 && plan.StackGroups[pair[1]] == 1,
                                "普通单分组应让同点名的高优先级在左、低优先级在右，与原触发器一致。");
                            Check((owner != slot) == (plan.StackGroups[slot] != slot / 4),
                                "只有原侧别不符合优先级分组的人需要换位，其余玩家保留原站位。");
                            if (solution == ApocalypseSolution.SingleReverseD && plan.Orders[0] == plan.Orders[2])
                                Check(plan.StackGroups[0] == 0 && plan.PositionOwners[0] == 0 && plan.StackGroups[2] == 1,
                                    "默认 T12、H12、D4 至 D1 优先级下，MT 与 H1 同点名应为 MT 留左、H1 换右。");
                            if (solution == ApocalypseSolution.SingleReverseD && plan.Orders[7] == plan.Orders[4])
                                Check(plan.StackGroups[7] == 0 && plan.StackGroups[4] == 1 && plan.PositionOwners[4] == 4,
                                    "默认优先级下，D4 与 D1 同点名应为 D4 换左、D1 留右。");
                        }
                    }
                    for (var group = 0; group < 2; group++)
                        Check(plan.Group(group).Select(slot => plan.Orders[slot]).OrderBy(order => order).SequenceEqual(new[] { 0, 1, 2, 3 }),
                            "换位后每侧应保留无、短、中、长各一人。");
                }
                if (solution != ApocalypseSolution.SingleBalanced)
                {
                    Check(swapCounts.SetEquals(solution == ApocalypseSolution.SingleHTDH ? new[] { 2, 4 } : new[] { 0, 2, 4 }),
                        "换位人数应符合原优先级分组；H2 最低优先级的选项必定需要换到右侧。");
                    if (solution == ApocalypseSolution.SingleReverseD)
                        Check(matchedTankAndHealerSwap, "检查必须覆盖 ST、H2 与 D2、D4 互换的近远程配对案例。");
                }
            }
        }

        public static void ApocalypseSpreadSwapExchangesPointPairsWithoutChangingEarlierAssignments()
        {
            foreach (ApocalypseSolution solution in Enum.GetValues(typeof(ApocalypseSolution)))
                for (var seed = 0; seed < 64; seed++)
                {
                    var normal = new ApocalypsePlan(0, ApocalypseDebuff.None, solution, ApocalypseReference.GroupSafe,
                        ApocalypseHalf.NorthToSouthwest, ExerciseDirection.N, ExerciseTurn.Clockwise, new Random(seed), 5);
                    var swapped = new ApocalypsePlan(0, ApocalypseDebuff.None, solution, ApocalypseReference.GroupSafe,
                        ApocalypseHalf.NorthToSouthwest, ExerciseDirection.N, ExerciseTurn.Clockwise, new Random(seed), 5,
                        swapSpreadPositions: true);
                    Check(normal.Orders.SequenceEqual(swapped.Orders) && normal.StackGroups.SequenceEqual(swapped.StackGroups) &&
                        normal.SpreadGroups.SequenceEqual(swapped.SpreadGroups) && normal.PositionOwners.SequenceEqual(swapped.PositionOwners),
                        "互换地火点位不能改变点名、分组或单分组的换位关系。");
                    Check(normal.GroupMarkers.SequenceEqual(swapped.GroupMarkers), "组内互换不能重新分配头标编号。");
                    for (var slot = 0; slot < 8; slot++)
                    {
                        var partner = Enumerable.Range(0, 8).Single(other => normal.SpreadGroups[other] == normal.SpreadGroups[slot] &&
                            normal.SpreadIndices[other] == (normal.SpreadIndices[slot] ^ 1));
                        Check(swapped.SpreadIndices[slot] == (normal.SpreadIndices[slot] ^ 1), "同组地火点位应按一二、三四成对互换。");
                        Check(Vector2.Distance(swapped.Spread(slot).Midpoint(), normal.Spread(partner).Midpoint()) < 0.0001 &&
                            Vector2.Distance(swapped.ReturnGate(slot).Midpoint(), normal.ReturnGate(partner).Midpoint()) < 0.0001,
                            "互换后的地火分散和回程都必须使用新点位。");
                        Check(Vector2.Distance(swapped.InitialStack(slot).Midpoint(), normal.InitialStack(slot).Midpoint()) < 0.0001 &&
                            Vector2.Distance(swapped.FirstJump(slot).Midpoint(), normal.FirstJump(slot).Midpoint()) < 0.0001,
                            "仅地火分散左右互换，开场预站位和碎灵分散不应受影响。");
                    }
                }
        }

        public static void ApocalypseBalancedMarkersMatchGroupsAndSpreadPositions()
        {
            var priority = new[] { 0, 1, 4, 5, 2, 3, 6, 7 };
            var marks = new[] { MarkerType.Attack1, MarkerType.Attack2, MarkerType.Attack3, MarkerType.Attack4,
                MarkerType.Bind1, MarkerType.Bind2, MarkerType.Bind3, MarkerType.Square };
            foreach (var swap in new[] { false, true })
                for (var seed = 0; seed < 128; seed++)
                {
                    var plan = new ApocalypsePlan(0, ApocalypseDebuff.None, ApocalypseSolution.SingleBalanced, ApocalypseReference.GroupSafe,
                        ApocalypseHalf.NorthToSouthwest, ExerciseDirection.N, ExerciseTurn.Clockwise, new Random(seed), 5,
                        swapSpreadPositions: swap);
                    Check(plan.GroupMarkers.Distinct().Count() == 8 && plan.StackGroups[0] == 0, "双近双远应保留 MT 左组并给八人分配不同头标。");
                    for (var group = 0; group < 2; group++)
                    {
                        var members = priority.Where(slot => plan.StackGroups[slot] == group).ToArray();
                        for (var index = 0; index < 4; index++)
                        {
                            var slot = members[index];
                            Check(plan.GroupMarkers[slot] == marks[group * 4 + index], "头标顺序应为 T、近战、治疗、远程，左攻击右锁链与正方形。");
                            Check(plan.PositionOwners[slot] == group * 4 + index &&
                                Vector2.Distance(plan.InitialStack(slot).Midpoint(), plan.Preposition(group * 4 + index).Midpoint()) < 0.0001,
                                "标点后预站位应由组内编号代替原职能，不能继续继承换位对象的位置。");
                            var point = new Vector2(index < 2 ? -4.5f : -13, index % 2 == 0 ? -4.5f : 4.5f);
                            Check(Vector2.Distance(plan.FirstJump(slot).Midpoint(), ApocalypsePlan.Center + (group == 0 ? point : -point)) < 0.0001,
                                "碎灵分散应按头标一二号在前、三四号在后，之后的地火分散沿用相同编号。");
                            Check(plan.SpreadIndices[slot] == (index ^ (swap ? 1 : 0)), "双近双远的地火站位必须与头标编号及互换选项一致。");
                            Check((slot < 2 || slot == 4 || slot == 5) == (index < 2), "一二号应为近战，三四号应为远程。");
                        }
                    }
                }
            foreach (ApocalypseSolution solution in Enum.GetValues(typeof(ApocalypseSolution)))
                if (solution != ApocalypseSolution.SingleBalanced)
                {
                    var plan = new ApocalypsePlan(0, ApocalypseDebuff.None, solution, ApocalypseReference.GroupSafe,
                        ApocalypseHalf.NorthToSouthwest, ExerciseDirection.N, ExerciseTurn.Clockwise, new Random(32), 5);
                    Check(plan.GroupMarkers.All(mark => mark == null), "其他攻略不应额外生成指挥头标。");
                }
        }

        public static void ApocalypseSingleGroupPrepositionsAndRectangleUseInheritedRoles()
        {
            var random = new Random(7015);
            foreach (var radius in new[] { 5f, 6f })
                foreach (var solution in new[] { ApocalypseSolution.SingleTHD, ApocalypseSolution.SingleReverseD,
                    ApocalypseSolution.SingleHTDH, ApocalypseSolution.SingleJP, ApocalypseSolution.SingleBalanced })
                    for (var sample = 0; sample < 128; sample++)
                    {
                        var plan = new ApocalypsePlan(0, ApocalypseDebuff.Random, solution, ApocalypseReference.GroupSafe,
                            ApocalypseHalf.NorthToSouthwest, ExerciseDirection.N, ExerciseTurn.Clockwise, random, radius);
                        var points = plan.SampleFirstJump(random);
                        for (var slot = 0; slot < 8; slot++)
                        {
                            var owner = plan.PositionOwners[slot];
                            var preposition = plan.Preposition(slot);
                            var standard = ApocalypsePlan.Center + new Vector2(slot < 4 ? -8 : 8, 0);
                            Check(Math.Abs(Vector2.Distance(preposition.Midpoint(), standard) - 1.5) < 0.0001 && preposition.DeviationRadius == 1,
                                "点名前应按原职能左 TH、右 DPS，向斜方向预站一米半并保留一米容差。");
                            Check(Vector2.Distance(plan.InitialStack(slot).Midpoint(), plan.Preposition(owner).Midpoint()) < 0.0001,
                                "点名后换位者应继承对方预站位；不换位者应留在原预站位。");
                            Check(plan.FirstJump(slot).DeviationRadius == 1, "单分组全员碎灵分散使用一米容差。");
                            var inherited = plan.FirstJump(slot).Midpoint() - ApocalypsePlan.Center;
                            var sameSideTank = Array.IndexOf(plan.PositionOwners, owner / 4 * 4 + owner % 2);
                            var tankPoint = plan.FirstJump(sameSideTank).Midpoint() - ApocalypsePlan.Center;
                            Check(Math.Abs(Math.Abs(tankPoint.X) - 4.5f) < 0.0001 && Math.Abs(Math.Abs(tankPoint.Y) - 4.5f) < 0.0001,
                                "近战应位于面朝 Boss 横向正负四米半、纵向四米半的位置。");
                            if (owner % 4 >= 2)
                                Check(Math.Abs(inherited.Y - tankPoint.Y) < 0.0001 && Math.Abs(Math.Abs(inherited.X) - 13) < 0.0001,
                                    "远程应在对应近战正后方，纵向固定十三米。");
                            var opposite = Array.IndexOf(plan.PositionOwners, (owner + 4) % 8);
                            Check((plan.FirstJump(opposite).Midpoint() - ApocalypsePlan.Center + inherited).Length() < 0.0001,
                                "另一侧四人的标准分散点必须为一百八十度旋转对称。");
                            Check(plan.FirstJump(slot).Contains(points[slot]) && Enumerable.Range(0, slot)
                                .All(other => Vector2.Distance(points[slot], points[other]) > radius + 0.1f),
                                "一米容差内的实际随机落点不得造成假队员互相波及。");
                            Check(Enumerable.Range(0, slot).All(other => Vector2.Distance(plan.FirstJump(slot).Midpoint(), plan.FirstJump(other).Midpoint()) - 2 > radius + 0.1f),
                                "单分组的一米完整容差范围应保证所有队员互不波及。");
                        }
                    }
        }

        public static void ApocalypseSelectedTankCanReachItsSafeBaitDirection()
        {
            var random = new Random(40182);
            foreach (ApocalypseSolution solution in Enum.GetValues(typeof(ApocalypseSolution)))
                foreach (BaitTank tank in Enum.GetValues(typeof(BaitTank)))
                    for (var direction = 0; direction < 8; direction++)
                        for (var turn = 1; turn <= 2; turn++)
                            for (var sample = 0; sample < 8; sample++)
                            {
                                var plan = new ApocalypsePlan(0, ApocalypseDebuff.Random, solution, ApocalypseReference.GroupSafe,
                                    ApocalypseHalf.NorthToSouthwest, (ExerciseDirection)direction, (ExerciseTurn)turn, random, 5, tank);
                                Check(plan.TankSlot == (int)tank, "必须保留选项指定的死刑引导坦克。");
                                Check(plan.TankDirection == ApocalypsePlan.Mod(plan.TwoDirection + plan.StackGroups[(int)tank] * 4, 8),
                                    "两侧都能引导死刑，坦克应从当前分摊半场就近出去，不能返回之前的分散半场。");
                                var departure = plan.SecondStack((int)tank);
                                var from = departure.Sample(random);
                                var destination = plan.TankPosition;
                                Check(Math.Abs(Vector2.Distance(destination, ApocalypsePlan.Center) - 18) < 0.0001,
                                    "死刑引导必须使用距场中十八米的贴边点，不能因移动窗口缩短距离。");
                                var distance = Vector2.Distance(from, destination);
                                Check(Vector2.Distance(departure.Midpoint(), destination) + departure.DeviationRadius < (36.81 - 34.55 - 0.15) * 7.8,
                                    "从四米内圈分摊后出发，应能在自动冲刺上限内赶到十八米引导点，并留有同步余量。");
                                var theta = ApocalypsePlan.Angle(plan.ThreeDirection + plan.StackGroups[(int)tank] * 4);
                                for (var step = 0; step <= 4; step++)
                                {
                                    var offset = Vector2.Lerp(from, destination, step / 4f) - ApocalypsePlan.Center;
                                    var angle = Math.IEEERemainder(Math.Atan2(offset.X, offset.Y) - theta, 2 * Math.PI) * plan.Turn;
                                    Check(angle >= Deg(30) - 0.00001 && angle <= Deg(45) + 0.00001,
                                        "T 应在分摊后边出去边从三十度修正至四十五度，不能提前贴到出口方向。");
                                }
                                var progress = Math.Min(distance, (ApocalypsePlan.ExplosionTimes[5] - 34.55 - 0.15) * 7.8);
                                var position = from + Vector2.Normalize(destination - from) * (float)progress;
                                Check(plan.Explosions(5).All(center => Vector2.Distance(position, center) > 9),
                                    "死刑引导路线不得穿过最后一次地火的判定。");
                                var landing = destination - 7.5f * ApocalypsePlan.Direction(ApocalypsePlan.Angle(plan.TankDirection));
                                for (var slot = 0; slot < 8; slot++)
                                    Check(Vector2.Distance(plan.FinalStack(slot, landing).Midpoint(), ApocalypsePlan.Center)
                                        + plan.FinalStack(slot, landing).DeviationRadius < 20,
                                        "更换引导半场后，第三次分摊的完整站位范围仍应留在电网内。");
                            }
        }

        public static void ApocalypseSecondStacksUseInnerSectorsBeforeTheTankLeaves()
        {
            var random = new Random(3453);
            foreach (ApocalypseSolution solution in Enum.GetValues(typeof(ApocalypseSolution)))
                foreach (BaitTank tank in Enum.GetValues(typeof(BaitTank)))
                    for (var direction = 0; direction < 8; direction++)
                        for (var turn = 1; turn <= 2; turn++)
                        {
                            var plan = new ApocalypsePlan(0, ApocalypseDebuff.Random, solution, ApocalypseReference.GroupSafe,
                                ApocalypseHalf.NorthToSouthwest, (ExerciseDirection)direction, (ExerciseTurn)turn, random, 5, tank);
                            var regions = Enumerable.Range(0, 8).Select(plan.SecondStack).ToArray();
                            var points = regions.Select(region => new[]
                            {
                                region.Sample(0, 0, Math.PI), region.Sample(0, 1, Math.PI),
                                region.Sample(1, 0, Math.PI), region.Sample(1, 1, Math.PI)
                            }).ToArray();
                            for (var slot = 0; slot < 8; slot++)
                                foreach (var point in points[slot])
                                {
                                    var offset = point - ApocalypsePlan.Center;
                                    var theta = ApocalypsePlan.Angle(plan.ThreeDirection + plan.StackGroups[slot] * 4);
                                    var angle = Math.IEEERemainder(Math.Atan2(offset.X, offset.Y) - theta, 2 * Math.PI) * plan.Turn;
                                    Check(offset.Length() >= 3.9999f && offset.Length() <= 4.8001f,
                                        "第二次分摊的所有人都应位于半径四至四点八米的扇环内。");
                                    Check(slot == (int)tank ? Math.Abs(angle - Deg(30)) < 0.00001 : angle >= Deg(-5) - 0.00001 && angle <= Deg(30) + 0.00001,
                                        "从人群朝单人为正：引导 T 固定三十度，其他人介于负五度和三十度。");
                                    for (var other = 0; other < 8; other++)
                                        foreach (var otherPoint in points[other])
                                            Check(plan.StackGroups[slot] == plan.StackGroups[other]
                                                ? Vector2.Distance(point, otherPoint) < 6 : Vector2.Distance(point, otherPoint) > 6,
                                                "扇环全部边界组合都应保证同组四人分摊，且不混入另一组。");
                                    Check(plan.Explosions(4).All(center => Vector2.Distance(point, center) > 9.19),
                                        "分摊完整扇环应保留约零点二米的内圈地火余量。");
                                }
                        }
        }

        public static void ApocalypseFinalStacksStayBehindBossWithoutMixingGroups()
        {
            var random = new Random(4356);
            foreach (ApocalypseSolution solution in Enum.GetValues(typeof(ApocalypseSolution)))
                for (var direction = 0; direction < 8; direction++)
                    foreach (var landingRadius in new[] { 5f, 10.5f })
                    {
                        var plan = new ApocalypsePlan(0, ApocalypseDebuff.Random, solution, ApocalypseReference.GroupSafe,
                            ApocalypseHalf.NorthToSouthwest, (ExerciseDirection)direction, ExerciseTurn.Clockwise, random, 5);
                        var heading = ApocalypsePlan.Angle(plan.TankDirection) + 0.05;
                        var landing = ApocalypsePlan.Center + landingRadius * ApocalypsePlan.Direction(heading);
                        var regions = Enumerable.Range(0, 8).Select(slot => plan.FinalStack(slot, landing)).ToArray();
                        var points = regions.Select(region => region.Sample(random)).ToArray();
                        for (var slot = 0; slot < 8; slot++)
                        {
                            var near = slot < 2 || slot == 4 || slot == 5;
                            var delta = points[slot] - landing;
                            var distance = delta.Length();
                            Check(near ? distance >= 10.9999 && distance <= 13.0001 : distance >= 12.9999 && distance <= 15.0001,
                                "最后分摊的近战扇环应为十一至十三米，远程为十三至十五米。");
                            var angle = Math.IEEERemainder(Math.Atan2(delta.X, delta.Y) - heading - Math.PI, Deg(360));
                            var sideAngle = ToDegrees(angle) * (plan.StackGroups[slot] == 0 ? -1 : 1);
                            Check(sideAngle >= 16.9999 && sideAngle <= 33.0001,
                                "两组应分别位于左右后二十五度，近战与远程均保留八度容差。");
                            Check(Vector2.Distance(regions[slot].Midpoint(), ApocalypsePlan.Center) + regions[slot].DeviationRadius < 20,
                                "返回 Boss 身后的整个分摊范围不得越过电网。");
                            for (var other = 0; other < 8; other++)
                            {
                                foreach (var radial in new[] { 0.0, 1.0 })
                                    foreach (var angular in new[] { 0.0, 1.0 })
                                        foreach (var otherRadial in new[] { 0.0, 1.0 })
                                            foreach (var otherAngular in new[] { 0.0, 1.0 })
                                            {
                                                var extreme = regions[slot].Sample(radial, angular, Math.PI);
                                                var otherExtreme = regions[other].Sample(otherRadial, otherAngular, Math.PI);
                                                var extremeDistance = Vector2.Distance(extreme, otherExtreme);
                                                Check(plan.StackGroups[slot] == plan.StackGroups[other] ? extremeDistance < 6 : extremeDistance > 6,
                                                    "即使点名者和参与者分别站在扇环极值，同组也必须互相覆盖，异组必须分离。");
                                            }
                            }
                        }
                        var gather = plan.AfterFinalStack(landing);
                        Check(gather.DeviationRadius == 3 && Vector2.Distance(gather.Midpoint(), landing + 7 * ApocalypsePlan.Direction(heading + Math.PI)) < 0.0001,
                            "分摊后应集合在 Boss 正后方七米、容差三米的圆内。");
                    }
        }

        public static void DarklitWingOriginMatchesTheAttackedHalf()
        {
            var center = new Vector2(100, 100);
            var area = ActionArea.Rectangle(80, 40);
            foreach (var wing in new[] { WingSide.Left, WingSide.Right })
                foreach (DarklitSolution solution in Enum.GetValues(typeof(DarklitSolution)))
                {
                    var plan = new DarklitPlan(0, DarklitDebuff.ChainWater, solution, DiagonalSwap.Anti, SideSwap.Left,
                        SomberDanceBait.MTThenST, wing, new Random(40228));
                    Check(plan.WingAction == (wing == WingSide.Left ? 40227u : 40228u), "左右刀应使用各自的技能。");
                    for (var x = -19; x <= 19; x += 2)
                        for (var y = -19; y <= 19; y += 2)
                        {
                            if (x * x + y * y >= 400)
                                continue;
                            var point = center + new Vector2(x, y);
                            Check(area.Contains(point, plan.WingOrigin, (float)Math.PI) == (wing == WingSide.Left ? x < 0 : x > 0),
                                "使用读条技能坐标作为矩形原点，应准确覆盖左右半场；不能将原点当成希瓦位置。");
                        }
                    for (var slot = 0; slot < 8; slot++)
                    {
                        var region = plan.Stack(slot, U7aCrystal.Position, 9.5f);
                        Check(!area.Contains(region.Midpoint(), plan.WingOrigin, (float)Math.PI), "两组分摊的标准站位应避开半场刀。");
                    }
                }
        }

        public static void DarklitNorthStackTeachesTheTargetToStepInward()
        {
            var random = new Random(40301);
            var edges = new[] { 0d, 1d };
            Vector2[] Corners(SimPosition region)
                => edges.SelectMany(radius => edges.Select(angle => region.Sample(radius, angle, Math.PI))).ToArray();

            var crystal = U7aCrystal.Position;
            const float crystalRadius = 3.5f;
            var area = ActionArea.Circle(6);
            var safeDistance = 6 + crystalRadius;
            foreach (DarklitSolution solution in Enum.GetValues(typeof(DarklitSolution)))
                foreach (WingSide wing in new[] { WingSide.Left, WingSide.Right })
                    for (var sample = 0; sample < 64; sample++)
                    {
                        var plan = new DarklitPlan(0, DarklitDebuff.Random, solution, DiagonalSwap.Anti, SideSwap.Left,
                            SomberDanceBait.MTThenST, wing, random);
                        var regions = Enumerable.Range(0, 8).Select(slot => plan.Stack(slot, crystal, safeDistance)).ToArray();
                        var positions = regions.Select(region => region.Sample(random)).ToArray();
                        for (var slot = 0; slot < 8; slot++)
                        {
                            Check((plan.RightSafe ? positions[slot].X - 100 : 100 - positions[slot].X) > 0,
                                "南北两组分摊的随机落点应处于半场刀安全侧。");
                            if (plan.Quadrants[slot] < 2)
                            {
                                var targeted = plan.Waters.Contains(slot);
                                var corners = Corners(regions[slot]).Select(point => point - crystal).ToArray();
                                var radii = corners.Select(offset => offset.Length()).ToArray();
                                var angles = corners.Select(offset => Math.Atan2(plan.RightSafe ? offset.X : -offset.X, offset.Y)).ToArray();
                                Check(Math.Abs(radii.Min() - (safeDistance + (targeted ? 0.1f : -2))) < 0.0001 &&
                                    Math.Abs(radii.Max() - (safeDistance + (targeted ? 2 : 0))) < 0.0001,
                                    "北侧点名者在水晶判定边界外 0.1 至 2 米，其他三人在边界内 0 至 2 米。");
                                Check(Math.Abs(angles.Min() - Deg(targeted ? 10 : 2)) < 0.0001 &&
                                    Math.Abs(angles.Max() - Deg(targeted ? 20 : 28)) < 0.0001,
                                    "北侧点名者朝安全侧偏 10 至 20 度，其他三人偏 2 至 28 度。");
                                Check(area.IntersectsCircle(crystal, crystalRadius, positions[slot]) != targeted,
                                    "北侧点名者在自身范围内应安全；若跟到参与者的范围，则必定击中水晶。");
                            }
                            else
                            {
                                Check(Math.Abs(regions[slot].DeviationRadius - 2) < 0.0001 && Math.Abs(regions[slot].Midpoint().Y - 109) < 0.0001,
                                    "南侧全员应使用中心偏南九米、半径两米的相同站位范围。");
                            }
                        }
                        foreach (var target in plan.Waters)
                            for (var slot = 0; slot < 8; slot++)
                            {
                                if (slot == target)
                                    continue;

                                var sameGroup = plan.Quadrants[target] / 2 == plan.Quadrants[slot] / 2;
                                if (sameGroup && plan.Quadrants[target] < 2)
                                {
                                    // 两个同心扇环的最大夹角在角边界，固定角度后最远距离在半径两端。
                                    var memberCorners = Corners(regions[slot]);
                                    Check(Corners(regions[target]).All(center => memberCorners.All(point => area.Contains(point, center, 0))),
                                        "北侧点名者在扇环任一点均应覆盖本组队员的完整扇环。");
                                    continue;
                                }

                                var distance = Vector2.Distance(regions[target].Midpoint(), regions[slot].Midpoint());
                                var tolerance = regions[target].DeviationRadius + regions[slot].DeviationRadius;
                                Check(sameGroup ? distance + tolerance < 6 : distance - tolerance > 6,
                                    "南北分摊的完整容差范围必须恰好包含本组四人，且不波及另一组。");
                            }
                    }
        }

        public static void ApocalypseReturnRouteLeavesNextFlamesBeforeTheyResolve()
        {
            var random = new Random(40297);
            for (var direction = 0; direction < 8; direction++)
                for (var rotation = 1; rotation <= 2; rotation++)
                    foreach (ApocalypseSolution solution in Enum.GetValues(typeof(ApocalypseSolution)))
                        foreach (ApocalypseReference reference in Enum.GetValues(typeof(ApocalypseReference)))
                            foreach (ApocalypseHalf half in Enum.GetValues(typeof(ApocalypseHalf)))
                            {
                                var plan = new ApocalypsePlan(0, ApocalypseDebuff.Random, solution, reference, half,
                                    (ExerciseDirection)direction, (ExerciseTurn)rotation, random, 5);
                                for (var i = 0; i < 8; i++)
                                {
                                    var from = plan.Spread(i).Sample(random);
                                    var to = plan.SecondStack(i).Sample(random);
                                    var gate = plan.ReturnGate(i).Sample(random);
                                    for (var volley = 2; volley < 6; volley++)
                                    {
                                        var time = ApocalypsePlan.ExplosionTimes[volley];
                                        // 保留一次网络移动包的同步余量，使用正常跑速验证每段路线。
                                        var firstLeg = time < 30.45;
                                        var start = firstLeg ? from : gate;
                                        var end = firstLeg ? gate : to;
                                        var departed = firstLeg ? 28.74 : 30.45;
                                        var distance = Vector2.Distance(start, end);
                                        var travel = Math.Min(distance, Math.Max(0, time - departed - 0.15) * 6);
                                        var position = distance < 0.0001f ? end : start + Vector2.Normalize(end - start) * (float)travel;
                                        Check(plan.Explosions(volley).All(center => Vector2.Distance(position, center) > 9.05),
                                            $"分散后回中路径必须避开后续地火：{solution}、{reference}、{half}、{direction}、{rotation}、职能 {i}、第 {volley + 1} 轮，三火 {plan.ThreeDirection}，分散组 {plan.SpreadGroups[i]}，分摊组 {plan.StackGroups[i]}，起点 {from}，过渡 {gate}，采样 {position}，最近火 {plan.Explosions(volley).Min(center => Vector2.Distance(position, center))}。");
                                    }
                                }
                            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }
    }
}
