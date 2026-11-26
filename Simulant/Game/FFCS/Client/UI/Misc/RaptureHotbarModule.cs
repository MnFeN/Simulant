using System;

namespace Simulant.Game.FFCS.Client.UI.Misc
{
    // FFXIVClientStructs/FFXIV/Client/UI/Misc/RaptureHotbarModule.cs
    public partial struct RaptureHotbarModule : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        internal bool ModuleReady => Ptr.Read<byte>(0x58) != 0;
        internal MemoryArray<HotbarSlot> StandardHotbarSlots
            => Ptr.ArrayField<HotbarSlot>(0xA0, StandardHotbarCount * SlotsPerHotbar);
    }
}
