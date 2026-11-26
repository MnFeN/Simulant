using System;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.System.Input
{
    // FFXIVClientStructs/FFXIV/Client/System/Input/KeyboardData.cs
    [StructLayout(LayoutKind.Explicit, Size = 0x494)]
    internal unsafe struct KeyboardInputData
    {
        internal const int KeyCount = 159;

        [FieldOffset(0x04)] private fixed int _keyState[KeyCount];

        internal KeyStateFlags GetKeyState(int key)
        {
            if ((uint)key >= KeyCount)
                throw new ArgumentOutOfRangeException(nameof(key));

            fixed (int* states = _keyState)
                return (KeyStateFlags)states[key];
        }
    }

    [Flags]
    internal enum KeyStateFlags
    {
        None = 0,
        Down = 1 << 0,
        Pressed = 1 << 1,
        Released = 1 << 2,
        Held = 1 << 3,
    }
}
