using System;

namespace Simulant.Game.FFCS.Client.UI
{
    // Client::UI::UIGlobals
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/UI/UIGlobals.cs
    internal static class UIGlobals
    {
        [SigPattern("E8 * * * * 45 0F B7 C5")]
        public static IntPtr PlaySoundEffectFuncPtr { get; set; }

        internal static void PlaySoundEffect(uint effectId)
            => PlaySoundEffectFuncPtr.Call(effectId, IntPtr.Zero, IntPtr.Zero, (byte)0);
    }
}
