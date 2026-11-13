using Simulant.Content.U6b;
using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Core.Environment;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Simulation;
using Simulant.Simulation.Options;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Simulant.Content.U6b.Presets
{
    internal class 宇宙天箭 : SimPresetBase
    {
        public override int TerritoryId { get; } = 1122;
        public override string Name { get; } = "P6 宇宙天箭";
        public override string Author { get; } = "阿洛";
        public override DateTime LastUpdated { get; } = new DateTime(2026, 5, 12);
        public override PhaseData Phase { get; } = U6b.P6;
        public override WaymarkLayout Waymarks => U6b.Waymarks;
        public override int Level => 90;
        public override Type SimLogicType { get; } = typeof(U6b6_宇宙天箭Logic);
        public override string Description { get; } = "目前仅实现了宇宙天箭部分，并未实现分散/分摊后续机制。";

        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            new ComboBoxOption<ArrowMode>(
                nameof(U6b6_宇宙天箭Logic.ArrowMode), 
                "宇宙天箭模式", 
                ArrowMode.Random,
                new Map<ArrowMode>
                {
                    [ArrowMode.Random] = "随机",
                    [ArrowMode.OutsideFirst] = "先外侧",
                    [ArrowMode.InsideFirst] = "先内侧",
                }),
        };
    }

    internal enum ArrowMode
    {
        [RandomEnum(OutsideFirst, InsideFirst)]
        Random,
        OutsideFirst,
        InsideFirst,
    }

    internal class U6b6_宇宙天箭Logic : SimLogicBase
    {

        [SimOption]
        internal ArrowMode ArrowMode { get; set; }

        public U6b6_宇宙天箭Logic(PluginHost host, SimPresetBase preset) : base(host, preset)
        {
        }

        protected override void OnStop()
        {
        }

        protected override void BuildTimeline(SimTimeline timeline)
        {
            timeline.At(0, () => EntityManager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec
            {
                BNpcBaseId = 15725, // alpha omega
                BNpcNameId = 12256,
                Level = 90,
                Pos = new Vector3(100, 100, 0),
                Heading = (float)Math.PI,
            }, boss =>
            {
                var mechanic = new SimTimeline();
                mechanic.At(0, () => boss.Cast(0x7BA2), "宇宙天箭读条");
                if (ArrowMode == ArrowMode.OutsideFirst)
                {
                    mechanic.At(0, OutsideArrowTimeline());
                    mechanic.At(2, InsideArrowTimeline());
                }
                else
                {
                    mechanic.At(0, InsideArrowTimeline());
                    mechanic.At(2, OutsideArrowTimeline());
                }

                mechanic.At(25, () => EntityManager.Delete(boss), "回收本体");
                Scheduler.ScheduleAfter(3, () => Play(mechanic), "本体就绪后开始宇宙天箭");
            }), "生成宇宙天箭本体");
        }

        private SimTimeline InsideArrowTimeline()
        {
            var timeline = new SimTimeline();
            timeline.At(0, SingleArrowTimeline(new Vector3(80, 100, 0), (float)Math.PI / 2));
            timeline.At(0, SingleArrowTimeline(new Vector3(100, 80, 0), 0f));
            return timeline;
        }

        private SimTimeline OutsideArrowTimeline()
        {
            var timeline = new SimTimeline();
            timeline.At(0, SingleArrowTimeline(new Vector3(80, 85, 0), (float)Math.PI / 2));
            timeline.At(0, SingleArrowTimeline(new Vector3(80, 115, 0), (float)Math.PI / 2));
            timeline.At(0, SingleArrowTimeline(new Vector3(85, 80, 0), 0f));
            timeline.At(0, SingleArrowTimeline(new Vector3(115, 80, 0), 0f));
            return timeline;
        }

        /// <summary> 每个初始宇宙天箭宽读条及后续扩散窄直条的完整时间轴。 </summary>
        private SimTimeline SingleArrowTimeline(Vector3 pos, float heading)
        {
            var timeline = new SimTimeline();
            var Δt = 2.005;
            Vector3 dir = new Vector3(
                (float)Math.Cos(heading), 
                (float)Math.Sin(heading), 
                0);
            
            var t0 = 10.6;

            // 初始宽直条
            timeline.At(0, () => WideArrow(pos, heading), "宇宙天箭宽直条");

            for (int step = 0; step < 10; step++)
            {
                var offset = 7.5f + 5f * step;
                var pos1 = pos + dir * offset;
                var pos2 = pos - dir * offset;

                var valid1 = IsInsideArena(pos1);
                var valid2 = IsInsideArena(pos2);
                if (!valid1 && !valid2)
                    break;

                timeline.At(t0 + Δt * step, () =>
                {
                    if (valid1) DummyExecute(pos1, heading, 0x7BA4, 3f);
                    if (valid2) DummyExecute(pos2, heading, 0x7BA4, 3f);
                }, $"宇宙天箭扩散 {step + 1}");
            }

            return timeline;
        }

        private void WideArrow(Vector3 pos, float heading)
        {
            DummyCast(pos, heading, 0x7BA3, 10f); // omen
            DummyCast(pos, heading, 0x7E51, 10f); // arrows vfx
        }

        private static bool IsInsideArena(Vector3 pos)
            => Math.Abs(pos.X - 100) <= 21 && Math.Abs(pos.Y - 100) <= 21;

    }
}
