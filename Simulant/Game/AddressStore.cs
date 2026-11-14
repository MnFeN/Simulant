using System;

namespace Simulant.Game
{
    internal static class AddressStore
    {
        // Hyperborea
        [SigPattern("48 89 5C 24 ?? 48 89 74 24 ?? 4C 89 64 24 ?? 55 41 56 41 57 48 8B EC 48 83 EC 70")]
        public static IntPtr OnSendPacketFuncPtr { get; set; }

        // Hyperborea
        //                      ↓ ↓ ↓ ↓
        [SigPattern("C7 44 24 ? ? ? ? ? 48 F7 F1")]
        public static IntPtr HeartBeatOpcodeLocatePtr { get; set; }
        public static ushort HeartBeatOpcode => HeartBeatOpcodeLocatePtr.Read<ushort>(0x4);

    }

}
