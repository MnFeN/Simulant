using System;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U7a.Presets
{
    internal sealed class P21钻石星尘Plan
    {
        private static readonly int[] Cardinal = { 0, 6, 2, 4, 2, 4, 0, 6 };
        private static readonly int[][] TrailAngles =
        {
            new[] { 160, 128, 96, 64, -20, -52, -84, -116 },
            new[] { 135, 110, 85, 60, -45, -70, -95, -120 },
            new[] { 90, 70, 50, 30, -90, -110, -130, -150 },
            new[] { 45, 70, 95, 120, -135, -110, -85, -60 }
        };

        internal bool Axe { get; }
        internal bool FrontFirst { get; }
        internal int First { get; }
        internal int Reflection { get; }
        internal int Gaze { get; }
        internal int[] Marked { get; }
        internal int[] Unmarked { get; }
        internal int[] IcicleDirections { get; }

        internal P21钻石星尘Plan(bool axe, bool frontFirst, bool markersOnDamageDealers, int first, int reflection, int gaze)
        {
            Axe = axe;
            FrontFirst = frontFirst;
            First = first % 4;
            Reflection = reflection;
            Gaze = gaze;
            Marked = Enumerable.Range(markersOnDamageDealers ? 4 : 0, 4).ToArray();
            Unmarked = Enumerable.Range(markersOnDamageDealers ? 0 : 4, 4).ToArray();
            IcicleDirections = new[] { First, First + 4 }.Concat(Enumerable.Range(0, 8).Where(dir => dir % 2 != First % 2))
                .Concat(new[] { (First + 2) % 8, (First + 6) % 8 }).ToArray();
        }

        internal static Vector2 At(int direction, float radius) => AtDegrees(direction * 45, radius);

        private static Vector2 AtDegrees(int angle, float radius)
        {
            var heading = Math.PI + angle * Math.PI / 180;
            return new Vector2((float)Math.Sin(heading), (float)Math.Cos(heading)) * radius;
        }

        internal Vector2 InitialBait(int slot, bool drop)
        {
            var marked = Marked.Contains(slot);
            var wantedParity = (First % 2) ^ (marked ? 1 : 0);
            var radius = Axe ? (marked ? 19.2f : 17f) : marked ? (drop ? 8.25f : 3.4f) : 1.7f;
            return At(Cardinal[slot] + wantedParity, radius);
        }

        internal Vector2 KnockbackStart(int group) => At(First + group * 4, 5.35f);

        private int TrailCase => (Reflection - First + 8) % 4;

        private int TrailIndex(int group)
        {
            var relative = (Reflection - First - group * 4 + 8) % 8;
            return relative >= 4 ? 0 : 1;
        }

        internal Vector2 StackOffset(int group, int wave)
        {
            var route = TrailIndex(group);
            var radius = TrailCase == 0 ? 16f : TrailCase == 3 || TrailCase == 2 && route == 1 ? 18.75f : 18f;
            return AtDegrees(Reflection * 45 + TrailAngles[TrailCase][route * 4 + wave], radius);
        }

        internal bool StartsBehind(int group) => Vector2.Dot(StackOffset(group, 3), At(Reflection, 1)) > 0;

        internal Vector2 IceOffset(int slot, bool behind)
        {
            var axis = At(Reflection, 1);
            return axis * (behind ? 16.15f : -16.15f) + new Vector2(-axis.Y, axis.X) * ((slot - 3.5f) * 0.22f);
        }
    }
}
