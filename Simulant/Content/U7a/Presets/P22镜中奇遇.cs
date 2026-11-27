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
    [PhaseSelectionOrder(2.2)]
    internal sealed class P22镜中奇遇 : SimPresetBase
    {
        public override int TerritoryId => 1238;
        public override string Name => "P2-2 镜中奇遇";
        public override string Author => "阿洛";
        public override DateTime LastUpdated => new DateTime(2026, 11, 26);
        public override PhaseData Phase => U7a.P2;
        public override int Level => 100;
        public override Type SimLogicType => typeof(MirrorMirrorLogic);
        public override WaymarkLayout Waymarks => U7a.Waymarks;
        public override string Description => "MT 把首领拉到白镜对侧，T 和近战在首领处引导，治疗和远程在白镜处引导。两组随后分别转到红镜。最后双人分摊或全体分散。";

        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            U7aExerciseOptions.Player(),
            U7aExerciseOptions.Direction(nameof(MirrorMirrorLogic.BlueDirection), "白镜方位"),
            new ComboBoxOption<RedMirrorAssignment>(nameof(MirrorMirrorLogic.RedAssignment), "镜子对称时，第二次红镜子分配方式",
                RedMirrorAssignment.RangedLeft, new Map<RedMirrorAssignment>
                {
                    [RedMirrorAssignment.RangedLeft] = "[国] 面朝红镜子远左近右",
                    [RedMirrorAssignment.RangedRight] = "面朝红镜子远右近左",
                    [RedMirrorAssignment.Clockwise] = "原地顺时针找红镜子",
                    [RedMirrorAssignment.Counterclockwise] = "原地逆时针找红镜子"
                }, "不对称时，两组各去离首轮站位近的红镜。")
        };
    }

    internal sealed class MirrorMirrorLogic : U7aP2ExerciseLogic
    {
        private MirrorMirrorRound _round;

        [SimOption] public ExerciseDirection BlueDirection { get; set; }
        [SimOption] public RedMirrorAssignment RedAssignment { get; set; }
        protected override double OpeningLeadIn => 0.82;
        protected override float InitialBossHeading => _round.Plan.OpeningHeading;

        public MirrorMirrorLogic(PluginHost host, SimPresetBase preset) : base(host, preset, 8)
        {
        }

        protected override void PreparePlan()
            => _round = new MirrorMirrorRound(this, new P22镜中奇遇Plan(Random, BlueDirection, RedAssignment));

        protected override SimPosition InitialPartyPosition(int slot) => _round.Plan.OpeningParty(slot);
        protected override void PrepareAdditionalActors(Action ready) => _round.PrepareActors(ready);

        protected override void SchedulePrelude()
        {
            _round.HoldOpeningTank();
            BeforeOpening(-0.82, BeginIceBoss, "冰巫女仇恨及初始形态");
            BeforeOpening(-0.789, AutoAttack, "冰巫女普攻");
        }

        protected override SimTimeline BuildExercise()
        {
            var timeline = _round.Build();
            timeline.At(35.2, () =>
            {
                Oracle.FollowTopEnmity = EnmityFollowMode.Disabled;
                Complete();
            }, "镜中奇遇结束");
            return timeline;
        }
    }
}
