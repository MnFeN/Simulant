using Simulant.Simulation.Geometry;
using Simulant.Simulation;
using Simulant.Simulation.Options;
using System;
using System.Linq;
using System.Numerics;
using static Simulant.Utils.Maths.Angles;

namespace Simulant.Content.U7a.Presets
{
    internal enum RelativitySolution
    {
        CN, CNHTDH, NA
    }

    internal enum RelativityDebuff
    {
        // 全随机随八人合法点名一起生成，各职能的短火、长火概率不同。
        Random = -1,
        Ice, ShortFire, MediumFire, LongFire
    }

    internal sealed class P31时间压缩绝Plan
    {
        internal static readonly Vector2 Center = new Vector2(100, 100);
        internal readonly int[] Codes;
        internal readonly int[] Spins;
        internal readonly double North;
        internal readonly int[] StackTargets;
        private readonly RelativitySolution _solution;

        internal P31时间压缩绝Plan(int self, RelativityDebuff debuff, RelativitySolution solution, ExerciseDirection north, Random random)
        {
            _solution = solution;
            var initial = (int)debuff;
            North = Math.PI + Deg((int)north.ResolveRandom(random) * 45);
            var iceOnSupport = initial == 0 ? self < 4 : initial == 1 && self < 4 ? false :
                initial == 3 && self >= 4 ? true : random.Next(2) == 0;
            Codes = iceOnSupport ? new[] { 1, 2, 3, 3, 5, 5, 7, 8 } : new[] { 0, 2, 3, 3, 5, 5, 7, 9 };
            for (var start = 0; start < 8; start += 4)
            {
                var constrainSelf = initial >= 0 && self >= start && self < start + 4;
                var group = AssignmentRandom.Draw(Codes.Skip(start).Take(4).ToArray(), random,
                    constrainSelf ? self - start : -1, constrainSelf ? (Predicate<int>)(code => Kind(code) == initial) : null);
                Array.Copy(group, 0, Codes, start, 4);
            }
            var priority = solution == RelativitySolution.CNHTDH ? new[] { 2, 0, 1, 4, 5, 6, 7, 3 } :
                solution == RelativitySolution.NA ? new[] { 1, 0, 2, 3, 5, 4, 6, 7 } : Enumerable.Range(0, 8).ToArray();
            Codes[priority.Last(i => Codes[i] == 3)] = 4;
            Codes[priority.Last(i => Codes[i] == 5)] = 6;
            Spins = Enumerable.Range(0, 8).Select(_ => random.Next(2) == 0 ? -1 : 1).ToArray();
            StackTargets = new int[3];
            for (var round = 0; round < 3; round++)
                StackTargets[round] = Enumerable.Range(0, 8).Where(i => !HasFire(i, round) && !StackTargets.Take(round).Contains(i))
                    .OrderBy(_ => random.Next()).First();
        }

        internal static int Kind(int code) => code == 1 || code == 9 ? 0 : code == 0 || code == 5 || code == 6 ? 1 : code == 2 || code == 7 ? 2 : 3;
        internal bool HasFire(int slot, int round) => Kind(Codes[slot]) == round + 1;
        internal bool EarlyReturn(int slot) => Codes[slot] != 3 && Codes[slot] != 4 && Codes[slot] != 8 && Codes[slot] != 9;
        internal bool Water(int slot) => Codes[slot] == 7;
        internal bool Gaze(int slot) => !EarlyReturn(slot);
        internal int DirectionIndex(int slot)
        {
            var directions = _solution == RelativitySolution.NA ? new[] { 4, 4, 6, 3, 5, 1, 7, 2, 0, 0 } : new[] { 4, 4, 2, 3, 5, 1, 7, 6, 0, 0 };
            return directions[Codes[slot]];
        }

        internal double Angle(int slot) => North + Deg(DirectionIndex(slot) * 45);
        internal static Vector2 Direction(double angle) => new Vector2((float)Math.Sin(angle), (float)Math.Cos(angle));
        internal Vector2 Hourglass(int direction) => Center + 9.5f * Direction(North + Deg(direction * 45));
        internal SimPosition LastLaserRetreat(int direction)
            => SimPosition.Ellipse(Center + 7 * Direction(North + Deg(direction * 45)), 0.5f);
        internal static int HourglassWave(int direction) => direction == 0 || direction == 3 || direction == 5 ? 0 :
            direction == 1 || direction == 4 || direction == 7 ? 1 : 2;
        internal uint HourglassMapSlot(int direction)
        {
            var absolute = (int)Math.Round(ToDegrees(North - Math.PI) / 45) + direction;
            return (uint)(0x1A + (8 - absolute % 8) % 8);
        }

        internal int LaserWave(int slot) => new[] { 1, 1, 2, 0, 0, 1, 1, 2, 0, 0 }[Codes[slot]];

        internal Vector2 LaserPoint(int slot)
        {
            var direction = DirectionIndex(slot);
            return Hourglass(direction) + Direction(Angle(slot) - Deg(Spins[direction] * 60));
        }

        internal SimPosition FirePosition(int slot, int round) => SimPosition.Ellipse(
            Center + (HasFire(slot, round) ? 13.5f : 0.7f) * Direction(Angle(slot)), HasFire(slot, round) ? 0.6f : 0.35f);

        internal SimPosition Between(int slot, int wave)
        {
            if (LaserWave(slot) == wave)
                return SimPosition.Ellipse(LaserPoint(slot), 0);
            var radius = wave == 0 ? (EarlyReturn(slot) && !Water(slot) ? 8.5f : 1f) : wave == 1 ? 1f : -1f;
            return SimPosition.Ellipse(Center + radius * Direction(Angle(slot)), radius > 2 ? 0.2f : 0.25f);
        }
    }
}
