using System;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.STD
{
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/STD/StdMap.cs
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/STD/Helper/RedBlackTree.cs
    [StructLayout(LayoutKind.Sequential)]
    internal struct StdMap
    {
        internal IntPtr Head;
        internal long LongCount;

        // 本机客户端 2026.11.03.0000.0000，LayoutManager::GetLayoutInstance（RVA 0x71DF50）。
        // 此处是 byte/ulong 键、指针值的 MSVC x64 节点特化，保留键的两种宽度。
        [StructLayout(LayoutKind.Explicit, Size = 0x30)]
        internal struct PointerNode
        {
            [FieldOffset(0x00)] internal IntPtr Left;
            [FieldOffset(0x08)] internal IntPtr Parent;
            [FieldOffset(0x10)] internal IntPtr Right;
            [FieldOffset(0x19)] internal byte IsNil;
            [FieldOffset(0x20)] internal byte ByteKey;
            [FieldOffset(0x20)] internal ulong UInt64Key;
            [FieldOffset(0x28)] internal IntPtr Value;
        }
    }
}
