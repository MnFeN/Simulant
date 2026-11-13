using System;

namespace Simulant.Game.FFCS.Client.Game.Character
{
    public struct CharacterData : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        public MemoryField<float> ModelScale => Ptr.Field<float>(0x8);
        public MemoryField<uint> Health => Ptr.Field<uint>(0xC);
        public MemoryField<uint> MaxHealth => Ptr.Field<uint>(0x10);
        public MemoryField<uint> Mana => Ptr.Field<uint>(0x14);
        public MemoryField<uint> MaxMana => Ptr.Field<uint>(0x18);
        public MemoryField<ushort> GatheringPoints => Ptr.Field<ushort>(0x1C);
        public MemoryField<ushort> MaxGatheringPoints => Ptr.Field<ushort>(0x1E);
        public MemoryField<ushort> CraftingPoints => Ptr.Field<ushort>(0x20);
        public MemoryField<ushort> MaxCraftingPoints => Ptr.Field<ushort>(0x22);
        public MemoryField<short> TransformationId => Ptr.Field<short>(0x24);
        public MemoryField<ushort> TitleId => Ptr.Field<ushort>(0x26);
        public MemoryField<ushort> StatusLoopVfxId => Ptr.Field<ushort>(0x28);
        public MemoryField<Job> ClassJob => Ptr.Field<Job>(0x2A);
        public MemoryField<byte> Level => Ptr.Field<byte>(0x2B);
        public MemoryField<byte> Icon => Ptr.Field<byte>(0x2C);
        public MemoryField<byte> SEPack => Ptr.Field<byte>(0x2D);
        public MemoryField<byte> ShieldValue => Ptr.Field<byte>(0x2E);
        public MemoryField<byte> Map => Ptr.Field<byte>(0x2F);
        public MemoryField<byte> OnlineStatus => Ptr.Field<byte>(0x30);
        public MemoryField<byte> Battalion => Ptr.Field<byte>(0x31);

        public MemoryField<byte> Flags => Ptr.Field<byte>(0x34);
        public MemoryBitField IsHostile => Ptr.BitField(0x34, 0);
        public MemoryBitField InCombat => Ptr.BitField(0x34, 1);
    }

}
