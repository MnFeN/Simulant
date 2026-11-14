using Simulant.Simulation.Geometry;
using Simulant.Simulation.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

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
    internal sealed class CrystallizeTimePlan
    {
        internal static readonly Vector2 Center = new Vector2(100, 100);
        internal static readonly Vector2 Crystal = new Vector2(100, 84);
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

        internal CrystallizeTimePlan(int self, CrystalDebuff selected, CrystalWave wave, CrystalPurple purple, Random random,
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
        internal static Vector2 Direction(double degrees) => new Vector2((float)Math.Sin(degrees * Math.PI / 180), (float)Math.Cos(degrees * Math.PI / 180));
        internal static SimPosition Circle(Vector2 point, float tolerance = 0.3f) => SimPosition.Ellipse(point, tolerance);
        internal static SimPosition Sector(float radius, double degrees, float dr = 0.3f, double dtheta = 0.6)
            => SimPosition.AnnularSector(Center, radius, degrees * Math.PI / 180, dr, dtheta * Math.PI / 180);

        internal SimPosition Initial(int slot)
        {
            if (Debuffs[slot] == CrystalDebuff.RedIce) return Sector(13, 90 * RedSide[slot]);
            // 开场暗点名先避开第一轮沙漏；五人土分摊在沙漏结算后另行集合。
            if (Debuffs[slot] == CrystalDebuff.BlueDark)
                return Sector(19, 140 * NorthSlowSide, 0.25f, 0.5);
            return Sector(19, 40 * (IsRed(slot) ? RedSide[slot] : -NorthSlowSide), 0.2f, 0.5);
        }
        internal SimPosition Stack => Sector(16, 135 * NorthSlowSide, 2, 7);
        internal SimPosition NorthRest => Circle(Polar(6, -150 * NorthSlowSide), 0.2f);
        internal SimPosition SouthRest => Circle(Polar(6, 30 * NorthSlowSide), 0.2f);

        internal SimPosition Wind(int side) => Circle(Polar(19, 30 * side), 0.1f);
        internal SimPosition WindRider(Vector2 wind)
        {
            var heading = Math.Atan2(Center.X - wind.X, Center.Y - wind.Y);
            // 以实际红风站位为圆心，避免独立随机后把 30 米击退的角度误差放大。
            return SimPosition.AnnularSector(wind, 2.5f, heading, 0.35f, 0.6 * Math.PI / 180);
        }
        internal SimPosition RedAfterIce(int side)
        {
            if (side == NorthSlowSide)
                return Stack;

            // 半径 0.8 米的圆同时内切场边、外切北侧沙漏的 12 米范围，取靠侧边的交点。
            const double radius = 20 - 0.8;
            var offset = Math.Acos((radius * radius + 11 * 11 - 12.8 * 12.8) / (2 * radius * 11));
            var angle = 120 - offset * 180 / Math.PI;
            return Circle(Polar((float)radius, angle * side), 0.5f);
        }

        internal SimPosition RedIceNorth => Circle(Center + new Vector2(0, -16), 0.5f);

        internal IEnumerable<Move> RedIceRoute(int side, double contactTime)
        {
            // 第一撞成功就撤离；北侧先炸等沙漏结束，南侧先炸则先完成五人分摊。
            yield return new Move(contactTime, 27.25, RedAfterIce(side));
            yield return new Move(side == NorthSlowSide ? 27.98 : SandHits[1] + 0.08, 31.1, RedIceNorth);
        }
        internal SimPosition RedHead(int side) => Sector(13, 33 * side, 0.2f, 0.5);

        internal Vector2 Hourglass(int index) => Polar(11, index * 60);
        internal int HourglassWave(int index)
        {
            if (index == 0 || index == 3)
                return 0;

            var point = Hourglass(index) - Center;
            return Math.Sign(point.X) == Math.Sign(-point.Y) * NorthSlowSide ? 2 : 1;
        }

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
        internal IEnumerable<Move> RewindRoute(int slot, Vector2[] puddles)
        {
            // 最后一组沙漏结束后立即跨过中线，必须在下一道光波前完成，而非只赶在后续转向前。
            var redIce = Debuffs[slot] == CrystalDebuff.RedIce;
            yield return new Move(32.46, 33.05, Circle(Canonical(1, redIce ? -14 : Debuffs[slot] == CrystalDebuff.RedWind ? 5 : -5), 0.1f));

            if (IsRed(slot))
                yield return new Move(33.48, 36.8, Circle(Canonical(-10, 7.5f), 0.15f));
            else
            {
                var number = Array.IndexOf(BlueOrder, slot) + 1;
                var point = puddles[number - 1];
                var x = (point.X - 100) * -FirstSide;

                if ((number == 2 || number == 3) && x > 0)
                {
                    yield return new Move(33.48, 35.15, Circle(Canonical(-1, 6), 0.1f));
                    yield return new Move(35.5, 36.95, Circle(point, 0.15f));
                }
                else
                    yield return new Move(33.48, x > 0 ? 35.05 : number == 2 || number == 3 ? 36.75 : 36.6, Circle(point, 0.15f));
            }

            var staging = Canonical(-10, SecondSide > 0 ? 13 : 5);
            if (IsY)
                staging.X = Rewind(slot).Midpoint().X;

            yield return new Move(37.8, IsY ? 39.7 : 41, Circle(staging, 0.15f));
            yield return new Move(39.92, IsY ? 41.5 : 43.6, Rewind(slot));
        }

        internal Move LeavePuddle(Vector2 position, double now)
        {
            var x = (position.X - 100) * -FirstSide;
            var arrival = SecondSide > 0 && position.Y > 109 ? 37.25 : 38.6;
            return new Move(Math.Max(now, x > 10 ? 35.5 : now), arrival, Circle(Canonical(x > 0 ? 4 : -10, 7.5f), 0.1f));
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
            var front = rank == 0 ? 14f : 11.5f + (rank - 1) * 0.45f;
            var back = 8f + rank * 0.35f;
            return Circle(Center + new Vector2(FirstSide * (firstGroup ? front : back), SecondSide * (firstGroup ? back : front)), 0.12f);
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
            Vector2 crystal, float crystalRadius, Random random)
        {
            if (area.IntersectsCircle(crystal, crystalRadius, positions[self]))
                return self;

            var nearby = Enumerable.Range(0, positions.Length)
                .Where(slot => slot != self && area.Contains(positions[self], positions[slot], 0)).ToArray();
            return nearby.Length != 0 ? nearby[random.Next(nearby.Length)] : random.Next(positions.Length);
        }

        internal static SimPosition AkhMorn(int slot) => Circle(Center + new Vector2(LeftParty.Contains(slot) ? -5 : 5, 3), 0.6f);
        internal static SimPosition MornAfah => Circle(Center + new Vector2(0, 4), 0.7f);

        internal static int[] FrontFour(Vector2[] positions, int wave, int side)
            => Enumerable.Range(0, 8).OrderByDescending(i => side * (wave == 0 ? positions[i].X : positions[i].Y)).ToArray();
    }
}
