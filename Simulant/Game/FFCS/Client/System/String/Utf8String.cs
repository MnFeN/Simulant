using System;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.System.String
{
    [StructLayout(LayoutKind.Explicit, Size = 0x68)]
    internal struct Utf8String
    {
        [FieldOffset(0x00)] public IntPtr Text;
        [FieldOffset(0x08)] public long Capacity;
        [FieldOffset(0x10)] public long BytesUsed;
        [FieldOffset(0x18)] public long Length;
    }
}
