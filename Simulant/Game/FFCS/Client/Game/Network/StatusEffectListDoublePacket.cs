using System;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.Game.Network
{
    // https://github.com/anomek/AnoMech/blob/1646a7068c2071a3d1fd9e3c84550dfd3f1b6ade/AnoMech/Core/Native/Implementations/Pointers/PacketDispatcherPointers.cs
    /// <summary>角色完整状态与血蓝量；后半区的 30 条状态在包内排在血量之前。</summary>
    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public unsafe struct StatusEffectListDoublePacket
    {
        public const int Size = 0x2E8;
        public const int Capacity = 30;
        public const int StatusCount = Capacity * 2;
        public const int ExtraStatusesOffset = 0;
        public const int StatusesOffset = 0x17C;

        [FieldOffset(ExtraStatusesOffset)] public fixed byte ExtraStatuses[Capacity * StatusEffectListStatus.Size];
        [FieldOffset(0x168)] public byte ClassJob;
        [FieldOffset(0x169)] public byte Level;
        [FieldOffset(0x16B)] public byte Level_2;
        [FieldOffset(0x16C)] public uint CurrentHP;
        [FieldOffset(0x170)] public uint MaxHP;
        [FieldOffset(0x174)] public ushort CurrentMP;
        [FieldOffset(0x176)] public ushort MaxMP;
        [FieldOffset(0x178)] public byte DamageShield;
        [FieldOffset(StatusesOffset)] public fixed byte Statuses[Capacity * StatusEffectListStatus.Size];

        public StatusEffectListStatus GetStatus(int index)
        {
            ValidateIndex(index);
            if (index < Capacity)
            {
                fixed (byte* statuses = Statuses)
                    return ((StatusEffectListStatus*)statuses)[index];
            }
            fixed (byte* statuses = ExtraStatuses)
                return ((StatusEffectListStatus*)statuses)[index - Capacity];
        }

        public void SetStatus(int index, StatusEffectListStatus status)
        {
            ValidateIndex(index);
            if (index < Capacity)
            {
                fixed (byte* statuses = Statuses)
                    ((StatusEffectListStatus*)statuses)[index] = status;
            }
            else
            {
                fixed (byte* statuses = ExtraStatuses)
                    ((StatusEffectListStatus*)statuses)[index - Capacity] = status;
            }
        }

        private static void ValidateIndex(int index)
        {
            if ((uint)index >= StatusCount)
                throw new ArgumentOutOfRangeException(nameof(index), "角色状态索引必须为 0-59。");
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public struct StatusEffectListStatus
    {
        public const int Size = 0xC;
        [FieldOffset(0)] public ushort StatusId;
        [FieldOffset(2)] public ushort Param;
        [FieldOffset(4)] public float RemainingTime;
        [FieldOffset(8)] public uint SourceEntityId;
    }
}
