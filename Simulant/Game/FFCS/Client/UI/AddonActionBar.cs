using System;

namespace Simulant.Game.FFCS.Client.UI
{
    // FFXIVClientStructs/FFXIV/Client/UI/AddonActionBar.cs
    public struct AddonActionBar : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        public MemoryField<byte> HotbarId => Ptr.Field<byte>(0x2CB);
    }
}
