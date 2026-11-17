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
        protected static readonly Vector2 Center = new Vector2(100, 100);
        protected static readonly string[] Roles = { "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };
        protected readonly Random Random = new Random();
        protected readonly SimActor[] Helpers = new SimActor[11];
        protected readonly SimActor[] Hourglasses = new SimActor[8];
        protected SimActor Oracle;
        protected SimActor Shiva;
        protected SimActor Wyrm;
        protected SimActor Crystal;
        protected double Origin;
        protected double Now => Scheduler.ElapsedSeconds - Origin;
        protected int Self => (int)PlayerSlot - 1;
        protected virtual bool IsP4 => false;
        protected virtual bool HasHourglasses => false;
        protected virtual double OpeningAdvance => 0;
        private readonly HashSet<uint> _mapChanges = new HashSet<uint>();
        private readonly HashSet<int> _marked = new HashSet<int>();
        private readonly HashSet<MarkerType> _partyMarks = new HashSet<MarkerType>();
        private int _partyMarksWorld;
        private bool _failedSelf;
        private bool _complete;
        private bool _crystalBroken;

        [SimOption]
        public ExerciseRole PlayerSlot { get; set; }

        protected U7aExerciseLogic(PluginHost host, SimPresetBase preset) : base(host, preset)
        {
        }

        protected sealed override void BuildTimeline(SimTimeline timeline)
        {
            PreparePlan();
            timeline.At(0, () => GeneratePartyMembers((int)PlayerSlot, PrepareScene), "准备模拟小队和场景");
        }

        protected abstract void PreparePlan();
        protected abstract SimTimeline BuildExercise();
        protected virtual SimPosition InitialPartyPosition(int slot) => Spot(Center, 5);

        private void PrepareScene()
        {
            for (var slot = 0; slot < 8; slot++)
            {
                Party[slot].Pos = InitialPartyPosition(slot).Sample(Random);
                Party[slot].Heading = (float)Math.PI;
            }

            var remaining = Helpers.Length + (HasHourglasses ? 8 : 0) + (IsP4 ? 6 : 1);
            void Ready()
            {
                if (--remaining == 0)
                    Scheduler.ScheduleAfter(2, BeginExercise, "场景就绪后开始机制");
            }

            void Spawn(uint id, uint name, Vector2 position, Action<SimActor> accept, bool hidden = false, ushort? model = null)
                => EntityManager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec
                {
                    BNpcBaseId = id,
                    BNpcNameId = name,
                    ModelCharaId = model,
                    LayoutId = id == 17831 ? 0xA55BFFu : id == 17832 ? 0xA55C00u : id == 17833 ? 0xA55C01u : id == 17835 ? 0xA55C02u : 0,
                    Pos = new Vector3(position, 0),
                    Heading = (float)Math.PI,
                    Level = 100,
                    Targetable = !hidden
                }, actor =>
                {
                    accept(actor);
                    if (hidden)
                        actor.Entity.DisableDraw();
                    Ready();
                });

            Spawn(IsP4 ? 17835u : 17831u, 9832, IsP4 ? Center + new Vector2(0, 5) : Center, actor => Oracle = actor);
            if (IsP4)
            {
                Spawn(17833, 12809, Center, actor => Shiva = actor, model: 4376);
                Spawn(17834, 9324, Center, actor =>
                {
                    Wyrm = actor;
                    actor.Entity.SetTargetable(false);
                });
                U7aCrystal.Spawn(EntityManager, Scheduler, actor =>
                {
                    if (actor.Entity.Native.BaseId.Get() == 17841)
                        Crystal = actor;
                    Ready();
                }, health: 201481);
            }

            if (HasHourglasses)
                for (var i = 0; i < 8; i++)
                {
                    var index = i;
                    Spawn(17832, 9825, Center, actor => Hourglasses[index] = actor, true);
                }

            for (var i = 0; i < Helpers.Length; i++)
            {
                var index = i;
                Dummy(new Vector3(Center, 0), 0, actor =>
                {
                    Helpers[index] = actor;
                    Ready();
                });
            }
        }

        private void BeginExercise()
        {
            var start = Scheduler.ElapsedSeconds;
            Origin = start - OpeningAdvance;
            EntityManager.SetCombatTarget(Shiva ?? Oracle);
            if (IsP4)
                Map(0x2E, 2);
            CheckArena();
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
        protected void Icon(int slot, uint icon) => Party[slot].Entity.ActorControl(ActorControlCategory.TargetIcon, icon, Party[slot].Id);

        protected void MarkPartyMember(int slot, MarkerType markType)
        {
            // 写入前登记标记，调用失败后仍可在停止时清除。
            if (_partyMarks.Count == 0)
                _partyMarksWorld = _host.WorldGeneration;
            _partyMarks.Add(markType);
            GameMarkers.Mark(markType, Party[slot].Id);
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
                    GameMarkers.ClearMarker(markType);
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
            actor.Entity.ActorSetPos(new Vector3(position, 0), heading);
            return actor;
        }

        protected void PointEffect(int helper, uint action, Vector2 position, float heading = 0)
        {
            var actor = Helper(helper, position, heading);
            actor.Entity.ExecuteActionEffect(action, actor.Entity, heading);
        }
        protected void TargetEffect(int helper, uint action, int target)
            => Helper(helper, Party[target].Pos).Entity.ExecuteActionEffect(action, Party[target].Entity);

        protected void Run(params SimMechanic[] mechanics)
        {
            if (mechanics.Length != 0)
                EntityManager.RunMechanic(mechanics.Length == 1 ? mechanics[0] : new MechanicGroup(mechanics));
        }

        protected AreaMechanic Avoid(string name, uint action, Vector2 center, int? helper = null, ActionArea? area = null, float heading = 0,
            MechanicFailureSeverity severity = MechanicFailureSeverity.Fatal)
            => new AreaMechanic
            {
                Name = name,
                Area = area ?? ActionArea.FromAction(action),
                Center = center,
                Heading = heading,
                MaxPlayers = 0,
                FailureSeverity = severity,
                Visual = _ =>
                {
                    if (helper.HasValue)
                        PointEffect(helper.Value, action, center, heading);
                    else
                        Dummy(new Vector3(center, 0), heading,
                            actor => actor.Entity.ExecuteActionEffectAt(action, new Vector3(center, 0), heading));
                },
                Failed = result => Report(MechanicError.Fixed(name, Array.Empty<int>(), result.Participants.Select(p => p.Slot))
                    .WithAttribution(result.Participants.Select(p => p.Slot)), result)
            };

        protected AreaMechanic Stack(string name, int target, int minimum, IEnumerable<int> expected, uint action = 40271)
            => ProtectCrystal(new AreaMechanic
            {
                Name = name,
                Area = ActionArea.FromAction(action),
                CenterAtResolve = snapshot => snapshot.Get(target).Position,
                MinPlayers = minimum,
                MaxPlayers = minimum == 4 ? (int?)4 : null,
                FailureSeverity = MechanicFailureSeverity.Warning,
                Check = members => minimum == 4 && !members.Select(p => p.Slot).OrderBy(i => i).SequenceEqual(expected.OrderBy(i => i))
                    ? "参与者不符合当前点名分工。" : null,
                Visual = result =>
                {
                    TargetEffect(target, action, target);
                    if (action == 40271 || action == 40277)
                        EntityManager.MarkUnderfilledStack(result.Participants.Select(p => p.Actor).ToArray(), minimum, Oracle);
                },
                Failed = result => Report(result.DescribeFailure(name, expected, target)
                    .WithAttribution(Responsible(result, expected, target), target == Self ? "可能是自身站歪了" : null), result)
            }, target);

        protected AreaMechanic Spread(string name, int target, uint action = 40274, bool protectCrystal = true)
        {
            var mechanic = new AreaMechanic
            {
                Name = name,
                Area = ActionArea.FromAction(action),
                CenterAtResolve = snapshot => snapshot.Get(target).Position,
                MaxPlayers = 1,
                Visual = _ => TargetEffect(target, action, target),
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
            var check = mechanic.Check;
            var visual = mechanic.Visual;
            var failed = mechanic.Failed;
            var center = mechanic.CenterAtResolve;
            var hit = false;
            mechanic.CenterAtResolve = snapshot =>
            {
                var position = center?.Invoke(snapshot) ?? mechanic.Center;
                hit = !_crystalBroken && mechanic.Area.IntersectsCircle(Crystal.Pos, Crystal.Entity.HitboxRadius, position, mechanic.Heading);
                if (hit)
                    mechanic.FailureSeverity = MechanicFailureSeverity.Fatal;
                return position;
            };
            mechanic.Check = members =>
            {
                var reason = check?.Invoke(members);
                return hit ? (reason == null ? "" : reason + " ") + "点名范围击碎了北侧水晶。" : reason;
            };
            mechanic.Visual = result =>
            {
                visual?.Invoke(result);
                if (hit)
                    BreakCrystal();
            };
            mechanic.Failed = result =>
            {
                if (hit)
                    Report(MechanicError.Rule(mechanic.Name, new[] { target }, result.Reason)
                        .WithAttribution(target == Self ? new[] { Self } : Array.Empty<int>(), "自身点名范围击中了北侧水晶"), result);
                else
                    failed?.Invoke(result);
            };
            return mechanic;
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
            var target = CrystallizeTimePlan.SpiritTakerTarget(
                positions, Self, area, Crystal?.Pos, Crystal?.Entity.HitboxRadius ?? 0, Random);
            var center = positions[target];
            var mechanic = Spread("碎灵一击分散", target, 40289, false);
            mechanic.LockedSnapshot = snapshot;
            mechanic.ResolveAfter = 0.934;
            var landing = JumpLanding(source, center);
            var heading = Heading(center - source.Pos);
            mechanic.Started = context =>
            {
                source.Entity.ActorMove(source.Entity.Pos3D, heading, 0);
                source.Entity.ExecuteActionEffect(40289, Party[target].Entity, heading);
                context.ScheduleAfter(0.716, () => source.Entity.ActorMove(new Vector3(landing, 0), heading, 0), "碎灵一击落点同步");
            };
            mechanic.Visual = result =>
            {
                foreach (var member in result.Participants.Where(p => p.Slot != target))
                    Party[member.Slot].KnockbackFrom(center, 40, 0.8f);
            };
            Run(ProtectCrystal(mechanic, target));
        }

        private void BreakCrystal()
        {
            if (_crystalBroken || Crystal == null)
                return;
            _crystalBroken = true;
            Crystal.Entity.SetNetworkVitals(0);
            Crystal.Entity.ActorControl(ActorControlCategory.DeathAnimation);
            Crystal.Entity.ActorControl(ActorControlCategory.SetStatus, 2);
            Map(0x2E, 0x10);
        }

        private void CheckArena()
        {
            if (_complete)
                return;
            var outside = Enumerable.Range(0, 8).Where(i => Vector2.DistanceSquared(Party[i].JudgementPosition, Center) > 400).ToArray();
            foreach (var slot in outside)
                Report(MechanicError.Boundary("电网", slot));
            if (outside.Length != 0)
                throw new SimMechanicFailedException("电网", "有人触碰了场外电网。", alreadyReported: true);
            Scheduler.ScheduleAfter(0.1, CheckArena, "电网检查");
        }

        protected void Map(uint index, ushort animation)
        {
            _mapChanges.Add(index);
            _host.EnvironmentService.PlayMapEffect(index, animation);
        }

        protected override void OnStop()
        {
            try
            {
                ClearPartyMarks();
            }
            finally
            {
                if (Oracle?.IsActive == true)
                    foreach (var slot in _mapChanges)
                    {
                        var original = slot < _preset.Phase.MapEffectFlags.Count ? _preset.Phase.MapEffectFlags[(int)slot] : null;
                        if (original.HasValue)
                            _host.EnvironmentService.PlayMapEffect(slot, original.Value);
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
