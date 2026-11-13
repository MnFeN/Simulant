using Simulant.Simulation.Geometry;
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
        private const float TowerEdgeMargin = 0.05f;
        private static readonly Vector2[] LocalTowerCenters =
        {
            new Vector2(-(float)Math.Sqrt(32), (float)Math.Sqrt(32)),
            new Vector2((float)Math.Sqrt(32), (float)Math.Sqrt(32)),
        };

        // 参考点 (94.16974, 105.0749)、(95.60394, 103.6675) 的平均方向取最近的三度倍数。
        private const double InitialDirection = -51 * Math.PI / 180;

        internal static SimPosition InitialPosition(int slot, bool melee)
        {
            if (slot < 0 || slot >= MemberCount) throw new ArgumentOutOfRangeException(nameof(slot), "职能下标必须在 0 至 7 之间。");
            var direction = slot < 4 ? InitialDirection : InitialDirection / 3;
            if (slot % 2 != 0) direction = -direction;
            return SimPosition.AnnularSector(Center, melee ? 5 : 7, direction, 1, Math.Abs(InitialDirection) / 6);
        }

        // 全部数组均使用 MT、ST、H1、H2、D1、D2、D3、D4 的零起始下标。
        private static readonly int[] Partners = { 2, 3, 0, 1, 6, 7, 4, 5 };
        private static readonly bool[] GroupAByRound = { true, true, true, false, false, false, false, true };
        private static readonly SimPosition[] OddSoakers =
        {
            TowerCircle(new Vector2(-5.66f, 9.16f), 0), TowerCircle(new Vector2(2.20f, 6.50f), 1),
            TowerCircle(new Vector2(-6.30f, 4.70f), 0), TowerCircle(new Vector2(8.19f, 3.66f), 1),
        };
        private static readonly SimPosition[] OddHelpers =
        {
            // 沿用用户修正后的左侧分摊闲人点位，避开半场刀覆盖。
            TowerCircle(new Vector2(-10.14f, 5.11f)), TowerCircle(new Vector2(-5.66f, 11.66f)),
            // 右侧近战使用左侧坦克的镜像点；远程沿右塔同一圆周向后移动两米。
            TowerCircle(new Vector2(10.14f, 5.11f)), TowerCircle(AlongTower(new Vector2(10.14f, 5.11f), 1, 2)),
        };
        private static readonly SimPosition[] OddMeleeSoakers =
        {
            TowerCircle(new Vector2(-8, 8), 0), TowerCircle(new Vector2(8.13f, 8.13f), 1),
            TowerCircle(new Vector2(-4.95f, 4.95f), 0),
            TowerCircle(new Vector2(5.66f, 2.16f) + Vector2.Normalize(new Vector2(-1, 1)), 1),
        };
        private static readonly SimPosition[] OddMeleeHelpers =
        {
            TowerCircle(new Vector2(-2.45f, 2.45f)), TowerCircle(new Vector2(-9, 9)),
            TowerCircle(new Vector2(2.25f, 2.65f)), TowerCircle(new Vector2(2.65f, 2.25f)),
        };
        private static readonly Vector2[] EvenLeftOffsets = { new Vector2(-9.51f, 6.27f), new Vector2(-8.30f, 8.52f) };
        private static readonly SimPosition[] EvenSoakers =
        {
            // 两个扇环分别向彼此收进半个展角，外侧角边界仍经过各自原点位。
            TowerArc(EvenLeftOffsets[0], 0, TowerRadius - 0.35f, 0.25f, EvenLeftOffsets[1]),
            TowerArc(EvenLeftOffsets[1], 0, TowerRadius - 0.35f, 0.25f, EvenLeftOffsets[0]),
            TowerCircle(new Vector2(4.71f, 7.72f), 1), TowerArc(new Vector2(8.69f, 3.22f), 1, TowerRadius - 0.5f, 0.4f),
        };
        private static readonly SimPosition[] EvenHelpers =
        {
            TowerCircle(new Vector2(-1.90f, -3.50f)), TowerArc(new Vector2(-3.20f, 2.40f), 0, TowerRadius + 0.5f, 0.4f),
            TowerCircle(new Vector2(3.97f, -3.55f)), TowerArc(new Vector2(3.20f, 2.40f), 1, TowerRadius + 0.5f, 0.4f),
        };

        static ForsakenPlan()
        {
            ConstrainSpellTolerances(OddSoakers, OddHelpers, true);
            ConstrainSpellTolerances(OddMeleeSoakers, OddMeleeHelpers, true);
            ConstrainSpellTolerances(EvenSoakers, EvenHelpers, false);
        }

        /// <summary>从一米圆形容差开始收缩，整个范围与两座塔的边界都至少相隔五厘米。</summary>
        private static SimPosition TowerCircle(Vector2 position, int insideTower = -1)
        {
            var radius = 1f;
            for (var tower = 0; tower < LocalTowerCenters.Length; tower++)
            {
                var distance = Vector2.Distance(position, LocalTowerCenters[tower]);
                radius = Math.Min(radius, tower == insideTower
                    ? TowerRadius - TowerEdgeMargin - distance
                    : distance - TowerRadius - TowerEdgeMargin);
            }
            if (radius < 0) throw new InvalidOperationException("遗弃末世点位未留足塔边界余量，无法定义圆形容差。");
            return SimPosition.Ellipse(position, radius);
        }

        /// <summary>按原点位确定角度；最外半径处的切向半宽为半米，可向另一点收进半个展角。</summary>
        private static SimPosition TowerArc(Vector2 position, int tower, float radius, float radialTolerance, Vector2? towards = null)
        {
            var center = LocalTowerCenters[tower];
            var heading = Math.Atan2(position.X - center.X, position.Y - center.Y);
            var halfAngle = Math.Asin(0.5 / (radius + radialTolerance));
            if (towards.HasValue)
            {
                var other = towards.Value - center;
                var difference = Math.IEEERemainder(Math.Atan2(other.X, other.Y) - heading, 2 * Math.PI);
                heading += Math.Sign(difference) * halfAngle;
            }
            return SimPosition.AnnularSector(center, radius, heading, radialTolerance, halfAngle);
        }

        private static Vector2 AlongTower(Vector2 position, int tower, float arcLength)
        {
            var offset = position - LocalTowerCenters[tower];
            var angle = arcLength / offset.Length();
            return LocalTowerCenters[tower] + new Vector2(
                (float)(offset.X * Math.Cos(angle) - offset.Y * Math.Sin(angle)),
                (float)(offset.X * Math.Sin(angle) + offset.Y * Math.Cos(angle)));
        }

        /// <summary>只在初始化时收缩三套固定布局；运行时各人独立取点，不重抽队伍或改变判定。</summary>
        private static void ConstrainSpellTolerances(SimPosition[] soakers, SimPosition[] helpers, bool odd)
        {
            var original = soakers.Concat(helpers).ToArray();
            var reference = original.Select(position => position.Midpoint()).ToArray();
            var fanCount = odd ? 1 : 2;
            var baits = Enumerable.Range(0, fanCount).Select(source => Enumerable.Range(0, MemberCount)
                .Where(slot => slot != source).OrderBy(slot => Vector2.DistanceSquared(reference[source], reference[slot])).First()).ToArray();
            var expected = new bool[4, MemberCount];
            for (var source = fanCount; source < 4; source++)
                for (var slot = 0; slot < MemberCount; slot++)
                    expected[source, slot] = Vector2.Distance(reference[source], reference[slot]) <= SpellRadius;

            var scales = new float[MemberCount];
            Func<SimPosition[]> build = () => original.Select((position, slot) =>
            {
                double? heading = null;
                if (!odd && slot < 2)
                {
                    // 缩角时仍固定踩一、踩二外侧的角边界，不能悄悄离开原点位对应的射线。
                    var edge = EvenLeftOffsets[slot] - LocalTowerCenters[0];
                    var angle = Math.Atan2(edge.X, edge.Y);
                    heading = angle + Math.IEEERemainder(position.Heading - angle, 2 * Math.PI) * scales[slot];
                }
                return position.ScaleTolerance(scales[slot], heading);
            }).ToArray();
            Func<bool> safe = () => IndependentSpellsAreSafe(build(), odd, baits, expected);
            if (!safe()) throw new InvalidOperationException("遗弃末世布局在零容差下仍存在重复受击，需先修正点位。");

            // 先保留共同可用比例，再分别放宽各点，避免一个紧张的点位压缩所有人的容差。
            var lower = 0f;
            var upper = 1f;
            for (var iteration = 0; iteration < 18; iteration++)
            {
                var scale = (lower + upper) * 0.5f;
                for (var slot = 0; slot < MemberCount; slot++) scales[slot] = scale;
                if (safe()) lower = scale; else upper = scale;
            }
            for (var slot = 0; slot < MemberCount; slot++) scales[slot] = lower;
            for (var slot = 0; slot < MemberCount; slot++)
            {
                var min = scales[slot];
                var max = 1f;
                for (var iteration = 0; iteration < 18; iteration++)
                {
                    scales[slot] = (min + max) * 0.5f;
                    if (safe()) min = scales[slot]; else max = scales[slot];
                }
                scales[slot] = min;
            }
            var constrained = build();
            if (!IndependentSpellsAreSafe(constrained, odd, baits, expected))
                throw new InvalidOperationException("遗弃末世容差收缩后仍无法保证独立取点安全。");
            Array.Copy(constrained, 0, soakers, 0, soakers.Length);
            Array.Copy(constrained, soakers.Length, helpers, 0, helpers.Length);
        }

        /// <summary>使用代表点周围的包围圆证明完整范围安全，而不是依赖少量随机样本碰巧通过。</summary>
        private static bool IndependentSpellsAreSafe(SimPosition[] positions, bool odd, int[] baits, bool[,] expected)
        {
            const double margin = 0.05;
            var points = positions.Select(position => position.Midpoint()).ToArray();
            var deviation = positions.Select(position => position.DeviationRadius).ToArray();
            for (var source = 0; source < 4; source++)
            {
                if (source >= baits.Length)
                {
                    for (var slot = 0; slot < MemberCount; slot++)
                    {
                        if (slot == source) continue;
                        var distance = Vector2.Distance(points[source], points[slot]);
                        var uncertainty = deviation[source] + deviation[slot];
                        // 分摊必须始终带上指定队员，也必须始终排除后方引导扇形的闲人。
                        if (expected[source, slot] ? distance + uncertainty > SpellRadius - margin
                            : distance - uncertainty < SpellRadius + margin) return false;
                    }
                    continue;
                }
                var bait = baits[source];
                var direction = points[bait] - points[source];
                var distanceToBait = direction.Length();
                var baitUncertainty = deviation[source] + deviation[bait];
                // 独立偏移后的引导角度不超过既有三十度限制。
                if (baitUncertainty > distanceToBait * 0.5 - margin) return false;
                var baitAngle = Math.Asin(baitUncertainty / distanceToBait);
                for (var slot = 0; slot < MemberCount; slot++)
                {
                    if (slot == source || slot == bait) continue;
                    var other = points[slot] - points[source];
                    var distance = other.Length();
                    var uncertainty = deviation[source] + deviation[slot];
                    if (distanceToBait + baitUncertainty + margin >= distance - uncertainty) return false;
                    var angle = Math.Abs(Math.Atan2(direction.X * other.Y - direction.Y * other.X, Vector2.Dot(direction, other)));
                    if (uncertainty >= distance || angle <= Math.PI / 4 + baitAngle + Math.Asin(uncertainty / distance) + 0.001)
                        return false;
                }
            }
            if (odd) return true;
            for (var helper = 4; helper < MemberCount; helper++)
                for (var slot = 0; slot < MemberCount; slot++)
                {
                    if (slot == helper) continue;
                    if (Vector2.Distance(points[helper], points[slot]) - deviation[helper] - deviation[slot] < SpellRadius + margin)
                        return false;
                    if (slot < 4 && points[helper].Length() + deviation[helper] + margin >= points[slot].Length() - deviation[slot])
                        return false;
                }
            return true;
        }

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
                var localPositions = new SimPosition[MemberCount];
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
                    .ThenBy(slot => localPositions[slot].Midpoint().X).ThenBy(slot => slot).ToArray();
                if (GroupAByRound[index]) activeA = nextActive;
                else activeB = nextActive;

                var future = !odd && random.Next(2) == 0;
                var nextNorth = NormalizeDirection(initialNorth + rotation * (index + 1));
                var bait = index == 7
                    ? Center + new Vector2(0, -8)
                    : Transform(new Vector2(0, future ? -8 : 8), nextNorth);
                plan.Rounds[index] = new ForsakenRoundPlan(
                    index + 1, north, (int[])active.Clone(), towerBySlot,
                    localPositions.Select(position => position.WithPosition(Transform(position.Pos, north))).ToArray(),
                    LocalTowerCenters.Select(position => Transform(position, north)).ToArray(),
                    new[] { (byte)(NormalizeDirection(5 + north) + 1), (byte)(NormalizeDirection(3 + north) + 1) },
                    beforeMarkers, (ForsakenMarker[])markers.Clone(), beforeStacks, (int[])stacks.Clone(),
                    future, bait, index == 7 && future ? Center + new Vector2(0, 5) : bait);
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
        /// <summary>本轮相对北在场地中的 ACT 朝向；点位范围使用它解释相对角度。</summary>
        internal double NorthHeading => Math.PI - NorthDirection * Math.PI / 4;
        internal int[] Soakers { get; }
        internal int[] TowerBySlot { get; }
        /// <summary>实际移动使用的容差范围；角度相对于 NorthHeading，不随中心坐标重复旋转。</summary>
        internal SimPosition[] SafePositions { get; }
        /// <summary>范围的代表落点，仅用于顺位、解法名单与诊断；不作为随机落点缓存。</summary>
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

        /// <summary>前三次按下一轮塔的相对北集合，最后一次按场地正北；最终前刀单独去背面圆形范围。</summary>
        internal SimPosition GatheringPosition(bool melee, bool finalDodge = false)
        {
            if (Number == 8 && FutureEnd && finalDodge) return SimPosition.Ellipse(FinalDodgePosition, 3);
            var heading = Number == 8 || FutureEnd ? Math.PI : 0;
            return SimPosition.AnnularSector(ForsakenPlan.Center, melee ? 8 : 9.5f, heading, melee ? 1 : 1.5f, 8 * Math.PI / 180);
        }

        internal ForsakenRoundPlan(int number, int northDirection, int[] soakers, int[] towerBySlot,
            SimPosition[] safePositions, Vector2[] towerCenters, byte[] towerMapIndices,
            ForsakenMarker[] markersBefore, ForsakenMarker[] markersAfter, int[] stacksBefore, int[] stacksAfter,
            bool futureEnd, Vector2 baitPosition, Vector2 finalDodgePosition)
        {
            Number = number;
            NorthDirection = northDirection;
            Soakers = soakers;
            TowerBySlot = towerBySlot;
            SafePositions = safePositions;
            Positions = safePositions.Select(position => position.Midpoint(NorthHeading)).ToArray();
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
