using System;
using System.Numerics;

namespace Simulant.Game.FFCS.Client.Graphics.Scene
{
    public struct DrawObject : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // [Inherits<Object>]
        private Object Object => Ptr.As<Object>();
        public MemoryField<ulong> ObjectFlags => Object.ObjectFlags;
        public MemoryField<Vector3> Position => Object.Position;
        public MemoryField<Quaternion> Rotation => Object.Rotation;
        public MemoryField<Vector3> Scale => Object.Scale;

        public MemoryField<byte> Flags => Ptr.Field<byte>(0x88);
    }
}
