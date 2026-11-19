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

        // 交互入口签名参考 Hyperborea/Memory.cs 的 TargetSystem_InteractWithObjectHook。
        // https://github.com/MnFeN/Hyperborea/blob/ee7c028e4b8475845b8f3d1bbd5957f5282181a2/Hyperborea/Memory.cs#L28-L30
        [SigPattern("48 89 5C 24 ?? 48 89 6C 24 ?? 56 48 83 EC 20 48 8B E9 41 0F B6 F0")]
        internal static IntPtr InteractWithObjectFuncPtr { get; set; }

        public static TargetSystem Instance() => InstancePtr.As<TargetSystem>();
        public MemoryField<IntPtr> Target => Ptr.Field<IntPtr>(0x80);
        public MemoryField<IntPtr> SoftTarget => Ptr.Field<IntPtr>(0x88);
    }
}
