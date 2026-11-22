using System;

namespace Simulant.Game.FFCS.Common.Component.BGCollision
{
    // base class for individual objects in the collision scene
    // Inherits Node and QuadtreeNode.
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/Collider.cs
    internal struct Collider : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        internal Collider Next => Ptr.ReadPtr(0x18).As<Collider>(); // NodeLink.Next
        internal Scene Scene => Ptr.ReadPtr(0x48).As<Scene>();
        internal MemoryField<ulong> LayoutObjectId => Ptr.Field<ulong>(0x58); // low dword = layoutinstance Id.InstanceKey, high dword = layoutinstance SubIndex
        internal MemoryField<ulong> LayerMask => Ptr.Field<ulong>(0x68);
        internal ColliderType GetColliderType() => (ColliderType)Ptr.CallVFunc<int>(18);
    }

    internal enum ColliderType
    {
        Streamed = 1,
        Mesh = 2,
        Box = 3,
        Cylinder = 4,
        Sphere = 5,
        Plane = 6,
        PlaneTwoSided = 7,
    }
}
