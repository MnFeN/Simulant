using System;

namespace Simulant.Game.FFCS.Client.Game.UI
{
    /// <summary>客户端界面状态，只映射模拟小队所需的仇恨数据。</summary>
    public struct UIState : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/Game/UI/UIState.cs
        [SigPattern("48 8D 0D * * * * 45 33 C0 BA ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? BA")]
        public static IntPtr InstancePtr { get; set; }

        public static UIState Instance() => InstancePtr.As<UIState>();
        public Hate Hate => Ptr.As<Hate>(0x8);
    }
}
