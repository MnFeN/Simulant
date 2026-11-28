using Simulant.Core;
using Simulant.Content.U7a.Presets;
using Simulant.Core.Environment;
using Simulant.Game.Data;
using Simulant.Simulation;
using Simulant.Simulation.Options;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.Local
{
    internal sealed class LocalPreset : SimPresetBase
    {
        internal readonly LocalEncounter Encounter;
        internal LocalPreset(LocalEncounter encounter, DateTime updated)
        {
            Encounter = encounter;
            LastUpdated = updated;
            if (encounter.PartyPositions != null)
            {
                Options.Add(U7aExerciseOptions.Player());
                Options.Add(new CheckBoxOption(nameof(LocalEncounterLogic.IncludeParty), "生成七名模拟队友", true));
                if (encounter.SwapLabel != null)
                    Options.Add(new CheckBoxOption(nameof(LocalEncounterLogic.SwapRoles), encounter.SwapLabel, false));
            }
            foreach (var choice in encounter.Choices)
            {
                var values = new Map<string>();
                if (choice.Random) values["*"] = "随机";
                foreach (var value in choice.Values) values[value] = value;
                Options.Add(new ComboBoxOption<string>("choice." + choice.Id, choice.Label, choice.Default, values));
            }
            if (encounter.AllEvents.Any(e => e.Op == "hint"))
                Options.Add(new CheckBoxOption(nameof(LocalEncounterLogic.ShowHints), "显示机制提示", true));
        }
        internal override string ConfigurationKey => "local:" + Encounter.Id;
        public override string Name => Encounter.Name;
        public override string Description => Encounter.Description;
        public override string Author => Encounter.Author ?? "未署名";
        public override DateTime LastUpdated { get; }
        public override int TerritoryId => Encounter.TerritoryId;
        public override PhaseData Phase => Encounter.Phase;
        public override WaymarkLayout Waymarks => Encounter.Waymarks;
        public override int Level => IsImplemented ? Encounter.Actors.Max(a => a.Spawn.Level) : 0;
        public override bool IsImplemented => Encounter.Timeline.Count != 0;
        internal override string UnavailableMessage => "场地预览。点击“加载区域”即可进入。";
        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>();
        public override Type SimLogicType => typeof(LocalEncounterLogic);
        public override string ToString() => (IsImplemented ? "[本地模拟] " : "[本地阶段] ") + Name;
    }

    internal sealed class LocalEncounterLogic : SimLogicBase
    {
        private readonly LocalEncounter _encounter;
        private readonly Dictionary<string, SimActor> _actors = new Dictionary<string, SimActor>(StringComparer.Ordinal);
        private readonly Dictionary<SimActor, SimScheduledAction> _groundCasts = new Dictionary<SimActor, SimScheduledAction>();
        private readonly SimActor[] _members = new SimActor[8];
        private Vector2[] _positions;
        private int _checks, _misses;
        private LocalFlow _flow;
        private LocalTargets _targets;
        private SimScheduledAction _targetPoll;
        private readonly Dictionary<string, SimScheduledAction> _fixedLines = new Dictionary<string, SimScheduledAction>();
        private readonly Dictionary<string, string> _choices = new Dictionary<string, string>();
        private readonly bool[] _objectSlots = new bool[40];
        private readonly Dictionary<(string, uint), int> _objects = new Dictionary<(string, uint), int>();
        private readonly HashSet<ushort> _changedMap = new HashSet<ushort>();
        private int _mapWorld;
        private int Self => _encounter.AssignedSlot((int)PlayerSlot - 1, SwapRoles);

        [SimOption(Optional = true)] public ExerciseRole PlayerSlot { get; set; } = ExerciseRole.MT;
        [SimOption(Optional = true)] public bool IncludeParty { get; set; } = true;
        [SimOption(Optional = true)] public bool ShowHints { get; set; } = true;
        [SimOption(Optional = true)] public bool SwapRoles { get; set; }

        public LocalEncounterLogic(PluginHost host, SimPresetBase preset) : base(host, preset)
        {
            _encounter = ((LocalPreset)preset).Encounter;
        }

        protected override void OnOptionsLoaded()
        {
            _choices.Clear();
            foreach (var choice in _encounter.Choices)
            {
                var option = _preset.Options.Single(o => o.PropertyName == "choice." + choice.Id);
                var value = choice.Resolve(option.GetValue<string>(), Random);
                _choices.Add(choice.Id, value);
                _host.LogSim($"{choice.Label} = {value}");
            }
        }

        protected override void BuildTimeline(SimTimeline timeline)
        {
            _actors.Clear();
            _groundCasts.Clear();
            Array.Clear(_members, 0, _members.Length);
            Array.Clear(_objectSlots, 0, _objectSlots.Length);
            _objects.Clear();
            _mapWorld = _host.WorldGeneration;
            _flow = _encounter.Version >= 3 ? new LocalFlow(Scheduler, Run, ReadWorld, _choices, slot => Position(slot).HasValue) : null;
            _targets = new LocalTargets(Position, id => _actors[id].IsActive ? _actors[id].Pos : (Vector2?)null,
                EnmityOrder, (step, slot) =>
                {
                    if (slot < 0) _actors[step.Actor].CancelTether(tetherId: step.TetherId);
                    else _actors[step.Actor].TetherTo(_members[slot], step.TetherId);
                });
            if (_encounter.AllEvents.Any(e => e.On?.Type == "castStarted"))
                EntityManager.CombatLog.CastStarted += OnCastStarted;
            if (_encounter.AllEvents.Any(e => e.On?.Type == "castResolved"))
                EntityManager.CombatLog.AbilityResolved += OnAbilityResolved;
            _checks = 0;
            _misses = 0;
            timeline.At(0, () =>
            {
                if (_encounter.PartyPositions != null && IncludeParty)
                    GeneratePartyMembers((int)PlayerSlot, PrepareActors);
                else
                    PrepareActors();
            }, "准备本地模拟");
        }

        private void PrepareActors()
        {
            if (_encounter.PartyPositions != null)
            {
                _positions = (Vector2[])_encounter.PartyPositions.Clone();
                if (IncludeParty)
                    for (var i = 0; i < _members.Length; i++)
                        _members[i] = Party[_encounter.AssignedSlot(i, SwapRoles)];
                else
                {
                    _members[Self] = EntityManager.Attach(_host.EntityProvider.GetMyself());
                    if (_encounter.InitialEnmity.Count != 0) Party.Add(_members[Self]);
                }
                for (var i = 0; i < _members.Length; i++)
                    if (_members[i] != null)
                        _members[i].Pos = _positions[i];
            }
            var ready = WhenAllActorsReady(_encounter.Actors.Count, () =>
            {
                if (_flow == null) Play(_encounter.BuildTimeline(Run));
                else _flow.Play(_encounter.Timeline);
            });
            foreach (var spec in _encounter.Actors)
                EntityManager.SpawnNetworkBNpc(spec.Spawn, spawned =>
                {
                    _actors.Add(spec.Id, spawned);
                    if (_encounter.InitialEnmity.TryGetValue(spec.Id, out var scores))
                        EntityManager.RegisterScriptEnemy(spawned, Party.Select(m => scores[Array.IndexOf(_members, m)]).ToArray(),
                            _encounter.Damageable.Contains(spec.Id));
                    if (_encounter.InitiallyHidden.Contains(spec.Id))
                        SetVisible(spawned, false);
                    ready();
                });
        }

        private void SetVisible(SimActor actor, bool visible, bool targetable = true)
        {
            if (visible)
                actor.EnableDraw();
            else
                actor.Entity.DisableDraw();
            actor.SetTargetable(visible && targetable);
            EntityManager.SetEnemyListVisibility(actor, visible && targetable);
        }

        private void Run(LocalEvent e)
        {
            var actor = e.Actor == null ? null : _actors[e.Actor];
            var role = e.Target == null ? e.Role : TargetSlot(e.Target);
            var target = role < 0 ? null : _members[role];
            if (e.Target != null && (target?.IsActive != true || target.Entity.HP == 0)) return;
            switch (e.Op)
            {
                case "selectTarget":
                case "trackTether":
                    if (e.Op == "trackTether") CancelFixedLine(e.Actor);
                    _targets.Start(e);
                    if (_targets.Tracking && _targetPoll == null) PollTargets();
                    break;
                case "resolveTether":
                    _targets.Resolve(e.Key);
                    var holder = _targets.Holder(e.Key);
                    if (ShowHints) Report(e.Key + " 收线：" + (holder < 0 ? "无人" : "{" + LocalEncounter.Roles[holder] + "}"));
                    break;
                case "partyHit":
                    if (e.Role != Self && target != null) EntityManager.ApplyScriptHit(target, actor, e.Damage);
                    break;
                case "cast":
                    if (_groundCasts.TryGetValue(actor, out var previous))
                    {
                        previous.Cancel();
                        _groundCasts.Remove(actor);
                    }
                    if (e.Ground)
                    {
                        var spot = e.Role < 0 ? actor.Pos3D : target?.Pos3D ?? new Vector3(_positions[e.Role], actor.Pos3D.Z);
                        var heading = actor.Heading;
                        var seconds = e.CastSeconds(GameDataService.Instance);
                        actor.Cast(e.ActionId, omenPosition: spot, omenHeading: heading, executeOnFinish: false);
                        _groundCasts[actor] = Scheduler.ScheduleAfter(seconds, () =>
                        {
                            _groundCasts.Remove(actor);
                            if (actor.IsActive) actor.ExecuteActionEffectAt(e.ActionId, spot, heading);
                        }, "地面技能结算");
                    }
                    else
                        actor.Cast(e.ActionId, target: target);
                    break;
                case "visible":
                    SetVisible(actor, e.Visible, e.Targetable);
                    break;
                case "place":
                    actor.ActorSetPos(new Vector3(e.Position, actor.Pos3D.Z), e.Heading);
                    break;
                case "move":
                    foreach (var slot in e.Slots)
                    {
                        _positions[slot] = e.Position;
                        if (slot != Self)
                            _members[slot]?.MoveNetworkTo(e.Position, 60);
                    }
                    break;
                case "check":
                    if (e.Slots.Contains(Self))
                        Grade(e, Vector2.Distance(_members[Self].Pos, e.Position) <= e.Radius);
                    break;
                case "judge":
                    Grade(e, _flow.Test(e.Condition));
                    break;
                case "health":
                    actor.Entity.SetNetworkVitals((uint)Math.Round(actor.Entity.MaxHP * (double)e.Percent / 100));
                    break;
                case "knockback":
                    var effect = e.Ground ? KnockbackEffect.RadialFromRow(e.Position, (ushort)e.KnockbackId)
                        : KnockbackEffect.DirectionalFromRow(e.Heading, (ushort)e.KnockbackId);
                    SimKnockback.Resolve(Scheduler, e.Slots.Select(slot => _members[slot]), effect);
                    break;
                case "mapEffect":
                    if (PlayMapEffect(e.MapSlot, e.MapFlags)) _changedMap.Add(e.MapSlot);
                    break;
                case "finish":
                    _targetPoll?.Cancel();
                    _targets.Clear();
                    var subject = _flow == null ? "个人站位" : "本轮判定";
                    Report($"练习结束。{subject}通过 {_checks - _misses}/{_checks} 项，可停止或切换职能重练。");
                    break;
                case "hint":
                    if (ShowHints)
                        Report(e.Message);
                    break;
                case "tether":
                    if (target == null) break;
                    _targets.CancelActor(e.Actor);
                    CancelFixedLine(e.Actor);
                    actor.TetherTo(target, e.TetherId);
                    _fixedLines[e.Actor] = Scheduler.ScheduleAfter(e.Duration, () =>
                    {
                        _fixedLines.Remove(e.Actor);
                        actor.CancelTether(tetherId: e.TetherId);
                    }, "连线结束");
                    break;
                case "object":
                    var index = Array.IndexOf(_objectSlots, false);
                    if (index < 0) throw new InvalidOperationException(e.Path + "：同时出现的场地物件超过 40 个");
                    var prop = EntityManager.SpawnEObj((ushort)index, e.ObjectId, actor.Pos3D);
                    var objectKey = (e.Actor, e.ObjectId);
                    _objects.TryGetValue(objectKey, out var active);
                    _objects[objectKey] = active + 1;
                    _objectSlots[index] = true;
                    Scheduler.ScheduleAfter(e.Duration, () =>
                    {
                        EntityManager.Delete(prop);
                        _objectSlots[index] = false;
                        if (--_objects[objectKey] == 0) _objects.Remove(objectKey);
                    }, "回收场地物件");
                    break;
                case "icon":
                    if (target != null)
                        target.ActorControl(Game.ActorControlCategory.TargetIcon, e.IconId, target.Id);
                    break;
                case "status":
                    var recipient = e.Target == null && e.Role < 0 ? actor : target;
                    if (recipient != null)
                    {
                        if (e.Duration == 0)
                            EntityManager.RemoveStatus(recipient, e.StatusId);
                        else
                        {
                            ushort stacks = e.Stacks;
                            if (e.AddStacks > 0)
                            {
                                EntityManager.TryGetOwnedStatus(recipient, e.StatusId, out var prior, out _);
                                stacks = (ushort)Math.Min(ushort.MaxValue, prior + e.AddStacks);
                            }
                            EntityManager.SetStatus(recipient, e.StatusId, e.Duration, stacks, actor);
                        }
                    }
                    break;
            }
        }

        private Vector2? Position(int slot) => slot >= 0 && _members[slot]?.IsActive == true && _members[slot].Entity.HP != 0
            ? _members[slot].JudgementPosition : (Vector2?)null;

        private int TargetSlot(string key) => key == "$member" ? _flow.Member : _targets.Current(key);

        private IEnumerable<int> EnmityOrder(string actor, bool first)
        {
            var order = first ? new[] { EntityManager.FirstEngager(_actors[actor]) } : EntityManager.GetPartyEnmityOrder(_actors[actor]);
            return order.Where(m => m != null).Select(m => Array.IndexOf(_members, m));
        }

        private void PollTargets()
        {
            _targetPoll = _targets.Tracking ? Scheduler.ScheduleAfter(0.1, () => { _targets.Refresh(); PollTargets(); }, "更新连线") : null;
        }

        private void CancelFixedLine(string actor)
        {
            if (!_fixedLines.TryGetValue(actor, out var timer)) return;
            timer.Cancel();
            _fixedLines.Remove(actor);
            _actors[actor].CancelTether();
        }

        private void Grade(LocalEvent e, bool passed)
        {
            _checks++;
            if (!passed) _misses++;
            var label = e.Op == "check" ? "站位" : "判定";
            var slot = _flow?.Member ?? -1;
            var member = slot < 0 ? "" : "{" + LocalEncounter.Roles[slot] + "} · ";
            Report(label + (passed ? "通过 · " : "失败 · ") + member + e.Message);
            _flow?.Record(e.CheckId, passed);
        }

        private bool ReadWorld(LocalCondition c)
        {
            var actor = c.Actor == null ? null : _actors[c.Actor];
            switch (c.Type)
            {
                case "hpAtMost":
                    return actor.IsActive && actor.Entity.MaxHP > 0 && actor.Entity.HP * 100d <= actor.Entity.MaxHP * c.Percent;
                case "targetIs": return TargetSlot(c.Target) == c.Role && Position(c.Role).HasValue;
                case "objectActive": return _objects.ContainsKey((c.Actor, c.ObjectId));
                case "hasStatus":
                    var slot = c.Target == null ? c.Role : TargetSlot(c.Target);
                    var target = actor ?? (slot < 0 ? null : _members[slot]);
                    if (target?.IsActive != true) return false;
                    return c.MinStacks == 0 ? target.Entity.HasStatus(c.StatusId) :
                        EntityManager.TryGetOwnedStatus(target, (ushort)c.StatusId, out var stacks, out _) && stacks >= c.MinStacks;
                case "areaCount":
                    if (actor != null && !actor.IsActive) return false;
                    var center = c.CenterRole >= 0 ? Position(c.CenterRole) : actor?.Pos ?? c.Position;
                    if (!center.HasValue) return false;
                    var origin = center.Value;
                    var heading = c.Heading ?? actor?.Heading ?? (c.CenterRole >= 0 ? _members[c.CenterRole].Heading : 0);
                    var slots = c.Target == null ? c.Slots : new[] { TargetSlot(c.Target) };
                    var positions = slots.Select(Position).Where(p => p.HasValue).Select(p => p.Value);
                    return c.MatchesArea(positions, origin, heading);
                default: throw new InvalidOperationException("不支持的条件 " + c.Type);
            }
        }

        protected override void OnStop()
        {
            if (_mapWorld == _host.WorldGeneration) _targets?.Clear();
            EntityManager.CombatLog.CastStarted -= OnCastStarted;
            EntityManager.CombatLog.AbilityResolved -= OnAbilityResolved;
            if (_mapWorld == _host.WorldGeneration)
                foreach (var slot in _changedMap)
                    PlayMapEffect(slot, _encounter.Phase.MapEffectFlags[slot].Value);
            _changedMap.Clear();
        }

        private void OnCastStarted(Core.Entity.Character source, uint actionId) => SignalCast("castStarted", source, actionId);
        private void OnAbilityResolved(Core.Entity.Character source, uint actionId) => SignalCast("castResolved", source, actionId);

        private void SignalCast(string type, Core.Entity.Character source, uint actionId)
        {
            if (!Scheduler.IsSchedulerThread) return;
            foreach (var pair in _actors)
                if (ReferenceEquals(pair.Value.Entity, source))
                {
                    _flow.Emit(new LocalSignal { Type = type, Actor = pair.Key, ActionId = actionId });
                    return;
                }
        }

        private void Report(string text)
        {
            text = _encounter.RoleText(text, SwapRoles);
            _host.LogSim(text);
            GameChat.SendEcho(text);
        }
    }
}
