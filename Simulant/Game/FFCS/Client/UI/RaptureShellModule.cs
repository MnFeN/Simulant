using System;

namespace Simulant.Game.FFCS.Client.UI
{
    internal struct RaptureShellModule : IMemoryObject
    {
        [SigPattern("FF 52 ? 4C 8B C7 48 8D 54 24 ? 48 8B C8 E8 * * * *",
            "E8 * * * * FE 86 ? ? ? ? C7 86")]
        public static IntPtr ProcessChatBoxFuncPtr { get; set; }

        public IntPtr Ptr { get; set; }

        internal void ProcessChatBox(IntPtr utf8String, UIModule ui)
        {
            GameFunctions.CheckStatus(Ptr, utf8String, ui.Ptr, ProcessChatBoxFuncPtr);
            ProcessChatBoxFuncPtr.Call(Ptr, utf8String, ui.Ptr);
        }
    }
}
