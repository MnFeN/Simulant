using System;
using System.Collections.Generic;
using System.Linq;
using static Simulant.Replay.ReplayReader;

namespace Simulant.Replay
{
    internal sealed partial class ReplayTimelineDecoder
    {
        private ReplayPacketUse StatusSnapshot(ActorState actor, byte[] b, int first, int second)
        {
            if (actor == null)
                return ReplayPacketUse.Reference;

            for (var slot = 0; slot < (second < 0 ? 30 : 60); slot++)
            {
                var at = slot < 30 ? first + slot * 12 : second + (slot - 30) * 12;
                var id = UInt16(b, at);
                var source = Find(UInt32(b, at + 8));

                if (id == 0 || IsHostileTarget(actor) && IsPlayerControlled(source))
                {
                    RemoveStatus(actor, slot);
                    continue;
                }

                SetStatus(actor, slot, id, UInt16(b, at + 2), Float(b, at + 4), source);
            }

            return ReplayPacketUse.Reference;
        }

        private ReplayPacketUse EffectResult(byte[] b, bool basic)
        {
            var stride = basic ? 16 : 88;
            if (4 + b[0] * stride > b.Length)
                return ReplayPacketUse.Unresolved;
            if (!basic)
                for (var i = 0; i < b[0]; i++)
                    if (b[25 + i * stride] > 4)
                        return ReplayPacketUse.Unresolved;

            var kept = false;
            for (var i = 0; i < b[0]; i++)
            {
                var at = 4 + i * stride;
                var sequence = UInt32(b, at);
                if (_effectSequences.TryGetValue(sequence, out var filtered) && filtered)
                    continue;

                // 分发后 0x140B37040 按路由对象应用，条目对象 ID 用于与 ActionEffect 目标配对。
                var actor = Find(_packet.ObjectId) ?? Find(UInt32(b, at + 4));
                var confirmation = Event("EffectResult", actor);
                confirmation.Target = Find(UInt32(b, at + 4))?.Entity.Id;
                confirmation.Sequence = sequence;
                confirmation.TargetIndex = b[at + (basic ? 12 : 18)];
                if (_effectSequences.ContainsKey(sequence))
                    Add(confirmation);
                kept = true;

                if (basic || actor == null)
                    continue;

                for (var j = 0; j < b[at + 21]; j++)
                {
                    var status = at + 24 + j * 16;
                    var source = Find(UInt32(b, status + 12));
                    if (IsHostileTarget(actor) && IsPlayerControlled(source))
                        continue;

                    SetStatus(actor, b[status], UInt16(b, status + 2), UInt16(b, status + 4),
                        Float(b, status + 8), source);
                }
            }

            return kept ? ReplayPacketUse.Reference : ReplayPacketUse.Filtered;
        }

        private static float Remaining(ReplayEvent status, long atMs)
        {
            var seconds = status.Status.Seconds;
            return seconds < 0 ? seconds : Math.Max(0, seconds - (atMs - status.TimeMs) / 1000f);
        }

        private void SetStatus(ActorState actor, int slot, ushort id, ushort param, float seconds, ActorState source)
        {
            if (id == 0 || !ReplayPosition.Finite(seconds) || slot >= 60)
                return;

            var change = Event("StatusSet", actor);
            var sourceId = source?.Entity.Id;
            if (actor.Statuses.TryGetValue(slot, out var prior))
            {
                var priorStatus = prior.Status;
                if (priorStatus.Id != id || priorStatus.Source != sourceId)
                    RemoveStatus(actor, slot);
                else if (priorStatus.Param == param && Math.Abs(Remaining(prior, change.TimeMs) - seconds) < 0.4f)
                    return;
            }

            change.Status = new ReplayStatus { Id = id, Param = param, Seconds = seconds, Slot = slot, Source = sourceId };
            actor.Statuses[slot] = change;
            Add(change);
        }

        private void RemoveStatus(ActorState actor, int slot)
        {
            if (!actor.Statuses.TryGetValue(slot, out var found))
                return;

            actor.Statuses.Remove(slot);
            var e = Event("StatusRemove", actor);
            e.Status = found.Status;
            Add(e);
        }

        private static readonly Dictionary<ushort, string> ControlNames = new Dictionary<ushort, string>
        {
            [0x0000] = "WeaponDrawn",
            [0x0001] = "AutoAttack",
            [0x0002] = "CharacterMode",
            [0x0004] = "CombatState",
            [0x000D] = "CharacterResource",
            [0x000E] = "DeathAnimation",
            [0x000F] = "CancelCast",
            [0x0014] = "StatusGainNotice",
            [0x0015] = "StatusLossNotice",
            [0x0016] = "StatusParam",
            [0x0017] = "PeriodicEffect",
            [0x001E] = "NpcPartState",
            [0x0021] = "PartyRoleBonus",
            [0x0022] = "HeadMarker",
            [0x0023] = "Tether",
            [0x0024] = "SpawnPresentation",
            [0x0027] = "DeathFade",
            [0x002F] = "TetherCancel",
            [0x0030] = "ModelScale",
            [0x0031] = "ModelAttributeFlags",
            [0x0032] = "Target",
            [0x0036] = "Targetable",
            [0x003A] = "SoftTarget",
            [0x003C] = "ResetBaseTimeline",
            [0x003E] = "AnimationState",
            [0x003F] = "ModelState",
            [0x0046] = "VoiceLine",
            [0x0047] = "LimitBreakPrepare",
            [0x0048] = "LimitBreakExecute",
            [0x0050] = "DamageNotice",
            [0x0058] = "CurrentHp",
            [0x0062] = "Name",
            [0x0064] = "DirectorInit",
            [0x0065] = "DirectorClear",
            [0x0069] = "ActorEvent",
            [0x006A] = "EventState",
            [0x006D] = "DirectorUpdate",
            [0x00B7] = "PositionVfx",
            [0x00B8] = "TargetVfx",
            [0x00C8] = "ZoneInPresentation",
            [0x00D4] = "DespawnPresentation",
            [0x00DF] = "TimelineMovement",
            [0x00E5] = "ReleaseSpecialMovement",
            [0x00EF] = "FinishTimelineMovement",
            [0x00F1] = "WarpWithAnimation",
            [0x0101] = "PetTargets",
            [0x0104] = "PetHotbar",
            [0x010A] = "FadeOut",
            [0x010B] = "FadeIn",
            [0x0123] = "CancelEmote",
            [0x0127] = "Pose",
            [0x0197] = "PlayTimeline",
            [0x0199] = "SetSharedTimelineState",
            [0x019D] = "EventObjectAnimation",
            [0x01A1] = "CompanionOwner",
            [0x01F6] = "TargetSign",
            [0x01F7] = "HateLetter",
            [0x01F8] = "OnlineStatusIcon",
            [0x01F9] = "LimitBreakGauge",
            [0x0207] = "PartyCutsceneFlags",
            [0x025F] = "ObjectPresentation",
            [0x02BC] = "ActionRejected",
            [0x03A6] = "ReplayLevelMetadata",
            [0x05E5] = "ActorToActorVfx",
            [0x05F9] = "ResetTimeline",
            [0x05FA] = "ShieldGauge",
            [0x05FB] = "StanceChange",
            [0x0604] = "PeriodicEffect",
            [0x0605] = "PeriodicEffect",
            [0x0D49] = "CancelMatchingCutscene"
        };

        internal static string ControlName(ushort id)
        {
            return ControlNames.TryGetValue(id, out var name) ? name : string.Empty;
        }

        private ReplayPacketUse ActorControl(ActorState actor, byte[] b, string packetName)
        {
            var category = UInt16(b, 0);
            var name = ControlName(category);
            if (name.Length == 0)
                return ReplayPacketUse.Unresolved;

            var p1 = UInt32(b, 4);
            var p2 = UInt32(b, 8);
            var p3 = UInt32(b, 12);
            var p4 = UInt32(b, 16);

            switch (category)
            {
                case 0x000F:
                    if (IsPlayerControlled(actor))
                        return ReplayPacketUse.Filtered;
                    if (actor != null)
                        actor.Cast = null;
                    var cancel = Event(name, actor);
                    cancel.ActionId = p3;
                    Add(cancel);
                    return ReplayPacketUse.Timeline;
                case 0x0015:
                    if (actor != null && p4 == 0)
                    {
                        var prior = actor.Statuses.Values.FirstOrDefault(candidate => candidate.Status.Id == p1 &&
                            (p3 == NoActor || candidate.Status.Source == Find(p3)?.Entity.Id));
                        if (prior != null)
                            RemoveStatus(actor, prior.Status.Slot);
                    }
                    return ReplayPacketUse.Reference;
                case 0x0016:
                    if (actor != null && p1 < 60 && actor.Statuses.TryGetValue((int)p1, out var status) && status.Status.Id == p2)
                    {
                        var source = FindById(status.Status.Source);
                        SetStatus(actor, (int)p1, (ushort)p2, (ushort)p3, Remaining(status, (long)_packet.TimestampMs - _document.StartMs), source);
                    }
                    return ReplayPacketUse.Reference;
                case 0x01A1:
                    if (actor != null)
                    {
                        actor.Owner = p1;
                        if (_packet.TimestampMs <= _document.EndMs)
                            actor.Entity.Owner = Find(p1)?.Entity.Id;
                    }
                    return ReplayPacketUse.Reference;
                case 0x0000:
                case 0x0002:
                case 0x0004:
                    return IsPlayerControlled(actor) ? ReplayPacketUse.Filtered : Presentation(actor, name, p1, p2);
                case 0x0030:
                case 0x0031:
                case 0x0036:
                case 0x003E:
                case 0x003F:
                case 0x006A:
                case 0x0199:
                case 0x05FB:
                    return Presentation(actor, name, p1, p2);
                case 0x0001:
                case 0x0017:
                case 0x0104:
                case 0x0021:
                case 0x01F8:
                case 0x01F9:
                case 0x02BC:
                case 0x0604:
                case 0x0605:
                    return ReplayPacketUse.Filtered;
                case 0x000D:
                case 0x0014:
                case 0x0050:
                case 0x0058:
                case 0x0062:
                case 0x0069:
                case 0x0101:
                case 0x01F7:
                case 0x0207:
                case 0x03A6:
                case 0x05FA:
                case 0x0D49:
                    return ReplayPacketUse.Reference;
            }

            var e = Event(name, actor);
            switch (category)
            {
                case 0x001E:
                    e.Target = Find(p3)?.Entity.Id;
                    e.Values = new[] { p1, p2, p4 };
                    break;
                case 0x0022:
                    e.Target = Find(p2)?.Entity.Id;
                    e.Values = new[] { p1 };
                    break;
                case 0x0023:
                    e.Target = Find(p3)?.Entity.Id;
                    e.Values = new[] { p1, p2, p4 };
                    if (actor != null)
                        actor.Presentation["Tether" + p1] = e;
                    break;
                case 0x002F:
                    e.Values = new[] { p1, p2 };
                    actor?.Presentation.Remove("Tether" + p1);
                    break;
                case 0x0032:
                case 0x003A:
                case 0x01F6:
                    e.Target = packetName == "ActorControlTarget" ? Find(UInt32(b, 24))?.Entity.Id : null;
                    if (category == 0x01F6)
                        e.Values = new[] { p1 };
                    break;
                case 0x00B7:
                    e.Position = new ReplayPosition { X = FloatCoordinate(p2), Y = FloatCoordinate(p4), Z = FloatCoordinate(p3) };
                    e.Values = new[] { p1 };
                    break;
                case 0x00B8:
                    return ReplayPacketUse.Unresolved;
                case 0x00DF:
                case 0x00F1:
                    e.Position = new ReplayPosition
                    {
                        X = FloatCoordinate(p1 >> 16),
                        Y = FloatCoordinate(p2 >> 16),
                        Z = FloatCoordinate(p1)
                    };
                    e.Heading = Simulant.Game.PacketCodec.DecodeUShortHeading((ushort)p2);
                    e.Values = category == 0x00DF ? new[] { p3 & 0xFF, p4 & 0xFFFF } : new[] { p3 & 0xFFFF, p3 >> 16, p4 };
                    break;
                case 0x019D:
                    e.SharedTimelineState = (ushort)p1;
                    e.PlaybackTimeline = (ushort)p2;
                    if (actor != null)
                        actor.Presentation[name] = e;
                    break;
                case 0x025F:
                    e.Actor = Find(p1)?.Entity.Id;
                    e.Values = new[] { p2, p3, p4 };
                    break;
                case 0x05E5:
                    e.Actor = Find(p2)?.Entity.Id;
                    e.Target = Find(p3)?.Entity.Id;
                    e.Values = new[] { p1 };
                    break;
                case 0x0064:
                case 0x0065:
                case 0x006D:
                    e.Actor = null;
                    var count = packetName == "ActorControlSelf" ? Math.Min(8, (b.Length - 4) / 4) : 4;
                    e.Values = Enumerable.Range(0, count).Select(i => UInt32(b, 4 + i * 4)).ToArray();
                    break;
                case 0x000E:
                case 0x0024:
                case 0x0027:
                case 0x0046:
                case 0x0047:
                case 0x0048:
                case 0x00C8:
                case 0x00D4:
                case 0x010A:
                case 0x010B:
                case 0x0127:
                case 0x0197:
                case 0x05F9:
                    e.Values = new[] { p1, p2 };
                    break;
            }

            Add(e);
            return ReplayPacketUse.Timeline;
        }

        private ActorState FindById(int? id)
        {
            return id.HasValue ? _actors.Values.FirstOrDefault(a => a.Entity.Id == id.Value) : null;
        }

        private static float FloatCoordinate(uint raw) => Simulant.Game.PacketCodec.DecodeUShortCoord((ushort)raw);
    }
}
