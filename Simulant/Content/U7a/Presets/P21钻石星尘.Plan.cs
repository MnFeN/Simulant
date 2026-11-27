using Simulant.Simulation.Geometry;
using System;
using System.Linq;
using System.Numerics;
using static Simulant.Utils.Maths.Angles;

namespace Simulant.Content.U7a.Presets
{
    internal sealed class P21钻石星尘Plan
    {
        private const float MaxTolerance = 2;
        private static readonly int[] Cardinal = { 0, 6, 2, 4, 2, 4, 0, 6 };
        private static readonly (int Start, int Step)[] TrailAngles =
        {
            (160, -32), (135, -25), (90, -20), (45, 25)
        };

        internal bool Axe { get; }
        internal bool FrontFirst { get; }
        internal bool SecondSafeBehind => !FrontFirst;
        internal bool ReflectionOnKnockbackAxis => TrailCase == 0;
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

        internal SimPosition OpeningArea(Vector2 center, int slot)
            => SimPosition.Ellipse(center + At((int)U7aPartyPositions.EightWayDirection(slot), 4), MaxTolerance);

        internal SimPosition KickArea(Vector2 center, int slot)
        {
            var marked = Marked.Contains(slot);
            var radius = Axe ? (marked ? 19.2f : 17f) : marked ? 3.4f : 1.7f;
            var tolerance = Axe ? (marked ? 0.65f : 0.7f) : marked ? 0.5f : 0.65f;
            // 钢铁的站位离场心较远，按扇环角点不超过两米预先限幅。
            var angle = Axe ? (marked ? 5.55 : 6.19) : marked ? 7 : 9;
            return BaitArea(center, slot, marked, radius, tolerance, angle);
        }

        internal SimPosition IceMarkerArea(Vector2 center, int slot)
        {
            if (!Marked.Contains(slot))
                return BaitArea(center, slot, false, Axe ? 4.75f : 1.7f, 0.65f, 9);

            // 远端冰花的双十字需要给两处击退落点留出横向通道。
            return BaitArea(center, slot, true, Axe ? 19.2f : 8.05f, 0.55f, Axe ? 1.55 : 3.1);
        }

        private double KnockbackHeading(int group) => Math.PI + Deg(45 * (First + group * 4));

        internal SimPosition KnockbackArea(Vector2 center, int group)
        {
            const float inner = 4;
            // 击退十二米后接近二十米电网，外沿留五厘米用于坐标量化。
            const float outer = 7.95f;
            return SimPosition.AnnularSector(center, (inner + outer) / 2, KnockbackHeading(group), (outer - inner) / 2, Deg(10));
        }

        internal SimPosition FlowerDodgeArea(Vector2 center, int group)
            => SimPosition.AnnularSector(center, 17.35f, KnockbackHeading(group), 0.38f, Deg(2.8));

        private int TrailCase => (Reflection - First + 8) % 4;

        private int TrailIndex(int group)
        {
            var relative = (Reflection - First - group * 4 + 8) % 8;
            return relative >= 4 ? 0 : 1;
        }

        private int StackAngle(int route, int wave)
        {
            var angles = TrailAngles[TrailCase];
            return angles.Start + angles.Step * wave - 180 * route;
        }

        internal SimPosition StackArea(Vector2 center, int group, int wave)
        {
            var route = TrailIndex(group);
            var radius = TrailCase == 0 ? 16f : TrailCase == 3 || TrailCase == 2 && route == 1 ? 18.75f : 18f;
            var heading = Math.PI + Deg(Reflection * 45 + StackAngle(route, wave));
            return SimPosition.AnnularSector(center, radius, heading, radius == 18.75f ? 1.15f : 1.2f, Deg(3.3));
        }

        internal bool StartsBehind(int group) => Math.Cos(Deg(StackAngle(TrailIndex(group), 3))) > 0;

        internal bool IsBehind(Vector2 center, Vector2 position) => Vector2.Dot(position - center, At(Reflection, 1)) > 0;

        private Vector2 IceOffset(float slot, bool behind)
        {
            var axis = At(Reflection, 1);
            var side = behind ? 1 : -1;
            // 同轴时两侧落点横向让开冰圈，给正式滑冰留出穿场通道。
            var aligned = ReflectionOnKnockbackAxis;
            var shift = aligned ? 4.1f * side : 0;
            var distance = aligned ? 15.95f : 16.15f;
            return axis * (distance * side) + new Vector2(axis.Y, -axis.X) * ((slot - 3.5f) * 0.14f + shift);
        }

        internal SimPosition IceArea(Vector2 center, int slot, bool behind)
            => SimPosition.Ellipse(center + IceOffset(slot, behind), 1.04f, 0.8f, Math.PI + Deg(Reflection * 45));

        internal SimPosition IceStartArea(Vector2 center, int group)
        {
            var behind = StartsBehind(group);
            var offset = IceOffset(3.5f, behind);
            var outward = At(Reflection, behind ? 1 : -1);
            var lateral = new Vector2(outward.Y, -outward.X);
            // 同轴与垂轴时，移动起滑区中心给残留留出通道。镜像背后的同轴区域还要留出第一刀的角度。
            if (ReflectionOnKnockbackAxis)
            {
                offset += lateral * 0.5f;
                if (behind)
                    return SimPosition.Ellipse(center + offset + outward * 1.4f, 2, 1.7f, Math.PI + Deg(Reflection * 45));
            }
            else if (TrailCase == 2)
                offset -= lateral * (behind ? 0.75f : 0.45f);
            return SimPosition.Ellipse(center + offset, 2);
        }

        internal SimPosition RayGatherArea(Vector2 center, float hitboxRadius)
        {
            var side = IceOffset(3.5f, SecondSafeBehind);
            var radius = Math.Max(1, hitboxRadius + 0.5f);
            var radialTolerance = Math.Min(1.6f, radius - 0.5f);
            // 目标圈半径取自首领，仍需按实际半径限制最外侧角点。
            var angleLimit = Math.Acos(1 - (MaxTolerance * MaxTolerance - radialTolerance * radialTolerance) /
                (2.0 * radius * (radius + radialTolerance)));
            return SimPosition.AnnularSector(center, radius, Math.Atan2(side.X, side.Y), radialTolerance, Math.Min(Deg(3), angleLimit));
        }

        private SimPosition BaitArea(Vector2 center, int slot, bool marked, float radius, float radialTolerance, double angleTolerance)
        {
            var parity = (First % 2) ^ (marked ? 1 : 0);
            return SimPosition.AnnularSector(center, radius, Math.PI + Deg(45 * (Cardinal[slot] + parity)), radialTolerance, Deg(angleTolerance));
        }
    }
}
