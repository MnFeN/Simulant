using Simulant.Game.FFCS.Client.Game.UI;
using Simulant.Simulation.Geometry;
using Simulant.Simulation;
using Simulant.Simulation.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static Simulant.Utils.Maths.Angles;

namespace Simulant.Content.U7a.Presets
{
    internal enum ApocalypseSolution
    {
        DoubleCN, DoubleHTDH, DoubleNA,
        SingleTHD, SingleReverseD, SingleHTDH, SingleJP, SingleBalanced
    }

    internal enum ApocalypseReference
    {
        FirstFire, GroupSafe, DriverSafe
    }

    internal enum ApocalypseHalf
    {
        NorthToSouthwest, NortheastToWest
    }

    internal enum ApocalypseDebuff
    {
        [RandomEnum(None, ShortWater, MediumWater, LongWater)]
        Random = -1,
        None, ShortWater, MediumWater, LongWater
    }

    internal sealed class P32启示Plan
    {
        private static readonly int[] BalancedPriority = { 0, 1, 4, 5, 2, 3, 6, 7 };
        private static readonly Lazy<List<int[]>[,,]> BalancedAssignments = new Lazy<List<int[]>[,,]>(CreateBalancedAssignments);
        internal const int LightAnimationCount = 5;
        internal const int ExplosionCount = 6;
        internal static double LightAnimationAt(int step) => 14.816 + 2.003 * step;
        internal static double ExplosionAt(int volley) => 26.013 + 2.006 * volley;
        internal readonly int[] Orders;
        internal readonly int[] StackGroups = new int[8];
        internal readonly int[] SpreadGroups = new int[8];
        internal readonly int[] SpreadIndices = new int[8];
        internal readonly MarkerType?[] GroupMarkers = new MarkerType?[8];
        /// <summary>点名后的站位编号：普通单分组继承换位对象，标点分组改用组内头标顺序。</summary>
        internal readonly int[] PositionOwners = Enumerable.Range(0, 8).ToArray();
        internal readonly int StartDirection;
        internal readonly int Turn;
        internal readonly int ThreeDirection;
        internal readonly int TankSlot;
        internal readonly int TankDirection;
        internal bool IsSingleGroup { get; }
        private readonly Vector2[] _firstJump = new Vector2[8];
        private readonly float _spiritTakerRadius;
        internal int TwoDirection => Mod(ThreeDirection + Turn, 8);

        internal P32启示Plan(int self, ApocalypseDebuff debuff, ApocalypseSolution solution, ApocalypseReference reference, ApocalypseHalf half,
            ExerciseDirection start, ExerciseTurn turn, Random random, float spiritTakerRadius, BaitTank tank = BaitTank.MT,
            bool swapSpreadPositions = false, bool equalGroups = true)
        {
            _spiritTakerRadius = spiritTakerRadius;
            IsSingleGroup = solution > ApocalypseSolution.DoubleNA;
            var initial = (int)debuff.ResolveRandom(random);
            var prio = solution == ApocalypseSolution.DoubleCN ? new[] { 2, 3, 0, 1, 4, 5, 6, 7 } :
                solution == ApocalypseSolution.DoubleHTDH || solution == ApocalypseSolution.SingleHTDH ? new[] { 2, 0, 1, 4, 5, 6, 7, 3 } :
                solution == ApocalypseSolution.DoubleNA || solution == ApocalypseSolution.SingleJP ? new[] { 3, 2, 1, 0, 4, 5, 6, 7 } :
                solution == ApocalypseSolution.SingleReverseD ? new[] { 0, 1, 2, 3, 7, 6, 5, 4 } :
                solution == ApocalypseSolution.SingleBalanced ? BalancedPriority : Enumerable.Range(0, 8).ToArray();
            var rank = Array.IndexOf(prio, self);
            // 两端职能保持原规则，其他职能可先等概率选择自身的组别。
            var balanceSelf = equalGroups && rank > 0 && rank < prio.Length - 1;
            Orders = balanceSelf && solution == ApocalypseSolution.SingleBalanced
                ? DrawBalanced(self, initial, random)
                : AssignmentRandom.Draw(Enumerable.Range(0, 8).Select(i => i / 2).ToArray(), random, self,
                    value => value == initial, balanceSelf ? prio : null);
            var ordered = OrderForGroups(Orders, prio, solution == ApocalypseSolution.SingleBalanced);
            var doubleGroups = !IsSingleGroup;
            // 同点名的两人按优先级分为左、右；单分组只让原侧别不符的人换位。
            for (var i = 0; i < 8; i++)
                StackGroups[ordered[i]] = i % 2;
            if (solution == ApocalypseSolution.SingleBalanced)
            {
                var markers = new[] { MarkerType.Attack1, MarkerType.Attack2, MarkerType.Attack3, MarkerType.Attack4,
                    MarkerType.Bind1, MarkerType.Bind2, MarkerType.Bind3, MarkerType.Square };
                for (var group = 0; group < 2; group++)
                {
                    var members = prio.Where(slot => StackGroups[slot] == group).ToArray();
                    for (var index = 0; index < members.Length; index++)
                    {
                        PositionOwners[members[index]] = group * 4 + index;
                        GroupMarkers[members[index]] = markers[group * 4 + index];
                    }
                }
            }
            else if (!doubleGroups)
                AssignSwaps();
            for (var i = 0; i < 8; i++)
            {
                SpreadGroups[i] = doubleGroups ? i / 4 : StackGroups[i];
                SpreadIndices[i] = PositionOwners[i] % 4;
            }

            // 碎灵一击先按左右分摊组展开，与稍后的地火分散使用不同站位。
            var farHealer = prio.First(i => i == 2 || i == 3);
            var farRanged = StackGroups[7] == 1 ? 7 : 6;
            for (var slot = 0; slot < 8; slot++)
            {
                var side = StackGroups[slot] == 0 ? -1 : 1;
                if (doubleGroups)
                {
                    _firstJump[slot] = slot == 0 ? new Vector2(0, -7.5f) : slot == 1 ? Vector2.Zero :
                        slot == 4 ? new Vector2(side * 7.5f, 0) : slot == 5 ? new Vector2(0, 7.5f) :
                        slot == farHealer ? new Vector2(-13, 0) : slot == farRanged ? new Vector2(13, 0) :
                        new Vector2(side * 7.071068f, slot < 4 ? -7.071068f : 7.071068f);
                }
                else
                {
                    var index = SpreadIndices[slot];
                    // 左组面朝场中时，纵向后退对应 X 负向，横向对应 Y；右组整体旋转 180°。
                    var position = new Vector2(index < 2 ? -4.5f : -13, index % 2 == 0 ? -4.5f : 4.5f);
                    _firstJump[slot] = side < 0 ? position : -position;
                }
            }

            if (swapSpreadPositions)
                for (var slot = 0; slot < 8; slot++)
                    SpreadIndices[slot] ^= 1;

            StartDirection = (int)start.ResolveRandom(random);
            Turn = turn.ResolveRandom(random) == ExerciseTurn.Clockwise ? 1 : -1;
            var d = StartDirection;
            ThreeDirection = half == ApocalypseHalf.NorthToSouthwest
                ? reference == ApocalypseReference.FirstFire
                    ? Mod(d, 4) + Turn : reference == ApocalypseReference.GroupSafe
                        ? Mod(d + Turn, 4) : Mod(d + 2, 4) - Turn
                : reference == ApocalypseReference.FirstFire
                    ? Mod(d + 1, 4) - 1 + Turn : reference == ApocalypseReference.GroupSafe
                        ? Mod(d + 1 + Turn, 4) - 1 : Mod(d + 3, 4) - 1 - Turn;
            ThreeDirection = Mod(ThreeDirection, 8);
            TankSlot = (int)tank;
            TankDirection = Mod(TwoDirection + StackGroups[TankSlot] * 4, 8);
        }

        private static int[] DrawBalanced(int self, int initial, Random random)
        {
            var group = random.Next(2);
            var candidates = BalancedAssignments.Value[self, initial, group];
            return candidates[random.Next(candidates.Count)].ToArray();
        }

        private static List<int[]>[,,] CreateBalancedAssignments()
        {
            // 自动双近双远还取决于其他点名组合；只枚举一次全部 2520 种合法点名，再按自身点名和组别直接抽取。
            var assignments = new List<int[]>[8, 4, 2];
            for (var self = 0; self < 8; self++)
                for (var kind = 0; kind < 4; kind++)
                    for (var group = 0; group < 2; group++)
                        assignments[self, kind, group] = new List<int[]>();
            var orders = new int[8];
            var remaining = new[] { 2, 2, 2, 2 };
            Add(0);
            return assignments;

            void Add(int slot)
            {
                if (slot == orders.Length)
                {
                    var candidate = orders.ToArray();
                    var ordered = OrderForGroups(candidate, BalancedPriority, true);
                    for (var index = 0; index < ordered.Length; index++)
                        assignments[ordered[index], candidate[ordered[index]], index % 2].Add(candidate);
                    return;
                }
                for (var kind = 0; kind < remaining.Length; kind++)
                    if (remaining[kind] > 0)
                    {
                        orders[slot] = kind;
                        remaining[kind]--;
                        Add(slot + 1);
                        remaining[kind]++;
                    }
            }
        }

        private static int[] OrderForGroups(int[] orders, int[] priority, bool balanced)
        {
            var ordered = priority.OrderBy(slot => orders[slot]).ToArray();
            if (!balanced)
                return ordered;

            var mixed = Enumerable.Range(0, 4).Where(i => Near(ordered[i * 2]) != Near(ordered[i * 2 + 1])).ToArray();
            for (var i = mixed.Length / 2; i < mixed.Length; i++)
            {
                var offset = mixed[i] * 2;
                (ordered[offset + 1], ordered[offset]) = (ordered[offset], ordered[offset + 1]);
            }
            if (Array.IndexOf(ordered, 0) % 2 != 0)
                for (var i = 0; i < 8; i += 2)
                {
                    (ordered[i + 1], ordered[i]) = (ordered[i], ordered[i + 1]);
                }
            return ordered;
        }

        private void AssignSwaps()
        {
            // 站位顺序固定为 T12 H12、D1234，与点名分组优先级无关；按序填对侧空位，未换组者保持原位。
            var left = Enumerable.Range(0, 4).Where(slot => StackGroups[slot] == 1).ToArray();
            var right = Enumerable.Range(4, 4).Where(slot => StackGroups[slot] == 0).ToArray();
            for (var i = 0; i < left.Length; i++)
            {
                PositionOwners[left[i]] = right[i];
                PositionOwners[right[i]] = left[i];
            }
        }

        private static bool Near(int slot) => slot < 2 || slot == 4 || slot == 5;
        internal static int Mod(int value, int divisor) => (value % divisor + divisor) % divisor;
        internal static double Angle(int direction) => Math.PI + Deg(direction * 45);
        internal static Vector2 Direction(double angle) => new Vector2((float)Math.Sin(angle), (float)Math.Cos(angle));
        internal static Vector2 Center => new Vector2(100, 100);
        internal int Target(int round, int group) => Enumerable.Range(0, 8).Single(i => Orders[i] == round + 1 && StackGroups[i] == group);
        internal int[] Group(int group) => Enumerable.Range(0, 8).Where(i => StackGroups[i] == group).ToArray();
        internal SimPosition Preposition(int slot) => PrepositionFor(slot);

        private static SimPosition PrepositionFor(int owner)
        {
            var diagonal = (float)(1.5 / Math.Sqrt(2));
            var index = owner % 4;
            var position = new Vector2(-8 + (index < 2 ? diagonal : -diagonal), index % 2 == 0 ? -diagonal : diagonal);
            return SimPosition.Ellipse(Center + (owner < 4 ? position : -position), 1);
        }

        internal SimPosition InitialStack(int slot)
        {
            if (IsSingleGroup)
                return PrepositionFor(PositionOwners[slot]);
            var center = Center + new Vector2(StackGroups[slot] == 0 ? -8 : 8, 0);
            var towardSpread = Vector2.Normalize(FirstJump(slot).Midpoint() - center);
            return SimPosition.Ellipse(center + 1.5f * towardSpread, 0.5f);
        }

        internal SimPosition FirstJump(int slot) => SimPosition.Ellipse(Center + _firstJump[slot], IsSingleGroup ? 1 : 0.1f);

        internal Vector2[] SampleFirstJump(Random random)
        {
            var positions = new Vector2[8];
            var minimum = _spiritTakerRadius + 0.1f;
            // 保留各点指定的容差，避免随机落点使假队员互相波及。
            for (var attempt = 0; attempt < 32; attempt++)
            {
                for (var slot = 0; slot < 8; slot++)
                    positions[slot] = FirstJump(slot).Sample(random);
                if (Enumerable.Range(0, 8).All(slot => Enumerable.Range(0, slot)
                    .All(other => Vector2.DistanceSquared(positions[slot], positions[other]) > minimum * minimum)))
                    return positions;
            }
            return Enumerable.Range(0, 8).Select(slot => FirstJump(slot).Midpoint()).ToArray();
        }

        internal SimPosition Spread(int slot)
        {
            var angle = Angle(ThreeDirection + SpreadGroups[slot] * 4);
            var n = Direction(angle);
            var t = Direction(angle + Deg(90));
            var index = SpreadIndices[slot];
            var position = index == 0 ? Center + 10.25f * n : index == 1 ? Center + 10.25f * Direction(angle + Deg(Turn * 45))
                : Center + 17.5f * n + (index == 2 ? -3.5f : 3.5f) * t;
            return SimPosition.Ellipse(position, 0.25f);
        }

        internal SimPosition ReturnGate(int slot)
        {
            var direction = (SpreadIndices[slot] == 1 ? TwoDirection : ThreeDirection) + SpreadGroups[slot] * 4;
            return SimPosition.Ellipse(Center + (SpreadIndices[slot] < 2 ? 4.5f : 10.25f) * Direction(Angle(direction)), 0.1f);
        }

        internal SimPosition SecondStack(int slot)
        {
            // 从人群方向朝单人方向为正；人群 -5°～30°，引导 T 固定 30°，判定后再边出去边修正至 45°。
            var angle = Angle(ThreeDirection + StackGroups[slot] * 4);
            return SimPosition.AnnularSector(Center, 4.4f, angle + Deg(Turn * (slot == TankSlot ? 30 : 12.5)),
                0.4f, Deg(slot == TankSlot ? 0 : 17.5));
        }

        internal Vector2 TankPosition => Center + 18 * Direction(Angle(TankDirection));
        private static double OutwardHeading(Vector2 landing) => Math.Atan2(landing.X - Center.X, landing.Y - Center.Y);
        internal SimPosition FinalStack(int slot, Vector2 landing)
            => SimPosition.AnnularSector(landing, Near(slot) ? 12 : 14,
                OutwardHeading(landing) + Math.PI + Deg(StackGroups[slot] == 0 ? -25 : 25),
                1, Deg(8));

        internal SimPosition AfterFinalStack(Vector2 landing)
            => SimPosition.Ellipse(landing + 7 * Direction(OutwardHeading(landing) + Math.PI), 3);

        internal Vector2[] Explosions(int volley)
        {
            var result = new List<Vector2>();
            for (var step = volley; step >= volley - 2; step--)
            {
                if (step < -1)
                    continue;
                var position = step == -1 ? Center : Center + 14 * Direction(Angle(StartDirection - Turn * step));
                result.Add(position);
                result.Add(2 * Center - position);
            }
            return result.ToArray();
        }
    }
}
