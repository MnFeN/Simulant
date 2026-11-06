using System;
using System.Numerics;

namespace Simulant.Game.FFCS.Client.Game.Group
{
    /// <summary>小队成员记录；状态管理器保留 60 个状态，与更新包的 30 项不同。</summary>
    public struct PartyMember : IMemoryObject
    {
        // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/Game/Group/PartyMember.cs
        public const int Size = 0x490;
        public const int NameLength = 64;
        public const int StatusCount = 60;

        public IntPtr Ptr { get; set; }

        public StatusManager StatusManager => Ptr.As<StatusManager>();
        public MemoryField<Vector3> Position => Ptr.Field<Vector3>(0x3E0);
        public MemoryField<ulong> AccountId => Ptr.Field<ulong>(0x3F0);
        public MemoryField<ulong> ContentId => Ptr.Field<ulong>(0x3F8);
        public MemoryField<uint> EntityId => Ptr.Field<uint>(0x400);
        public MemoryField<uint> PetEntityId => Ptr.Field<uint>(0x404);
        public MemoryField<uint> CompanionEntityId => Ptr.Field<uint>(0x408);
        public MemoryField<uint> CurrentHP => Ptr.Field<uint>(0x40C);
        public MemoryField<uint> MaxHP => Ptr.Field<uint>(0x410);
        public MemoryField<ushort> CurrentMP => Ptr.Field<ushort>(0x414);
        public MemoryField<ushort> MaxMP => Ptr.Field<ushort>(0x416);
        public MemoryField<ushort> TerritoryType => Ptr.Field<ushort>(0x418);
        public MemoryField<ushort> HomeWorld => Ptr.Field<ushort>(0x41A);
        public IntPtr NamePtr => Ptr == IntPtr.Zero ? IntPtr.Zero : Ptr + 0x41C;
        public byte[] ReadNameBytes() => Ptr.ReadBytes(NameLength, 0x41C);
        public MemoryField<byte> Sex => Ptr.Field<byte>(0x468);
        public MemoryField<byte> ClassJob => Ptr.Field<byte>(0x469);
        public MemoryField<byte> Level => Ptr.Field<byte>(0x46A);
        public MemoryField<byte> DamageShield => Ptr.Field<byte>(0x46B);
        public MemoryField<byte> Flags => Ptr.Field<byte>(0x484);
    }
}
