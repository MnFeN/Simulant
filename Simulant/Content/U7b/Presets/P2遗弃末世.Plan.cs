using Simulant.Simulation.Options;
using System;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U7b.Presets
{
    internal enum ForsakenMarker : ushort
    {
        None = 0,
        Stack = 0x2CB,
        Circle = 0x2CC,
        Fan = 0x2CD,
    }

    /// <summary>自己的初始点名限制；其余分配仍从符合机制规则的组合中随机选择。</summary>
    internal enum ForsakenAssignment
    {
        [RandomEnum(GroupARandom, GroupBRandom)]
        Random,
        [RandomEnum(GroupAFan, GroupACircle, GroupAStackWithFan, GroupAStackWithCircle)]
        GroupARandom,
        [RandomEnum(GroupBFan, GroupBCircle)]
        GroupBRandom,
        GroupAFan,
        GroupACircle,
        GroupAStackWithFan,
        GroupAStackWithCircle,
        GroupBFan,
        GroupBCircle,
    }

    /// <summary>按用户提供的「U7b 绝妖星宝宝椅 v1.0.6」及所选攻略设置，预先生成本次机制分配。</summary>
    internal sealed class ForsakenPlan
    {
        internal static readonly Vector2 Center = new Vector2(100, 100);
        internal const float TowerRadius = 4;
        internal const float SpellRadius = 5;
        internal const int MemberCount = 8;

        // 正北为前方；每组两人间隔两米，避免头顶点名重叠。
        private static readonly Vector2[] InitialOffsets =
        {
            new Vector2(-6, -5), new Vector2(6, -5), new Vector2(-4, -5), new Vector2(4, -5),
            new Vector2(-6, 5), new Vector2(6, 5), new Vector2(-4, 5), new Vector2(4, 5),
        };

        internal static Vector2 InitialPosition(int slot)
        {
            if (slot < 0 || slot >= MemberCount) throw new ArgumentOutOfRangeException(nameof(slot), "职能下标必须在 0 至 7 之间。");
            return Center + InitialOffsets[slot];
        }

        // 全部数组均使用 MT、ST、H1、H2、D1、D2、D3、D4 的零起始下标。
        private static readonly int[] Partners = { 2, 3, 0, 1, 6, 7, 4, 5 };
        private static readonly bool[] GroupAByRound = { true, true, true, false, false, false, false, true };
        private static readonly Vector2[] OddSoakers =
        {
            new Vector2(-5.66f, 9.16f), new Vector2(2.20f, 6.50f),
            new Vector2(-6.30f, 4.70f), new Vector2(8.19f, 3.66f),
        };
        private static readonly Vector2[] OddHelpers =
        {
            // 沿用用户修正后的左侧分摊闲人点位，避开半场刀覆盖。
            new Vector2(-10.14f, 5.11f), new Vector2(-5.66f, 11.66f),
            new Vector2(8.70f, 2.34f), new Vector2(9.59f, 3.45f),
        };
        private static readonly Vector2[] OddMeleeSoakers =
        {
            new Vector2(-8, 8), new Vector2(8.13f, 8.13f),
            new Vector2(-4.95f, 4.95f), new Vector2(5.66f, 2.16f),
        };
        private static readonly Vector2[] OddMeleeHelpers =
        {
            new Vector2(-2.45f, 2.45f), new Vector2(-9, 9),
            new Vector2(2.25f, 2.65f), new Vector2(2.65f, 2.25f),
        };
        private static readonly Vector2[] EvenSoakers =
        {
            new Vector2(-9.51f, 6.27f), new Vector2(-8.30f, 8.52f),
            new Vector2(4.71f, 7.72f), new Vector2(8.69f, 3.22f),
        };
        private static readonly Vector2[] EvenHelpers =
        {
            new Vector2(-1.90f, -3.50f), new Vector2(-3.20f, 2.40f),
            new Vector2(3.97f, -3.55f), new Vector2(3.20f, 2.40f),
        };

        internal ForsakenMarker[] InitialMarkers { get; private set; }
        internal bool[] GroupA { get; private set; }
        internal ForsakenRoundPlan[] Rounds { get; private set; }

        /// <summary>只进行托管计算。初始北向按正北起顺时针计数；塔逐轮转过 45 度。</summary>
        internal static ForsakenPlan Create(Random random, int selfSlot = 0,
            ForsakenAssignment assignment = ForsakenAssignment.Random, bool optimizeMelee = false, bool helpersByRole = true)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (selfSlot < 0 || selfSlot >= MemberCount) throw new ArgumentOutOfRangeException(nameof(selfSlot), "职能下标必须在 0 至 7 之间。");

            // 初始点名只有 32 种合法组合：TH/DPS 扇形钢铁互换，各自再抽一人分摊。
            // 先筛选再等概率抽取，避免反复重抽，也不偏向点名种类较多的 A 组。
            var candidates = (from fan in new[] { true, false }
                              from support in Enumerable.Range(0, 4)
                              from damage in Enumerable.Range(4, 4)
                              where MatchesAssignment(assignment, selfSlot, fan, support, damage)
                              select (SupportFan: fan, SupportStack: support, DamageStack: damage)).ToArray();
            var initial = candidates[random.Next(candidates.Length)];
            var supportFan = initial.SupportFan;
            var markers = Enumerable.Range(0, MemberCount)
                .Select(slot => (slot < 4) == supportFan ? ForsakenMarker.Fan : ForsakenMarker.Circle)
                .ToArray();
            var supportStack = initial.SupportStack;
            var damageStack = initial.DamageStack;
            markers[supportStack] = markers[damageStack] = ForsakenMarker.Stack;

            var coneStack = supportFan ? supportStack : damageStack;
            var circleStack = supportFan ? damageStack : supportStack;
            var activeA = new[] { Partners[coneStack], Partners[circleStack], coneStack, circleStack };
            var groupA = Enumerable.Range(0, MemberCount).Select(activeA.Contains).ToArray();
            var activeB = Enumerable.Range(0, MemberCount).Where(slot => !groupA[slot])
                .OrderBy(slot => MarkerOrder(markers[slot])).ThenBy(slot => slot).ToArray();
            var stacks = Enumerable.Repeat(4, MemberCount).ToArray();
            var initialNorth = random.Next(8);
            var rotation = random.Next(2) == 0 ? -1 : 1;
            var oddSoakers = optimizeMelee ? OddMeleeSoakers : OddSoakers;
            var oddHelpers = optimizeMelee ? OddMeleeHelpers : OddHelpers;
            var plan = new ForsakenPlan
            {
                InitialMarkers = (ForsakenMarker[])markers.Clone(),
                GroupA = groupA,
                Rounds = new ForsakenRoundPlan[8],
            };

            for (var index = 0; index < plan.Rounds.Length; index++)
            {
                var odd = index % 2 == 0;
                var active = GroupAByRound[index] ? activeA : activeB;
                var north = NormalizeDirection(initialNorth + rotation * index);
                var localPositions = new Vector2[MemberCount];
                var towerBySlot = Enumerable.Repeat(-1, MemberCount).ToArray();
                for (var slot = 0; slot < MemberCount; slot++)
                {
                    var positionIndex = Array.IndexOf(active, slot);
                    if (positionIndex < 0)
                    {
                        // 按初始点名分边时，扇形组在左、钢铁组在右；组内仍是 T/近战在前、H/远程在后。
                        var helperIndex = slot / 2;
                        if (!helpersByRole && !supportFan) helperIndex = (helperIndex + 2) % 4;
                        localPositions[slot] = (odd ? oddHelpers : EvenHelpers)[helperIndex];
                        continue;
                    }

                    localPositions[slot] = (odd ? oddSoakers : EvenSoakers)[positionIndex];
                    towerBySlot[slot] = odd ? positionIndex % 2 : positionIndex / 2;
                }

                var beforeMarkers = (ForsakenMarker[])markers.Clone();
                var beforeStacks = (int[])stacks.Clone();
                var nextMarkers = odd
                    ? new[] { ForsakenMarker.Fan, ForsakenMarker.Fan, ForsakenMarker.Circle, ForsakenMarker.Circle }
                    : new[] { ForsakenMarker.Fan, ForsakenMarker.Circle, ForsakenMarker.Stack, ForsakenMarker.Stack };
                Shuffle(nextMarkers, random);
                for (var i = 0; i < active.Length; i++)
                {
                    var slot = active[i];
                    stacks[slot]--;
                    markers[slot] = stacks[slot] == 0 ? ForsakenMarker.None : nextMarkers[i];
                }

                // 同类头标以刚才踩塔时的左右顺序决定下一次的位置，不再按职业重新排序。
                var nextActive = active.OrderBy(slot => MarkerOrder(markers[slot]))
                    .ThenBy(slot => localPositions[slot].X).ThenBy(slot => slot).ToArray();
                if (GroupAByRound[index]) activeA = nextActive;
                else activeB = nextActive;

                var future = !odd && random.Next(2) == 0;
                var nextNorth = NormalizeDirection(initialNorth + rotation * (index + 1));
                var bait = index == 7
                    ? Center + new Vector2(0, -8)
                    : Transform(new Vector2(0, future ? -8 : 8), nextNorth);
                var diagonal = (float)Math.Sqrt(32);
                plan.Rounds[index] = new ForsakenRoundPlan(
                    index + 1, north, (int[])active.Clone(), towerBySlot,
                    localPositions.Select(position => Transform(position, north)).ToArray(),
                    new[] { Transform(new Vector2(-diagonal, diagonal), north), Transform(new Vector2(diagonal, diagonal), north) },
                    new[] { (byte)(NormalizeDirection(5 + north) + 1), (byte)(NormalizeDirection(3 + north) + 1) },
                    beforeMarkers, (ForsakenMarker[])markers.Clone(), beforeStacks, (int[])stacks.Clone(),
                    future, bait, index == 7 && future ? Center + new Vector2(0, 8) : bait);
            }

            return plan;
        }

        private static bool MatchesAssignment(ForsakenAssignment assignment, int slot, bool supportFan, int supportStack, int damageStack)
        {
            var stack = slot == supportStack || slot == damageStack;
            var groupA = stack || Partners[slot] == supportStack || Partners[slot] == damageStack;
            var fanGroup = (slot < 4) == supportFan;
            switch (assignment)
            {
                case ForsakenAssignment.Random: return true;
                case ForsakenAssignment.GroupARandom: return groupA;
                case ForsakenAssignment.GroupBRandom: return !groupA;
                case ForsakenAssignment.GroupAFan: return groupA && !stack && fanGroup;
                case ForsakenAssignment.GroupACircle: return groupA && !stack && !fanGroup;
                case ForsakenAssignment.GroupAStackWithFan: return stack && fanGroup;
                case ForsakenAssignment.GroupAStackWithCircle: return stack && !fanGroup;
                case ForsakenAssignment.GroupBFan: return !groupA && fanGroup;
                case ForsakenAssignment.GroupBCircle: return !groupA && !fanGroup;
                default: throw new ArgumentOutOfRangeException(nameof(assignment), "未识别的遗弃末世点名选项。");
            }
        }

        /// <summary>局部坐标的上方为 Boss，左右塔位于下方；返回游戏水平 XY 坐标。</summary>
        internal static Vector2 Transform(Vector2 position, int northDirection)
        {
            var angle = NormalizeDirection(northDirection) * Math.PI / 4;
            var cosine = (float)Math.Cos(angle);
            var sine = (float)Math.Sin(angle);
            return Center + new Vector2(position.X * cosine - position.Y * sine, position.X * sine + position.Y * cosine);
        }

        private static int NormalizeDirection(int direction) => (direction % 8 + 8) % 8;
        private static int MarkerOrder(ForsakenMarker marker) => marker == ForsakenMarker.Fan ? 0 : marker == ForsakenMarker.Circle ? 1 : 2;

        private static void Shuffle(ForsakenMarker[] values, Random random)
        {
            for (var i = values.Length - 1; i > 0; i--)
            {
                var target = random.Next(i + 1);
                var swap = values[target];
                values[target] = values[i];
                values[i] = swap;
            }
        }
    }

    /// <summary>一轮塔的头标、层数和固定解法站位；不包含任何原生实体或计时器。</summary>
    internal sealed class ForsakenRoundPlan
    {
        internal int Number { get; }
        internal int NorthDirection { get; }
        internal int[] Soakers { get; }
        internal int[] TowerBySlot { get; }
        internal Vector2[] Positions { get; }
        internal Vector2[] TowerCenters { get; }
        internal byte[] TowerMapIndices { get; }
        internal ForsakenMarker[] MarkersBefore { get; }
        internal ForsakenMarker[] MarkersAfter { get; }
        internal int[] StacksBefore { get; }
        internal int[] StacksAfter { get; }
        internal bool FutureEnd { get; }
        internal Vector2 BaitPosition { get; }
        internal Vector2 FinalDodgePosition { get; }

        internal ForsakenRoundPlan(int number, int northDirection, int[] soakers, int[] towerBySlot,
            Vector2[] positions, Vector2[] towerCenters, byte[] towerMapIndices,
            ForsakenMarker[] markersBefore, ForsakenMarker[] markersAfter, int[] stacksBefore, int[] stacksAfter,
            bool futureEnd, Vector2 baitPosition, Vector2 finalDodgePosition)
        {
            Number = number;
            NorthDirection = northDirection;
            Soakers = soakers;
            TowerBySlot = towerBySlot;
            Positions = positions;
            TowerCenters = towerCenters;
            TowerMapIndices = towerMapIndices;
            MarkersBefore = markersBefore;
            MarkersAfter = markersAfter;
            StacksBefore = stacksBefore;
            StacksAfter = stacksAfter;
            FutureEnd = futureEnd;
            BaitPosition = baitPosition;
            FinalDodgePosition = finalDodgePosition;
        }
    }
}
