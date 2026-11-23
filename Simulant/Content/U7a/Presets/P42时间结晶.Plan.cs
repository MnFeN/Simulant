using Simulant.Simulation.Geometry;
using Simulant.Simulation.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static Simulant.Utils.Maths.Angles;

namespace Simulant.Content.U7a.Presets
{
    internal enum CrystalDebuff
    {
        [RandomEnum(RandomRed, RandomBlue)]
        Random,
        [RandomEnum(RedWind, RedIce)]
        RandomRed,
        [RandomEnum(BlueWater, BlueEarth, BlueDark, BlueIce)]
        RandomBlue,
        RedWind,
        RedIce,
        BlueWater,
        BlueEarth,
        BlueDark,
        BlueIce
    }

    internal enum CrystalWave
    {
        [RandomEnum(LeftUp, LeftDown, RightUp, RightDown)]
        Random,
        LeftUp,
        LeftDown,
        RightUp,
        RightDown
    }

    internal enum CrystalPurple
    {
        [RandomEnum(MainDiagonal, AntiDiagonal)]
        Random = -1,
        MainDiagonal = 0,
        AntiDiagonal
    }

    internal enum CrystalSolution
    {
        Knockback,
        YAntiKnockback
    }

    /// <summary>时间结晶的点名、范围与解法坐标。只描述机制约束，实际移动由模拟角色处理。</summary>
    internal sealed class P42时间结晶Plan
    {
        internal static readonly Vector2 Center = new Vector2(100, 100);
        internal static readonly string[] Roles =
        {
            "MT",
            "ST",
            "H1",
            "H2",
            "D1",
            "D2",
            "D3",
            "D4"
        };
        internal static readonly int[] LeftParty =
        {
            0,
            2,
            4,
            6
        };
        internal static readonly int[] RightParty =
        {
            1,
            3,
            5,
            7
        };
        // 各位分别对应 MT、ST、H1、H2、D1–D4 的优先级，数值越小越靠左。
        internal const string DefaultRedPriority = "34125678";
        internal readonly CrystalDebuff[] Debuffs;
        internal readonly int[] RedSide = new int[8];
        internal readonly int NorthSlowSide, FirstSide, SecondSide;
        internal readonly int[] BlueOrder;
        internal readonly CrystalSolution Solution;
        internal bool IsY => Solution == CrystalSolution.YAntiKnockback;
        internal const double RewindTime = 43.759;
        internal const double ReturnDebuffEnd = 50.759;
        internal const double SpreadArrival = 47.25;

        internal P42时间结晶Plan(int self, CrystalDebuff selected, CrystalWave wave, CrystalPurple purple, Random random,
            string redPriority = DefaultRedPriority, CrystalSolution solution = CrystalSolution.Knockback)
        {
            Solution = solution;
            if (self < 0 || self >= 8 || selected < CrystalDebuff.RedWind || selected > CrystalDebuff.BlueIce || wave == CrystalWave.Random || purple == CrystalPurple.Random)
                throw new ArgumentException("时间结晶的职能、点名或光波方向尚未确定。");

            if (redPriority == null || !redPriority.OrderBy(rank => rank).SequenceEqual("12345678"))
                throw new ArgumentException("红组优先级必须为 1–8 的不重复排列。", nameof(redPriority));

            var pool = new[]
            {
                CrystalDebuff.RedIce,
                CrystalDebuff.RedIce,
                CrystalDebuff.RedWind,
                CrystalDebuff.RedWind,
                CrystalDebuff.BlueWater,
                CrystalDebuff.BlueEarth,
                CrystalDebuff.BlueDark,
                CrystalDebuff.BlueIce
            }.ToList();
            pool.Remove(selected);
            Debuffs = new CrystalDebuff[8];

            for (var i = 0; i < 8; i++)
            {
                if (i == self)
                {
                    Debuffs[i] = selected;
                    continue;
                }

                var index = random.Next(pool.Count);
                Debuffs[i] = pool[index];
                pool.RemoveAt(index);
            }

            foreach (var kind in new[]
            {
                CrystalDebuff.RedIce,
                CrystalDebuff.RedWind
            }

            )
            {
                var pair = Enumerable.Range(0, 8).Where(slot => Debuffs[slot] == kind).OrderBy(slot => redPriority[slot]).ToArray();
                RedSide[pair[0]] = -1;
                RedSide[pair[1]] = 1;
            }

            NorthSlowSide = purple == CrystalPurple.MainDiagonal ? -1 : 1;
            FirstSide = wave == CrystalWave.LeftUp || wave == CrystalWave.LeftDown ? -1 : 1;
            SecondSide = wave == CrystalWave.LeftUp || wave == CrystalWave.RightUp ? -1 : 1;
            BlueOrder = new[]
            {
                Slot(CrystalDebuff.BlueEarth),
                Slot(CrystalDebuff.BlueWater),
                Slot(CrystalDebuff.BlueIce),
                Slot(CrystalDebuff.BlueDark)
            };
        }

        internal int Slot(CrystalDebuff kind) => Array.IndexOf(Debuffs, kind);
        internal int Red(CrystalDebuff kind, int side) => Enumerable.Range(0, 8).Single(i => Debuffs[i] == kind && RedSide[i] == side);
        internal bool IsRed(int slot) => RedSide[slot] != 0;
        internal int[] WaterGroup => new[]
        {
            Slot(CrystalDebuff.BlueWater),
            Slot(CrystalDebuff.BlueEarth),
            Slot(CrystalDebuff.BlueIce),
            Red(CrystalDebuff.RedWind, -NorthSlowSide)
        };
        internal int[] EarthGroup => BlueOrder.Concat(new[]
        {
            Red(CrystalDebuff.RedIce, NorthSlowSide)
        }).ToArray();

        internal static ushort Status(CrystalDebuff kind) => kind == CrystalDebuff.RedWind ? (ushort)2463 :
            kind == CrystalDebuff.BlueWater ? (ushort)2461 : kind == CrystalDebuff.BlueEarth ? (ushort)2454 :
            kind == CrystalDebuff.BlueDark ? (ushort)2460 : (ushort)2462;
        internal static string Label(CrystalDebuff kind) => kind == CrystalDebuff.RedWind ? "红风" : kind == CrystalDebuff.RedIce ? "红冰" :
            kind == CrystalDebuff.BlueWater ? "蓝水" : kind == CrystalDebuff.BlueEarth ? "蓝土" : kind == CrystalDebuff.BlueDark ? "蓝暗" : "蓝冰";
        internal static Vector2 Polar(float radius, double degrees) => Center + Direction(degrees) * radius;
        internal static Vector2 Direction(double degrees) => new Vector2((float)Math.Sin(Deg(degrees)), (float)Math.Cos(Deg(degrees)));
        internal static SimPosition Circle(Vector2 point, float tolerance = 1) => SimPosition.Ellipse(point, tolerance);
        internal static SimPosition Sector(float radius, double degrees, float dr = 0.3f, double dtheta = 0.6)
            => SimPosition.AnnularSector(Center, radius, Deg(degrees), dr, Deg(dtheta));

        internal SimPosition Initial(int slot)
        {
            if (Debuffs[slot] == CrystalDebuff.RedIce)
                return Circle(Polar(13, 90 * RedSide[slot]), 0.8f);

            // 开场暗点名先避开第一轮沙漏；五人土分摊在沙漏结算后另行集合。
            if (Debuffs[slot] == CrystalDebuff.BlueDark)
                return Corner(180, -NorthSlowSide);
            return Corner(0, IsRed(slot) ? RedSide[slot] : -NorthSlowSide);
        }

        /// <summary>
        /// 沙漏与场地边缘的交点处的角落安全区，以交点前方指向开口的扇形定义。
        /// </summary>
        private static SimPosition Corner(double sandHeading, int openingSide)
        {
            // 以南侧沙漏右方为模板，从场边与沙漏边界的交点朝原站位前移 0.3 米作圆心。
            var origin = Center + 16.904352f * Direction(sandHeading)
                + (openingSide * 10.502559f) * Direction(sandHeading + 90);
            // 取 0–2.7 米与 ±13°，离场边和沙漏最近约为 0.064 米和 0.048 米。
            return SimPosition.AnnularSector(origin, 1.35f, Deg(sandHeading + openingSide * 138), 1.35f, Deg(13));
        }
        internal SimPosition Stack => Sector(16, 135 * NorthSlowSide, 3, 7);
        // 点名者缩小随机区；距水晶的最小距离约 11.15 米，与参与者最远相距 4.97 米。
        internal SimPosition StackFor(int slot) => Debuffs[slot] == CrystalDebuff.BlueEarth
            ? Sector(16, 135 * NorthSlowSide, 1, 3) : Stack;
        // 跨线窗口不足一秒：等待区靠近中线，南北保留随机性，东西限制最大横移路程。
        internal SimPosition NorthRest => SimPosition.Ellipse(Center + new Vector2(-3 * NorthSlowSide, -6.5f), 0.9f, 0.35f);
        // 红风留在正南，先横向躲光波，不能从另一名红风的撞头位置穿回场中。
        internal SimPosition SouthRest => SimPosition.Ellipse(Canonical(-2, 18.5f), 0.4f, 0.25f);

        internal SimPosition Wind(int side) => Circle(Polar(19, 30 * side), 0.9f);
        internal SimPosition WindApproach => Circle(Polar(17.5f, -30 * NorthSlowSide), 0.9f);
        internal SimPosition WindRider(Vector2 wind)
        {
            // 随实际红风位置平移，中轴固定朝场内，不随红风偏离预设中心而转向。
            return SimPosition.AnnularSector(wind, 2, Math.PI - Deg(35 * NorthSlowSide),
                0.9f, Deg(15));
        }
        internal SimPosition RedAfterIce(int side)
        {
            if (side == NorthSlowSide)
                return Stack;

            return Corner(side * 120, -side);
        }

        internal SimPosition WindWait => Corner(NorthSlowSide * 60, -NorthSlowSide);
        internal SimPosition RedIceNorth => Circle(Center + new Vector2(0, -16));

        internal IEnumerable<Move> RedIceRoute(int side, double contactTime)
        {
            // 第一撞成功就撤离；北侧先炸等沙漏结束，南侧先炸则先完成五人分摊。
            yield return new Move(contactTime, 27.25, RedAfterIce(side));
            yield return new Move(side == NorthSlowSide ? 27.98 : SandHits[1] + 0.08, 31.1, RedIceNorth);
        }
        internal IEnumerable<Move> RedWindRoute(Vector2 position, double contactTime)
        {
            // 保持撞头时的横坐标，竖直向南撤到半径 19 米处，再沿南侧靠近中线。
            var x = position.X - Center.X;
            var edge = new Vector2(position.X, Center.Y + (float)Math.Sqrt(Math.Max(0, 19 * 19 - x * x)));
            var retreatEnd = contactTime + Vector2.Distance(position, edge) / 6 + 0.2;
            yield return new Move(contactTime, retreatEnd, SimPosition.Ellipse(edge, 0.25f, 0));
            yield return new Move(retreatEnd, 32.1, SouthRest);
        }

        internal SimPosition RedHead(int side) => Circle(Polar(13, 33 * side), 0.9f);

        internal Vector2 Hourglass(int index) => Polar(11, index * 60);
        internal int HourglassWave(int index)
        {
            if (index == 0 || index == 3)
                return 0;

            var point = Hourglass(index) - Center;
            return Math.Sign(point.X) == Math.Sign(-point.Y) * NorthSlowSide ? 2 : 1;
        }

        internal static readonly double[] SandCasts =
        {
            20.472, 26.004, 31.191
        };
        internal static readonly double[][] WaveCasts =
        {
            new[]
            {
                28.355, 31.391, 33.456, 35.507
            },
            new[]
            {
                34.706, 37.790, 39.858, 41.942
            }
        };

        internal static readonly double[] SandHits =
        {
            21.955,
            27.436,
            32.423
        };
        internal static readonly double[][] WaveHits =
        {
            new[]
            {
                31.324,
                33.373,
                35.423,
                37.507
            },
            new[]
            {
                37.723,
                39.791,
                41.826,
                43.842
            }
        };
        internal Vector2 WaveStart(int wave) => Center + (wave == 0 ? new Vector2(FirstSide * 20, 0) : new Vector2(0, SecondSide * 20));
        internal Vector2 WaveDirection(int wave) => wave == 0 ? new Vector2(-FirstSide, 0) : new Vector2(0, -SecondSide);
        internal Vector2 WavePosition(int wave, int step) => WaveStart(wave) + WaveDirection(wave) * (step * 10);
        internal Vector2 HeadPosition(int side, double time) => Polar(13, side * (171 - Math.Max(0, time - 18.9) * 13));
        internal Vector2 PuddleDefault(int number) => number == 1 ? Polar(13, 90) : number == 2 ? Polar(13, 33) : number == 3 ? Polar(13, -33) : Polar(13, -90);
        internal int PuddleNumber(int red) => Debuffs[red] == CrystalDebuff.RedIce ? (RedSide[red] > 0 ? 1 : 4) : (RedSide[red] > 0 ? 2 : 3);
        internal Vector2 Canonical(float x, float y) => Center + new Vector2(-FirstSide * x, y);
        // 南北向光波的起点为相对北；起点在南时整体旋转半圈，左右也随之交换。
        internal Vector2 RelativeNorth(float x, float y) => Center - SecondSide * new Vector2(x, y);

        internal readonly struct Move
        {
            internal readonly double Start, Arrival;
            internal readonly SimPosition Area;
            internal Move(double start, double arrival, SimPosition area)
            {
                Start = start;
                Arrival = arrival;
                Area = area;
            }
        }

        /// <summary>光波期间的转折点属于机制约束；沿途避开尚未爆炸的条带。</summary>
        internal IEnumerable<Move> RewindRoute(int slot, Vector2 position, Vector2[] puddles)
        {
            // 最后一组沙漏结束后立即跨过中线，必须在下一道光波前完成，而非只赶在后续转向前。
            var redIce = Debuffs[slot] == CrystalDebuff.RedIce;
            // 红风仍在正南横向跨线；下一道横向光波结束后再进场内，此时最后的沙漏已经炸完。
            // 蓝色同样沿当前南北位置跨线，避免斜跑拖慢东西方向的避让。
            yield return new Move(32.46, 33.05, SimPosition.Ellipse(Canonical(2, redIce ? -14 : position.Y - 100), 0.8f, 0.3f));

            if (IsRed(slot))
                yield return new Move(33.48, 36.8, Circle(Canonical(-10, 7.5f)));
            else
            {
                var number = Array.IndexOf(BlueOrder, slot) + 1;
                var point = puddles[number - 1];
                var x = (point.X - 100) * -FirstSide;

                if ((number == 2 || number == 3) && x > 0)
                {
                    yield return new Move(33.48, 35.15, Circle(Canonical(-1.1f, 6)));
                    yield return new Move(35.5, 36.95, Circle(point, 0.9f));
                }
                else
                    yield return new Move(33.48, x > 0 ? 35.05 : number == 2 || number == 3 ? 36.75 : 36.6, Circle(point, 0.9f));
            }

            var staging = Canonical(-10, SecondSide > 0 ? 13 : 5);
            if (IsY)
                staging.X = Rewind(slot).Midpoint().X;

            yield return new Move(37.8, IsY ? 39.7 : 41, Circle(staging));
            yield return new Move(39.92, IsY ? 41.5 : 43.6, Rewind(slot));
        }

        internal Move LeavePuddle(Vector2 position, double now)
        {
            var x = (position.X - 100) * -FirstSide;
            var arrival = SecondSide > 0 && position.Y > 109 ? 37.25 : 38.6;
            return new Move(Math.Max(now, x > 10 ? 35.5 : now), arrival, Circle(Canonical(x > 0 ? 4 : -10, SecondSide > 0 ? 6 : 7.5f)));
        }

        internal SimPosition Rewind(int slot)
        {
            if (IsY)
            {
                // 通用斜向标点半径为 10 米；双 T 站在标点相对北侧 2 米，人群在相对北侧 4 米。
                var diagonal = 10 / (float)Math.Sqrt(2);
                return Circle(slot < 2 ? RelativeNorth(slot == 0 ? -diagonal : diagonal, -diagonal - 2) : RelativeNorth(0, -4), 2);
            }

            var left = LeftParty.Contains(slot);
            // 两列在两个坐标轴上的前后关系相反；各轴第一人是坦克，前四人互补。
            var firstGroup = (FirstSide == SecondSide) == left;
            var rank = Array.IndexOf(left ? LeftParty : RightParty, slot);
            var front = rank == 0 ? 14.5f : 11.5f + (rank - 1) * 0.2f;
            var back = rank == 0 ? 9 : 8.5f;
            return Circle(Center + new Vector2(FirstSide * (firstGroup ? front : back), SecondSide * (firstGroup ? back : front)));
        }

        internal SimPosition Spread(int slot)
        {
            if (IsY)
            {
                // 按示意图取对称站位，再为完整的 2 米容差留足电网、水晶和分散间距。
                var point = slot < 2 ? new Vector2(11, -14) : slot < 4 ? new Vector2(5, -1) :
                    slot < 6 ? new Vector2(13.5f, -5) : new Vector2(9, 9);
                return Circle(RelativeNorth(slot % 2 == 0 ? -point.X : point.X, point.Y), 2);
            }

            var firstGroup = (FirstSide == SecondSide) == LeftParty.Contains(slot);
            // 双 T 贴边，近战留在侧后，治疗略退，远程展开；两列按原回返分工就近散开。
            var position = slot < 2 ? (firstGroup ? new Vector2(16, 8) : new Vector2(11, 14)) :
                slot < 4 ? (firstGroup ? new Vector2(13, -4) : new Vector2(-7, 9)) :
                slot < 6 ? (firstGroup ? new Vector2(6, -2) : new Vector2(-5, 2)) :
                firstGroup ? new Vector2(10, -12) : new Vector2(-15, 5);
            return Circle(Center + new Vector2(FirstSide * position.X, SecondSide * position.Y), 1);
        }

        internal bool WingTanksCorrect(int[] order, int wave)
            => order.Take(IsY && wave == 1 ? 2 : 1).All(slot => slot < 2);

        internal static int SpiritTakerTarget(Vector2[] positions, int self, ActionArea area,
            Vector2? crystal, float crystalRadius, Random random)
        {
            if (crystal.HasValue && area.IntersectsCircle(crystal.Value, crystalRadius, positions[self]))
                return self;

            var nearby = Enumerable.Range(0, positions.Length)
                .Where(slot => slot != self && area.Contains(positions[self], positions[slot], 0)).ToArray();
            return nearby.Length != 0 ? nearby[random.Next(nearby.Length)] : random.Next(positions.Length);
        }

        internal static SimPosition AkhMorn(int slot) => Circle(Center + new Vector2(LeftParty.Contains(slot) ? -5 : 5, 3));
        internal static SimPosition MornAfah => Circle(Center + new Vector2(0, 4));

        internal static int[] FrontFour(Vector2[] positions, int wave, int side)
            => Enumerable.Range(0, 8).OrderByDescending(i => side * (wave == 0 ? positions[i].X : positions[i].Y)).ToArray();
    }
}
