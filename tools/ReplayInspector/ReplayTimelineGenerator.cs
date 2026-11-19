using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Simulant.Replay
{
    /// <summary>只消费中间文档，生成可接入 SimLogicBase 的 C# 时间轴底稿。</summary>
    internal sealed class ReplayTimelineGenerator
    {
        private const int NodesPerMethod = 160;
        private readonly Dictionary<int, ReplayEntity> _entities;
        private readonly ReplayTimelineDocument _document;
        private readonly TextWriter _writer;
        private readonly List<string> _nodes = new List<string>();
        internal readonly Dictionary<string, int> Pending = new Dictionary<string, int>();
        internal int NodeCount { get; private set; }

        internal ReplayTimelineGenerator(ReplayTimelineDocument document, TextWriter writer)
        {
            _document = document;
            _writer = writer;
            _entities = document.Entities.ToDictionary(entity => entity.Id);
        }

        internal void Write()
        {
            _writer.NewLine = "\r\n";
            foreach (var e in _document.InitialState.Concat(_document.Events))
            {
                var statement = Statement(e);
                var time = (Math.Max(0, e.TimeMs) / 1000m).ToString("0.###", CultureInfo.InvariantCulture);
                if (statement == null)
                {
                    var label = new string(e.Kind.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
                    Pending.TryGetValue(label, out var count);
                    Pending[label] = count + 1;
                    _nodes.Add($"// 待核对 {time}s {label}，记录 {e.Record}，实体 {e.Actor?.ToString() ?? "无"}");
                }
                else if (statement.Length != 0)
                {
                    _nodes.Add($"timeline.At({time}, () => {statement}, \"{e.Kind} #{e.Record}\");");
                    NodeCount++;
                }
            }

            _writer.WriteLine("// 回放时间轴底稿。接入时提供小队对应关系，复核点名、命中与随机逻辑。");
            _writer.WriteLine($"// 区域 {_document.TerritoryId}，窗口 {_document.StartMs}–{_document.EndMs} ms。");
            _writer.WriteLine("using Simulant.Core.Entity;");
            _writer.WriteLine("using Simulant.Game;");
            _writer.WriteLine("using Simulant.Game.FFCS.Client.Game.Network;");
            _writer.WriteLine("using Simulant.Simulation.Runtime;");
            _writer.WriteLine("using Simulant.Simulation.Timeline;");
            _writer.WriteLine("using System;");
            _writer.WriteLine("using System.Collections.Generic;");
            _writer.WriteLine("using System.Numerics;");
            _writer.WriteLine();
            _writer.WriteLine("namespace Simulant.Content.Replay");
            _writer.WriteLine("{");
            _writer.WriteLine("    internal sealed class ReplayTimelineDraft");
            _writer.WriteLine("    {");
            _writer.WriteLine("        private readonly SimEntityManager _entities;");
            _writer.WriteLine("        private readonly Func<int, SimActor> _partyMember;");
            _writer.WriteLine("        private readonly Action<uint, ushort, ushort> _mapEffect;");
            _writer.WriteLine("        private readonly Action<byte, float> _weather;");
            _writer.WriteLine("        private readonly Dictionary<int, SimActor> _actors = new Dictionary<int, SimActor>();");
            _writer.WriteLine("        private readonly Dictionary<int, EventObject> _objects = new Dictionary<int, EventObject>();");
            _writer.WriteLine();
            _writer.WriteLine("        internal ReplayTimelineDraft(SimEntityManager entities, Func<int, SimActor> partyMember,");
            _writer.WriteLine("            Action<uint, ushort, ushort> mapEffect, Action<byte, float> weather)");
            _writer.WriteLine("        {");
            _writer.WriteLine("            _entities = entities;");
            _writer.WriteLine("            _partyMember = partyMember;");
            _writer.WriteLine("            _mapEffect = mapEffect;");
            _writer.WriteLine("            _weather = weather;");
            _writer.WriteLine("        }");
            _writer.WriteLine();
            _writer.WriteLine("        private SimActor Actor(int id) => _actors.TryGetValue(id, out var actor) ? actor : null;");
            _writer.WriteLine("        private EntityBase Entity(int id) => (EntityBase)Actor(id)?.Entity ??");
            _writer.WriteLine("            (_objects.TryGetValue(id, out var obj) ? obj : null);");
            _writer.WriteLine();
            _writer.WriteLine("        private void Despawn(int id, bool owned)");
            _writer.WriteLine("        {");
            _writer.WriteLine("            if (owned && Actor(id) is SimActor actor)");
            _writer.WriteLine("                _entities.Delete(actor);");
            _writer.WriteLine("            if (_objects.TryGetValue(id, out var obj))");
            _writer.WriteLine("                _entities.Delete(obj);");
            _writer.WriteLine("            _actors.Remove(id);");
            _writer.WriteLine("            _objects.Remove(id);");
            _writer.WriteLine("        }");
            _writer.WriteLine();
            _writer.WriteLine("        private void SetStatus(int target, ushort id, float seconds, ushort param, int source)");
            _writer.WriteLine("        {");
            _writer.WriteLine("            if (Actor(target) is SimActor actor && Actor(source) is SimActor caster)");
            _writer.WriteLine("                _entities.SetStatus(actor, id, seconds, param, caster);");
            _writer.WriteLine("        }");
            _writer.WriteLine();
            _writer.WriteLine("        private void RemoveStatus(int target, ushort id)");
            _writer.WriteLine("        {");
            _writer.WriteLine("            if (Actor(target) is SimActor actor)");
            _writer.WriteLine("                _entities.RemoveStatus(actor, id);");
            _writer.WriteLine("        }");
            _writer.WriteLine();
            _writer.WriteLine("        internal void AppendTo(SimTimeline timeline)");
            _writer.WriteLine("        {");

            for (var i = 0; i < _nodes.Count; i += NodesPerMethod)
                _writer.WriteLine($"            AppendPart{i / NodesPerMethod + 1}(timeline);");
            _writer.WriteLine("        }");

            for (var i = 0; i < _nodes.Count; i += NodesPerMethod)
            {
                _writer.WriteLine();
                _writer.WriteLine($"        private void AppendPart{i / NodesPerMethod + 1}(SimTimeline timeline)");
                _writer.WriteLine("        {");
                for (var j = i; j < Math.Min(_nodes.Count, i + NodesPerMethod); j++)
                    _writer.WriteLine("            " + _nodes[j]);
                _writer.WriteLine("        }");
            }

            _writer.WriteLine("    }");
            _writer.WriteLine("}");
        }

        private string Statement(ReplayEvent e)
        {
            switch (e.Kind)
            {
                case "MapEffect":
                case "MapEffectState":
                    return e.MapSlot.HasValue && e.SharedTimelineState.HasValue
                        ? $"_mapEffect({e.MapSlot}, {Hex(e.PlaybackTimeline ?? e.SharedTimelineState.Value)}, {Hex(e.SharedTimelineState.Value)})" : null;
                case "Weather":
                    return e.WeatherId.HasValue && e.WeatherId <= byte.MaxValue
                        ? $"_weather({e.WeatherId}, {Float(e.TransitionTime ?? 1f)})" : null;
                case "EffectResult":
                case "Party":
                case "Target":
                case "SoftTarget":
                case "DirectorInit":
                case "DirectorClear":
                case "DirectorUpdate":
                    return string.Empty;
            }

            if (!e.Actor.HasValue || !_entities.TryGetValue(e.Actor.Value, out var entity))
                return null;
            var id = entity.Id;
            var actor = $"Actor({id})";
            var nativeEntity = $"Entity({id})";
            var target = e.Target.HasValue ? $"Actor({e.Target.Value})" : "null";
            var pos = e.Position?.IsFinite == true ? Position(e.Position) : null;
            var heading = e.Heading.HasValue ? Float(e.Heading.Value) : null;

            switch (e.Kind)
            {
                case "Spawn":
                    switch (entity.Kind)
                    {
                        case "Player":
                            return entity.PartySlot.HasValue ? $"_actors[{id}] = _partyMember({entity.PartySlot})" : null;
                        case "Npc":
                            return entity.BaseId != 0 && pos != null && heading != null
                                ? $"_actors[{id}] = _entities.SpawnBNpc(new CharacterSpawnSpec {{ BNpcBaseId = {entity.BaseId}, BNpcNameId = {entity.NameId}, Level = {Math.Max(1, (int)entity.Level)}, Pos = {pos}, Heading = {heading} }})" : null;
                        case "EventObject":
                            return entity.SpawnSlot < 40 && entity.BaseId != 0 && pos != null && heading != null
                                ? $"_objects[{id}] = _entities.SpawnEObj(new EventObjectSpawnSpec {{ Index = {entity.SpawnSlot}, BaseId = {entity.BaseId}, LayoutId = {entity.LayoutId}u, EventId = {entity.EventId}u, GimmickId = {entity.GimmickId}u, TargetableFlags = {entity.TargetableFlags}, Hidden = {Bool(entity.Hidden)}, Scale = {Float(entity.Scale ?? 1f)}, FateId = {entity.FateId}, EventState = {entity.EventState}, SharedTimelineState = {Hex(entity.SharedTimelineState)}, SharedGroupState = {entity.SharedGroupState}u, Pos = {pos}, Heading = {heading} }})" : null;
                        default:
                            return null;
                    }
                case "Despawn":
                    return $"Despawn({id}, {Bool(entity.Kind != "Player")})";
                case "Move":
                    return entity.Kind == "Npc" && pos != null && heading != null && e.Speed.HasValue
                        ? $"{actor}?.ActorMove({pos}, {heading}, {e.Speed}, (ActorMoveFlags){Hex(e.MoveFlags.GetValueOrDefault())})" : null;
                case "SetPosition":
                    return entity.Kind == "Npc" && pos != null && heading != null ? $"{actor}?.ActorSetPos({pos}, {heading})" : null;
                case "Cast":
                    return entity.Kind == "Npc" && e.ActionType == 1 && e.ActionId.HasValue &&
                        e.ActionId == e.SpellId && pos != null && heading != null && e.CastTime.HasValue && e.OmenDelay.HasValue
                        ? $"{actor}?.Cast({Hex(e.ActionId.Value)}, {Float(e.OmenDelay.Value)}, {target}, omenPosition: {pos}, omenHeading: {heading}, castTime: {Float(e.CastTime.Value)}, executeOnFinish: false)" : null;
                case "CancelCast":
                    return entity.Kind == "Npc" ? $"{actor}?.StopCasting()" : null;
                case "ActionEffect":
                    if (entity.Kind != "Npc" || e.ActionType != 1 || e.ActionId != e.SpellId || !e.ActionId.HasValue || heading == null)
                        return null;
                    var hitTargets = HitTargetsArgument(e.Targets);
                    return !e.Target.HasValue && pos != null
                        ? $"{actor}?.ExecuteActionEffectAt({Hex(e.ActionId.Value)}, {pos}, {heading}, {Float(e.AnimationLock ?? 0f)}{hitTargets})"
                        : $"{actor}?.ExecuteActionEffect({Hex(e.ActionId.Value)}, {target}, {heading}{hitTargets})";
                case "StatusSet":
                    return e.Status?.Source != null && e.Status.Seconds > 0 && ReplayPosition.Finite(e.Status.Seconds)
                        ? $"SetStatus({id}, {e.Status.Id}, {Float(e.Status.Seconds)}, {e.Status.Param}, {e.Status.Source})" : null;
                case "StatusRemove":
                    return e.Status?.Source != null && e.Status.Seconds > 0 ? $"RemoveStatus({id}, {e.Status.Id})" : null;
                case "PlayTimeline":
                    return e.Values?.Length >= 2 ? Control(nativeEntity, "PlayActionTimeline", e.Values[0], e.Values[1]) : null;
                case "Targetable":
                    return e.Values?.Length >= 1 ? $"{nativeEntity}?.SetTargetable({Bool(e.Values[0] != 0)})" : null;
                case "EventState":
                    return e.Values?.Length >= 1 && e.Values[0] <= byte.MaxValue ? $"{nativeEntity}?.SetEventState({e.Values[0]})" : null;
                case "EventObjectAnimation":
                    return e.PlaybackTimeline.HasValue && e.SharedTimelineState.HasValue
                        ? $"({nativeEntity} as EventObject)?.PlayAnimation({Hex(e.SharedTimelineState.Value)}, {Hex(e.PlaybackTimeline.Value)})" : null;
                case "SetSharedTimelineState":
                    return e.Values?.Length >= 1 && e.Values[0] <= ushort.MaxValue
                        ? $"({nativeEntity} as EventObject)?.SetSharedTimelineState({Hex(e.Values[0])})" : null;
                case "HeadMarker":
                    return e.Target.HasValue && e.Values?.Length >= 1
                        ? $"{actor}?.ActorControl(ActorControlCategory.TargetIcon, {Hex(e.Values[0])}, {target}?.Id ?? 0xE0000000u)" : null;
                case "Tether":
                    return e.Target.HasValue && e.Values?.Length >= 3
                        ? $"{actor}?.ActorControl(ActorControlCategory.Tether, {e.Values[0]}u, {e.Values[1]}u, {target}?.Id ?? 0xE0000000u, {e.Values[2]}u)" : null;
                case "TetherCancel":
                    return e.Values?.Length >= 2 ? $"{actor}?.CancelTether({e.Values[0]}u, {e.Values[1]}u)" : null;
                case "PositionVfx":
                    return e.Values?.Length >= 1 && pos != null ? $"{actor}?.Entity.PlayPositionVfx({e.Values[0]}u, {pos})" : null;
                case "ModelState":
                case "ModelScale":
                case "AnimationState":
                case "StanceChange":
                    return e.Values?.Length >= 2 ? Control(nativeEntity, PresentationCategory(e.Kind), e.Values[0], e.Values[1]) : null;
                default:
                    return null;
            }
        }

        private static string HitTargetsArgument(ReplayActionTarget[] targets)
        {
            if (targets == null)
                return string.Empty;

            var actors = targets.Where(target => target?.Actor != null).Select(target => $"Actor({target.Actor.Value})").ToArray();
            return actors.Length == 0
                ? ", hitTargets: Array.Empty<SimActor>()"
                : $", hitTargets: new[] {{ {string.Join(", ", actors)} }}";
        }

        private static string PresentationCategory(string kind)
        {
            return kind == "StanceChange" ? kind : "Set" + kind;
        }

        private static string Control(string entity, string category, uint a1, uint a2)
            => $"{entity}?.ActorControl(ActorControlCategory.{category}, {a1}u, {a2}u)";

        private static string Position(ReplayPosition p)
            => $"new Vector3({Float(p.X)}, {Float(p.Y)}, {Float(p.Z)})";

        private static string Float(float value)
        {
            if (!ReplayPosition.Finite(value))
                throw new InvalidDataException("时间轴包含非有限数。");

            return value.ToString("R", CultureInfo.InvariantCulture) + "f";
        }

        private static string Hex(uint value) => "0x" + value.ToString("X", CultureInfo.InvariantCulture);
        private static string Bool(bool value) => value ? "true" : "false";
    }
}
