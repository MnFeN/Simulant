using Simulant.Content.U7a.Presets;
using Simulant.Simulation.Geometry;
using System;
using System.Linq;
using System.Numerics;
using static Simulant.Utils.Maths.Angles;

namespace Simulant.Tests
{
    internal static class U7aP5ExerciseTests
    {
        private static readonly Vector2 Center = new Vector2(100, 100);
        private static readonly int[] Slots = Enumerable.Range(0, 8).ToArray();

        public static void ParadiseRegainedFitsAllTowerAssignmentsAndBothWings()
        {
            var tower = ActionArea.Circle(3);
            var cleave = ActionArea.Cone(100, 225);
            var tether = ActionArea.Circle(4);
            var random = new Random(0x9D7F);
            var groupOrders = new (ParadiseTowerGroups Groups, int[] Starts)[]
            {
                (ParadiseTowerGroups.H12D12D34, new[] { 2, 4, 6 }),
                (ParadiseTowerGroups.H12D34D12, new[] { 2, 6, 4 }),
                (ParadiseTowerGroups.D12H12D34, new[] { 4, 2, 6 }),
                (ParadiseTowerGroups.D12D34H12, new[] { 4, 6, 2 }),
                (ParadiseTowerGroups.D34H12D12, new[] { 6, 2, 4 }),
                (ParadiseTowerGroups.D34D12H12, new[] { 6, 4, 2 })
            };
            var scenarios =
                from first in new[] { ParadiseTower.South, ParadiseTower.Northeast, ParadiseTower.Northwest }
                from turn in new[] { ExerciseTurn.Clockwise, ExerciseTurn.Counterclockwise }
                from wings in new[] { ParadiseWings.DarkFirst, ParadiseWings.LightFirst }
                from rule in Enum.GetValues(typeof(ParadiseTowerRule)).Cast<ParadiseTowerRule>()
                from order in groupOrders
                select (first, turn, wings, rule, order);

            foreach (var (first, turn, wings, rule, order) in scenarios)
            {
                var plan = new P52复乐园Plan(Center, first, turn, wings, rule, order.Groups);
                var left = turn == ExerciseTurn.Clockwise ? 1 : 2;
                var danger = wings == ParadiseWings.DarkFirst ? 3 - left : left;
                var middle = rule == ParadiseTowerRule.FirstWing ? danger : rule == ParadiseTowerRule.LeftRight ? left : 1;
                var assignedWaves = new[] { 0, middle, 3 - middle };
                for (var group = 0; group < assignedWaves.Length; group++)
                    Check(plan.TowerPair(assignedWaves[group]).SequenceEqual(Enumerable.Range(order.Starts[group], 2)),
                        $"踩塔分配不符合所选规则与排列：{first}、{turn}、{wings}、{rule}、{order.Groups}。");
                Check(Enumerable.Range(0, 3).Select(plan.TowerMapSlot).Distinct().Count() == 3, "三座塔应使用独立的场地槽。");

                for (var sample = 0; sample < 128; sample++)
                {
                    var starts = Slots.Select(slot => plan.FirstPosition(slot).Sample(random)).ToArray();
                    var ends = Slots.Select(slot => plan.SecondPosition(slot).Sample(random)).ToArray();
                    Check(starts.Concat(ends).All(point => Vector2.Distance(point, Center) < 20), "孤翼的站位不能碰到电网。");
                    Check(Slots.All(slot => Vector2.Distance(starts[slot], ends[slot]) < 18), "假队员必须能在下一轮前穿过场中。");
                    CheckBusters(plan, starts, 0, cleave, tether);
                    CheckBusters(plan, ends, 1, cleave, tether);

                    for (var wave = 0; wave < 3; wave++)
                    {
                        var points = wave == 0 ? starts : ends;
                        var actual = Slots.Where(slot => tower.Contains(points[slot], plan.TowerCenter(wave), 0));
                        Check(actual.SequenceEqual(plan.TowerPair(wave)), "整个随机站位范围都必须由两名指定队员正确踩塔。");
                    }
                }
            }
        }

        public static void PolarizingStrikesSeparatesTheLinesAndPreservesElementSwaps()
        {
            var line = ActionArea.Rectangle(100, 6);
            var random = new Random(0x9D7C);
            foreach (PolarizingOrder order in Enum.GetValues(typeof(PolarizingOrder)))
                for (var facing = 0; facing < 8; facing++)
                {
                    var plan = new P53星灵之剑Plan(Center, (float)(Math.PI + Deg(facing * 45)), order);
                    var blocked = new bool[2, 8];
                    for (var round = 0; round < 4; round++)
                        for (var side = 0; side < 2; side++)
                        {
                            var planned = Slots.Where(slot => plan.Side(round, slot) == side).ToArray();
                            var front = plan.Front(round, side);
                            Check(planned.Length == 4 && planned.All(slot => !blocked[side, slot]), "换边后同属性光束中仍有四名无该属性耐性降低的队员。");
                            Check(front / 2 == ExpectedRole(order, round), "挡枪前排职能未按所选顺序轮换。");
                            blocked[side, front] = true;
                            for (var sample = 0; sample < 64; sample++)
                            {
                                var positions = Slots.Select(slot => plan.Stack(round, slot).Sample(random)).ToArray();
                                var closest = Slots.Where(slot => plan.Side(positions[slot]) == side)
                                    .OrderBy(slot => Vector2.DistanceSquared(positions[slot], Center)).First();
                                Check(closest == front, "随机落点不能使后排成为离 Boss 最近的挡枪者。");
                                var heading = Heading(positions[front]);
                                var actual = Slots.Where(slot => line.Contains(positions[slot], Center, heading));
                                Check(actual.SequenceEqual(planned), "分摊必须覆盖本侧整组并避开对侧。");
                                Check(Slots.All(slot => !line.Contains(plan.Dodge(slot).Sample(random), Center, heading)), "余波回中位置必须避开锁定后的两道直线。");
                            }
                        }
                }
        }

        private static int ExpectedRole(PolarizingOrder order, int round)
        {
            if (order == PolarizingOrder.THMR)
                return round;
            if (round == 3)
                return order == PolarizingOrder.TMRT ? 0 : 1;
            return round == 0 ? 0 : round + 1;
        }

        private static void CheckBusters(P52复乐园Plan plan, Vector2[] points, int wave, ActionArea cleave, ActionArea tether)
        {
            var facing = P52复乐园Plan.CleaveHeading(Heading(points[wave]), plan.Dark(wave));
            Check(Slots.Where(slot => cleave.Contains(points[slot], Center, facing)).SequenceEqual(new[] { wave }),
                "225 度的孤翼扇形必须只命中当前一仇。");
            var tethered = Slots.Where(slot => slot != wave)
                .OrderBy(slot => (plan.Dark(wave) ? 1 : -1) * Vector2.DistanceSquared(points[slot], Center)).First();
            Check(tethered == 1 - wave, "光翼最远或暗翼最近的连线应由另一坦克引导。");
            Check(Slots.Where(slot => tether.Contains(points[slot], points[tethered], 0)).SequenceEqual(new[] { tethered }),
                "半径 4 米的连线死刑不可波及其他队员。");
        }

        private static float Heading(Vector2 point) => (float)Math.Atan2(point.X - Center.X, point.Y - Center.Y);

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
