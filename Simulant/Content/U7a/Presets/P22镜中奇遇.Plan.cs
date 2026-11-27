using Simulant.Simulation.Geometry;
using Simulant.Simulation.Options;
using Simulant.Simulation.Runtime;
using System;
using System.Numerics;
using static Simulant.Utils.Maths.Angles;

namespace Simulant.Content.U7a.Presets
{
    internal enum RedMirrorAssignment
    {
        RangedLeft,
        RangedRight,
        Clockwise,
        Counterclockwise
    }

    internal sealed class P22镜中奇遇Plan
    {
        private const float NearSpreadTolerance = 2;
        private const float FarSpreadTolerance = 1;
        private const float FarSpreadRadius = 18.9f;
        private static readonly Vector2 Center = new Vector2(100, 100);
        private static readonly Vector2 FarHealerAwayOffset = new Vector2(-7.5f, 6);
        private static readonly Vector2 FarDpsSpreadOffset = Direction(Deg(11.5)) * FarSpreadRadius;
        private static readonly Vector2[] FarSpreadOffsets =
        {
            FarHealerAwayOffset,
            new Vector2(0, 9),
            FarDpsSpreadOffset,
            new Vector2(-FarDpsSpreadOffset.X, FarDpsSpreadOffset.Y)
        };
        // 镜边两侧的落点角度控制在距轴线 80°～84°，给场边留出余量。
        private static readonly (int Angle, int Tolerance)[] WhiteOffsets =
        {
            (25, 5), (-25, 5), (-82, 2), (82, 2), (75, 5), (-75, 5), (-40, 5), (40, 5)
        };
        private static readonly (int Angle, int Tolerance)[] RedOffsetsNearClockwise =
        {
            (-82, 2), (82, 2), (-82, 2), (82, 2), (-5, 5), (40, 5), (-40, 5), (5, 5)
        };
        private static readonly (int Angle, int Tolerance)[] RedOffsetsNearCounterclockwise =
        {
            (-82, 2), (82, 2), (-82, 2), (82, 2), (-40, 5), (5, 5), (-5, 5), (40, 5)
        };
        private readonly (int Angle, int Tolerance)[] _redOffsets;

        internal int White { get; }
        internal int NearRed { get; }
        internal int FarRed { get; }
        internal bool Spread { get; }
        internal float OpeningHeading { get; }
        internal SimPosition OpeningParty(int slot) => slot == 0
            ? SimPosition.Ellipse(Center, 1)
            : SimPosition.Ellipse(Center + Direction(OpeningHeading) * 5, 3);
        internal SimPosition FirstPull => SimPosition.Ellipse(Center - Direction(MirrorHeading(White)) * 16.3f, 0.12f);

        internal P22镜中奇遇Plan(Random random, ExerciseDirection white, RedMirrorAssignment assignment)
            : this((int)white.ResolveRandom(random), RedMidpointOffset(random.Next(6)), assignment,
                1 + 2 * random.Next(4), random.Next(2) == 0)
        {
        }

        internal P22镜中奇遇Plan(int white, int redMidpointOffset, RedMirrorAssignment assignment, int opening, bool spread)
        {
            White = white;
            Spread = spread;
            OpeningHeading = (float)MirrorHeading(opening);
            var midpoint = (White + redMidpointOffset) % 8;
            var left = (midpoint + 1) % 8;
            var right = (midpoint + 7) % 8;
            var leftDistance = ArcDistance(White, left);
            var rightDistance = ArcDistance(White, right);

            // 正反两种对称镜型对应相反的原地顺逆方向。
            var rangedLeft = assignment == RedMirrorAssignment.RangedLeft ||
                (assignment == RedMirrorAssignment.Clockwise && midpoint != White) ||
                (assignment == RedMirrorAssignment.Counterclockwise && midpoint == White);
            var farGoesLeft = leftDistance == rightDistance ? rangedLeft : leftDistance < rightDistance;
            FarRed = farGoesLeft ? left : right;
            NearRed = farGoesLeft ? right : left;
            _redOffsets = (NearRed - FarRed + 8) % 8 == 6 ? RedOffsetsNearClockwise : RedOffsetsNearCounterclockwise;
        }

        internal static Vector2 MirrorPosition(int direction) => Center + Direction(MirrorHeading(direction)) * 20;

        internal SimPosition BossBait(int slot, Vector2 boss)
        {
            var outward = boss - MirrorPosition(White);
            return Bait(boss, Math.Atan2(outward.X, outward.Y), WhiteOffsets[slot].Angle, 2);
        }

        internal SimPosition WhiteBait(int slot)
        {
            var (angle, tolerance) = WhiteOffsets[slot];
            return Bait(MirrorPosition(White), MirrorHeading(White) + Math.PI, angle, tolerance);
        }

        internal SimPosition RedBait(int slot)
        {
            var mirror = IsNear(slot) ? NearRed : FarRed;
            var (angle, tolerance) = _redOffsets[slot];
            return Bait(MirrorPosition(mirror), MirrorHeading(mirror) + Math.PI, angle, tolerance);
        }

        internal SimPosition SecondPull
        {
            get
            {
                var heading = MirrorHeading(NearRed);
                return SimPosition.Ellipse(Center + Direction(heading) * 16 + Direction(heading - Math.PI / 2) * 6, 1);
            }
        }

        internal SimPosition SecondPullTurnPoint(Vector2 boss, SimPosition tankBait)
        {
            var right = Direction(MirrorHeading(NearRed) - Math.PI / 2);
            var bossSide = Vector2.Dot(boss - Center, right);
            var tankSide = Vector2.Dot(tankBait.Midpoint() - Center, right);
            return Math.Abs(bossSide) > SimMovementController.NetworkArrivalTolerance && bossSide * tankSide < 0
                ? tankBait
                : SecondPull;
        }

        internal Func<int, SimPosition> BanishPositions(int[] partners, Vector2 boss, float hitboxRadius)
        {
            var near = Spread ? NearBanishSpread(boss, hitboxRadius) : NearBanishStack(boss);
            return slot =>
            {
                if (IsNear(slot))
                    return near(slot);
                var spreadCenter = FarBanishSpreadCenter(slot);
                if (Spread)
                    return SimPosition.Ellipse(spreadCenter, FarSpreadTolerance);

                var midpoint = (spreadCenter + FarBanishSpreadCenter(partners[slot])) * 0.5f;
                return SimPosition.Ellipse(midpoint, 0.2f);
            };
        }

        private Func<int, SimPosition> NearBanishStack(Vector2 boss)
        {
            var offset = boss - Center;
            var distance = offset.Length();
            var heading = distance > 0.01f ? Math.Atan2(offset.X, offset.Y) : MirrorHeading(NearRed);
            var midpoint = Center + Direction(heading) * (Math.Min(distance, 16.5f) - 2.9f);
            var lateral = Direction(heading + Math.PI / 2) * 3;
            return slot => SimPosition.Ellipse(midpoint + ((slot & 1) == 0 ? lateral : -lateral), 0.2f);
        }

        private Vector2 FarBanishSpreadCenter(int slot)
        {
            // H2 沿远红镜方向留在场内，H1 偏离近红镜。D3、D4 站远红镜两侧。
            var mirrorHeading = MirrorHeading(FarRed);
            var spreadOffset = FarSpreadOffsets[slot < 4 ? slot - 2 : slot - 4];
            var sideHeading = slot < 4 ? MirrorHeading(NearRed) : mirrorHeading + Math.PI / 2;
            return Center + Direction(mirrorHeading) * spreadOffset.Y + Direction(sideHeading) * spreadOffset.X;
        }

        private Func<int, SimPosition> NearBanishSpread(Vector2 boss, float hitboxRadius)
        {
            const float safeRadius = 20 - NearSpreadTolerance - 0.05f;
            const float farGap = 5 + NearSpreadTolerance + FarSpreadTolerance + 0.05f;
            var heading = MirrorHeading(NearRed);
            var forward = Direction(heading);
            var left = Direction(heading + Math.PI / 2);
            var towardFar = Direction(MirrorHeading(FarRed));
            var offset = boss - Center;

            // 四个对角点以目标圈为基准，间距不足时扩大方形，给两边各两米容差。
            var reservedSide = farGap - FarHealerAwayOffset.Y;
            var spacingLimit = (safeRadius - reservedSide) / (float)Math.Sqrt(5);
            var halfSpacing = Math.Max(4.55f, Math.Min(spacingLimit, hitboxRadius / (float)Math.Sqrt(2)));

            var depthLimit = (float)Math.Sqrt(safeRadius * safeRadius - halfSpacing * halfSpacing) - halfSpacing;
            var depth = Math.Max(-depthLimit, Math.Min(depthLimit, Vector2.Dot(offset, forward)));
            var (minSide, maxSide) = SideLimits(depth);

            // 原深度没有横向空间时才往场内收，预留能避开远程组的平移距离。
            if (maxSide < minSide)
            {
                var cornerSide = 2 * halfSpacing + reservedSide;
                depthLimit = (float)Math.Sqrt(safeRadius * safeRadius - cornerSide * cornerSide) - halfSpacing;
                depth = Math.Max(-depthLimit, Math.Min(depthLimit, depth));
                (minSide, maxSide) = SideLimits(depth);
            }

            var side = Math.Max(minSide, Math.Min(maxSide, Vector2.Dot(offset, towardFar)));
            return slot =>
            {
                var row = slot < 2 ? halfSpacing : -halfSpacing;
                var column = (slot & 1) == 0 ? halfSpacing : -halfSpacing;
                return SimPosition.Ellipse(Center + forward * (depth + row) + towardFar * side + left * column,
                    NearSpreadTolerance);
            };

            (float Min, float Max) SideLimits(float atDepth)
            {
                var cornerDepth = Math.Abs(atDepth) + halfSpacing;
                var wallLimit = (float)Math.Sqrt(Math.Max(0, safeRadius * safeRadius - cornerDepth * cornerDepth)) - halfSpacing;
                var towardFarLimit = wallLimit;
                foreach (var farOffset in FarSpreadOffsets)
                    towardFarLimit = LimitNearSpreadSide(towardFarLimit, atDepth, halfSpacing, farGap, farOffset);
                return (-wallLimit, towardFarLimit);
            }
        }

        private static float LimitNearSpreadSide(float limit, float depth, float halfSpacing, float minGap, Vector2 farOffset)
        {
            var distance = Math.Min(Math.Abs(depth - halfSpacing - farOffset.X),
                Math.Abs(depth + halfSpacing - farOffset.X));
            if (distance >= minGap)
                return limit;

            var horizontalGap = (float)Math.Sqrt(minGap * minGap - distance * distance);
            return Math.Min(limit, farOffset.Y - horizontalGap - halfSpacing);
        }

        private static SimPosition Bait(Vector2 source, double axis, int angle, int tolerance)
            => SimPosition.AnnularSector(source, 3.3f, axis + Deg(angle), 0.2f, Deg(tolerance));

        private static bool IsNear(int slot) => slot < 2 || (slot >= 4 && slot < 6);
        private static int RedMidpointOffset(int index) => index == 0 ? 0 : index + 1;
        private static int ArcDistance(int a, int b) => Math.Min((a - b + 8) % 8, (b - a + 8) % 8);
        private static double MirrorHeading(int direction) => Math.PI + direction * Math.PI / 4;
        private static Vector2 Direction(double heading) => new Vector2((float)Math.Sin(heading), (float)Math.Cos(heading));
    }
}
