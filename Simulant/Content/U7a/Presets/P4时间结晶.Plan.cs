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
        MainDiagonal,
        OtherDiagonal
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

        internal CrystallizeTimePlan(int self, CrystalDebuff selected, CrystalWave wave, CrystalPurple purple, Random random,
            string redPriority = DefaultRedPriority)
        {
            if (self < 0 || self >= 8 || selected < CrystalDebuff.RedWind || selected > CrystalDebuff.BlueIce || wave == CrystalWave.Random)
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
            if (Debuffs[slot] == CrystalDebuff.RedIce)
                return Sector(13, 90 * RedSide[slot]);

            if (Debuffs[slot] == CrystalDebuff.BlueDark)
                return Stack;

            return Sector(19, 40 * (IsRed(slot) ? RedSide[slot] : -NorthSlowSide), 0.2f, 0.5);
        }

        internal SimPosition Stack => Sector(19, 140 * NorthSlowSide, 0.25f, 0.5);
        internal SimPosition NorthRest => Circle(Polar(6, -150 * NorthSlowSide), 0.2f);
        internal SimPosition SouthRest => Circle(Polar(6, 30 * NorthSlowSide), 0.2f);

        internal SimPosition Wind(int side) => Circle(Polar(19, 30 * side), 0.1f);
        internal static SimPosition WindRider(Vector2 wind)
        {
            var heading = Math.Atan2(Center.X - wind.X, Center.Y - wind.Y);

            // 以实际红风站位为圆心，避免独立随机后把 30 米击退的角度误差放大。
            return SimPosition.AnnularSector(wind, 2.5f, heading, 0.35f, 0.6 * Math.PI / 180);
        }

        internal SimPosition RedAfterIce(int side) => side == NorthSlowSide ? Sector(18.5f, 140 * side, 0.2f, 0.5) : Sector(19, 80 * side, 0.2f, 0.5);
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
            yield return new Move(32.46, 33.8, Circle(Canonical(1, Debuffs[slot] == CrystalDebuff.RedWind ? 5 : -5), 0.1f));

            if (IsRed(slot))
                yield return new Move(33.48, 36.8, Circle(Canonical(-10, 7.5f), 0.15f));
            else
            {
                var number = Array.IndexOf(BlueOrder, slot) + 1;
                var point = puddles[number - 1];
                var x = (point.X - 100) * -FirstSide;

                if ((number == 2 || number == 3) && x > 0)
                {
                    yield return new Move(33.24, 35.65, Circle(Canonical(-1, 6), 0.1f));
                    yield return new Move(35.5, 37.6, Circle(point, 0.15f));
                }
                else
                    yield return new Move(x > 0 ? 33.24 : 33.48, number == 2 || number == 3 ? 37.15 : 36.6, Circle(point, 0.15f));
            }

            yield return new Move(37.8, 41, Circle(Canonical(-10, SecondSide > 0 ? 13 : 5), 0.15f));
            yield return new Move(39.92, 43.6, Rewind(slot));
        }

        internal Move LeavePuddle(Vector2 position, double now)
        {
            var x = (position.X - 100) * -FirstSide;
            return new Move(Math.Max(now + 0.1, x > 10 ? 35.5 : now + 0.1), 38.6, Circle(Canonical(x > 0 ? 4 : -10, 7.5f), 0.1f));
        }

        internal SimPosition Rewind(int slot)
        {
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
            // 在回返象限展开，去掉贴近北侧水晶的一格；相邻间距 6 米。
            var x = new[]
            {
                13,
                13,
                7,
                7,
                13,
                7,
                1,
                1
            }[slot];
            var y = new[]
            {
                13,
                7,
                13,
                7,
                1,
                1,
                7,
                1
            }[slot];
            return Circle(Center + new Vector2(FirstSide * x, SecondSide * y), 0.3f);
        }

        internal static SimPosition AkhMorn(int slot) => Circle(Center + new Vector2(LeftParty.Contains(slot) ? -5 : 5, 3), 0.6f);
        internal static SimPosition MornAfah => Circle(Center + new Vector2(0, 4), 0.7f);

        internal static int[] FrontFour(Vector2[] positions, int wave, int side)
            => Enumerable.Range(0, 8).OrderByDescending(i => side * (wave == 0 ? positions[i].X : positions[i].Y)).ToArray();
    }
}
