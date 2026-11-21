using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Simulant.Replay
{
    /// <summary>模拟时间轴的中间格式。包版本只用于回溯数据来源。</summary>
    [DataContract]
    internal sealed class ReplayTimelineDocument
    {
        internal const int CurrentSchemaVersion = 1;

        [DataMember(Order = 1)] public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        [DataMember(Order = 2)] public ReplayOrigin Source { get; set; }
        [DataMember(Order = 3)] public ushort TerritoryId { get; set; }
        [DataMember(Order = 4)] public ushort DutyId { get; set; }
        [DataMember(Order = 5)] public uint StartMs { get; set; }
        [DataMember(Order = 6)] public uint EndMs { get; set; }
        [DataMember(Order = 7)] public List<ReplayEntity> Entities { get; set; } = new List<ReplayEntity>();
        [DataMember(Order = 8)] public List<ReplayEvent> InitialState { get; set; } = new List<ReplayEvent>();
        [DataMember(Order = 9)] public List<ReplayEvent> Events { get; set; } = new List<ReplayEvent>();
    }

    [DataContract]
    internal sealed class ReplayOrigin
    {
        [DataMember] public string File { get; set; }
        [DataMember] public string Sha256 { get; set; }
        [DataMember] public string Profile { get; set; }
        [DataMember] public uint Build { get; set; }
        [DataMember] public ushort Format { get; set; }
    }

    [DataContract]
    internal sealed class ReplayEntity
    {
        [DataMember(Order = 1)] public int Id { get; set; }
        [DataMember(Order = 2)] public string Kind { get; set; }
        [DataMember(EmitDefaultValue = false)] public uint BaseId { get; set; }
        [DataMember(EmitDefaultValue = false)] public uint NameId { get; set; }
        [DataMember(EmitDefaultValue = false)] public byte Level { get; set; }
        [DataMember(EmitDefaultValue = false)] public byte Job { get; set; }
        [DataMember(EmitDefaultValue = false)] public byte? PartySlot { get; set; }
        [DataMember(EmitDefaultValue = false)] public ushort Model { get; set; }
        [DataMember(EmitDefaultValue = false)] public byte SubKind { get; set; }
        [DataMember(EmitDefaultValue = false)] public uint DisplayFlags { get; set; }
        [DataMember(EmitDefaultValue = false)] public byte TargetableFlags { get; set; }
        [DataMember(EmitDefaultValue = false)] public bool Hidden { get; set; }
        [DataMember(EmitDefaultValue = false)] public uint LayoutId { get; set; }
        [DataMember(EmitDefaultValue = false)] public uint EventId { get; set; }
        [DataMember(EmitDefaultValue = false)] public uint GimmickId { get; set; }
        [DataMember(EmitDefaultValue = false)] public float? Scale { get; set; }
        [DataMember(EmitDefaultValue = false)] public byte EventState { get; set; }
        [DataMember(EmitDefaultValue = false)] public ushort SharedTimelineState { get; set; }
        [DataMember(EmitDefaultValue = false)] public uint SharedGroupState { get; set; }
        [DataMember(EmitDefaultValue = false)] public ushort FateId { get; set; }
        [DataMember(EmitDefaultValue = false)] public byte SpawnSlot { get; set; }
        [DataMember(EmitDefaultValue = false)] public int? Owner { get; set; }
        [DataMember(EmitDefaultValue = false)] public byte ModelScale { get; set; }
        [DataMember(EmitDefaultValue = false)] public byte ModelState { get; set; }
        [DataMember(EmitDefaultValue = false)] public byte AnimationState { get; set; }
        [DataMember(EmitDefaultValue = false)] public byte StatusLoopVfx { get; set; }
    }

    /// <summary>采用项目坐标系，Z 为高度。</summary>
    [DataContract]
    internal sealed class ReplayPosition
    {
        [DataMember(Order = 1)] public float X { get; set; }
        [DataMember(Order = 2)] public float Y { get; set; }
        [DataMember(Order = 3)] public float Z { get; set; }

        internal bool IsFinite => Finite(X) && Finite(Y) && Finite(Z);
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [DataContract]
    internal sealed class ReplayEvent
    {
        [DataMember(Order = 1)] public long Record { get; set; }
        [DataMember(Order = 2)] public long Offset { get; set; }
        [DataMember(Order = 3)] public long TimeMs { get; set; }
        [DataMember(Order = 4)] public string Kind { get; set; }
        [DataMember(EmitDefaultValue = false)] public int? Actor { get; set; }
        [DataMember(EmitDefaultValue = false)] public int? Target { get; set; }
        [DataMember(EmitDefaultValue = false)] public ReplayPosition Position { get; set; }
        [DataMember(EmitDefaultValue = false)] public float? Heading { get; set; }
        [DataMember(EmitDefaultValue = false)] public uint? ActionId { get; set; }
        [DataMember(EmitDefaultValue = false)] public ushort? SpellId { get; set; }
        [DataMember(EmitDefaultValue = false)] public byte? ActionType { get; set; }
        [DataMember(EmitDefaultValue = false)] public float? CastTime { get; set; }
        [DataMember(EmitDefaultValue = false)] public float? OmenDelay { get; set; }
        [DataMember(EmitDefaultValue = false)] public float? AnimationLock { get; set; }
        [DataMember(EmitDefaultValue = false)] public byte? AnimationVariation { get; set; }
        [DataMember(EmitDefaultValue = false)] public byte? Speed { get; set; }
        [DataMember(EmitDefaultValue = false)] public ushort? MoveFlags { get; set; }
        [DataMember(EmitDefaultValue = false)] public ReplayStatus Status { get; set; }
        [DataMember(EmitDefaultValue = false)] public ReplayActionTarget[] Targets { get; set; }
        [DataMember(EmitDefaultValue = false)] public int? PacketTargetCapacity { get; set; }
        [DataMember(EmitDefaultValue = false)] public uint? Sequence { get; set; }
        [DataMember(EmitDefaultValue = false)] public ushort? SourceSequence { get; set; }
        [DataMember(EmitDefaultValue = false)] public int? TargetIndex { get; set; }
        [DataMember(EmitDefaultValue = false)] public byte? Waymark { get; set; }
        [DataMember(EmitDefaultValue = false)] public bool? Active { get; set; }
        [DataMember(EmitDefaultValue = false)] public byte? MapSlot { get; set; }
        [DataMember(EmitDefaultValue = false)] public ushort? PlaybackTimeline { get; set; }
        [DataMember(EmitDefaultValue = false)] public ushort? SharedTimelineState { get; set; }
        [DataMember(EmitDefaultValue = false)] public ushort? WeatherId { get; set; }
        [DataMember(EmitDefaultValue = false)] public float? TransitionTime { get; set; }
        [DataMember(EmitDefaultValue = false)] public ushort? TextId { get; set; }
        [DataMember(EmitDefaultValue = false)] public ushort? NameId { get; set; }
        [DataMember(EmitDefaultValue = false)] public uint[] Values { get; set; }
    }

    [DataContract]
    internal sealed class ReplayStatus
    {
        [DataMember] public ushort Id { get; set; }
        [DataMember(EmitDefaultValue = false)] public ushort Param { get; set; }
        [DataMember(EmitDefaultValue = false)] public int? Source { get; set; }
        [DataMember] public float Seconds { get; set; }
        [DataMember] public int Slot { get; set; }
    }

    [DataContract]
    internal sealed class ReplayActionTarget
    {
        [DataMember(EmitDefaultValue = false)] public int? Actor { get; set; }
        [DataMember] public ReplayActionEffect[] Effects { get; set; }
    }

    [DataContract]
    internal sealed class ReplayActionEffect
    {
        [DataMember] public byte Type { get; set; }
        [DataMember] public byte[] Parameters { get; set; }
        [DataMember] public ushort Value { get; set; }
    }
}
