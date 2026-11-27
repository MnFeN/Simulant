using Simulant.Core;
using Simulant.Core.Environment;
using Simulant.Simulation;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Options;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;

namespace Simulant.Content.U7a.Presets
{
    [PhaseSelectionOrder(2.1)]
    internal sealed class P21钻石星尘 : SimPresetBase
    {
        public override int TerritoryId => 1238;
        public override string Name => "P2-1 钻石星尘";
        public override string Author => "阿洛";
        public override DateTime LastUpdated => new DateTime(2026, 11, 26);
        public override PhaseData Phase => U7a.P2;
        public override int Level => 100;
        public override Type SimLogicType => typeof(DiamondDustLogic);
        public override WaymarkLayout Waymarks => U7a.Waymarks;
        public override string Description => "钢铁月环 + 扇形/冰花，击退后分摊放圈，处理背对、滑冰、双剑技。";
        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase> { U7aExerciseOptions.Player() };
    }

    internal sealed class DiamondDustLogic : U7aP2ExerciseLogic
    {
        private const double SouthernJumpAt = -1.367;
        private DiamondDustRound _round;

        protected override double OpeningLeadIn => -SouthernJumpAt;
        protected override float InitialBossHeading => 0;

        public DiamondDustLogic(PluginHost host, SimPresetBase preset) : base(host, preset, 24)
        {
        }

        protected override void PreparePlan()
            => _round = new DiamondDustRound(this, new P21钻石星尘Plan(Random.Next(2) == 0, Random.Next(2) == 0,
                Random.Next(2) == 0, Random.Next(4), Random.Next(8), Random.Next(8)));

        protected override SimPosition InitialPartyPosition(int slot) => _round.Plan.OpeningArea(Center, slot);
        protected override void PrepareAdditionalActors(Action ready) => _round.PrepareActors(ready);

        protected override void SchedulePrelude()
        {
            BeforeOpening(SouthernJumpAt, BeginIceBoss, "冰巫女仇恨及初始形态");
            BeforeOpening(SouthernJumpAt, _round.StartSouthernJump, "冰巫女跳向南侧");
            BeforeOpening(-0.567, _round.FinishSouthernJump, "冰巫女在南侧落地");
            BeforeOpening(-0.455, AutoAttack, "冰巫女普攻");
        }

        protected override SimTimeline BuildExercise()
        {
            var timeline = _round.Build();
            timeline.At(63.55, Complete, "钻石星尘及神圣射线结束");
            return timeline;
        }
    }
}
