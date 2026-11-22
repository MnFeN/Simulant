using Simulant.Game.FFCS.Client.LayoutEngine.Layer;
using Simulant.Game.FFCS.Common.Component.BGCollision;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.LayoutEngine
{
    // Client::LayoutEngine::ILayoutInstance
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/LayoutEngine/ILayoutInstance.cs
    internal struct ILayoutInstance : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        internal LayerManager Layer => Ptr.ReadPtr(0x08).As<LayerManager>();
        internal LayoutManager Layout => Ptr.ReadPtr(0x10).As<LayoutManager>();
        internal MemoryField<uint> SubId => Ptr.Field<uint>(0x20);
        internal MemoryField<byte> Flags1 => Ptr.Field<byte>(0x29);
        private MemoryField<byte> Flags3 => Ptr.Field<byte>(0x2B);
        internal bool IsActive => (Flags3.Get() & (1 << 4)) != 0; // FFCS BitField<bool>(IsActive, 4).

        // FFCS BitField<int>(NestingLevel, 4, 3).
        internal static int GetNestingLevel(byte flags1) => (flags1 >> 4) & 7;

        /// <summary>If the primary object has been loaded.</summary>
        internal bool IsPrimaryLoaded() => Ptr.CallVFunc<bool>(22);

        internal IntPtr GetPrimaryPath() => Ptr.CallVFunc<IntPtr>(7);
        internal IntPtr GetSecondaryPath() => Ptr.CallVFunc<IntPtr>(31);
        internal Collider GetCollider() => Ptr.CallVFunc<IntPtr>(34).As<Collider>();
        internal Collider GetCollider2() => Ptr.CallVFunc<IntPtr>(35).As<Collider>();
        internal void SetColliderActive(bool active) => Ptr.CallVFunc(37, active);
        internal void SetActive(bool active) => Ptr.CallVFunc(63, active);
    }

    // FFCS 的 Client.LayoutEngine.Transform 定义在同一文件内。
    [StructLayout(LayoutKind.Explicit, Size = 0x30)]
    internal struct Transform
    {
        [FieldOffset(0x00)]
        internal Vector3 Translation;
        [FieldOffset(0x0C)]
        internal int Type;
        [FieldOffset(0x10)]
        internal Quaternion Rotation;
        [FieldOffset(0x20)]
        internal Vector3 Scale;
    }

    internal enum InstanceType : byte
    {
        BgPart = 1,
        Light = 3,
        SharedGroup = 6,
        CollisionBox = 57, // generic collider
    }
}
