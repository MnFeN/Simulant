using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Common.Component.BGCollision
{
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/BGCollisionModule.cs
    [StructLayout(LayoutKind.Explicit, Size = 0x58)]
    internal struct RaycastHit
    {
        [FieldOffset(0x00)] internal Vector3 Point;
        [FieldOffset(0x40)] internal ulong Material; // see notes on collider materials
        [FieldOffset(0x48)] internal float Distance;
        [FieldOffset(0x50)] internal IntPtr Object; // it's always a Collider*
    }
}
