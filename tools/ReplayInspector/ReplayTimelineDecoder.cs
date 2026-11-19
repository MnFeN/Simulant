using Simulant.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using static Simulant.Replay.ReplayReader;

namespace Simulant.Replay
{
    internal enum ReplayPacketUse
    {
        Timeline,
        Reference,
        Filtered,
        Unresolved
    }

    /// <summary>将 7.58 回放记录归一化为时间轴事件，并维护窗口起点的实体和场地快照。</summary>
    internal sealed partial class ReplayTimelineDecoder
    {
        private const uint NoActor = 0xE0000000;
        private const byte EventObjectKind = 7;
        private readonly ReplayTimelineDocument _document;
        private readonly Dictionary<uint, ActorState> _actors = new Dictionary<uint, ActorState>();
        private readonly Dictionary<int, uint> _spawnSlots = new Dictionary<int, uint>();
        private readonly Dictionary<uint, byte> _partySlots = new Dictionary<uint, byte>();
        private readonly Dictionary<byte, ReplayEvent> _map = new Dictionary<byte, ReplayEvent>();
        private readonly Dictionary<byte, ReplayEvent> _waymarks = new Dictionary<byte, ReplayEvent>();
        private readonly Dictionary<uint, bool> _effectSequences = new Dictionary<uint, bool>();
        private ReplayEvent _weather;
        private ReplayPacket _packet;
        private bool _insideWindow;
        private int _nextActorId;

        private sealed class ActorState
        {
            internal ReplayEntity Entity;
            internal uint Owner;
            internal ReplayEvent Spawn;
            internal ReplayEvent Position;
            internal ReplayEvent Cast;
            internal readonly Dictionary<int, ReplayEvent> Statuses = new Dictionary<int, ReplayEvent>();
            internal readonly Dictionary<string, ReplayEvent> Presentation = new Dictionary<string, ReplayEvent>();
        }

        internal ReplayTimelineDecoder(ReplayTimelineDocument document)
        {
            _document = document;
        }

        internal ReplayPacketUse Read(ReplayPacket packet, string name)
        {
            _packet = packet;
            _insideWindow = packet.TimestampMs >= _document.StartMs && packet.TimestampMs <= _document.EndMs;
            var b = packet.Payload;
            _actors.TryGetValue(packet.ObjectId, out var actor);

            switch (name)
            {
                case "InitZone":
                    if (_document.TerritoryId != 0 && _document.TerritoryId != UInt16(b, 2))
                        return ReplayPacketUse.Unresolved;
                    _document.TerritoryId = UInt16(b, 2);
                    _weather = Event("Weather", null);
                    _weather.WeatherId = b[0x10];
                    return ReplayPacketUse.Reference;
                case "PlayerSpawn":
                case "NpcSpawn":
                case "NpcSpawn2":
                    return SpawnCharacter(name == "PlayerSpawn", b);
                case "ObjectSpawn":
                    return SpawnObject(b);
                case "DespawnCharacter":
                    return Despawn(b[0], UInt32(b, 4));
                case "ObjectDespawn":
                    return Despawn(0x100 + b[0], 0);
                case "UpdateParty":
                    return UpdateParty(b);
                case "ActorMove":
                case "ActorSetPos":
                    return Move(actor, b, name == "ActorSetPos");
                case "ActorCast":
                    return Cast(actor, b);
                case "ActionEffect1":
                case "ActionEffect8":
                case "ActionEffect16":
                case "ActionEffect24":
                case "ActionEffect32":
                    return ActionEffect(actor, b, b.Length == 0x78 ? 1 : (b.Length - 56) / 72);
                case "EffectResult":
                case "EffectResult4":
                case "EffectResult8":
                case "EffectResult16":
                case "EffectResultBasic":
                case "EffectResultBasic4":
                case "EffectResultBasic8":
                case "EffectResultBasic16":
                case "EffectResultBasic32":
                case "EffectResultBasic64":
                    return EffectResult(b, name.StartsWith("EffectResultBasic", StringComparison.Ordinal));
                case "StatusEffectList":
                    return StatusSnapshot(actor, b, 0x14, -1);
                case "BossStatusEffectList":
                case "StatusEffectListDouble":
                    return StatusSnapshot(actor, b, 0x17C, 0);
                case "StatusEffectListPlayerDouble":
                    return StatusSnapshot(actor, b, 0, 0x168);
                case "StatusEffectList2":
                case "StatusEffectList3":
                    return ReplayPacketUse.Unresolved;
                case "ActorControl":
                case "ActorControlSelf":
                case "ActorControlTarget":
                    return ActorControl(actor, b, name);
                case "ActorTimelineBatch":
                    // 0x140AFCE20 等待生成完成后，按配对数组逐个调用 0x1408BDCB0。
                    for (var i = 0; i < 10; i++)
                    {
                        var id = UInt32(b, i * 4);
                        if (id == NoActor)
                            break;
                        var timeline = UInt16(b, 0x28 + i * 2);
                        if (timeline != 0)
                        {
                            var e = Event("PlayTimeline", Find(id));
                            e.Values = new[] { (uint)timeline, 0u };
                            Add(e);
                        }
                    }
                    return ReplayPacketUse.Timeline;
                case "StatusLoopVfx":
                    return Presentation(actor, "StatusLoopVfx", b[0]);
                case "CharacterDrawData":
                    return Presentation(actor, "CharacterDrawData", UInt32(b, 0));
                case "MapEffect":
                    AddMapEffect(b[8], UInt16(b, 4), UInt16(b, 6));
                    return ReplayPacketUse.Timeline;
                case "MapEffect4":
                case "MapEffect8":
                case "MapEffect12":
                    return MapEffectBatch(b, name == "MapEffect4" ? 4 : name == "MapEffect8" ? 8 : 12);
                case "MapEffectStateSnapshot":
                    return MapEffectSnapshot(b);
                case "WeatherChange":
                    _weather = Event("Weather", null);
                    _weather.WeatherId = b[0];
                    var transition = Float(b, 4);
                    if (!ReplayPosition.Finite(transition) || transition < 0)
                        return ReplayPacketUse.Unresolved;
                    _weather.TransitionTime = transition;
                    _weather.Values = new[] { (uint)(b[1] & 1) };
                    Add(_weather);
                    return ReplayPacketUse.Timeline;
                case "NpcYell":
                    var yell = Event("NpcYell", Find(UInt32(b, 0)));
                    yell.NameId = UInt16(b, 8);
                    yell.TextId = UInt16(b, 12);
                    Add(yell);
                    return ReplayPacketUse.Timeline;
                case "PlaceFieldMarker":
                    return Waymark(b, b[0], b[1] != 0, 4, 8, 12);
                case "PlaceFieldMarkerPreset":
                    for (byte i = 0; i < 8; i++)
                        Waymark(b, i, (b[0] & (1 << i)) != 0, 4 + i * 4, 36 + i * 4, 68 + i * 4);
                    return ReplayPacketUse.Timeline;
                case "ActorGauge":
                case "Countdown":
                case "CountdownCancel":
                case "UpdatePartyMemberPositions":
                    return ReplayPacketUse.Filtered;
                case "PrepareZoning":
                case "UpdateHpMpTp":
                case "UpdateHate":
                case "UpdateHater":
                case "FirstAttack":
                case "SystemLogMessage":
                case "ModelEquip":
                case "EventStart":
                case "EventFinish":
                case "EventPlay":
                case "EventPlay16":
                case "ContentDirectorEvent":
                case "ContentDirectorUiState":
                case "ContentDirectorSlotAssignments":
                    return ReplayPacketUse.Reference;
                case "IDScramble":
                    return UInt32(b, 0) == 0 ? ReplayPacketUse.Reference : ReplayPacketUse.Unresolved;
                default:
                    return ReplayPacketUse.Unresolved;
            }
        }

        internal void CaptureInitial()
        {
            foreach (var actor in _actors.Values.OrderBy(a => a.Entity.Id))
            {
                var start = CopyAtStart(actor.Spawn);
                var transform = actor.Position ?? actor.Spawn;
                start.Position = transform.Position;
                start.Heading = transform.Heading;
                _document.InitialState.Add(start);
            }

            foreach (var actor in _actors.Values.OrderBy(a => a.Entity.Id))
            {
                foreach (var state in actor.Presentation.Values.OrderBy(e => e.Record))
                    _document.InitialState.Add(CopyAtStart(state));
                foreach (var status in actor.Statuses.Values.OrderBy(e => e.Status.Slot))
                {
                    var remaining = Remaining(status, 0);
                    if (remaining <= 0 && status.Status.Seconds > 0)
                        continue;
                    var state = CopyAtStart(status);
                    state.Kind = "StatusSet";
                    state.Status = new ReplayStatus
                    {
                        Id = status.Status.Id,
                        Param = status.Status.Param,
                        Source = status.Status.Source,
                        Slot = status.Status.Slot,
                        Seconds = remaining
                    };
                    _document.InitialState.Add(state);
                }

                if (actor.Cast != null && actor.Cast.TimeMs + actor.Cast.CastTime.GetValueOrDefault() * 1000 > 0)
                {
                    var cast = CopyAtStart(actor.Cast);
                    cast.Kind = "CastInProgress";
                    cast.ActionId = actor.Cast.ActionId;
                    cast.CastTime = actor.Cast.CastTime;
                    cast.OmenDelay = actor.Cast.OmenDelay;
                    cast.Target = actor.Cast.Target;
                    _document.InitialState.Add(cast);
                }
            }

            if (_weather != null)
                _document.InitialState.Add(CopyAtStart(_weather));
            _document.InitialState.AddRange(_map.Values.OrderBy(e => e.Record).Select(CopyAtStart));
            _document.InitialState.AddRange(_waymarks.Values.OrderBy(e => e.Waymark).Select(CopyAtStart));
        }

        private static ReplayEvent CopyAtStart(ReplayEvent source)
        {
            return new ReplayEvent
            {
                Kind = source.Kind,
                Record = source.Record,
                Offset = source.Offset,
                Actor = source.Actor,
                Target = source.Target,
                Heading = source.Heading,
                Position = source.Position,
                Values = source.Values,
                PlaybackTimeline = source.SharedTimelineState ?? source.PlaybackTimeline,
                SharedTimelineState = source.SharedTimelineState,
                MapSlot = source.MapSlot,
                Waymark = source.Waymark,
                Active = source.Active,
                WeatherId = source.WeatherId,
                TransitionTime = source.TransitionTime
            };
        }

        internal void PruneUnusedEntities()
        {
            var used = new HashSet<int>();

            foreach (var e in _document.InitialState.Concat(_document.Events))
            {
                if (e.Actor.HasValue)
                    used.Add(e.Actor.Value);
                if (e.Target.HasValue)
                    used.Add(e.Target.Value);
                if (e.Status?.Source is int source)
                    used.Add(source);
                if (e.Targets != null)
                    foreach (var target in e.Targets)
                        if (target.Actor.HasValue)
                            used.Add(target.Actor.Value);
            }

            for (var i = _document.Entities.Count - 1; i >= 0; i--)
            {
                var entity = _document.Entities[i];
                if (used.Contains(entity.Id) && entity.Owner.HasValue)
                    used.Add(entity.Owner.Value);
            }

            _document.Entities.RemoveAll(entity => !used.Contains(entity.Id));
        }

        private ReplayPacketUse SpawnCharacter(bool player, byte[] b)
        {
            // SpawnPackets.cs/FFCS：NPC 的 CommonSpawnData 在 0x10，玩家在 0x20。
            var c = player ? 0x20 : 0x10;
            var rawId = _packet.ObjectId;
            var entity = new ReplayEntity
            {
                Kind = player ? "Player" : b[c + 0x71] == 2 ? "Npc" : "OtherCharacter",
                BaseId = UInt32(b, c + 0x30),
                NameId = UInt32(b, c + 0x34),
                LayoutId = UInt32(b, c + 0x38),
                EventId = UInt32(b, c + 0x40),
                DisplayFlags = UInt32(b, c + 0x54),
                Model = UInt16(b, c + 0x60),
                SpawnSlot = b[c + 0x6E],
                SubKind = b[c + 0x72],
                Level = b[c + 0x76],
                Job = b[c + 0x77],
                EventState = b[c + 0x78],
                StatusLoopVfx = b[c + 0x7F],
                ModelScale = b[c + 0x82],
                ModelState = b[c + 0x83],
                AnimationState = b[c + 0x85]
            };

            if (player && _partySlots.TryGetValue(rawId, out var slot))
                entity.PartySlot = slot;

            var state = Spawn(rawId, entity, entity.SpawnSlot, FloatPosition(b, c + 0x1F0),
                Heading(b, c + 0x62), UInt32(b, c + 0x44));
            if (state == null)
                return ReplayPacketUse.Unresolved;

            StatusSnapshot(state, b, c + 0x88, -1);
            return ReplayPacketUse.Timeline;
        }

        private ReplayPacketUse SpawnObject(byte[] b)
        {
            // SpawnObject 的包头路由给收件者，实体 ID 在正文 0x08。
            var entity = new ReplayEntity
            {
                Kind = b[1] == EventObjectKind ? "EventObject" : "OtherObject",
                SpawnSlot = b[0],
                TargetableFlags = b[2],
                Hidden = b[3] != 0,
                BaseId = UInt32(b, 4),
                LayoutId = UInt32(b, 0x0C),
                EventId = UInt32(b, 0x10),
                GimmickId = UInt32(b, 0x18),
                Scale = Float(b, 0x1C),
                FateId = UInt16(b, 0x24),
                EventState = b[0x26],
                SharedTimelineState = UInt16(b, 0x2C),
                SharedGroupState = UInt32(b, 0x30)
            };

            return Spawn(UInt32(b, 8), entity, 0x100 + entity.SpawnSlot, FloatPosition(b, 0x34),
                Heading(b, 0x22), UInt32(b, 0x14)) != null ? ReplayPacketUse.Timeline : ReplayPacketUse.Unresolved;
        }

        private ActorState Spawn(uint rawId, ReplayEntity entity, int slot, ReplayPosition position, float heading, uint owner)
        {
            if (rawId == 0 || rawId == NoActor || !position.IsFinite || entity.Scale.HasValue && !ReplayPosition.Finite(entity.Scale.Value))
                return null;

            if (_spawnSlots.TryGetValue(slot, out var priorId) && priorId != rawId)
                Despawn(slot, priorId);
            if (_actors.TryGetValue(rawId, out var old))
                Despawn(Slot(old.Entity), rawId);

            entity.Id = ++_nextActorId;
            entity.Owner = Find(owner)?.Entity.Id;
            _spawnSlots[slot] = rawId;
            var actor = new ActorState
            {
                Entity = entity,
                Owner = owner
            };
            _actors[rawId] = actor;
            actor.Spawn = Event("Spawn", actor);
            actor.Spawn.Position = position;
            actor.Spawn.Heading = heading;
            _document.Entities.Add(entity);
            Add(actor.Spawn);
            return actor;
        }

        private ReplayPacketUse Despawn(int slot, uint rawId)
        {
            if (rawId == NoActor || rawId == 0)
                _spawnSlots.TryGetValue(slot, out rawId);
            if (!_actors.TryGetValue(rawId, out var actor))
                return ReplayPacketUse.Reference;

            var actorSlot = Slot(actor.Entity);
            if (_spawnSlots.TryGetValue(actorSlot, out var recent) && recent == rawId)
                _spawnSlots.Remove(actorSlot);
            _actors.Remove(rawId);
            Add(Event("Despawn", actor));
            return ReplayPacketUse.Timeline;
        }

        private static int Slot(ReplayEntity entity)
        {
            return entity.Kind == "EventObject" || entity.Kind == "OtherObject" ? 0x100 + entity.SpawnSlot : entity.SpawnSlot;
        }

        private ReplayPacketUse UpdateParty(byte[] b)
        {
            var count = b[0xE51];
            if (count > 8)
                return ReplayPacketUse.Unresolved;

            _partySlots.Clear();
            var members = new List<ReplayActionTarget>();
            for (byte i = 0; i < count; i++)
            {
                var offset = i * 0x1C8;
                var id = UInt32(b, offset + 0x38);
                _partySlots[id] = (byte)(i + 1);

                if (Find(id) is ActorState player)
                {
                    if (_packet.TimestampMs <= _document.EndMs)
                        player.Entity.PartySlot = (byte)(i + 1);
                    if (player.Entity.Job == 0 && _packet.TimestampMs <= _document.EndMs)
                        player.Entity.Job = b[offset + 0x55];
                    members.Add(new ReplayActionTarget { Actor = player.Entity.Id });
                }
            }

            // 进图时名单先于 PlayerSpawn，此时由后续生成记录绑定角色。
            if (members.Count > 0)
            {
                var e = Event("Party", null);
                e.Targets = members.ToArray();
                Add(e);
            }

            return ReplayPacketUse.Reference;
        }

        private ReplayPacketUse Move(ActorState actor, byte[] b, bool setPosition)
        {
            if (actor == null)
                return ReplayPacketUse.Reference;

            var move = Event(setPosition ? "SetPosition" : "Move", actor);
            move.Position = setPosition ? FloatPosition(b, 8) : QuantizedPosition(b, 6);
            move.Heading = Heading(b, 0);

            if (!move.Position.IsFinite)
                return ReplayPacketUse.Unresolved;
            if (!setPosition)
            {
                move.Speed = b[4];
                move.MoveFlags = UInt16(b, 2);
            }

            var last = actor.Position ?? actor.Spawn;
            var unchanged = last.Heading == move.Heading && last.Position.X == move.Position.X &&
                last.Position.Y == move.Position.Y && last.Position.Z == move.Position.Z &&
                last.Speed == move.Speed && last.MoveFlags == move.MoveFlags;

            if (!unchanged)
                actor.Position = move;
            if (unchanged || IsPlayerControlled(actor) && (setPosition ? b[2] == 0 : (move.MoveFlags.Value & 8) == 0))
                return ReplayPacketUse.Filtered;

            Add(move);
            return ReplayPacketUse.Timeline;
        }

        private ReplayPacketUse Cast(ActorState actor, byte[] b)
        {
            var target = Find(UInt32(b, 0x0C));
            if (IsPlayerControlled(actor) && IsHostileTarget(target))
                return ReplayPacketUse.Filtered;

            var cast = Event("Cast", actor);
            cast.ActionId = UInt32(b, 4);
            cast.SpellId = UInt16(b, 0);
            cast.ActionType = b[2];
            cast.OmenDelay = b[3] / 10f;
            cast.CastTime = Float(b, 8);
            cast.Target = target?.Entity.Id;
            cast.Heading = Heading(b, 0x10);
            cast.Position = QuantizedPosition(b, 0x18);
            if (!ReplayPosition.Finite(cast.CastTime.Value) || cast.CastTime.Value < 0)
                return ReplayPacketUse.Unresolved;

            if (actor != null)
                actor.Cast = cast;
            Add(cast);
            return ReplayPacketUse.Timeline;
        }

        private ReplayPacketUse ActionEffect(ActorState actor, byte[] b, int capacity)
        {
            // 回放分发器：单体目标从 0x70 读取；群体目标数组从 0x30 + 0x40 * 容量读取。
            if (b[0x21] > capacity)
                return ReplayPacketUse.Unresolved;

            var target = Find(UInt32(b, 0));
            var sequence = UInt32(b, 0x0C);
            var targetsAt = 0x30 + 0x40 * capacity;
            var filter = IsPlayerControlled(actor) && (IsHostileTarget(target) ||
                Enumerable.Range(0, b[0x21]).Any(i => IsHostileTarget(Find(UInt32(b, targetsAt + i * 8)))));
            if (sequence != 0)
                _effectSequences[sequence] = filter;
            if (filter)
                return ReplayPacketUse.Filtered;
            if (!ReplayPosition.Finite(Float(b, 0x10)))
                return ReplayPacketUse.Unresolved;
            if (actor?.Cast?.ActionId == UInt32(b, 8))
                actor.Cast = null;
            if (!_insideWindow)
                return ReplayPacketUse.Timeline;

            var effect = Event("ActionEffect", actor);
            effect.Target = target?.Entity.Id;
            effect.ActionId = UInt32(b, 8);
            effect.SpellId = UInt16(b, 0x1C);
            effect.ActionType = b[0x1F];
            effect.Heading = Heading(b, 0x1A);
            effect.Sequence = sequence;
            effect.SourceSequence = UInt16(b, 0x18);
            effect.AnimationLock = Float(b, 0x10);
            effect.AnimationVariation = b[0x1E];
            effect.Targets = new ReplayActionTarget[b[0x21]];

            for (var i = 0; i < effect.Targets.Length; i++)
            {
                var items = new List<ReplayActionEffect>();
                for (var j = 0; j < 8; j++)
                {
                    // 7.58 回放结算入口传入正文 +0x2A，与 0x28 长度的内存 Header 有两字节间隔。
                    var at = 0x2A + i * 64 + j * 8;
                    if (b[at] != 0)
                    {
                        items.Add(new ReplayActionEffect
                        {
                            Type = b[at],
                            Parameters = new[] { b[at + 1], b[at + 2], b[at + 3], b[at + 4], b[at + 5] },
                            Value = UInt16(b, at + 6)
                        });
                    }
                }

                effect.Targets[i] = new ReplayActionTarget
                {
                    Actor = Find(UInt32(b, targetsAt + i * 8))?.Entity.Id,
                    Effects = items.ToArray()
                };
            }

            if (capacity > 1)
                effect.Position = QuantizedPosition(b, targetsAt + capacity * 8);
            Add(effect);
            return ReplayPacketUse.Timeline;
        }

        private void AddMapEffect(byte slot, ushort state, ushort playback)
        {
            var e = Event("MapEffect", null);
            e.MapSlot = slot;
            e.SharedTimelineState = state;
            e.PlaybackTimeline = playback;
            _map[slot] = e;
            Add(e);
        }

        private ReplayPacketUse MapEffectBatch(byte[] b, int capacity)
        {
            // 0x140B6C8A0/0x140B6C9B0/0x140B6CAD0：状态、播放和槽各成一列。
            if (b[0] > capacity)
                return ReplayPacketUse.Unresolved;

            for (var i = 0; i < b[0]; i++)
                AddMapEffect(b[2 + 4 * capacity + i], UInt16(b, 2 + 2 * i), UInt16(b, 2 + 2 * capacity + 2 * i));
            return ReplayPacketUse.Timeline;
        }

        private ReplayPacketUse MapEffectSnapshot(byte[] b)
        {
            // 0x140B82450：+0x06 起顺序更新 Director 的 MapEffectList。快照没有即时播放字段。
            for (byte slot = 0; slot < 64; slot++)
            {
                var state = UInt16(b, 6 + slot * 2);
                if (!_map.TryGetValue(slot, out var prior) ? state == 0 : prior.SharedTimelineState == state)
                    continue;
                var e = Event("MapEffectState", null);
                e.MapSlot = slot;
                e.SharedTimelineState = state;
                _map[slot] = e;
                Add(e);
            }

            return ReplayPacketUse.Reference;
        }

        private ReplayPacketUse Waymark(byte[] b, byte slot, bool active, int x, int y, int z)
        {
            if (slot >= 8)
                return ReplayPacketUse.Unresolved;

            var e = Event("Waymark", null);
            e.Waymark = slot;
            e.Active = active;
            if (active)
            {
                e.Position = new ReplayPosition
                {
                    X = unchecked((int)UInt32(b, x)) / 1000f,
                    Y = unchecked((int)UInt32(b, z)) / 1000f,
                    Z = unchecked((int)UInt32(b, y)) / 1000f
                };
            }

            _waymarks[slot] = e;
            Add(e);
            return ReplayPacketUse.Timeline;
        }

        private ReplayPacketUse Presentation(ActorState actor, string kind, params uint[] values)
        {
            var e = Event(kind, actor);
            e.Values = values;
            if (actor != null)
                actor.Presentation[kind] = e;
            Add(e);
            return ReplayPacketUse.Timeline;
        }

        private ReplayEvent Event(string kind, ActorState actor)
        {
            return new ReplayEvent
            {
                Actor = actor?.Entity.Id,
                Kind = kind,
                Record = _packet.Index,
                Offset = _packet.Offset,
                TimeMs = (long)_packet.TimestampMs - _document.StartMs
            };
        }

        private void Add(ReplayEvent e)
        {
            if (_insideWindow)
                _document.Events.Add(e);
        }

        private ActorState Find(uint rawId)
        {
            _actors.TryGetValue(rawId, out var result);
            return result;
        }

        private bool IsHostileTarget(ActorState actor)
        {
            return actor != null && actor.Entity.Kind == "Npc" && !IsPlayerControlled(actor);
        }

        private bool IsPlayerControlled(ActorState actor)
        {
            // 召唤物按已出现的拥有者归类，避免用录制对象 ID 的数值范围猜玩家身份。
            for (var i = 0; i < 8 && actor != null; i++)
            {
                if (actor.Entity.Kind == "Player")
                    return true;
                actor = Find(actor.Owner);
            }

            return false;
        }

        private static float Float(byte[] b, int at) => BitConverter.ToSingle(b, at);
        private static float Heading(byte[] b, int at) => PacketCodec.DecodeUShortHeading(UInt16(b, at));

        private static ReplayPosition FloatPosition(byte[] b, int at)
        {
            return new ReplayPosition { X = Float(b, at), Y = Float(b, at + 8), Z = Float(b, at + 4) };
        }

        private static ReplayPosition QuantizedPosition(byte[] b, int at)
        {
            return new ReplayPosition { X = Coord(b, at), Y = Coord(b, at + 4), Z = Coord(b, at + 2) };
        }

        private static float Coord(byte[] b, int at) => PacketCodec.DecodeUShortCoord(UInt16(b, at));
    }
}
