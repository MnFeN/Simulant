using System;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.Game.Network
{
    // https://github.com/anomek/AnoMech/blob/1646a7068c2071a3d1fd9e3c84550dfd3f1b6ade/AnoMech/Core/Native/Implementations/Pointers/PacketDispatcherPointers.cs
    /// <summary>完整小队名单；成员加入或退出都发送整包，空包表示离队。</summary>
    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public unsafe struct UpdatePartyPacket
    {
        public const int Size = 0xE58;
        public const int Capacity = 8;

        [FieldOffset(0)] public fixed byte Members[Capacity * UpdatePartyPacketMember.Size];
        [FieldOffset(0xE40)] public long PartyId;
        [FieldOffset(0xE48)] public long PartyId_2;
        [FieldOffset(0xE50)] public byte LeaderIndex;
        [FieldOffset(0xE51)] public byte MemberCount;

        public UpdatePartyPacketMember GetMember(int index)
        {
            ValidateIndex(index);
            fixed (byte* members = Members)
                return ((UpdatePartyPacketMember*)members)[index];
        }

        public void SetMember(int index, UpdatePartyPacketMember member)
        {
            ValidateIndex(index);
            fixed (byte* members = Members)
                ((UpdatePartyPacketMember*)members)[index] = member;
        }

        private static void ValidateIndex(int index)
        {
            if ((uint)index >= Capacity)
                throw new ArgumentOutOfRangeException(nameof(index), "小队更新包的成员索引必须为 0-7。");
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public unsafe struct UpdatePartyPacketMember
    {
        public const int Size = 0x1C8;
        public const int NameLength = 0x20;
        public const int StatusCount = 30;
        public const int StatusesOffset = 0x60;

        [FieldOffset(0)] public fixed byte Name[NameLength];
        [FieldOffset(0x28)] public ulong AccountId;
        [FieldOffset(0x30)] public ulong ContentId;
        [FieldOffset(0x38)] public uint EntityId;
        [FieldOffset(0x3C)] public uint PetEntityId;
        [FieldOffset(0x40)] public uint CompanionEntityId;
        [FieldOffset(0x44)] public uint CurrentHP;
        [FieldOffset(0x48)] public uint MaxHP;
        [FieldOffset(0x4C)] public ushort CurrentMP;
        [FieldOffset(0x4E)] public ushort MaxMP;
        [FieldOffset(0x50)] public ushort HomeWorld;
        [FieldOffset(0x52)] public ushort TerritoryType;
        [FieldOffset(0x54)] public byte Valid;
        [FieldOffset(0x55)] public byte ClassJob;
        [FieldOffset(0x56)] public byte Sex;
        [FieldOffset(0x57)] public byte Level;
        [FieldOffset(0x58)] public byte SyncedLevel;
        [FieldOffset(StatusesOffset)] public fixed byte Statuses[StatusCount * UpdatePartyPacketStatus.Size];

        public UpdatePartyPacketStatus GetStatus(int index)
        {
            ValidateStatusIndex(index);
            fixed (byte* statuses = Statuses)
                return ((UpdatePartyPacketStatus*)statuses)[index];
        }

        public void SetStatus(int index, UpdatePartyPacketStatus status)
        {
            ValidateStatusIndex(index);
            fixed (byte* statuses = Statuses)
                ((UpdatePartyPacketStatus*)statuses)[index] = status;
        }

        private static void ValidateStatusIndex(int index)
        {
            if ((uint)index >= StatusCount)
                throw new ArgumentOutOfRangeException(nameof(index), "小队更新包的状态索引必须为 0-29。");
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public struct UpdatePartyPacketStatus
    {
        public const int Size = 0xC;
        [FieldOffset(0)] public ushort StatusId;
        [FieldOffset(2)] public ushort Param;
        // 原生小队收包函数不读取剩余时间，恢复计时需另用成员的状态记录。
        [FieldOffset(4)] public float RemainingTime;
        [FieldOffset(8)] public uint SourceEntityId;
    }
}
