using Simulant.Core;
using Simulant.Core.Environment;
using Simulant.Simulation;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Options;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Simulant.Content.U7a.Presets
{
    [PhaseSelectionOrder(2.3)]
    internal sealed class P23光之失控 : SimPresetBase
    {
        public override int TerritoryId => 1238;
        public override string Name => "P2-3 光之失控";
        public override string Author => "阿洛";
        public override DateTime LastUpdated => new DateTime(2026, 11, 26);
        public override PhaseData Phase => U7a.P2;
        public override int Level => 100;
        public override Type SimLogicType => typeof(LightRampantLogic);
        public override WaymarkLayout Waymarks => U7a.Waymarks;
        public override string Description =>
            "从镜中奇遇的分散分摊开始，Boss 跳回场中，八方预站位时需站在目标圈上。\n" +
            "光分摊不应主动迎接其他人，所有点名应该均有能力在无冲刺的情况下就位。\n" +
            "无连线两人分开放圈，连线六人踩塔。四人分摊并躲两轮光球，2 层光踩中央塔，再处理放逐和光之波动。";
        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            U7aExerciseOptions.Player(),
            new ComboBoxOption<LightRampantStrategy>(nameof(LightRampantLogic.Strategy), "光之暴走解法",
                LightRampantStrategy.GreyNine, new Map<LightRampantStrategy>
                {
                    [LightRampantStrategy.GreyNine] = "南北向六角星（MMW / 灰九）",
                    [LightRampantStrategy.Mmw] = "斜向六角星（MMW，THD）"
                }, hint: 
                    "· 正向六角星：\n" +
                    "　初始正八方站位，以正北为起点，顺时针为 MT、D4、ST、D2、H2、D1、H1、D3\n" +
                    "　六名连线玩家依次踩 C、1、2、4、3、A\n\n" +
                    "· 斜向六角星：\n" +
                    "　初始斜八方站位，以正西为起点，顺时针为 MT、ST、H1、H2、D4、D3、D2、D1" +
                    "　六名连线玩家依次踩 1、C、2、4、A、3"),
            new ComboBoxOption<LightRampantAssignment>(nameof(LightRampantLogic.Assignment), "初始点名",
                LightRampantAssignment.Random, new Map<LightRampantAssignment>
                {
                    [LightRampantAssignment.Random] = "随机",
                    [LightRampantAssignment.Hammer] = "放光圈",
                    [LightRampantAssignment.Chain] = "连线"
                })
        };
    }

    internal sealed class LightRampantLogic : U7aP2ExerciseLogic
    {
        private Vector2 _initialBossPosition;
        private Func<int, SimPosition> _initialPartyPositions;
        private LightRampantRound _round;

        [SimOption] public LightRampantStrategy Strategy { get; set; }
        [SimOption] public LightRampantAssignment Assignment { get; set; }
        protected override double OpeningLeadIn => 1.19;
        protected override Vector2 InitialBossPosition => _initialBossPosition;
        protected override float InitialBossHeading => Heading(Center - InitialBossPosition);

        public LightRampantLogic(PluginHost host, SimPresetBase preset) : base(host, preset, 8)
        {
        }

        protected override void PreparePlan()
        {
            _round = new LightRampantRound(this, new P23光之失控Plan(Random, Strategy), Random.Next(2) == 0, assignment: Assignment);
            var mirrors = new P22镜中奇遇Plan(Random, ExerciseDirection.Random, RedMirrorAssignment.RangedLeft);
            _initialBossPosition = Vector2.Lerp(Center, P22镜中奇遇Plan.MirrorPosition(mirrors.NearRed), 0.6f);
            _initialPartyPositions = mirrors.BanishPositions(Partners, _initialBossPosition, 0);
        }

        // 单独练习时没有上一轮的首领状态，取近红镜方向的代表位置，并使用分散的最小间距。
        protected override SimPosition InitialPartyPosition(int slot)
            => P23光之失控Plan.ArenaArea(Center, _initialPartyPositions(slot).Midpoint());

        protected override void SchedulePrelude()
        {
            BeforeOpening(-1.19, () =>
            {
                BeginIceBoss();
                var hold = Math.Max(0, -Now);
                for (var slot = 0; slot < Party.Count; slot++)
                    if (slot != Self)
                        Party[slot].SuspendBehavior(hold);
            }, "冰巫女仇恨及放逐站位");
            // CN7.58 回放中先播放回场技能，再在读条前 0.542 秒同步跳跃落点。
            BeforeOpening(-1.182, () => StartIceBossJump(Oracle, Center, InitialBossHeading), "冰巫女跳回场中");
            BeforeOpening(-0.542, () => FinishIceBossJump(Oracle, Center, InitialBossHeading), "冰巫女回场落地");
            BeforeOpening(-0.238, AutoAttack, "冰巫女普攻");
        }

        protected override SimTimeline BuildExercise()
        {
            var timeline = _round.Build();
            timeline.At(44.5, Complete, "光之失控结束");
            return timeline;
        }

        protected override void OnStop()
        {
            try
            {
                _round?.CancelChains();
            }
            finally
            {
                base.OnStop();
            }
        }
    }
}
