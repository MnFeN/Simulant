using System;
using System.Numerics;

namespace Simulant.Game.FFCS.Client.Graphics.Scene
{
    public struct Object : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        public Object ParentObject => Ptr.ReadPtr(0x18).As<Object>();
        public MemoryField<ulong> ObjectFlags => Ptr.Field<ulong>(0x38);

        internal bool IsTransformChanged
        {
            get => (ObjectFlags.Get() & 2UL) != 0;
            set
            {
                const ulong mask = 1UL << 1;
                var flags = ObjectFlags.Get();
                ObjectFlags.Set(value ? flags | mask : flags & ~mask);
            }
        }

        public MemoryField<Vector3> Position => Ptr.Field<Vector3>(0x50);
        public MemoryField<Quaternion> Rotation => Ptr.Field<Quaternion>(0x60);
        public MemoryField<Vector3> Scale => Ptr.Field<Vector3>(0x70);
    }
}
