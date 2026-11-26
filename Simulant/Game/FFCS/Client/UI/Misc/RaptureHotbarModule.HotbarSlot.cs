using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.UI.Misc
{
    public partial struct RaptureHotbarModule
    {
        internal const int StandardHotbarCount = 10;
        internal const int SlotsPerHotbar = 16;

        // FFXIVClientStructs/FFXIV/Client/UI/Misc/RaptureHotbarModule.HotbarSlot.cs
        [StructLayout(LayoutKind.Explicit, Size = 0xE8)]
        internal struct HotbarSlot
        {
            [FieldOffset(0xB8)] public uint CommandId;
            [FieldOffset(0xC7)] public HotbarSlotType CommandType;
            [FieldOffset(0xD0)] public uint IconId;
        }

        internal enum HotbarSlotType : byte
        {
            Empty = 0,
            Action = 1,
            Item = 2,
            Macro = 7,
            GeneralAction = 10,
        }
    }
}
