using System;

namespace Simulant.Game.FFCS.Client.Game.Control
{
    /// <summary>读取当前选中目标，用于切换目标后补发模拟仇恨。</summary>
    public struct TargetSystem : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/Game/Control/TargetSystem.cs
        [SigPattern("48 8D 0D * * * * E8 ?? ?? ?? ?? 48 3B C6 0F 95 C0")]
        public static IntPtr InstancePtr { get; set; }
        public static TargetSystem Instance() => InstancePtr.As<TargetSystem>();
        public MemoryField<IntPtr> Target => Ptr.Field<IntPtr>(0x80);
        public MemoryField<IntPtr> SoftTarget => Ptr.Field<IntPtr>(0x88);
    }
}
