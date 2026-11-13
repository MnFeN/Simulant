using Simulant.Content.U7b.Presets;
using Simulant.Simulation.Geometry;
using System;
using System.Linq;
using System.Numerics;
using static Simulant.Content.U7b.Presets.ForsakenTiming;

namespace Simulant.Tests
{
    internal static class PositionToleranceTests
    {
        internal static void AnnularSectorUsesMidRadiusAndRelativeHeading()
        {
            var center = new Vector2(100, 100);
            var sector = SimPosition.AnnularSector(center, 4.5f, Math.PI / 2, 0.4f, Math.PI / 12);
            for (var direction = 0; direction < 8; direction++)
            {
                var north = Math.PI - direction * Math.PI / 4;
                var heading = north - Math.PI / 2;
                var middle = center + Direction(heading) * 4.5f;
                Assert(Vector2.Distance(sector.Midpoint(north), middle) < 0.0001f, "扇环代表落点必须位于中间半径和相对中轴上。");
                foreach (var radial in new[] { 0.0, 0.5, 1.0 })
                    foreach (var angular in new[] { 0.0, 0.5, 1.0 })
                    {
                        var point = sector.Sample(new FixedSample(radial, angular), north);
                        Assert(sector.Contains(point, north), "扇环边界取点必须落在同一相对北的范围内。");
                        var expected = center + Direction(heading + (angular * 2 - 1) * Math.PI / 12) * (float)(4.1 + radial * 0.8);
                        Assert(Vector2.Distance(point, expected) < 0.0001f, "扇环必须按半径与角度各自的正负半宽取点。");
                    }
                Assert(!sector.Contains(center + Direction(heading) * 4.099f, north), "内边界外的点不应被接纳。");
                Assert(!sector.Contains(center + Direction(heading) * 4.901f, north), "外边界外的点不应被接纳。");
                Assert(!sector.Contains(center + Direction(heading + Math.PI / 12 + 0.001) * 4.5f, north), "角边界外的点不应被接纳。");
                var shift = new Vector2(7, -9);
                Assert(Vector2.Distance(sector.WithPosition(center + shift).Midpoint(north), middle + shift) < 0.0001f,
                    "替换中心坐标不能再次旋转范围角度。");
            }
            Reject(() => SimPosition.AnnularSector(center, 1, 0, 2, 0));
            Reject(() => SimPosition.AnnularSector(center, 1, 0, -1, 0));
            Reject(() => SimPosition.AnnularSector(center, 1, 0, 0, Math.PI + 0.01));
            Reject(() => SimPosition.AnnularSector(center, float.MaxValue, 0, float.MaxValue, 0));
        }

        internal static void ForsakenTolerancesStayOnTheirAssignedSideOfEveryTower()
        {
            foreach (var optimize in new[] { false, true })
                foreach (var byRole in new[] { false, true })
                    for (var seed = 0; seed < 64; seed++)
                    {
                        var plan = ForsakenPlan.Create(new Random(seed), optimizeMelee: optimize, helpersByRole: byRole);
                        foreach (var round in plan.Rounds)
                            for (var slot = 0; slot < 8; slot++)
                            {
                                var area = round.SafePositions[slot];
                                Assert(area.Contains(round.Positions[slot], round.NorthHeading), "诊断代表点必须属于对应容差范围。");
                                foreach (var radial in new[] { 0.0, 0.5, 1.0 })
                                    for (var angle = 0; angle <= 16; angle++)
                                    {
                                        var point = area.Sample(new FixedSample(radial, angle / 16.0), round.NorthHeading);
                                        Assert(area.Contains(point, round.NorthHeading), "旋转后的容差边界取点必须仍在范围中。");
                                        for (var tower = 0; tower < 2; tower++)
                                        {
                                            var distance = Vector2.Distance(point, round.TowerCenters[tower]);
                                            Assert(round.TowerBySlot[slot] == tower ? distance <= 3.9501f : distance >= 4.0499f,
                                                "全部落点必须保持正确的塔内外关系，并至少留下五厘米余量。");
                                        }
                                    }
                            }
                    }
        }

        internal static void ForsakenLeftTowerArcsKeepTheirOriginalOuterAngularEdges()
        {
            var plan = ForsakenPlan.Create(new Random(1));
            var original = new[] { new Vector2(-9.51f, 6.27f), new Vector2(-8.30f, 8.52f) };
            foreach (var round in plan.Rounds.Where(round => round.Number % 2 == 0))
                for (var index = 0; index < 2; index++)
                {
                    var area = round.SafePositions[round.Soakers[index]];
                    var center = round.TowerCenters[0];
                    var direction = Vector2.Normalize(ForsakenPlan.Transform(original[index], round.NorthDirection) - center);
                    var edge = area.Sample(new FixedSample(0.5, index == 0 ? 0 : 1), round.NorthHeading);
                    Assert(Vector2.Distance(Vector2.Normalize(edge - center), direction) < 0.0001f,
                        "踩一、踩二各自靠外的角边界必须仍指向原点位。");
                    var axis = Vector2.Normalize(area.Midpoint(round.NorthHeading) - center);
                    var tangent = new Vector2(-axis.Y, axis.X);
                    var outerEdge = area.Sample(new FixedSample(1, 0), round.NorthHeading);
                    Assert(Math.Abs(Vector2.Dot(outerEdge - center, tangent)) <= 0.5001f,
                        "收缩后最外半径处的切向半宽不得大于半米。");
                    foreach (var radial in new[] { 0.0, 1.0 })
                    {
                        var distance = Vector2.Distance(area.Sample(new FixedSample(radial, 0.5), round.NorthHeading), center);
                        Assert(distance >= 3.3999f && distance <= 3.9001f, "踩一、踩二收缩后的半径范围必须仍在 3.4 至 3.9 米内。");
                    }
                }
        }

        internal static void ForsakenInitialAndGatheringRangesMatchRolesAndNorth()
        {
            for (var slot = 0; slot < 8; slot++)
            {
                foreach (var melee in new[] { true, false })
                {
                    var area = ForsakenPlan.InitialPosition(slot, melee);
                    var radius = melee ? 5 : 7;
                    foreach (var radial in new[] { 0.0, 1.0 })
                        foreach (var angular in new[] { 0.0, 1.0 })
                        {
                            var offset = area.Sample(new FixedSample(radial, angular)) - ForsakenPlan.Center;
                            Assert(Math.Abs(offset.Length() - (radius - 1 + 2 * radial)) < 0.0001f, "初始扇环的径向半宽必须为一米。");
                            var heading = Math.Atan2(offset.X, offset.Y);
                            Assert(Math.Abs(Math.Abs(heading - area.Heading) * 180 / Math.PI - 8.5) < 0.001,
                                "初始扇环的角度半宽必须为八点五度。");
                        }
                }
            }
            for (var seed = 0; seed < 32; seed++)
            {
                var plan = ForsakenPlan.Create(new Random(seed));
                foreach (var round in plan.Rounds.Where(round => round.Number % 2 == 0))
                    foreach (var melee in new[] { false, true })
                        foreach (var final in round.Number == 8 ? new[] { false, true } : new[] { false })
                        {
                            var area = round.GatheringPosition(melee, final);
                            var north = round.Number == 8 ? Math.PI : plan.Rounds[round.Number].NorthHeading;
                            if (round.Number == 8 && round.FutureEnd && final)
                            {
                                Assert(area.Pos == new Vector2(100, 105), "最终穿背圆心必须在场地南侧五米处。");
                                for (var angle = 0; angle < 16; angle++)
                                {
                                    var point = area.Sample(new FixedSample(1, angle / 16.0), north);
                                    Assert(Math.Abs(Vector2.Distance(point, area.Pos) - 3) < 0.0001f && point.Y > 100,
                                        "最终穿背的三米圆形容差必须完整处于背面。");
                                }
                                continue;
                            }
                            var expectedDirection = Vector2.Normalize((final ? round.FinalDodgePosition : round.BaitPosition) - ForsakenPlan.Center);
                            Assert(Vector2.Distance(Vector2.Normalize(area.Midpoint(north) - ForsakenPlan.Center), expectedDirection) < 0.0001f,
                                "集合和穿背的扇环中轴必须沿用各自正确的相对北方向。");
                            foreach (var radial in new[] { 0.0, 1.0 })
                                foreach (var angular in new[] { 0.0, 1.0 })
                                {
                                    var point = area.Sample(new FixedSample(radial, angular), north);
                                    var offset = point - ForsakenPlan.Center;
                                    var radius = melee ? 7 + radial * 2 : 8 + radial * 3;
                                    Assert(Math.Abs(offset.Length() - radius) < 0.0001f, "近战集合半径应为七至九米，远程应为八至十一米。");
                                    Assert(Math.Abs(Vector2.Dot(Vector2.Normalize(offset), expectedDirection) - Math.Cos(8 * Math.PI / 180)) < 0.0001,
                                        "集合扇环的角度半宽必须为八度。");
                                }
                        }
            }
        }

        private static Vector2 Direction(double heading) => new Vector2((float)Math.Sin(heading), (float)Math.Cos(heading));
        internal static void ForsakenToleranceRoutesFitNormalRunningSpeed()
        {
            for (var seed = 0; seed < 32; seed++)
            {
                var plan = ForsakenPlan.Create(new Random(seed), optimizeMelee: (seed & 1) != 0, helpersByRole: (seed & 2) == 0);
                var previous = Enumerable.Range(0, 8).Select(slot => ForsakenPlan.InitialPosition(slot, slot < 2 || slot == 4 || slot == 5)).ToArray();
                var previousNorth = Math.PI;
                for (var index = 0; index < 8; index++)
                {
                    var round = plan.Rounds[index];
                    for (var slot = 0; slot < 8; slot++)
                        AssertTravel(previous[slot], previousNorth, round.SafePositions[slot], round.NorthHeading,
                            TowerArrival(index) - TowerMovementAvailable(index), $"第 {index + 1} 轮塔位，队员 {slot}");
                    previous = round.SafePositions;
                    previousNorth = round.NorthHeading;
                    if (index % 2 == 0) continue;

                    var pair = index / 2;
                    var gatheringNorth = index == 7 ? Math.PI : plan.Rounds[index + 1].NorthHeading;
                    var gathering = Enumerable.Range(0, 8).Select(slot => round.GatheringPosition(slot < 2 || slot == 4 || slot == 5)).ToArray();
                    for (var slot = 0; slot < 8; slot++)
                    {
                        AssertTravel(round.SafePositions[slot], round.NorthHeading, gathering[slot], gatheringNorth,
                            GatheringArrival(pair) - SpellHit[index] - 0.15, $"第 {index + 1} 轮集合，队员 {slot}");
                        if (index == 7 && round.FutureEnd)
                            AssertTravel(gathering[slot], gatheringNorth, round.GatheringPosition(slot < 2 || slot == 4 || slot == 5, true), Math.PI,
                                RowMin(CleaveHit, pair) - RowMax(CleaveLock, pair) - 0.1, $"最终穿背，队员 {slot}");
                    }
                    previous = gathering;
                    previousNorth = gatheringNorth;
                }
            }
        }

        private static void AssertTravel(SimPosition from, double fromNorth, SimPosition to, double toNorth, double available, string description)
        {
            var origins = BoundarySamples(from, fromNorth);
            var destinations = BoundarySamples(to, toNorth);
            var longest = origins.Max(origin => destinations.Max(destination => Vector2.Distance(origin, destination)));
            // 与实体行为的单段路线一致：正常跑速，并计入两次共享轮询余量。
            Assert(longest / 6 + 0.4 <= available, $"{description} 的容差边界路线超时：需要 {longest / 6 + 0.4:0.000} 秒，仅有 {available:0.000} 秒。");
        }

        private static Vector2[] BoundarySamples(SimPosition area, double north)
            => new[] { 0.0, 1.0 }.SelectMany(radial => Enumerable.Range(0, 17)
                .Select(angle => area.Sample(new FixedSample(radial, angle / 16.0), north))).ToArray();

        private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Reject(Action action)
        {
            try { action(); }
            catch (ArgumentOutOfRangeException) { return; }
            throw new Exception("无效的扇环半径或半宽应被拒绝。");
        }

        /// <summary>按顺序提供径向和角度取样值；包含端点以覆盖完整边界。</summary>
        private sealed class FixedSample : Random
        {
            private readonly double _radial, _angular;
            private int _calls;
            internal FixedSample(double radial, double angular) { _radial = radial; _angular = angular; }
            public override double NextDouble() => _calls++ % 2 == 0 ? _radial : _angular;
        }
    }
}
