using Simulant.Simulation.Geometry;
using System;
using System.Linq;
using System.Numerics;
using static Simulant.Utils.Maths.Angles;

namespace Simulant.Content.U7a.Presets
{
    internal enum PolarizingOrder
    {
        TMRH,
        THMR,
        TMRT
    }

    internal sealed class P53星灵之剑Plan
    {
        private readonly Vector2 _center;
        private readonly float _heading;
        private readonly PolarizingOrder _order;

        internal P53星灵之剑Plan(Vector2 center, float heading, PolarizingOrder order)
        {
            _center = center;
            _heading = heading;
            _order = order;
        }

        internal int Side(int round, int slot)
        {
            var first = FirstBait(slot);
            return (slot & 1) ^ (round > first ? 1 : 0);
        }

        internal int Front(int round, int side)
            => Enumerable.Range(0, 8).First(slot => Side(round, slot) == side &&
                (FirstBait(slot) == round || (_order == PolarizingOrder.TMRT && round == 3 && slot < 2)));

        internal int Side(Vector2 position)
            => Vector2.Dot(position - _center, Direction(_heading + Deg(90))) > 0 ? 0 : 1;

        internal double LineHeading(int side) => _heading + Deg(side == 0 ? 135 : -135);

        internal SimPosition Stack(int round, int slot)
        {
            var side = Side(round, slot);
            var front = Front(round, side) == slot;
            // 两个径向区间不相交。最外侧队员与挡枪者角度最多相差 14°，仍在宽 6 米的直线内。
            return SimPosition.AnnularSector(_center, front ? 5.65f : 9.9f, LineHeading(side), front ? 0.65f : 1.1f, Deg(7));
        }

        internal SimPosition Dodge(int slot)
            => SimPosition.AnnularSector(_center, slot < 4 ? 8.3f : 10.5f, _heading + Math.PI, 1.1f, Deg(9));

        private int FirstBait(int slot)
        {
            var role = slot / 2;
            if (_order == PolarizingOrder.THMR || role == 0)
                return role;
            if (role == 1)
                return _order == PolarizingOrder.TMRT ? 4 : 3;
            return role - 1;
        }

        private static Vector2 Direction(double heading) => new Vector2((float)Math.Sin(heading), (float)Math.Cos(heading));
    }
}
