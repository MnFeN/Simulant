using Simulant.Simulation.Geometry;
using Simulant.Simulation.Options;
using System;
using System.Numerics;
using static Simulant.Utils.Maths.Angles;

namespace Simulant.Content.U7a.Presets
{
    internal enum ParadiseTower
    {
        [RandomEnum(South, Northeast, Northwest)]
        Random = -1,
        South = 0,
        Northeast = 1,
        Northwest = 2
    }

    internal enum ParadiseWings
    {
        [RandomEnum(DarkFirst, LightFirst)]
        Random,
        DarkFirst,
        LightFirst
    }

    internal enum ParadiseTowerRule
    {
        FirstWing,
        LeftRight,
        SpawnOrder
    }

    internal enum ParadiseTowerGroups
    {
        H12D12D34,
        H12D34D12,
        D12H12D34,
        D12D34H12,
        D34H12D12,
        D34D12H12
    }

    internal sealed class P52复乐园Plan
    {
        private readonly Vector2 _center;
        private readonly ParadiseTower _first;
        private readonly int _step;
        private readonly bool _darkFirst;
        private readonly int[] _towerPairStarts;
        private int SafeSide => _darkFirst ? -1 : 1;
        private int SafeTowerWave => _step == SafeSide ? 1 : 2;
        private double South => TowerHeading(_first);

        internal P52复乐园Plan(Vector2 center, ParadiseTower first, ExerciseTurn order, ParadiseWings wings,
            ParadiseTowerRule rule, ParadiseTowerGroups groups)
        {
            _center = center;
            _first = first;
            _step = order == ExerciseTurn.Clockwise ? -1 : 1;
            _darkFirst = wings == ParadiseWings.DarkFirst;
            _towerPairStarts = PairStarts(groups);
            var swapLaterPairs = rule == ParadiseTowerRule.FirstWing && SafeTowerWave == 1 ||
                rule == ParadiseTowerRule.LeftRight && _step == 1;
            if (swapLaterPairs)
                (_towerPairStarts[1], _towerPairStarts[2]) = (_towerPairStarts[2], _towerPairStarts[1]);
        }

        internal bool Dark(int wave) => wave == 0 ? _darkFirst : !_darkFirst;

        internal ParadiseTower Tower(int wave) => OffsetTower(wave * _step);

        internal uint TowerMapSlot(int wave)
        {
            switch (Tower(wave))
            {
                case ParadiseTower.Northwest:
                    return 0x33;
                case ParadiseTower.Northeast:
                    return 0x34;
                default:
                    return 0x35;
            }
        }

        internal Vector2 TowerCenter(int wave) => _center + Offset(TowerHeading(Tower(wave)), 7);

        internal int[] TowerPair(int wave)
        {
            var first = _towerPairStarts[wave];
            return new[] { first, first + 1 };
        }

        internal SimPosition FirstPosition(int slot)
        {
            switch (RouteSlot(slot))
            {
                case 0:
                    return At(180, 8, 0.55f);
                case 1:
                    return At(SafeSide * 60, _darkFirst ? 1.6f : 16, 0.55f);
                case 2:
                case 3:
                    return At(SafeSide * 14, 7, 0.65f);
                case 4:
                case 5:
                    return At(SafeSide * 60, 8, 0.65f);
                default:
                    return At(SafeSide * 124, 7, 0.65f);
            }
        }

        internal SimPosition SecondPosition(int slot)
        {
            switch (RouteSlot(slot))
            {
                case 0:
                    return At(180, _darkFirst ? 16 : 1.6f, 0.55f);
                case 1:
                    return At(SafeSide * 60, 7.5f, 0.55f);
                case 2:
                    return At(174, 8.5f, 0.65f);
                case 3:
                    return At(186, 8.5f, 0.65f);
                case 4:
                case 5:
                    return At(-SafeSide * 136, 7, 0.65f);
                default:
                    return At(SafeSide * 136, 7, 0.65f);
            }
        }

        // 技能包的朝向指向一仇；225° 扇形在亮翼的一侧，越过正面 45°。
        internal static float CleaveHeading(float facing, bool dark)
            => facing + (float)Deg(dark ? -67.5 : 67.5);

        private static int[] PairStarts(ParadiseTowerGroups groups)
        {
            const int healers = (int)ExerciseRole.H1 - 1;
            const int melee = (int)ExerciseRole.D1 - 1;
            const int ranged = (int)ExerciseRole.D3 - 1;
            switch (groups)
            {
                case ParadiseTowerGroups.H12D34D12:
                    return new[] { healers, ranged, melee };
                case ParadiseTowerGroups.D12H12D34:
                    return new[] { melee, healers, ranged };
                case ParadiseTowerGroups.D12D34H12:
                    return new[] { melee, ranged, healers };
                case ParadiseTowerGroups.D34H12D12:
                    return new[] { ranged, healers, melee };
                case ParadiseTowerGroups.D34D12H12:
                    return new[] { ranged, melee, healers };
                default:
                    return new[] { healers, melee, ranged };
            }
        }

        private int RouteSlot(int slot)
        {
            if (slot < 2)
                return slot;
            var member = slot % 2;
            var pair = slot - member;
            if (pair == _towerPairStarts[0])
                return 2 + member;
            return (pair == _towerPairStarts[SafeTowerWave] ? 6 : 4) + member;
        }

        private ParadiseTower OffsetTower(int step) => (ParadiseTower)(((int)_first + step + 3) % 3);

        private static double TowerHeading(ParadiseTower tower) => (int)tower * Deg(120);

        private SimPosition At(double degrees, float radius, float tolerance)
            => SimPosition.Ellipse(_center + Offset(South + Deg(degrees), radius), tolerance);

        private static Vector2 Offset(double heading, float radius)
            => new Vector2((float)Math.Sin(heading), (float)Math.Cos(heading)) * radius;
    }
}
