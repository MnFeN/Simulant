using Simulant.Simulation.Geometry;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U7a.Presets
{
    internal enum LightRampantStrategy
    {
        GreyNine,
        Mmw
    }

    internal enum LightRampantAssignment
    {
        // 八人有两名光锤点名，自身全随机时仍保留四分之一的概率。
        [RandomEnum(Hammer, Chain, Chain, Chain)]
        Random,
        Hammer,
        Chain
    }

    internal sealed class P23光之失控Plan
    {
        internal const float PositionTolerance = 2;
        internal const float DefaultOpeningRadius = 6;
        internal const float HammerTolerance = 1;
        internal const float StackTolerance = 1;
        internal const float PuddleRadius = 6;
        private const float OuterTowerRadius = 16;
        private const float ArenaLimit = 19.95f;
        // 半径六米时离线核算的容差，最近的非邻边仍比最长的邻边多约 0.15 米。
        private const float OpeningRadialTolerance = 1.05f;
        private const double OpeningAngularTolerance = 6.2 * Math.PI / 180;
        internal static readonly int[] PartySlots = Enumerable.Range(0, 8).ToArray();
        private static readonly int[] GreyNineInitDir16 = PartySlots.Select(slot => 2 * (int)U7aPartyPositions.EightWayDirection(slot)).ToArray();
        private static readonly int[] MmwInitDir16 = { 3, 1, 15, 13, 5, 7, 9, 11 };
        private static readonly int[] GreyNineChainPrio = { 0, 7, 1, 5, 3, 4, 2, 6 };
        private static readonly int[] MmwChainPrio = { 0, 1, 2, 3, 7, 6, 5, 4 };
        private static readonly int[] GreyNineHammerPrio = { 4, 8, 1, 5, 3, 7, 2, 6 };
        private static readonly int[] MmwHammerPriority = { 4, 6, 8, 7, 2, 1, 3, 5 };
        private static readonly int[] GreyNineTowerOrder = { 5, 1, 3, 0, 4, 2 };
        private static readonly int[] MmwTowerOrder = { 4, 0, 3, 1, 5, 2 };
        private static readonly Vector2[] Trail =
        {
            new Vector2(-8, 0), new Vector2(-5, -6), new Vector2(1.33f, -8),
            new Vector2(7.66f, -10), new Vector2(14, -12)
        };
        private readonly int[] _initialDirections;
        private readonly int[] _chainPriority;
        private readonly int[] _hammerPriority;
        private readonly int[] _towerOrder;
        private readonly int[] _sides = new int[8];
        private readonly int _rotationSteps;
        private readonly Matrix3x2 _rotation;

        internal int[] Baits { get; private set; }
        internal int[] Chain { get; private set; }
        internal int[] TowerPlayers { get; private set; }
        internal int[] Weights { get; private set; }
        internal int[] InitialStacks { get; private set; }
        internal int[] CenterPlayers { get; private set; }
        internal int FirstOrbParity { get; }

        internal P23光之失控Plan(Random random, LightRampantStrategy strategy = LightRampantStrategy.GreyNine)
        {
            var mmw = strategy == LightRampantStrategy.Mmw;
            _initialDirections = mmw ? MmwInitDir16 : GreyNineInitDir16;
            _rotationSteps = mmw ? 1 : 0;
            _rotation = Matrix3x2.CreateRotation(-_rotationSteps * (float)Math.PI / 3);
            _chainPriority = mmw ? MmwChainPrio : GreyNineChainPrio;
            _hammerPriority = mmw ? MmwHammerPriority : GreyNineHammerPrio;
            _towerOrder = mmw ? MmwTowerOrder : GreyNineTowerOrder;
            FirstOrbParity = random.Next(2) ^ _rotationSteps;
        }

        internal (int Slot, int Other, int Previous, int Next)? AssignOpening(PartySnapshot snapshot, Vector2 center,
            Random random, int self, LightRampantAssignment assignment)
        {
            var positions = PartySlots.Select(slot => snapshot.Get(slot).Position).ToArray();
            // 从攻略首位的扇区边界开始按实际角度顺时针排八人，首尾也是相邻的一对。
            var startHeading = Math.PI + (_initialDirections[_chainPriority[0]] + 1) * Math.PI / 8;
            var order = PartySlots.OrderBy(slot =>
            {
                var offset = positions[slot] - center;
                return (startHeading - Math.Atan2(offset.X, offset.Y) + 2 * Math.PI) % (2 * Math.PI);
            }).ToArray();

            var error = FindOpeningError(order, positions);
            if (error.HasValue)
            {
                // 先保留导致纠正的 A → B，再从 B 起每一步选最近的未入环者。等距时沿用原八方顺序。
                var (slot, other, _, _) = error.Value;
                var remaining = order.Where(member => member != slot && member != other).ToList();
                order[0] = slot;
                order[1] = other;
                for (var i = 2; i < order.Length; i++)
                {
                    var previous = positions[order[i - 1]];
                    var next = remaining.OrderBy(member => Vector2.DistanceSquared(previous, positions[member])).First();
                    order[i] = next;
                    remaining.Remove(next);
                }
            }

            // 八人顺序确定后才抽光锤。自身设置只约束是否被抽中，其他名额仍在剩余玩家中随机。
            assignment = assignment.ResolveRandom(random);
            var available = order.Where(slot => slot != self).ToList();
            var baits = new List<int>(2);
            if (assignment == LightRampantAssignment.Hammer)
                baits.Add(self);
            while (baits.Count < 2)
            {
                var index = random.Next(available.Count);
                baits.Add(available[index]);
                available.RemoveAt(index);
            }
            Baits = baits.OrderBy(slot => _hammerPriority[slot]).ToArray();
            Chain = order.Where(slot => !baits.Contains(slot)).ToArray();

            // 实际连线和重负沿重排后的顺序分配，队友踩塔和分组继续按配置的攻略优先级执行。
            var strategyChain = _chainPriority.Except(Baits).ToArray();
            TowerPlayers = _towerOrder.Select(index => strategyChain[index]).ToArray();
            for (var i = 0; i < strategyChain.Length; i++)
                _sides[strategyChain[i]] = ChainSide(i);
            for (var i = 0; i < Baits.Length; i++)
                _sides[Baits[i]] = i ^ 1;
            var weightIndex = random.Next(Chain.Length);
            Weights = new[] { Chain[weightIndex], Chain[(weightIndex + 1) % Chain.Length] }.OrderBy(Side).ToArray();

            InitialStacks = new int[8];
            foreach (var slot in Baits)
                InitialStacks[slot] = random.Next(1, 3);
            var remainingStacks = 6 - InitialStacks.Sum();
            foreach (var slot in Chain.OrderBy(_ => random.Next()).Take(remainingStacks))
                InitialStacks[slot] = 1;
            CenterPlayers = PartySlots.Where(slot => InitialStacks[slot] + (Chain.Contains(slot) ? 1 : 0) == 1).ToArray();
            return error;
        }

        internal int Side(int slot) => _sides[slot];

        private static int ChainSide(int index) => (index & 1) ^ 1;

        private double OpeningHeading(int slot) => Math.PI + _initialDirections[slot] * Math.PI / 8;

        internal Vector2 InitialOffset(int slot) => Direction(OpeningHeading(slot)) * DefaultOpeningRadius;

        internal SimPosition OpeningArea(Vector2 center, int slot, float radius)
        {
            var radial = OpeningRadialTolerance * Math.Min(1, radius / DefaultOpeningRadius);
            return SimPosition.AnnularSector(center, radius, OpeningHeading(slot), radial, OpeningAngularTolerance);
        }

        internal (double Degrees, float Metres)? CheckSelfOpening(Vector2 center, Vector2 position, int slot, float radius)
        {
            var offset = position - center;
            var degrees = Math.Abs(Math.IEEERemainder(Math.Atan2(offset.X, offset.Y) - OpeningHeading(slot), 2 * Math.PI)) * 180 / Math.PI;
            var metres = offset.Length() - radius;
            if (degrees > 22.5 || Math.Abs(metres) > PositionTolerance)
                return (degrees, metres);
            return null;
        }

        private static (int Slot, int Other, int Previous, int Next)? FindOpeningError(int[] order, Vector2[] positions)
        {
            for (var i = 0; i < order.Length; i++)
            {
                var slot = order[i];
                var previous = order[(i + order.Length - 1) % order.Length];
                var next = order[(i + 1) % order.Length];
                var origin = positions[slot];
                var other = -1;
                var nearest = float.PositiveInfinity;
                foreach (var member in order)
                {
                    if (member == slot || member == previous || member == next)
                        continue;
                    var distance = Vector2.DistanceSquared(origin, positions[member]);
                    if (distance < nearest)
                    {
                        nearest = distance;
                        other = member;
                    }
                }
                // 与任一八方邻位的距离相比更短就纠正。按角度顺序检查，以首个违规者的最近非邻位为起点。
                var longerEdge = Math.Max(Vector2.DistanceSquared(origin, positions[previous]), Vector2.DistanceSquared(origin, positions[next]));
                if (nearest < longerEdge)
                    return (slot, other, previous, next);
            }
            return null;
        }

        private static double TowerHeading(int index) => Math.PI + index * Math.PI / 3;

        internal static Vector2 TowerOffset(int index) => Direction(TowerHeading(index)) * OuterTowerRadius;

        private int TowerSide(int tower) => ChainSide(_towerOrder[tower]);

        internal int? StackViaTower(int tower)
        {
            // 放圈轴线两端的远塔先经过分摊同侧的相邻塔。
            if ((tower - _rotationSteps) % 3 != 0)
                return null;

            var adjacent = (tower + 1) % 6;
            return TowerSide(tower) == TowerSide(adjacent) ? adjacent : (tower + 5) % 6;
        }

        internal SimPosition OuterTowerArea(Vector2 center, int tower)
        {
            var heading = TowerHeading(tower);
            var stack = StackOffset(TowerSide(tower));
            var turn = Math.Sign(Math.IEEERemainder(Math.Atan2(stack.X, stack.Y) - heading, 2 * Math.PI));
            // 沿场心半径 15.5 至 16 米的圆弧，朝分摊方向偏 12°，留 ±1.5° 容差。
            return SimPosition.AnnularSector(center, OuterTowerRadius - 0.25f, heading + turn * 12 * Math.PI / 180,
                0.25f, 1.5 * Math.PI / 180);
        }

        internal Vector2 Rotate(Vector2 offset) => Vector2.Transform(offset, _rotation);

        internal Vector2 HammerOffset(int side, int wave) => Rotate(Trail[wave] * (side == 0 ? 1 : -1));
        private Vector2 StackOffset(int side) => Rotate(new Vector2(side == 0 ? -18 : 18,
            FirstOrbParity == _rotationSteps ? -3.8f : 3.8f));

        internal SimPosition OrbSafeArea(Vector2 center, int side, bool afterFirst)
        {
            if (!afterFirst)
                return SimPosition.Ellipse(center + StackOffset(side), StackTolerance);

            var lateral = side == 0 ? -1 : 1;
            var south = FirstOrbParity == _rotationSteps;
            var depth = south ? 1 : -1;
            // 以东南侧为模板，在场边与光球的夹角略往内取圆心，向场内开 0–2 米的扇形。
            // 扇形离场心最多约 19.85 米，离第二波光球至少约 11.12 米。
            var origin = center + Rotate(new Vector2(lateral * 19.8f, depth * 1.4f));
            var heading = (south ? 0 : Math.PI) - lateral * depth * Math.PI / 6 + _rotationSteps * Math.PI / 3;
            return SimPosition.AnnularSector(origin, 1, heading, 1, Math.PI / 12);
        }

        internal static SimPosition ArenaArea(Vector2 center, Vector2 position, float tolerance = PositionTolerance)
        {
            var offset = position - center;
            var limit = ArenaLimit - tolerance;
            if (offset.LengthSquared() > limit * limit)
                position = center + Vector2.Normalize(offset) * limit;
            return SimPosition.Ellipse(position, tolerance);
        }

        internal static Vector2 PuddleWaypoint(Vector2 start, Vector2 destination,
            IReadOnlyList<(Vector2 Center, float Radius)> circles)
        {
            if (Clear(start, destination, allowExit: true))
                return destination;

            var waypoint = start;
            var bestDistance = float.PositiveInfinity;
            foreach (var circle in circles)
                for (var direction = 0; direction < 16; direction++)
                {
                    var heading = Direction(direction * Math.PI / 8);
                    Consider(circle.Center + heading * (circle.Radius + 1));
                    Consider(circle.Center + heading * (circle.Radius + 3));
                }
            if (bestDistance < float.PositiveInfinity)
                return waypoint;

            // 暂时过不去时先走到圈外，残留消退后再继续。
            var direct = destination - start;
            var distanceLeft = direct.Length();
            if (distanceLeft == 0)
                return start;

            var directionToDestination = direct / distanceLeft;
            foreach (var circle in circles)
            {
                var offset = circle.Center - start;
                var projection = Vector2.Dot(offset, directionToDestination);
                var discriminant = circle.Radius * circle.Radius - offset.LengthSquared() + projection * projection;
                if (discriminant < 0)
                    continue;
                var halfChord = (float)Math.Sqrt(discriminant);
                if (projection + halfChord >= 0)
                    distanceLeft = Math.Min(distanceLeft, projection - halfChord - 0.05f);
            }
            return start + directionToDestination * Math.Max(0, distanceLeft);

            void Consider(Vector2 point)
            {
                var distance = Vector2.Distance(start, point) + Vector2.Distance(point, destination);
                if (distance >= bestDistance || point.LengthSquared() > ArenaLimit * ArenaLimit ||
                    !Clear(start, point, allowExit: true) || !Clear(point, destination, allowExit: false))
                    return;
                waypoint = point;
                bestDistance = distance;
            }

            bool Clear(Vector2 from, Vector2 to, bool allowExit)
            {
                var travel = to - from;
                var lengthSquared = travel.LengthSquared();
                foreach (var circle in circles)
                {
                    var offset = from - circle.Center;
                    var radiusSquared = circle.Radius * circle.Radius;
                    var outward = Vector2.Dot(offset, travel);
                    if (allowExit && offset.LengthSquared() < radiusSquared && outward >= 0 &&
                        Vector2.DistanceSquared(to, circle.Center) > radiusSquared)
                        continue;

                    var fraction = lengthSquared > 0 ? Math.Max(0, Math.Min(1, -outward / lengthSquared)) : 0;
                    if ((offset + travel * fraction).LengthSquared() <= radiusSquared)
                        return false;
                }
                return true;
            }
        }

        private static Vector2 Direction(double heading) => new Vector2((float)Math.Sin(heading), (float)Math.Cos(heading));
    }
}
