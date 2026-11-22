using System;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Common.Component.BGCollision
{
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/BGCollisionModule.cs
    [StructLayout(LayoutKind.Explicit, Size = 0x40)]
    internal struct RaycastParams
    {
        [FieldOffset(0x08)] internal IntPtr Origin; // either ray origin (Vector3) or swept sphere (Vector4 with radius in w component)
        [FieldOffset(0x10)] internal IntPtr Direction;
        [FieldOffset(0x18)] internal IntPtr MaxDistance;
    }
}
