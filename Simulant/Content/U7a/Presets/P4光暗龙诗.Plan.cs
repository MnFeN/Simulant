using Simulant.Simulation.Geometry;
using Simulant.Simulation.Options;
using System;
using System.Linq;
using System.Numerics;
using static Simulant.Utils.Maths.Angles;

namespace Simulant.Content.U7a.Presets
{
    internal enum DarklitSolution
    {
        RopeTHMR, RopeHTMR, RopeHTRM
    }

    internal enum DiagonalSwap
    {
        Main, Anti
    }

    internal enum SideSwap
    {
        Left, Right
    }

    internal enum SomberDanceBait
    {
        MTInvulnerable, STInvulnerable, MTThenST, STThenMT
    }

    internal enum DarklitDebuff
    {
        // 连线、无线各四人，其中各一人带水；按实际人数分布随机自身点名。
        [RandomEnum(ChainWater, Chain, Chain, Chain, ProteanWater, Protean, Protean, Protean)]
        Random,
        ChainWater, Chain, ProteanWater, Protean
    }

    internal enum WingSide
    {
        [RandomEnum(Left, Right)]
        Random,
        Left, Right
    }

    internal enum RopeShape
    {
        Butterfly, Square, Hourglass
    }

    internal sealed class DarklitPlan
    {
        internal readonly int[] Cycle;
        internal readonly int[] Waters;
        // 均按左上、右上、左下、右下排列。
        internal readonly int[] Soakers;
        internal readonly int[] Baiters;
        internal readonly int[] Quadrants = new int[8];
        internal readonly int[] Priority;
        internal readonly int FirstTank;
        internal readonly int SecondTank;
        internal readonly bool NeedsInvulnerability;
        internal readonly bool RightSafe;
        internal readonly RopeShape Shape;
        private readonly int[] _lineup;
        private static Vector2 Center => new Vector2(100, 100);
        internal uint WingAction => RightSafe ? 40227u : 40228u;
        // 半场刀读条包中的技能坐标；希瓦本体始终留在场中。
        internal Vector2 WingOrigin => Center + new Vector2(RightSafe ? -20 : 20, 40);

        internal DarklitPlan(int self, DarklitDebuff initial, DarklitSolution solution, DiagonalSwap diagonal,
            SideSwap side, SomberDanceBait tanks, WingSide wing, Random random)
        {
            initial = initial.ResolveRandom(random);
            var chained = initial == DarklitDebuff.Chain || initial == DarklitDebuff.ChainWater;
            var water = initial == DarklitDebuff.ChainWater || initial == DarklitDebuff.ProteanWater;
            int[] Pick(int start, int count, int take)
            {
                var candidates = Enumerable.Range(start, count).Where(i => i != self).OrderBy(_ => random.Next()).ToList();
                if (chained && self >= start && self < start + count)
                    candidates.Insert(0, self);
                return candidates.Take(take).ToArray();
            }
            // 连线固定一坦、一奶、两名输出；分摊各点一名连线和无连线玩家。
            Cycle = Pick(0, 2, 1).Concat(Pick(2, 2, 1)).Concat(Pick(4, 4, 2)).OrderBy(_ => random.Next()).ToArray();
            var unchained = Enumerable.Range(0, 8).Where(i => !Cycle.Contains(i)).ToArray();
            Waters = new[]
            {
                chained && water ? self : Cycle.Where(i => i != self).OrderBy(_ => random.Next()).First(),
                !chained && water ? self : unchained.Where(i => i != self).OrderBy(_ => random.Next()).First()
            };
            Priority = solution == DarklitSolution.RopeHTMR ? new[] { 2, 3, 0, 1, 4, 5, 6, 7 } :
                solution == DarklitSolution.RopeHTRM ? new[] { 2, 3, 0, 1, 6, 7, 4, 5 } : Enumerable.Range(0, 8).ToArray();
            _lineup = Enumerable.Range(0, 8).OrderBy(i => Priority[i]).ToArray();
            var anchor = Cycle.OrderBy(i => Priority[i]).First();
            var offset = Array.IndexOf(Cycle, anchor);
            var ring = Enumerable.Range(0, 4).Select(i => Cycle[(offset + i) % 4]).ToArray();
            if (Priority[ring[1]] > Priority[ring[3]])
                ring = new[] { ring[0], ring[3], ring[2], ring[1] };
            Shape = Priority[ring[1]] > Priority[ring[2]] ? RopeShape.Butterfly :
                Priority[ring[2]] > Priority[ring[3]] ? RopeShape.Square : RopeShape.Hourglass;
            int[] mapping;
            if (Shape == RopeShape.Butterfly)
                mapping = new[] { 0, 2, 1, 3 };
            else if (Shape == RopeShape.Square)
                mapping = side == SideSwap.Left ? new[] { 3, 1, 0, 2 } : new[] { 0, 2, 3, 1 };
            else
                mapping = diagonal == DiagonalSwap.Main ? new[] { 3, 1, 2, 0 } : new[] { 0, 2, 1, 3 };
            Soakers = mapping.Select(i => ring[i]).ToArray();
            Baiters = _lineup.Where(i => !Cycle.Contains(i)).ToArray();
            var waterQuadrant = Array.IndexOf(Baiters, Waters[1]);
            if ((Array.IndexOf(Soakers, Waters[0]) < 2) == (waterQuadrant < 2))
                Swap(Baiters, waterQuadrant, waterQuadrant ^ 2);
            for (var i = 0; i < 4; i++)
            {
                Quadrants[Soakers[i]] = i;
                Quadrants[Baiters[i]] = i;
            }
            FirstTank = tanks == SomberDanceBait.STInvulnerable || tanks == SomberDanceBait.STThenMT ? 1 : 0;
            SecondTank = tanks == SomberDanceBait.MTThenST ? 1 : tanks == SomberDanceBait.STThenMT ? 0 : FirstTank;
            NeedsInvulnerability = FirstTank == SecondTank;
            RightSafe = wing.ResolveRandom(random) == WingSide.Left;
        }

        private static void Swap(int[] values, int first, int second)
        {
            var value = values[first];
            values[first] = values[second];
            values[second] = value;
        }

        internal int[] Group(int quadrant) => Enumerable.Range(0, 8).Where(i => Quadrants[i] / 2 == quadrant / 2).ToArray();
        internal Vector2 Tower(int northSouth) => Center + new Vector2(0, northSouth == 0 ? -8 : 8);
        internal SimPosition Initial(int slot)
        {
            var index = Array.IndexOf(_lineup, slot);
            return SimPosition.Ellipse(Center + new Vector2((index % 4 - 1.5f) * 2.5f, index < 4 ? -5 : 5), 0.5f);
        }

        internal SimPosition TowerOrBait(int slot)
        {
            var quadrant = Quadrants[slot];
            var signX = quadrant % 2 == 0 ? -1 : 1;
            var signY = quadrant < 2 ? -1 : 1;
            return SimPosition.Ellipse(Center + (Cycle.Contains(slot) ? new Vector2(signX * 2, signY * 10)
                : new Vector2(signX * 4.53154f, signY * 2.11309f)), Cycle.Contains(slot) ? 0.45f : 0.12f);
        }

        internal SimPosition Spread(int slot)
        {
            var q = Quadrants[slot];
            var x = (q % 2 == 0 ? -1 : 1) * (Cycle.Contains(slot) ? 3.4f : 8.2f);
            var y = (q < 2 ? -1 : 1) * (Cycle.Contains(slot) ? q < 2 ? 7.1f : 8.2f : 3.4f);
            return SimPosition.Ellipse(Center + new Vector2(x, y), 0.25f);
        }

        internal SimPosition Stack(int slot, Vector2 crystal, float safeDistance)
        {
            if (Quadrants[slot] >= 2)
                return SimPosition.Ellipse(Center + new Vector2(RightSafe ? 2.5f : -2.5f, 9), 2);

            // 北侧点名者必须比队友更靠场中；跟着队友站位会使分摊波及水晶。
            var targeted = Waters.Contains(slot);
            return SimPosition.AnnularSector(crystal, safeDistance + (targeted ? 1.05f : -1),
                Deg(RightSafe ? 15 : -15), targeted ? 0.95f : 1, Deg(targeted ? 5 : 13));
        }
        internal SimPosition FirstBait(int slot) => SimPosition.Ellipse(Center +
            (slot == FirstTank ? new Vector2(RightSafe ? 15 : -15, 11) : new Vector2(RightSafe ? 2 : -2, 0)), 0.45f);
        internal SimPosition SecondBait(int slot, Vector2 landing) => SimPosition.Ellipse(
            slot == SecondTank ? landing + Vector2.Normalize(Center - landing) * 1.5f : Center, slot == SecondTank ? 0.3f : 1.1f);
    }
}
