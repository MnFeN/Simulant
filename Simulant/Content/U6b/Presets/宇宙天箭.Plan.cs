using Simulant.Simulation.Options;
using Simulant.Simulation.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U6b.Presets
{
    internal enum ArrowMode
    {
        [RandomEnum(OutsideFirst, InsideFirst)] Random,
        OutsideFirst,
        InsideFirst,
    }

    internal enum ArrowOccurrence
    {
        First = 1,
        Second,
    }

    /// <summary>天箭的安全区切换、伤害时刻和两次机制的职能路线；坐标为 ACT 水平 XY。</summary>
    internal static class CosmoArrowPlan
    {
        internal static readonly Vector2 Center = new Vector2(100, 100);
        internal static readonly string[] Roles = { "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };
        // 从左上起，顺时针旋转；TH 最终到正点，DPS 最终留在斜点。
        private static readonly int[] Quadrants = { 0, 1, 3, 2, 3, 2, 0, 1 };
        internal const double FirstHit = 8.08;
        internal const double Interval = 2.005;
        internal static readonly SimPosition SpawnArea = SimPosition.Ellipse(Center, 8);
        internal static double Hit(int wave) => FirstHit + Interval * wave;
        internal static double FollowupCast(ArrowOccurrence occurrence) => Hit(3) + (occurrence == ArrowOccurrence.First ? 1.1 : 0);
        internal static double FollowupHit(ArrowOccurrence occurrence) => FollowupCast(occurrence) + (occurrence == ArrowOccurrence.First ? 8.1 : 11.3);
        internal static double BeamHit(int wave) => FollowupCast(ArrowOccurrence.Second) + 3 + 2 * wave;

        internal readonly struct Shot
        {
            internal readonly Vector2 Position;
            internal readonly float Heading;
            internal readonly uint Action;
            internal Shot(Vector2 position, float heading, uint action)
            { Position = position; Heading = heading; Action = action; }
        }

        internal readonly struct Move
        {
            internal readonly double Start, Arrival;
            internal readonly SimPosition Area;
            internal Vector2 Position => Area.Midpoint();
            internal Move(double start, double arrival, SimPosition area)
            { Start = start; Arrival = arrival; Area = area; }
        }

        internal static Shot[] InitialLines(bool inside)
        {
            var offsets = inside ? new[] { 0f } : new[] { -15f, 15f };
            return offsets.SelectMany(offset => new[]
            {
                new Shot(new Vector2(80, 100 + offset), (float)Math.PI / 2, 0x7BA3),
                new Shot(new Vector2(100 + offset, 80), 0, 0x7BA3),
            }).ToArray();
        }

        internal static Shot[] Wave(ArrowMode mode, int wave)
        {
            var shots = new List<Shot>();
            for (var set = 0; set < 2; set++)
            {
                var inside = (mode == ArrowMode.InsideFirst) == (set == 0);
                var step = wave - set;
                if (step < 0 || step > (inside ? 3 : 6)) continue;
                if (step == 0) { shots.AddRange(InitialLines(inside)); continue; }
                var offset = inside ? 7.5f + 5 * (step - 1) : 7.5f - 5 * (step - 1);
                foreach (var sign in new[] { -1, 1 })
                {
                    shots.Add(new Shot(new Vector2(80, 100 + sign * offset), (float)Math.PI / 2, 0x7BA4));
                    shots.Add(new Shot(new Vector2(100 + sign * offset, 80), 0, 0x7BA4));
                }
            }
            return shots.ToArray();
        }

        internal static Vector2 InitialPosition(ArrowOccurrence occurrence, int slot)
            => Rotate(new Vector2(-7.5f, -7.5f), Quadrant(occurrence, slot));

        /// <summary>以格心的方向和半径定义扇环：径向 ±1.5 米，角度 ±1°，避免波动炮分散时横向靠近队友。</summary>
        internal static SimPosition SafeCell(Vector2 position)
        {
            var offset = position - Center;
            return SimPosition.AnnularSector(Center, offset.Length(), Math.Atan2(offset.X, offset.Y), 1.5f, Math.PI / 180);
        }

        internal static Move[] Route(ArrowMode mode, ArrowOccurrence occurrence, int slot)
        {
            var steps = new List<Move> { new Move(0, Hit(0) - 0.05, SafeCell(InitialPosition(occurrence, slot))) };
            var support = slot < 4;
            void Add(int wave, float x, float y, int? arriveWave = null)
            {
                // 判定前进入安全格即可，允许稍后走完格内剩余路程，给原生移动更新留出余量。
                steps.Add(new Move(Hit(wave - 1) + 0.02, Hit(arriveWave ?? wave) + 0.3,
                    SafeCell(Rotate(new Vector2(x, y), Quadrant(occurrence, slot)))));
            }
            if (mode == ArrowMode.InsideFirst)
            {
                Add(1, -2.5f, -2.5f);
                // DPS 连续穿过两格；第三轮先进入外侧安全区，第四轮前再到最外侧格心附近。
                Add(3, support ? -7.5f : -12.5f, support ? -7.5f : -12.5f, support ? 3 : 4);
                if (support)
                {
                    Add(4, -7.5f, -12.5f);
                    Add(5, -2.5f, -12.5f);
                }
                Add(6, support ? 0 : -7.5f, -7.5f);
            }
            else
            {
                Add(1, -12.5f, -12.5f);
                Add(3, -7.5f, -7.5f);
                Add(4, support ? -2.5f : -12.5f, -12.5f);
                Add(5, support ? 0 : -7.5f, -7.5f);
            }
            var gather = occurrence == ArrowOccurrence.First ? Hit(6) + 0.05 : Math.Max(BeamHit(1), Hit(6)) + 0.05;
            steps.Add(new Move(gather, FollowupHit(occurrence) - 0.15, FinalArea(occurrence, slot)));
            return steps.ToArray();
        }

        internal static SimPosition FinalArea(ArrowOccurrence occurrence, int slot)
        {
            if (slot < 2)
                return SimPosition.Ellipse(Center + (occurrence == ArrowOccurrence.First
                    ? (slot == 0 ? new Vector2(0, -7.5f) : new Vector2(7.5f, 0))
                    : new Vector2(-4 - slot, 4 + slot)), 1);
            // 人群在左下分散站进同一个扇环；第二次收窄角度，保证任意队员被点名时全员仍在线内。
            return SimPosition.AnnularSector(Center, 12, -Math.PI / 4, 1,
                (occurrence == ArrowOccurrence.First ? 8 : 5) * Math.PI / 180);
        }

        /// <summary>用于归责的参考站位；只判断这些站位对应的技能范围，不按距导航点的距离画叉。</summary>
        internal static Vector2[] PositionsAt(ArrowMode mode, ArrowOccurrence occurrence, double time)
            => PositionsAt(Enumerable.Range(0, 8).Select(slot => Route(mode, occurrence, slot)).ToArray(), time);

        internal static Vector2[] PositionsAt(Move[][] routes, double time, double delay = 0.1)
        {
            return Enumerable.Range(0, 8).Select(slot =>
            {
                var moves = routes[slot];
                var position = moves[0].Position;
                for (var i = 1; i < moves.Length; i++)
                {
                    var move = moves[i];
                    if (time < move.Start) break;
                    var delta = move.Position - position;
                    var length = delta.Length();
                    var end = i + 1 < moves.Length ? Math.Min(time, moves[i + 1].Start) : time;
                    var distance = Math.Max(0, end - move.Start - delay) * 6;
                    position = length <= distance ? move.Position : position + delta * (float)(distance / length);
                }
                return position;
            }).ToArray();
        }

        internal static int[] DiveTargets(Vector2[] positions)
        {
            var order = Enumerable.Range(0, 8).OrderBy(slot => Vector2.DistanceSquared(positions[slot], Center)).ThenBy(slot => slot).ToArray();
            return new[] { order[0], order[1], order[7] };
        }

        internal static int[] Hits(Vector2[] positions, ActionArea area, Vector2 center, float heading = 0)
            => Enumerable.Range(0, 8).Where(slot => area.Contains(positions[slot], center, heading)).ToArray();

        internal static float TargetHeading(Vector2[] positions, int target)
            => (float)Math.Atan2(positions[target].X - Center.X, positions[target].Y - Center.Y);

        internal static int[] ChargeFront(Vector2[] positions, float heading)
        {
            var direction = new Vector2((float)Math.Sin(heading), (float)Math.Cos(heading));
            return Enumerable.Range(0, 8).OrderBy(slot => Vector2.Dot(positions[slot] - Center, direction)).ThenBy(slot => slot).Take(2).ToArray();
        }

        internal static bool ValidDive(Vector2[] positions, ActionArea tankArea, ActionArea stackArea)
        {
            var targets = DiveTargets(positions);
            return targets[0] < 2 && targets[1] < 2 && targets[2] >= 2
                && targets.Take(2).All(target => Hits(positions, tankArea, positions[target]).SequenceEqual(new[] { target }))
                && Hits(positions, stackArea, positions[targets[2]]).SequenceEqual(Enumerable.Range(2, 6));
        }

        internal static bool ValidBeams(Vector2[] positions, int[] targets, ActionArea area)
            => targets.All(target => Hits(positions, area, Center, TargetHeading(positions, target)).SequenceEqual(new[] { target }));

        internal static bool ValidCharge(Vector2[] positions, int target, ActionArea area)
        {
            var heading = TargetHeading(positions, target);
            return Hits(positions, area, Center, heading).Length == 8 && ChargeFront(positions, heading).All(slot => slot < 2);
        }

        /// <summary>逐人代入实际位置，只有其位置单独破坏技能安全条件时才画叉；多人共同偏移不强行归责。</summary>
        internal static int[] FindOffenders(Vector2[] actual, Vector2[] reference, Func<Vector2[], bool> valid)
        {
            if (!valid(reference)) return Array.Empty<int>();
            var offenders = new List<int>();
            var trial = (Vector2[])reference.Clone();
            for (var slot = 0; slot < 8; slot++)
            {
                trial[slot] = actual[slot];
                if (!valid(trial)) offenders.Add(slot);
                trial[slot] = reference[slot];
            }
            return offenders.ToArray();
        }

        private static int Quadrant(ArrowOccurrence occurrence, int slot)
            => occurrence == ArrowOccurrence.First && slot >= 2 ? 3 : Quadrants[slot];

        private static Vector2 Rotate(Vector2 offset, int turns)
        {
            for (var i = 0; i < turns; i++) offset = new Vector2(-offset.Y, offset.X);
            return Center + offset;
        }
    }
}
