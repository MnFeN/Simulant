using System;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U7a.Presets
{
    internal sealed class P23光之失控Plan
    {
        internal static readonly int[] Clock = { 0, 7, 1, 5, 3, 4, 2, 6 };
        private static readonly int[] PuddlePriority = { 4, 8, 1, 5, 3, 7, 2, 6 };
        private static readonly int[] TowerOrder = { 5, 1, 3, 0, 4, 2 };
        private static readonly Vector2[] Trail =
        {
            new Vector2(-8, 0), new Vector2(-5, -6), new Vector2(1.33f, -8),
            new Vector2(7.66f, -10), new Vector2(14, -12)
        };

        internal int[] Baits { get; }
        internal int[] Chain { get; }
        internal int[] TowerPlayers { get; }
        internal int[] Weights { get; }
        internal int[] InitialStacks { get; }
        internal int[] CenterPlayers { get; }
        internal int FirstOrbParity { get; }

        internal P23光之失控Plan(Random random)
        {
            Baits = Clock.OrderBy(_ => random.Next()).Take(2).OrderBy(slot => PuddlePriority[slot]).ToArray();
            Chain = Clock.Except(Baits).ToArray();
            TowerPlayers = TowerOrder.Select(index => Chain[index]).ToArray();
            var weight = random.Next(6);
            Weights = new[] { Chain[weight], Chain[(weight + 1) % 6] }.OrderBy(Side).ToArray();
            FirstOrbParity = random.Next(2);

            InitialStacks = new int[8];
            foreach (var slot in Baits)
                InitialStacks[slot] = random.Next(1, 3);
            var remaining = 6 - InitialStacks.Sum();
            foreach (var slot in Chain.OrderBy(_ => random.Next()).Take(remaining))
                InitialStacks[slot] = 1;
            CenterPlayers = Clock.Where(slot => InitialStacks[slot] + (Chain.Contains(slot) ? 1 : 0) == 1).ToArray();
        }

        internal int Side(int slot)
        {
            var tower = Array.IndexOf(TowerPlayers, slot);
            return tower >= 0 ? tower / 3 : Array.IndexOf(Baits, slot) ^ 1;
        }

        internal static Vector2 TowerOffset(int index, float radius = 16)
        {
            var angle = Math.PI + index * Math.PI / 3;
            return new Vector2((float)Math.Sin(angle), (float)Math.Cos(angle)) * radius;
        }

        internal Vector2 HammerOffset(int side, int wave) => Trail[wave] * (side == 0 ? 1 : -1);
        internal Vector2 OrbSafeOffset(int side, bool afterFirst)
        {
            var south = (FirstOrbParity == 1) ^ afterFirst;
            return new Vector2(side == 0 ? -18 : 18, south ? 3.8f : -3.8f);
        }

        internal Vector2 EdgeWaypoint(int tower)
        {
            // 北、南两塔先沿边移动，避开场地内侧仍在放置的光锤。
            if (tower == 0)
                return new Vector2(-11.5f, -15);
            if (tower == 3)
                return new Vector2(11.5f, 15);
            return TowerOffset(tower, 18.6f);
        }
    }
}
