using System;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.Game.Network
{
    // https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Network/ServerIPC.cs
    /// <summary>当前目标的小队仇恨更新；成员按仇恨从高到低排列，与敌对列表更新包不同。</summary>
    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public unsafe struct UpdateHatePacket
    {
        public const int Size = 0x48;
        public const int Capacity = 8;

        [FieldOffset(0)] public byte MemberCount;
        [FieldOffset(4)] public fixed byte Members[Capacity * UpdateHatePacketMember.Size];

        public UpdateHatePacketMember GetMember(int index)
        {
            ValidateIndex(index);
            fixed (byte* members = Members)
                return ((UpdateHatePacketMember*)members)[index];
        }

        public void SetMember(int index, UpdateHatePacketMember member)
        {
            ValidateIndex(index);
            if (member.Enmity > 100)
                throw new ArgumentOutOfRangeException(nameof(member), "仇恨百分比必须为 0-100。");
            fixed (byte* members = Members)
                ((UpdateHatePacketMember*)members)[index] = member;
        }

        private static void ValidateIndex(int index)
        {
            if ((uint)index >= Capacity)
                throw new ArgumentOutOfRangeException(nameof(index), "目标仇恨包的成员索引必须为 0-7。");
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public struct UpdateHatePacketMember
    {
        public const int Size = 8;
        [FieldOffset(0)] public uint EntityId;
        [FieldOffset(4)] public byte Enmity;
    }
}
