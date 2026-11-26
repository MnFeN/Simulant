using System;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.System.Input
{
    // FFXIVClientStructs/FFXIV/Client/System/Input/InputData.cs
    [StructLayout(LayoutKind.Explicit, Size = Size)]
    internal struct Keybind
    {
        internal const int Size = 0x0B;

        // FFCS KeySettings 数组的两项键盘绑定。
        [FieldOffset(0x00)] public KeySetting Primary;
        [FieldOffset(0x02)] public KeySetting Secondary;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x02)]
    internal struct KeySetting
    {
        [FieldOffset(0x00)] public byte Key;
        [FieldOffset(0x01)] public KeyModifierFlag KeyModifier;
    }

    [Flags]
    internal enum KeyModifierFlag : byte
    {
        None = 0,
        Shift = 1 << 0,
        Ctrl = 1 << 1,
        Alt = 1 << 2,
    }
}
