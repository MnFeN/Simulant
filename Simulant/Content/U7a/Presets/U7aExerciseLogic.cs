using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Core.Environment;
using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.UI;
using Simulant.Simulation;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U7a.Presets
{
    /// <summary>绝伊甸练习共用的小队、场景和判定演出；机制各自定义选项、解法与时间轴。</summary>
    internal abstract class U7aExerciseLogic : SimLogicBase
    {
        protected const ushort SpiritTakerKnockbackRow = 173;
        protected static readonly Vector2 Center = new Vector2(100, 100);
        protected static readonly string[] Roles = { "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };
        protected readonly SimActor[] Helpers;
        protected SimActor Oracle;
        protected SimActor Shiva;
        protected SimActor Wyrm;
        protected SimActor Crystal;
        protected double Origin;
        protected double Now => Scheduler.ElapsedSeconds - Origin;
        protected int Self => Solo ? 0 : (int)PlayerSlot - 1;
        protected virtual bool Solo => false;
        protected virtual bool IsP4 => false;
        protected virtual uint BossBaseId => IsP4 ? 17835u : 17831u;
        protected virtual uint BossNameId => 9832;
        protected virtual uint BossLayoutId => 0;
        protected virtual ushort? BossModel => null;
        protected virtual uint? BossMaxHp => null;
        protected virtual uint HelperNameId => 0;
        protected virtual double OpeningAdvance => 0;
        protected virtual double OpeningLeadIn => 0;
        private readonly HashSet<uint> _mapChanges = new HashSet<uint>();
        private int _mapChangesWorld;
        private readonly HashSet<int> _marked = new HashSet<int>();
        private readonly HashSet<MarkerType> _partyMarks = new HashSet<MarkerType>();
        private int _partyMarksWorld;
        private bool _failedSelf;
        private bool _complete;
        private bool _crystalBroken;

        [SimOption(Optional = true)]
        public ExerciseRole PlayerSlot { get; set; } = ExerciseRole.MT;

        protected U7aExerciseLogic(PluginHost host, SimPresetBase preset, int helperCount = 11) : base(host, preset)
        {
            Helpers = new SimActor[helperCount];
        }

        protected sealed override void BuildTimeline(SimTimeline timeline)
        {
            _mapChanges.Clear();
            _mapChangesWorld = _host.WorldGeneration;
            _marked.Clear();
            _failedSelf = false;
            _complete = false;
            _crystalBroken = false;
            PreparePlan();
            timeline.At(0, () =>
            {
                if (Solo)
                {
                    Party.Add(EntityManager.Attach(_host.EntityProvider.GetMyself()));
                    PrepareScene();
                }
                else
                    GeneratePartyMembers((int)PlayerSlot, PrepareScene);
            }, "准备模拟小队和场景");
        }

        protected virtual void PreparePlan()
        {
        }

        protected abstract SimTimeline BuildExercise();
        protected virtual SimPosition InitialPartyPosition(int slot) => Spot(Center, 5);
        protected virtual void OnBossSpawned()
        {
        }

        protected virtual void PrepareAdditionalActors(Action ready) => ready();
        protected virtual void SchedulePrelude()
        {
        }

        private void PrepareScene()
        {
            for (var slot = 0; !Solo && slot < Party.Count; slot++)
            {
                Party[slot].Pos = InitialPartyPosition(slot).Sample(Random);
                Party[slot].Heading = (float)Math.PI;
            }

            var remaining = Helpers.Length + (IsP4 ? 6 : 1);
            void Ready()
            {
                if (--remaining == 0)
                    PrepareAdditionalActors(() => Scheduler.ScheduleAfter(2, BeginExercise, "场景就绪后开始机制"));
            }

            void Spawn(uint id, uint name, Vector2 position, Action<SimActor> accept, ushort? model = null, uint layout = 0,
                uint? maxHp = null)
            {
                var spec = new NetworkNpcSpawnSpec
                {
                    BNpcBaseId = id,
                    BNpcNameId = name,
                    ModelCharaId = model,
                    LayoutId = id == 17831 ? 0xA55BFFu : id == 17833 ? 0xA55C01u : id == 17835 ? 0xA55C02u : id == 17839 ? 0xA55C06u : layout,
                    Pos = new Vector3(position, 0),
                    Heading = (float)Math.PI,
                    Level = 100
                };
                if (maxHp.HasValue)
                    spec.MaxHealthPoints = maxHp.Value;

                EntityManager.SpawnNetworkBNpc(spec, actor =>
                {
                    accept(actor);
                    Ready();
                });
            }

            Spawn(BossBaseId, BossNameId, IsP4 ? Center + new Vector2(0, 5) : Center, actor =>
            {
                Oracle = actor;
                OnBossSpawned();
            }, BossModel, BossLayoutId, BossMaxHp);
            if (IsP4)
            {
                Spawn(17833, 12809, Center, actor => Shiva = actor, model: 4376);
                Spawn(17834, 9324, Center, actor =>
                {
                    Wyrm = actor;
                    actor.SetTargetable(false);
                });
                U7aCrystal.Spawn(EntityManager, Scheduler, actor =>
                {
                    if (actor.Entity.Native.BaseId.Get() == 17841)
                        Crystal = actor;
                    Ready();
                }, health: 201481);
            }

            SpawnHelpers(Helpers, new Vector3(Center, 0), Ready, HelperNameId);
        }

        private void BeginExercise()
        {
            var start = Scheduler.ElapsedSeconds;
            Origin = start + OpeningLeadIn - OpeningAdvance;
            EntityManager.SetCombatTarget(Shiva ?? Oracle);
            if (IsP4)
                Map(0x2E, 2);
            if (!Solo)
                CheckArena();
            SchedulePrelude();
            foreach (var node in BuildExercise().Snapshot())
                Scheduler.Schedule(Math.Max(start, Origin + node.Seconds), node.Action, node.Name);
        }

        protected static Vector2 Direction(double angle) => new Vector2((float)Math.Sin(angle), (float)Math.Cos(angle));
        protected static float Heading(Vector2 direction) => (float)Math.Atan2(direction.X, direction.Y);
        protected static SimPosition Spot(Vector2 position, float radius = 0.8f) => SimPosition.Ellipse(position, radius);

        protected void Move(int slot, SimPosition position, double arrive, double hold, bool urgent = false, bool gapClose = false)
        {
            if (slot != Self)
                Party[slot].MoveForMechanic(position, Origin + arrive, Origin + hold, urgent, gapClose: gapClose);
        }

        protected void MoveAll(Func<int, SimPosition> destination, double arrive, double hold, bool urgent = false, bool gapClose = false)
        {
            for (var slot = 0; slot < 8; slot++)
                Move(slot, destination(slot), arrive, hold, urgent, gapClose);
        }

        protected void Status(int slot, ushort status, float seconds, ushort param = 0)
            => EntityManager.SetStatus(Party[slot], status, seconds, param, Oracle);
        protected void ClearStatus(int slot, ushort status) => EntityManager.RemoveStatus(Party[slot], status);
        protected void Icon(int slot, uint icon) => Party[slot].ActorControl(ActorControlCategory.TargetIcon, icon, Party[slot].Id);

        protected void MarkPartyMember(int slot, MarkerType markType)
        {
            // 写入前登记标记，调用失败后仍可在停止时清除。
            if (_partyMarks.Count == 0)
                _partyMarksWorld = _host.WorldGeneration;
            _partyMarks.Add(markType);
            EntityManager.Mark(markType, Party[slot].Id);
        }

        protected void ClearPartyMarks()
        {
            if (_partyMarksWorld != _host.WorldGeneration)
            {
                _partyMarks.Clear();
                return;
            }

            var failures = new List<Exception>();
            foreach (var markType in _partyMarks.ToArray())
                try
                {
                    EntityManager.Mark(markType, SimCombatLog.NoTarget);
                    _partyMarks.Remove(markType);
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }
            if (failures.Count != 0)
                throw new AggregateException("清除小队头标失败。", failures);
        }

        protected SimActor Helper(int index, Vector2 position, float heading = 0)
        {
            var actor = Helpers[index];
            // 辅助施法者只承载瞬时特效；原生瞬移同步位置和显示朝向，不能等插值移动后再播放。
            actor.ActorSetPos(new Vector3(position, 0), heading);
            return actor;
        }

        protected void PointEffect(int helper, uint action, Vector2 position, float heading = 0,
            IEnumerable<SimActor> hitTargets = null)
        {
            var actor = Helper(helper, position, heading);
            actor.ExecuteActionEffect(action, actor, heading, hitTargets);
        }
        protected void TargetEffect(int helper, uint action, int target, IEnumerable<SimActor> hitTargets = null)
            => Helper(helper, Party[target].Pos).ExecuteActionEffect(action, Party[target], hitTargets: hitTargets);

        protected void Run(SimMechanic mechanic) => EntityManager.RunMechanic(mechanic);

        protected void Run(IEnumerable<SimMechanic> mechanics)
        {
            var group = mechanics as IReadOnlyList<SimMechanic> ?? mechanics.ToArray();
            if (group.Count != 0)
                Run(group.Count == 1 ? group[0] : new MechanicGroup(group));
        }

        protected AreaMechanic Avoid(string name, uint action, Vector2 center, int? helper = null, ActionArea? area = null, float heading = 0,
            MechanicFailureSeverity severity = MechanicFailureSeverity.Fatal)
            => new AreaMechanic(name, area ?? ActionArea.FromAction(action), center)
            {
                Heading = heading,
                MaxPlayers = 0,
                FailureSeverity = severity,
                Visual = result =>
                {
                    if (helper.HasValue)
                        PointEffect(helper.Value, action, center, heading, result.HitActors);
                    else
                        Dummy(new Vector3(center, 0), heading,
                            actor => actor.ExecuteActionEffectAt(action, new Vector3(center, 0), heading, hitTargets: result.HitActors));
                },
                Failed = result => Report(MechanicError.Fixed(name, Array.Empty<int>(), result.Participants.Select(p => p.Slot))
                    .WithAttribution(result.Participants.Select(p => p.Slot)), result)
            };

        protected AreaMechanic Stack(string name, int target, int minimum, IEnumerable<int> expected, uint action = 40271)
            => ProtectCrystal(new AreaMechanic(name, ActionArea.FromAction(action), centerSlot: target)
            {
                MinPlayers = minimum,
                MaxPlayers = minimum == 4 ? (int?)4 : null,
                FailureSeverity = MechanicFailureSeverity.Warning,
                Check = members => minimum == 4 ? AreaMechanic.CheckPartyAssignment(members, expected) : null,
                Visual = result =>
                {
                    TargetEffect(target, action, target, result.HitActors);
                    if (action == 40271 || action == 40277)
                        EntityManager.MarkUnderfilledStack(result.Participants.Select(p => p.Actor).ToArray(), minimum, Oracle);
                },
                Failed = result => Report(result.DescribeFailure(name, expected, target)
                    .WithAttribution(Responsible(result, expected, target), target == Self ? "可能是自身站歪了" : null), result)
            }, target);

        protected AreaMechanic Spread(string name, int target, uint action = 40274, bool protectCrystal = true)
        {
            var mechanic = new AreaMechanic(name, ActionArea.FromAction(action), centerSlot: target)
            {
                MaxPlayers = 1,
                Visual = result => TargetEffect(target, action, target, result.HitActors),
                Failed = result => Report(result.DescribeFailure(name, new[] { target }, target)
                    .WithAttribution(Responsible(result, new[] { target }, target), "可能是自身站歪了"), result)
            };
            return protectCrystal ? ProtectCrystal(mechanic, target) : mechanic;
        }

        private int[] Responsible(MechanicResult result, IEnumerable<int> expected, int target)
            => target == Self || expected.Contains(Self) != result.Participants.Any(p => p.Slot == Self)
                ? new[] { Self } : Array.Empty<int>();

        protected AreaMechanic ProtectCrystal(AreaMechanic mechanic, int target)
        {
            if (Crystal == null)
                return mechanic;
            return U7aCrystal.Protect(mechanic,
                center => !_crystalBroken && mechanic.Area.IntersectsCircle(Crystal.Pos, Crystal.Entity.HitboxRadius, center, mechanic.Heading),
                BreakCrystal, result => Report(MechanicError.Rule(mechanic.Name, new[] { target }, result.Reason)
                    .WithAttribution(target == Self ? new[] { Self } : Array.Empty<int>(), "自身点名范围击中了北侧水晶"), result));
        }

        protected static Vector2 JumpLanding(SimActor actor, Vector2 target)
        {
            var offset = actor.Pos - target;
            return offset.LengthSquared() < 0.0001f ? target :
                target + Vector2.Normalize(offset) * Math.Min(actor.Entity.HitboxRadius, offset.Length());
        }

        // 同样的落地演出由 P3、P4 共用；先锁定，再播放跳跃，落地时才结算击退。
        protected void SpiritTaker(SimActor source)
        {
            var snapshot = PartySnapshot.Capture(Party);
            var positions = Enumerable.Range(0, 8).Select(i => snapshot.Get(i).Position).ToArray();
            var area = ActionArea.FromAction(40289);
            var target = P42时间结晶Plan.SpiritTakerTarget(
                positions, Self, area, Crystal?.Pos, Crystal?.Entity.HitboxRadius ?? 0, Random);
            var center = positions[target];
            var mechanic = Spread("碎灵一击分散", target, 40289, false);
            mechanic.LockedSnapshot = snapshot;
            mechanic.ResolveAfter = 0.934;
            var landing = JumpLanding(source, center);
            var heading = Heading(center - source.Pos);
            mechanic.Started = _ =>
            {
                source.ActorMove(source.Entity.Pos3D, heading, 0);
                source.ExecuteActionEffect(40289, Party[target], heading);
                Scheduler.ScheduleRelativeToNode(0.716, () =>
                {
                    if (source.IsActive)
                        source.ActorMove(new Vector3(landing, 0), heading, 0);
                }, "碎灵一击落点同步", finishOnMechanicFailure: true);
            };
            mechanic.Visual = result =>
            {
                SimKnockback.Resolve(Scheduler, result.HitActors, KnockbackEffect.Radial(center, 40, 0.8f),
                    exclude: Party[target]);
            };
            Run(ProtectCrystal(mechanic, target));
        }

        private void BreakCrystal()
        {
            if (_crystalBroken || Crystal == null)
                return;
            _crystalBroken = true;
            Crystal.Entity.SetNetworkVitals(0);
            Crystal.ActorControl(ActorControlCategory.DeathAnimation);
            Crystal.ActorControl(ActorControlCategory.SetStatus, 2);
            Map(0x2E, 0x10);
        }

        private void CheckArena()
        {
            if (_complete)
                return;
            var outside = Enumerable.Range(0, Party.Count).Where(i => Vector2.DistanceSquared(Party[i].JudgementPosition, Center) > 400).ToArray();
            foreach (var slot in outside)
                Report(MechanicError.Boundary("电网", slot));
            if (outside.Length != 0)
                throw new SimMechanicFailedException("电网", "有人触碰了场外电网。", alreadyReported: true);
            Scheduler.ScheduleAfter(0.1, CheckArena, "电网检查");
        }

        protected void Map(uint index, ushort animation, ushort? lowFlags = null)
        {
            _mapChanges.Add(index);
            PlayMapEffect(index, animation, lowFlags);
        }

        protected override void OnStop()
        {
            try
            {
                ClearPartyMarks();
            }
            finally
            {
                if (_mapChangesWorld == _host.WorldGeneration)
                    foreach (var slot in _mapChanges)
                    {
                        var original = slot < _preset.Phase.MapEffectFlags.Count ? _preset.Phase.MapEffectFlags[(int)slot] : null;
                        if (original.HasValue)
                            PlayMapEffect(slot, original.Value);
                    }
            }
        }

        protected void Report(MechanicError error, MechanicResult result = null)
        {
            foreach (var slot in error.Offenders)
            {
                _failedSelf |= slot == Self;
                if (_marked.Add(slot))
                    VfxManager.ShowLockOn(Party[slot].Address, "m0489trg_b0c");
            }
            var message = FormattableString.Invariant($"[{_preset.Name}] {Now:0.000} 秒：") + error.Format(Self, s => Roles[s]);
            _host.LogWarning(message);
            GameChat.SendEcho(message, playSound: true);
            if (result != null)
                result.FailureReported = true;
        }

        protected void Complete()
        {
            _complete = true;
            if (!_failedSelf)
                VfxManager.ShowLockOn(Party[Self].Address, "m0489trg_a0c");
            _host.LogSim("本轮机制已结束，可停止或重新开始模拟。");
        }
    }
}
