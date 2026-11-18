using System;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.STD
{
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/STD/StdVector.cs
    // Convert the embedded value layout to remote pointer fields.
    [StructLayout(LayoutKind.Sequential)]
    internal struct StdVector
    {
        internal IntPtr First;
        internal IntPtr Last;
        internal IntPtr End;

        internal bool IsEmpty => First == Last;
    }
}
