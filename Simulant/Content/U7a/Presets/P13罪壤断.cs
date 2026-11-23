using Simulant.Core;
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
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U7a.Presets
{
    [PhaseSelectionOrder(1.3)]
    internal sealed class P13罪壤断 : SimPresetBase
    {
        public override int TerritoryId => 1238;
        public override string Name => "P1-3 罪壤断";
        public override string Author => "阿洛";
        public override DateTime LastUpdated => new DateTime(2026, 11, 22);
        public override PhaseData Phase => U7a.P1;
        public override int Level => 100;
        public override Type SimLogicType => typeof(FallOfFaithLogic);
        public override WaymarkLayout Waymarks => U7a.Waymarks;
        public override string Description => "暂未完成。\n\n罪壤断的四根雷火线。奇数线与偶数线分在对侧，火线三人分摊，雷线三向引导。无连线者按优先级补位。";

        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
            U7aExerciseOptions.Player(),
            new CheckBoxOption(nameof(FallOfFaithLogic.NorthSouth), "南北分组", false, "奇数线在北，偶数线在南。默认奇数线在西，偶数线在东。")
        };
    }

    internal sealed class FallOfFaithLogic : U7aEarlyExerciseLogic
    {
        private const ushort Fetters = 2304;
        private const float FettersDuration = 3.956f;
        private const double FirstCloneEntrance = 1.9;
        private const double CloneInterval = 2.51;

        private readonly SimActor[] _clones = new SimActor[3];
        private readonly Vector2[] _lockedPositions = new Vector2[4];
        private readonly float[] _chargeHeadings = new float[4];
        private int[] _targets;
        private int[] _free;
        private bool[] _fire;

        [SimOption] public bool NorthSouth { get; set; }
        protected override uint BossBaseId => 17819;
        protected override uint BossNameId => 9707;
        protected override uint BossLayoutId => 0xA55BF4;
        protected override ushort? BossModel => 3021;
        protected override double OpeningLeadIn => 3.7;

        public FallOfFaithLogic(PluginHost host, SimPresetBase preset) : base(host, preset, 4)
        {
        }

        protected override void PreparePlan()
        {
            var slots = Enumerable.Range(0, 8).OrderBy(_ => Random.Next()).ToArray();
            _targets = slots.Take(4).ToArray();
            _free = slots.Skip(4).OrderBy(slot => slot).ToArray();
            _fire = Enumerable.Range(0, 4).Select(_ => Random.Next(2) == 0).ToArray();
        }

        protected override SimPosition InitialPartyPosition(int slot) => Spot(Center + new Vector2((slot % 4 - 1.5f) * 1.5f, slot < 2 ? -4 : 5), 0.25f);

        protected override void PrepareAdditionalActors(Action ready)
        {
            var actorReady = WhenAllActorsReady(_clones.Length, ready);
            for (var i = 0; i < _clones.Length; i++)
            {
                var index = i;
                SpawnMechanicActor(17820, 9708, 3021, 0xA55BF5, Center + new Vector2(0, -30), 0, actor =>
                {
                    _clones[index] = actor;
                    actorReady();
                });
            }
        }

        protected override void SchedulePrelude()
        {
            BeforeOpening(-3.7, () => SetTankTarget(Oracle), "绝命战士仇恨");
            foreach (var time in new[] { -3.615, -0.573 })
                BeforeOpening(time, AutoAttack, "绝命战士普攻");
            BeforeOpening(-0.130, () => Link(0, Oracle), "罪壤断第一道连线");
        }

        private void AutoAttack() => Oracle.ExecuteActionEffect(0x9CB4, Party[0], null, null, 1, 0.1f);

        protected override SimTimeline BuildExercise()
        {
            var timeline = new SimTimeline().At(0, BuildFallOfFaith(Oracle, 0));
            for (var i = 0; i < _clones.Length; i++)
                timeline.At(FirstCloneEntrance + CloneInterval * i, BuildCloneFallOfFaith(_clones[i], i + 1));

            timeline.At(2.1, () => PositionSide(0, 7.9, 13.4), "奇数线的第一组站位");
            timeline.At(4.6, () => PositionSide(1, 10.6, 16.5), "偶数线的第一组站位");
            timeline.At(13.56, () => PositionSide(2, 14.92, 19.1), "奇数线换位");
            timeline.At(16.69, () => PositionSide(3, 17.45, 21.6), "偶数线换位");
            timeline.At(22.670, AutoAttack, "绝命战士普攻");
            timeline.At(24.45, Complete, "罪壤断结束");
            return timeline;
        }

        private SimTimeline BuildCloneFallOfFaith(SimActor clone, int wave)
        {
            const double appearAt = 0.1;
            const double tetherAt = appearAt + 1.035;
            var point = Center + Direction(Math.PI + (2 - wave) * Math.PI / 4) * 20;
            return new SimTimeline()
                .At(0, () => clone.ActorSetPos(new Vector3(point, 0), Heading(Center - point)), "分身移至入场位置")
                .At(appearAt, () =>
                {
                    Play(clone, 7747);
                    EntityManager.SetEnemyListVisibility(clone, true);
                }, "绝命战士分身现身")
                .At(tetherAt, () => Link(wave, clone), "罪壤断连线")
                .At(tetherAt + 0.055, BuildFallOfFaith(clone, wave));
        }

        private SimTimeline BuildFallOfFaith(SimActor caster, int wave)
        {
            const double castDuration = 8.7;
            const double castEffectAt = castDuration + 0.3;
            const double lockAt = castEffectAt + 0.58;
            const double chargeAt = lockAt + 2.055;
            const double hitDelay = 1.37;
            const double hitAt = chargeAt + hitDelay;
            var fire = _fire[wave];
            var castAction = fire ? 0x9CC9u : 0x9CCCu;
            var chargeAction = fire ? 0x9CCAu : 0x9CCDu;
            var timeline = new SimTimeline()
                .At(0, () => CastFrom(caster, castAction, (float)castDuration), "罪壤断读条")
                .At(castEffectAt, () => caster.ExecuteActionEffect(castAction, caster, null, null, 1, 0.6f), "罪壤断读条结算")
                .At(lockAt, () => Lock(wave, caster), "解除连线并施加浮空拘束")
                .After(0.52, () => caster.ActorControl(ActorControlCategory.SetModelState, 28), "施法者蓄力姿态")
                .At(chargeAt, () => caster.ExecuteActionEffect(chargeAction, Party[_targets[wave]], _chargeHeadings[wave], null, 1, 3.1f),
                    "雷火冲锋")
                .After(hitDelay, () => ResolveHit(wave), "火线分摊或雷线点名结算")
                .After(0.12, () => caster.ActorControl(ActorControlCategory.SetModelState, 0), "施法者恢复姿态");

            if (!fire)
                timeline.At(hitAt + 0.33, () => ResolveLightningCones(wave), "雷线三道扇形结算");
            if (wave != 0)
            {
                timeline.At(hitAt + 1.72, () => Play(caster, 7737), "绝命战士分身退场")
                    .After(1.1, () => EntityManager.SetEnemyListVisibility(caster, false), "分身退出敌对列表")
                    .After(0.4, () => EntityManager.Delete(caster), "回收分身");
            }
            return timeline;
        }

        private void Link(int wave, SimActor actor) => actor.TetherTo(Party[_targets[wave]], _fire[wave] ? 249u : 287u);

        private int[] Group(int wave)
        {
            var side = wave % 2;
            return new[] { _targets[wave], _targets[wave ^ 2], _free[side * 2], _free[side * 2 + 1] };
        }

        private void PositionSide(int wave, double arrive, double hold)
        {
            var group = Group(wave);
            for (var i = 0; i < group.Length; i++)
                Move(group[i], Spot(Center + Position(wave, i, _fire[wave], NorthSouth), 0.2f), arrive, hold, urgent: wave >= 2);
        }

        internal static Vector2 Position(int wave, int index, bool fire, bool northSouth)
        {
            var axis = (northSouth ? new Vector2(0, -1) : new Vector2(-1, 0)) * (wave % 2 == 0 ? 1 : -1);
            var across = new Vector2(-axis.Y, axis.X);
            if (index == 0)
                return axis * 5;
            if (index == 1)
                return axis * 8;
            var side = index == 2 ? -1 : 1;
            return fire ? axis * 9 + across * (side * 0.75f) : axis * 5 + across * (side * 2.8f);
        }

        private void Lock(int wave, SimActor actor)
        {
            var slot = _targets[wave];
            var player = Party[slot];
            _lockedPositions[wave] = player.JudgementPosition;
            _chargeHeadings[wave] = Heading(_lockedPositions[wave] - actor.Pos);
            actor.CancelTether();
            actor.ActorMove(actor.Pos3D, _chargeHeadings[wave], 0);
            actor.ExecuteActionEffect(0x9CEB, player, _chargeHeadings[wave], null, 1, 2.1f);
            // 回放 A 的首次拘束另有拉向当前位置的单体效果，落点仍用拘束快照。
            if (wave == 0)
                Helper(0, _lockedPositions[wave]).ExecuteActionEffect(0x9CEC, player, 0, null, 1, 1.1f);
            // 原生效果中另有拘束分支；Status 2304 本身没有 LockMovement 位。
            EntityManager.SetStatus(player, Fetters, FettersDuration, 0xC8, actor, lockMovement: true);
            player.SuspendBehavior(FettersDuration);
        }

        private void ResolveHit(int wave)
        {
            var target = _targets[wave];
            var position = _lockedPositions[wave];
            if (!_fire[wave])
            {
                var helper = Helper(0, position);
                helper.ExecuteActionEffect(0x9CCE, Party[target], 0, null, 1, 3.1f);
                RefreshStatus(target, MagicVulnerability, 3.955f, helper);
                return;
            }

            var snapshot = PartySnapshot.Capture(Party);
            var nearest = Nearest(snapshot, position, 1, slot => slot != target)[0];
            var heading = Heading(snapshot.Get(nearest).Position - position);
            var mechanic = HitArea("火线四人分摊", ActionArea.Cone(60, 90), position, heading, result =>
            {
                var helper = Helper(0, position, heading);
                helper.ExecuteActionEffect(0x9CDC, helper, heading, result.HitActors, 8, 0.6f);
                Vulnerability(result, helper, 3.955f);
            }, Group(wave));
            mechanic.LockedSnapshot = snapshot;
            Run(mechanic);
        }

        private void ResolveLightningCones(int wave)
        {
            var snapshot = PartySnapshot.Capture(Party);
            var target = _targets[wave];
            var position = _lockedPositions[wave];
            var group = Group(wave);
            Run(Nearest(snapshot, position, 3, slot => slot != target).Select((slot, i) =>
            {
                var heading = Heading(snapshot.Get(slot).Position - position);
                var cone = HitArea("雷线扇形", ActionArea.Cone(60, 120), position, heading, result =>
                {
                    var helper = Helper(i + 1, position, heading);
                    helper.ExecuteActionEffect(0x9CCF, helper, heading, result.HitActors, 1, 0.6f);
                    Vulnerability(result, helper, 3.955f);
                }, group.Contains(slot) ? new[] { slot } : Array.Empty<int>());
                cone.Exclude = Party[target];
                cone.LockedSnapshot = snapshot;
                return (SimMechanic)cone;
            }));
        }
    }
}
