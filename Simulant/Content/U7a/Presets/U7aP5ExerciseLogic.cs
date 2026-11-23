using Simulant.Core;
using Simulant.Game;
using Simulant.Simulation;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U7a.Presets
{
    internal abstract partial class U7aP5ExerciseLogic : U7aExerciseLogic
    {
        protected const ushort PhysicalVulnerability = 2940;
        protected const ushort MagicVulnerability = 2941;
        protected const uint RyneNameId = 9347;
        protected const uint GaiaNameId = 9326;
        protected const int AutoHelper = 4;
        protected const int FulgentBladeHelper = AutoHelper + 2;
        protected const int FulgentBladeHelperCount = 12;
        protected override uint BossBaseId => 17839;
        protected override uint BossNameId => 13561;
        protected override uint HelperNameId => 9707;
        protected SimActor Pandora => Oracle;
        private float _bossHeading;
        protected float BossHeading => Pandora.FollowTopEnmity == EnmityFollowMode.Disabled ? _bossHeading : Pandora.Heading;
        protected int MainTank => Party.IndexOf(EntityManager.GetPartyEnmityOrder(Pandora).FirstOrDefault() ?? Party[0]);
        private int GetInitialTank() => Party.IndexOf(EntityManager.GetPartyEnmityOrder(Pandora)
            .FirstOrDefault(actor => actor == Party[0] || actor == Party[1]) ?? Party[0]);

        protected U7aP5ExerciseLogic(PluginHost host, SimPresetBase preset, int helperCount)
            : base(host, preset, helperCount)
        {
        }

        protected override void OnBossSpawned()
        {
            _bossHeading = (float)Math.PI;
            EntityManager.SetPartyEnmity(Pandora, Solo ? new[] { 100 } : new[] { 100, 32, 2, 2, 4, 4, 3, 3 });
            Pandora.FollowTopEnmity = EnmityFollowMode.FacingOnly;
        }

        protected void FaceBoss(float heading)
        {
            _bossHeading = heading;
            Pandora.ActorMove(Pandora.Pos3D, heading, 90);
        }

        protected void LockBossFacing()
        {
            _bossHeading = Pandora.Heading;
            Pandora.FollowTopEnmity = EnmityFollowMode.Disabled;
        }

        protected void CastBoss(uint action, float duration)
            => Pandora.Cast(action, target: Pandora, castTime: duration, omenHeading: BossHeading, executeOnFinish: false);

        protected void BossEffect(uint action, float animationLock)
            => Pandora.ExecuteActionEffect(action, Pandora, BossHeading, null, 1, animationLock);

        protected void Raidwide(uint action, float animationLock)
            => Pandora.ExecuteActionEffect(action, Pandora, BossHeading, Party, 8, animationLock);

        protected void ShowDialogue(uint textIndex, uint nameId, SimActor speaker = null)
        {
            var director = _host.ZoneService.ContentDirectorId;
            if (director != 0x800375BF)
                return;
            // 由副本控制器读取原始台词、语音和显示时长。说话者存在时使用本场实体 ID。
            Party[Self].ActorControlSelf(ActorControlCategory.DirectorUpdate, director, 0x80000027,
                textIndex, 2, nameId, speaker?.Id ?? SimCombatLog.NoTarget);
        }

        protected void AddDialogue(SimTimeline timeline, double time, uint textIndex, uint nameId, string name)
        {
            timeline.At(time, () => ShowDialogue(textIndex, nameId, nameId == BossNameId ? Pandora : null), name);
        }

        protected void AddDoubleAuto(SimTimeline timeline, double start, double hit)
        {
            timeline.At(start, () => BossEffect(0x9CB2, 0.2f), "潘多拉双坦普攻起手");
            timeline.At(hit - 0.12, () =>
            {
                Helper(AutoHelper, Center, BossHeading);
                Helper(AutoHelper + 1, Center, BossHeading);
            }, "普攻辅助施法者转向");
            timeline.At(hit, () =>
            {
                var targets = EntityManager.GetPartyEnmityOrder(Pandora);
                for (var index = 0; index < Math.Min(2, targets.Length); index++)
                    Helpers[AutoHelper + index].ExecuteActionEffect(0x9CB3, targets[index], BossHeading, null, 1, 0.6f);
            }, "双坦普攻结算");
        }

        protected void ApplyStatus(MechanicResult result, ushort id, float duration, SimActor source, double delay = 0)
        {
            void Apply()
            {
                foreach (var member in result.Participants)
                    ApplyStatus(member.Slot, id, duration, source);
            }

            if (delay > 0)
                Scheduler.ScheduleRelativeToNode(delay, Apply, "结算机制状态", finishOnMechanicFailure: true);
            else
                Apply();
        }

        protected void ApplyStatus(int slot, ushort id, float duration, SimActor source)
        {
            // 连续误吃或同时命中两道光束时，后一次的施法者可能不同。
            EntityManager.RemoveStatus(Party[slot], id);
            EntityManager.SetStatus(Party[slot], id, duration, 0, source);
        }
    }
}
