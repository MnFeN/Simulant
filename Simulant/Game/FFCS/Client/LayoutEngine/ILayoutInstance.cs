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

        internal MemoryField<uint> SubId => Ptr.Field<uint>(0x20);
        internal MemoryField<byte> Flags1 => Ptr.Field<byte>(0x29);

        // FFCS BitField<int>(NestingLevel, 4, 3).
        internal static int GetNestingLevel(byte flags1) => (flags1 >> 4) & 7;

        /// <summary>If the primary object has been loaded.</summary>
        internal bool IsPrimaryLoaded() => Ptr.CallVFunc<bool>(22);

        internal void SetColliderActive(bool active) => Ptr.CallVFunc(37, active);
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
        SharedGroup = 6,
    }
}
