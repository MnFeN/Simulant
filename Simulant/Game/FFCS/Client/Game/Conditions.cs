using System;

namespace Simulant.Game.FFCS.Client.Game
{
    /// <summary>本机的操作限制；状态图标与这些由服务器下发的限制彼此独立。</summary>
    public struct Conditions : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/Conditions.cs
        [SigPattern("48 8D 0D * * * * 66 2B D8")]
        public static IntPtr InstancePtr { get; set; }

        // Conditions::Set；收包更新同样逐项调用此函数。
        [SigPattern("83 FA 70 7D ?? 53 48 83 EC 20 48 63 C2 41 0F B6 D8")]
        public static IntPtr SetFuncPtr { get; set; }

        public static Conditions Instance() => InstancePtr.As<Conditions>();
        public MemoryField<bool> SufferingStatusAffliction => Ptr.Field<bool>(28);

        /// <summary>只更新禁止自主移动的异常状态标志，不改写其他操作限制。</summary>
        public void SetMovementLocked(bool locked) => SetFuncPtr.Call(Ptr, 28, locked);
    }
}
