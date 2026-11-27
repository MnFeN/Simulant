using System;

namespace Simulant.Game.FFCS.Component.GUI
{
    // FFXIVClientStructs/FFXIV/Component/GUI/AtkModule.cs
    public struct AtkModule : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        [SigPattern("E8 * * * * 44 0F B6 44 24 ?? 8B D3")]
        private static IntPtr IsTextInputActiveFuncPtr { get; set; }

        internal bool IsTextInputActive() => IsTextInputActiveFuncPtr.Call<bool>(Ptr);
    }
}
