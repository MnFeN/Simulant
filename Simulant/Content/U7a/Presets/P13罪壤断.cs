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
        private static readonly double[] TetherAt = { -0.130, 3.057, 5.519, 8.053 };
        private static readonly double[] CastAt = { 0, 3.114, 5.576, 8.103 };
        private static readonly double[] CastHit = { 8.946, 12.083, 14.628, 17.124 };
        private static readonly double[] LockAt = { 9.580, 12.700, 15.176, 17.718 };
        private static readonly double[] ChargeAt = { 11.624, 14.752, 17.240, 19.774 };
        private static readonly double[] HitAt = { 13.013, 16.128, 18.595, 21.130 };
        private static readonly double[] ConesAt = { 13.365, 16.397, 18.940, 21.468 };
        private static readonly double[] RestoreAt = { 13.139, 16.245, 18.726, 21.238 };
        private static readonly double[] AppearAt = { 2.020, 4.481, 6.986 };
        private static readonly double[] MoveCloneAt = { 1.892, 4.357, 6.940 };
        private static readonly double[] ModelAt = { 10.071, 13.194, 15.714, 18.283 };
        private static readonly double[] FadeAt = { 17.872, 20.307, 22.845 };

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
            BeforeOpening(TetherAt[0], () => Link(0, Oracle), "罪壤断第一道连线");
        }

        private void AutoAttack() => Oracle.ExecuteActionEffect(0x9CB4, Party[0], null, null, 1, 0.1f);

        protected override SimTimeline BuildExercise()
        {
            var timeline = new SimTimeline();

            for (var i = 0; i < 4; i++)
            {
                var wave = i;
                var actor = wave == 0 ? Oracle : _clones[wave - 1];
                var action = _fire[wave] ? 0x9CC9u : 0x9CCCu;
                if (wave != 0)
                {
                    var point = Center + Direction(Math.PI + (2 - wave) * Math.PI / 4) * 20;
                    timeline.At(MoveCloneAt[wave - 1], () => actor.ActorSetPos(new Vector3(point, 0), Heading(Center - point)), "分身移至入场位置");
                    timeline.At(AppearAt[wave - 1], () =>
                    {
                        Play(actor, 7747);
                        EntityManager.SetEnemyListVisibility(actor, true);
                    }, "绝命战士分身现身");
                    timeline.At(FadeAt[wave - 1], () => Play(actor, 7737), "绝命战士分身退场");
                    timeline.At(FadeAt[wave - 1] + 1.1, () => EntityManager.SetEnemyListVisibility(actor, false), "分身退出敌对列表");
                    timeline.At(FadeAt[wave - 1] + 1.5, () => EntityManager.Delete(actor), "回收分身");
                    timeline.At(TetherAt[wave], () => Link(wave, actor), "罪壤断连线");
                }
                timeline.At(CastAt[wave], () => CastFrom(actor, action, 8.7f), "罪壤断读条");
                timeline.At(CastHit[wave], () => actor.ExecuteActionEffect(action, actor, null, null, 1, 0.6f), "罪壤断读条结算");
                timeline.At(LockAt[wave], () => Lock(wave, actor), "解除连线并施加浮空拘束");
                timeline.At(ModelAt[wave], () => actor.ActorControl(ActorControlCategory.SetModelState, 28), "分身蓄力姿态");
                timeline.At(ChargeAt[wave], () => actor.ExecuteActionEffect(_fire[wave] ? 0x9CCAu : 0x9CCDu,
                    Party[_targets[wave]], _chargeHeadings[wave], null, 1, 3.1f), "雷火冲锋");
                timeline.At(HitAt[wave], () => ResolveHit(wave), "火线分摊或雷线点名结算");
                if (!_fire[wave])
                    timeline.At(ConesAt[wave], () => ResolveLightningCones(wave), "雷线三道扇形结算");
                timeline.At(RestoreAt[wave], () => actor.ActorControl(ActorControlCategory.SetModelState, 0), "分身恢复姿态");
                timeline.At(LockAt[wave] + 3.956, () => ClearStatus(_targets[wave], Fetters), "解除浮空拘束");
            }

            timeline.At(2.1, () => PositionSide(0, 7.9, 13.4), "奇数线的第一组站位");
            timeline.At(4.6, () => PositionSide(1, 10.6, 16.5), "偶数线的第一组站位");
            timeline.At(13.56, () => PositionSide(2, 14.92, 19.1), "奇数线换位");
            timeline.At(16.69, () => PositionSide(3, 17.45, 21.6), "偶数线换位");
            timeline.At(22.670, AutoAttack, "绝命战士普攻");
            timeline.At(24.45, Complete, "罪壤断结束");
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
            EntityManager.SetStatus(player, Fetters, 3.956f, 0xC8, actor, lockMovement: true);
            player.SuspendBehavior(3.956);
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
