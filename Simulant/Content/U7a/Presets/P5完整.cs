using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Core.Environment;
using Simulant.Game;
using Simulant.Simulation;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Options;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;

namespace Simulant.Content.U7a.Presets
{
    [PhaseSelectionOrder(5.0)]
    internal sealed class P5完整 : SimPresetBase
    {
        public override int TerritoryId => 1238;
        public override string Name => "P5 完整";
        public override string Author => "阿洛";
        public override DateTime LastUpdated => new DateTime(2026, 11, 23);
        public override PhaseData Phase => U7a.P5;
        public override int Level => 100;
        public override Type SimLogicType => typeof(PandoraLogic);
        public override WaymarkLayout Waymarks => U7a.Waymarks;
        public override string Description => "连续练习 P5 的完整机制，包含三轮光尘之剑、两轮复乐园、两轮星灵之剑，以及潘多拉魔盒与失乐园。";

        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            new LabelOption("沿用各阶段模拟中的可用选项。"),
            U7aExerciseOptions.Player(),
            new CheckBoxOption(nameof(PandoraLogic.ClearEnding), "播放过本收尾", true,
                "在失乐园演出中击败潘多拉。取消勾选时播放完整狂暴结算。")
        };
    }

    internal class PandoraLogic : U7aP5ExerciseLogic
    {
        private const float OpeningStunSeconds = 2;
        private const double TargetableAfterStun = 0.223;
        private const ushort OpeningStun = 2408;
        private const ushort OpeningStunParam = 3783;
        private const double PullLeadIn = 5.315;
        private const uint Memories = 0x2F;
        private const uint PandoraMaxHp = 48_541_857;
        private const uint TotemItemId = 44_743;
        // 2026.11.15 01.05.50 国服回放，潘多拉可选中为零点，最后一点是过本前的有效血量。
        private static readonly SimHealthTrack HealthTrack = new SimHealthTrack(
            (0, PandoraMaxHp), (10, 46_661_100), (20, 42_925_906), (30, 40_614_513),
            (40, 39_321_179), (50, 37_930_947), (60, 36_530_857), (70, 35_022_902),
            (80, 32_823_142), (90, 31_289_317), (100, 30_259_842), (110, 28_805_588),
            (120, 27_692_164), (130, 25_434_985), (140, 21_876_658), (150, 19_506_802),
            (160, 18_052_802), (170, 16_840_709), (180, 15_583_431), (190, 14_141_900),
            (200, 11_975_841), (210, 10_572_687), (220, 9_144_724), (230, 7_719_414),
            (240, 6_588_630), (250, 4_419_532), (260, 705_151), (262.823, 74_100));
        // 回放中由机制触发的额外充能，沿用血量回放的时间原点。
        private static readonly (double At, uint Units, uint Presentation)[] LimitBreakBonuses =
        {
            (38.299, 1800, 3), (112.301, 2400, 3), (124.394, 600, 3), (151.396, 1800, 3),
            (174.999, 300, 3), (177.966, 300, 3), (180.535, 600, 1), (215.300, 600, 3), (242.275, 1800, 3)
        };
        private (ParadiseTowerRule Rule, ParadiseTowerGroups Groups) _towerAssignment;
        private PolarizingOrder _polarizingOrder;
        private int? _clearSceneWorld;
        private bool _clearTransitionActive;

        [SimOption(Optional = true)] public bool ClearEnding { get; set; } = true;
        protected override double OpeningLeadIn => OpeningStunSeconds + TargetableAfterStun;
        protected override uint? BossMaxHp => PandoraMaxHp;

        public PandoraLogic(PluginHost host, SimPresetBase preset) : base(host, preset, FulgentBladeHelper + FulgentBladeHelperCount)
        {
        }

        protected PandoraLogic(PluginHost host, SimPresetBase preset, int helperCount) : base(host, preset, helperCount)
        {
        }

        protected override void PreparePlan()
        {
            _towerAssignment = P52复乐园.ConfiguredAssignment(_host.Configuration);
            _polarizingOrder = P53星灵之剑.ConfiguredOrder(_host.Configuration);
        }

        protected override SimPosition InitialPartyPosition(int slot) => Spot(Center + new Vector2(0, 14), 0.5f);

        protected override void OnBossSpawned()
        {
            Pandora.SetTargetable(false);
            base.OnBossSpawned();
            LockBossFacing();
            FaceBoss(0);
        }

        protected override void SchedulePrelude()
        {
            // 两份回放均以辅助施法者的击倒状态锁住全队，解除后才开放 Boss。
            foreach (var actor in Party)
                EntityManager.SetStatus(actor, OpeningStun, OpeningStunSeconds, OpeningStunParam, Helpers[0]);
            EntityManager.ShowLimitBreakGauge(Party[Self], 3, 30_000);

            Scheduler.ScheduleAfter(OpeningStunSeconds,
                () => EntityManager.CombatLog?.Director(_host.ZoneService.ContentDirectorId, 0x8000001B, 1, 0, 0, 0), "全队击倒解除");
        }

        protected override SimTimeline BuildExercise()
        {
            var timeline = new SimTimeline();
            timeline.At(0, () =>
            {
                Pandora.SetTargetable(true);
                Pandora.FollowTopEnmity = EnmityFollowMode.FacingOnly;
                HealthTrack.Play(Pandora, Scheduler, Origin);
            }, "潘多拉可选中并开始血量回放");
            timeline.AtEach(LimitBreakBonuses, bonus => bonus.At,
                bonus => EntityManager.GainLimitBreak(bonus.Units, bonus.Presentation), _ => "极限技槽额外充能");
            AddDialogue(timeline, 1.023, 36, BossNameId, "潘多拉开场台词");
            AddDoubleAuto(timeline, 3.159, 3.835);

            FulgentBlade(0, 1);
            ParadiseRegained(37.149, 1);
            PolarizingStrikes(66.071, 1);
            timeline.At(PullLeadIn + 94.969, PandorasBox());
            FulgentBlade(113.150, 2);
            ParadiseRegained(154.464, 2);
            PolarizingStrikes(178.302, 2);
            FulgentBlade(203.974, 3);
            timeline.At(PullLeadIn + 245.343, ParadiseLost());
            return timeline;

            void FulgentBlade(double start, int occurrence)
            {
                var offset = PullLeadIn + start;
                timeline.At(offset, new FulgentBladeRound(this, offset, occurrence,
                    CenterDir4.Random.ResolveRandom(Random), FirstLineDir4.Random.ResolveRandom(Random),
                    RotateDirection.Random.ResolveRandom(Random)).Build());
            }

            void ParadiseRegained(double start, int occurrence)
            {
                var offset = PullLeadIn + start;
                timeline.At(offset, new ParadiseRegainedRound(this, offset, occurrence,
                    ParadiseTower.Random.ResolveRandom(Random), ExerciseTurn.Random.ResolveRandom(Random),
                    ParadiseWings.Random.ResolveRandom(Random), _towerAssignment.Rule, _towerAssignment.Groups).Build());
            }

            void PolarizingStrikes(double start, int occurrence)
            {
                var offset = PullLeadIn + start;
                timeline.At(offset, new PolarizingStrikesRound(this, offset, occurrence, _polarizingOrder).Build());
            }
        }

        private SimTimeline PandorasBox()
        {
            var timeline = new SimTimeline();
            timeline.At(0, () =>
            {
                LockBossFacing();
                CastBoss(0x9D86, 11.7f);
            }, "潘多拉魔盒读条");
            AddDialogue(timeline, 0.889, 34, BossNameId, "潘多拉魔盒起手台词");
            AddDialogue(timeline, 7.930, 38, BossNameId, "潘多拉魔盒结算前台词");
            timeline.At(8.222, UseTankLimitBreakIfNeeded, "非坦克玩家由假坦克开启三级极限技");
            timeline.At(12.017, () =>
            {
                Raidwide(0x9D86, 6.1f);
                if (!EntityManager.TankLimitBreakActive)
                    throw new SimMechanicFailedException("潘多拉魔盒", "结算时需要坦克三级极限技减伤。");
            }, "潘多拉魔盒全屏结算");
            timeline.At(13.960, () => Map(Memories, 0x10, 8), "场外回忆画面暗下");
            timeline.At(18.12, () => Pandora.FollowTopEnmity = EnmityFollowMode.FacingOnly, "魔盒演出后恢复一仇转向");
            timeline.At(19.072, () => Map(Memories, 0x20, 1), "场外回忆画面恢复");
            return timeline;
        }

        private void UseTankLimitBreakIfNeeded()
        {
            if (SimEntityManager.IsTankJob(Party[Self].Job))
                return;

            var tank = Party.FirstOrDefault(actor => actor.IsActive && actor.Entity.HP != 0 && SimEntityManager.IsTankJob(actor.Job));
            if (tank != null)
                EntityManager.UsePartyLimitBreak(tank, 3);
        }

        protected SimTimeline ParadiseLost()
        {
            const double enrageAt = 21.5;
            var timeline = new SimTimeline();
            AddDialogue(timeline, 0, 48, BossNameId, "潘多拉的失乐园台词");
            timeline.At(0, () =>
            {
                LockBossFacing();
                Helper(0, Center, BossHeading).Cast(0x9D88, target: Helpers[0], castTime: 21.2f, executeOnFinish: false);
                CastBoss(0x9D87, 11.7f);
            }, "失乐园本体与辅助施法者读条");
            AddDialogue(timeline, 5.429, 49, GaiaNameId, "盖娅的失乐园台词");
            AddDialogue(timeline, 10.501, 50, RyneNameId, "琳的失乐园台词");
            timeline.At(12.035, () => BossEffect(0x9D87, 13.8f), "失乐园本体演出");
            timeline.At(12.454, () => EntityManager.GainLimitBreak(30_000, 5), "失乐园读条中补满极限技槽");
            if (ClearEnding)
            {
                const double defeatAt = enrageAt - 3;
                const double completeAt = defeatAt + 1.639;
                timeline.At(defeatAt, DefeatPandora, "潘多拉倒下");
                timeline.At(defeatAt + 0.048, BeginClearScene, "过本黑屏");
                timeline.At(defeatAt + 1.069, () =>
                {
                    Pandora.ActorControl(ActorControlCategory.SetRenderVisibility, 0);
                    Pandora.ActorControl(ActorControlCategory.PlayActionTimeline, 0x49);
                    _host.EnvironmentService.SetWeather(U7a.Complete.Weather, 0);
                    _host.EnvironmentService.SetBgm(U7a.Complete.BGM);
                }, "隐藏潘多拉并切换过本天气和音乐");
                timeline.At(defeatAt + 1.473, SpawnClearActors, "北侧出现琳和盖娅");
                timeline.At(completeAt, () =>
                {
                    FinishClearTransition(30);
                    _clearTransitionActive = false;
                    Complete();
                }, "过本黑屏结束");
                timeline.At(completeAt + 1, () => Party[Self].ShowItemGain(TotemItemId, 1), "获得巫女图腾");
                timeline.At(defeatAt + 8.079, () => Pandora.ActorControl(ActorControlCategory.DeadFadeOut), "潘多拉死亡淡出");
                timeline.At(defeatAt + 9.917, () => EntityManager.Delete(Pandora), "回收潘多拉");
            }
            else
            {
                timeline.At(enrageAt, () =>
                {
                    Helpers[0].ExecuteActionEffect(0x9D88, Helpers[0], BossHeading, Party, 8, 2.1f);
                }, "失乐园狂暴结算");
                timeline.At(enrageAt + 2.1, () => throw new SimMechanicFailedException("失乐园狂暴"), "狂暴结束");
            }
            return timeline;
        }

        private void DefeatPandora()
        {
            Pandora.StopCasting();
            Helpers[0].StopCasting();
            Pandora.Entity.SetNetworkVitals(0);
            Pandora.ActorControl(ActorControlCategory.ResetActionTimeline);
            Pandora.ActorControl(ActorControlCategory.DeathAnimation, 0xFFFF);
            Pandora.ActorControl(ActorControlCategory.SetStatus, 2);
            Pandora.SetTargetable(false);
            EntityManager.SetCombatTarget(null);
            for (uint slot = 0x30; slot <= 0x35; slot++)
                Map(slot, 4, 4);
            Map(Memories, 8, 4);
        }

        private void BeginClearScene()
        {
            _clearSceneWorld = _host.WorldGeneration;
            _clearTransitionActive = true;
            StartClearTransition();

            var director = _host.ZoneService.ContentDirectorId;
            EntityManager.CombatLog?.Director(director, 0x40000003, 0, 0, 0, 0);
            EntityManager.CombatLog?.Director(director, 0x40000007, 0, 0, 0, 0);
            EntityManager.CombatLog?.Director(director, 0x40000007, 1, 0, 0, 0);
        }

        protected virtual void StartClearTransition() => _host.EnvironmentService.FadeScreen(true, 30);
        protected virtual void FinishClearTransition(uint frames) => _host.EnvironmentService.FadeScreen(false, frames);

        /// <summary> 生成过本场地中的琳和盖娅。</summary>
        private void SpawnClearActors()
        {
            Spawn(17830, GaiaNameId, 99, 0.83f,
                new uint[] { 0x10001, 0x10001, 0x10001, 0x10001, 0x10001, 0, 0, 0, 0, 0 });
            Spawn(10069, RyneNameId, 101, 0.84f,
                new uint[] { 0, 0x123C8, 0x126AF, 0x123C8, 0x123C8, 0, 0, 0, 0, 0 });

            void Spawn(uint baseId, uint nameId, float x, float scale, uint[] equipment)
            {
                var spec = new NetworkNpcSpawnSpec
                {
                    BNpcBaseId = baseId,
                    BNpcNameId = nameId,
                    Pos = new Vector3(x, 92.5f, 0),
                    Equipment = equipment,
                    DisplayFlags = 0x40008,
                    CharacterDataFlags = 0,
                    Targetable = false,
                    EnemyListVisibility = false
                };

                EntityManager.SpawnNetworkBNpc(spec, actor => actor.Entity.Scale = scale);
            }
        }

        protected override void OnStop()
        {
            try
            {
                if (_clearSceneWorld == _host.WorldGeneration)
                {
                    if (_clearTransitionActive)
                        FinishClearTransition(0);
                    _host.EnvironmentService.SetBgm(U7a.P5.BGM);
                    _host.EnvironmentService.SetWeather(U7a.P5.Weather);
                }
            }
            finally
            {
                _clearSceneWorld = null;
                _clearTransitionActive = false;
                base.OnStop();
            }
        }
    }
}
