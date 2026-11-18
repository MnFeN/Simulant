using System;

namespace Simulant.Game.FFCS.Client.Game.UI
{
    /// <summary>客户端快捷栏，提供原生读条取消操作。</summary>
    public struct Hotbar : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // FFXIVClientStructs Client/Game/UI/Hotbar.cs 的 Hotbar.CancelCast，走客户端正常取消请求。
        [SigPattern("48 83 EC 38 33 D2 C7 44 24 ? ? ? ? ? 45 33 C9 45 33 C0 8D 4A 69 E8 ? ? ? ? 48 83 C4 38 C3")]
        internal static IntPtr CancelCastFuncPtr { get; set; }

        internal void CancelCast() => CancelCastFuncPtr.Call(Ptr);
    }
}
