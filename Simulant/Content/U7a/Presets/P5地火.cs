using Simulant.Core;
using Simulant.Core.Environment;
using Simulant.Game;
using Simulant.Simulation;
using Simulant.Simulation.Options;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Numerics;
using static System.Math;

namespace Simulant.Content.U7a.Presets
{
    internal class P5地火 : SimPresetBase
    {
        public override int TerritoryId { get; } = 1238;
        public override string Name { get; } = "P5 地火";
        public override string Author { get; } = "阿洛";
        public override DateTime LastUpdated { get; } = new DateTime(2026, 5, 12);
        public override PhaseData Phase { get; } = U7a.P5;
        public override int Level => 100;
        public override Type SimLogicType { get; } = typeof(P5地火Logic);
        public override string Description { get; } = null;

        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            new ComboBoxOption<CenterDir4>(
                nameof(P5地火Logic.CenterDir4),
                "地火安全点相对于中心的方向",
                CenterDir4.Random,
                new Map<CenterDir4>
                {
                    [CenterDir4.Random] = "随机",
                    [CenterDir4.N] = "北 (0)",
                    [CenterDir4.W] = "西 (1)",
                    [CenterDir4.S] = "南 (2)",
                    [CenterDir4.E] = "东 (3)",
                }),

            new ComboBoxOption<FirstLineDir8>(
                nameof(P5地火Logic.FirstLineDir8),
                "地火第一根线相对于安全点的方向",
                FirstLineDir8.Random,
                new Map<FirstLineDir8>
                {
                    [FirstLineDir8.Random] = "随机",
                    [FirstLineDir8.N] = "北 (0)",
                    [FirstLineDir8.NW] = "西北 (1)",
                    [FirstLineDir8.W] = "西 (2)",
                    [FirstLineDir8.SW] = "西南 (3)",
                    [FirstLineDir8.S] = "南 (4)",
                    [FirstLineDir8.SE] = "东南 (5)",
                    [FirstLineDir8.E] = "东 (6)",
                    [FirstLineDir8.NE] = "东北 (7)",
                }),

            new ComboBoxOption<RotateDirection>(
                nameof(P5地火Logic.RotateDirection),
                "地火旋转方向",
                RotateDirection.Random,
                new Map<RotateDirection>
                {
                    [RotateDirection.Random] = "随机",
                    [RotateDirection.ClockWise] = "顺时针",
                    [RotateDirection.CounterClockwise] = "逆时针",
                }),
        };

    }

    internal enum CenterDir4
    {
        [RandomEnum(N, W, S, E)]
        Random = -1,
        N = 0, W = 1, S = 2, E = 3
    }

    internal enum FirstLineDir8
    {
        [RandomEnum(N, NW, W, SW, S, SE, E, NE)]
        Random = -1,
        N = 0, NW = 1, W = 2, SW = 3, S = 4, SE = 5, E = 6, NE = 7
    }

    internal enum RotateDirection
    {
        [RandomEnum(ClockWise, CounterClockwise)]
        Random,
        ClockWise,
        CounterClockwise
    }

    internal class P5地火Logic : SimLogicBase
    {
        [SimOption]
        internal CenterDir4 CenterDir4 { get; set; }

        [SimOption]
        internal FirstLineDir8 FirstLineDir8 { get; set; }

        [SimOption]
        internal RotateDirection RotateDirection { get; set; }

        public P5地火Logic(PluginHost host, SimPresetBase preset) : base(host, preset)
        {
        }

        protected override void OnStop()
        {
        }

        protected override void BuildTimeline(SimTimeline timeline)
        {
            var boss = EntityManager.SpawnBNpc(17839, 13561, 100); // Pandora
            boss.Pos3D = new Vector3(100, 100, 0);
            boss.Heading = (float)PI;
            boss.SetReadyToDraw();
            boss.EnableDraw();

            var mechanic = new SimTimeline();
            var lines = new List<Simulant.Core.Entity.EventObject>();
            mechanic.At(0, () =>
            {
                boss.Cast(0x9D72);
                // 读条的同时生成 EObj，但不播放动画。
                var eObjsPose = CalculateEObjsPose();
                for (int i = 0; i < 6; i++)
                {
                    var (pos, heading) = eObjsPose[i];
                    lines.Add(EntityManager.SpawnEObj((ushort)i, 2014199, pos, heading));
                }
            }, "地火读条与生成直线");
            mechanic.At(8, () => lines.ForEach(e => e.PlayAnimation(0x2)), "地火细线出现");
            mechanic.Repeat(10, 4, 3, group =>
            {
                Play(SingleTimelineStartFromGlowing(lines[group * 2]));
                Play(SingleTimelineStartFromGlowing(lines[group * 2 + 1]));
            }, "地火分组发光");
            mechanic.At(20, () => boss.Heading = (float)((int)CenterDir4 / 2.0 * PI - PI), "本体转向安全点");
            mechanic.At(25, () => boss.Cast(0x9D76), "双分摊读条");
            mechanic.At(42, () => EntityManager.Delete(boss), "回收本体");
            timeline.At(3, mechanic);
        }

        /// <summary> 每条地火直线从细线扩大开始的完整时间轴。 </summary>
        private SimTimeline SingleTimelineStartFromGlowing(Simulant.Core.Entity.EventObject lineEObj)
        {
            // 实体面向与直线垂直，面向光侧

            var timeline = new SimTimeline();
            var pos = lineEObj.Pos3D;
            var heading = lineEObj.Heading;
            var oppositeHeading = (float)(lineEObj.Heading + PI);

            timeline.At(0, () =>
            {
                _host.LogVerbose($"地火线 {lineEObj.Id} @ {lineEObj.Native.Ptr.Hex()} 开始时间轴: Pos = <{pos.X:F2}, {pos.Y:F2}>, Heading = {heading:F2}");
                lineEObj.PlayAnimation(0x10);
                // 初始的矩形 Omen，技能实际没有其他效果。
                DummyCast(pos, heading, 0x9D73, 10, 5); // 光
                DummyCast(pos, oppositeHeading, 0x9CB6, 10, 5); // 暗
            }, "地火光暗预兆");
            timeline.At(7, () => lineEObj.PlayAnimation(0x8), "地火箭头出现");
            timeline.At(7, StepsTimeline(pos, heading, isLight: true));
            timeline.At(7, StepsTimeline(pos, oppositeHeading, isLight: false));
            return timeline;
        }

        private SimTimeline StepsTimeline(Vector3 pos, float heading, bool isLight)
        {
            var timeline = new SimTimeline();
            var step = new Vector3((float)(5 * Sin(heading)), (float)(5 * Cos(heading)), 0);
            uint abilityId = isLight ? 0x9D74u : 0x9D75u;
            var center = new Vector3(100, 100, 0);

            for (int i = 1; i < 10; i++)
            {
                var currentPos = pos + step * i;
                if (Vector3.Distance(currentPos, center) > 21)
                    break;

                timeline.At(2 * i, () => DummyExecute(currentPos, heading, abilityId, 3),
                    $"{(isLight ? "光" : "暗")}地火第 {i} 步");
            }
            return timeline;
        }

        /// <summary>
        /// 计算绝伊甸地火八角星的 6 条边的地火实体相对于场中心的位姿。 
        /// </summary>
        /// <returns>
        /// 返回一个长度为 6 的数组。<br />
        /// 每个元素表示一条边的中心点及其方向角度。<br />
        /// 发光顺序为 01 - 23 - 45，间隔 4 秒。  
        /// </returns>
        (Vector3 pos, float heading)[] CalculateEObjsPose()
        {
            int centerDir4 = (int)CenterDir4;
            bool isClockWise = RotateDirection == RotateDirection.ClockWise;
            int firstLineDir8 = (int)FirstLineDir8;

            // 八角星中心方向（0~3）
            double theta4 = centerDir4 * (PI / 2) - PI;
            Vector2 octagonCenter = 7.0711f * new Vector2((float)Sin(theta4), (float)Cos(theta4)); // 八角星中心距离场地中心 5√2

            var result = new (Vector3 pos, float heading)[6];
            for (int i = 0; i < 6; i++)
            {
                // 当前方向（未取余至 0-7）
                int eObjDir8Raw = firstLineDir8 + (isClockWise ? -i : i);
                double eObjHeading = eObjDir8Raw * (PI / 4) - PI;

                var sin = (float)Sin(eObjHeading);
                var cos = (float)Cos(eObjHeading);

                // 八角星每条边的中心点
                Vector2 pos = octagonCenter + 10f * new Vector2(sin, cos);

                // 平移至场地中心到八角星此边直线垂足位置
                // 垂线方向单位向量 u
                var unit = new Vector2(cos, -sin);

                // 平移距离 k 至 (x+ku, y+ku), 使得 (x+ku) / (y+ku) = sinθ / cosθ, 求解 k
                var k = pos.Y * sin - pos.X * cos;
                pos += k * unit;

                // 令相邻两条线正对中心方向光暗交替
                if (eObjDir8Raw % 2 == 0)
                    eObjHeading += PI;

                var absolutePos = new Vector3(pos.X + 100, pos.Y + 100, 0);
                result[i] = (absolutePos, (float)eObjHeading);

                _host.LogVerbose($"地火线 {i} 计算结果：Pos = <{absolutePos.X:F2}, {absolutePos.Y:F2}>, heading = {result[i].heading:F2}");
            }

            return result;
        }


    }

}
