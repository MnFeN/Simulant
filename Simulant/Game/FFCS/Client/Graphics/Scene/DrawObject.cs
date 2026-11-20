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

        internal void UpdateTransforms(bool a2)
        {
            // FFCS VirtualFunction(7).
            var address = Ptr.ReadPtr().ReadPtr(7 * IntPtr.Size);
            GameFunctions.CheckStatus(address);
            address.Call(Ptr, a2);
        }
    }
}
