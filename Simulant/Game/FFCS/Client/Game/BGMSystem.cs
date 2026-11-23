using System;

namespace Simulant.Game.FFCS.Client.Game
{
    // Client::Game::BGMSystem
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/Game/BGMSystem.cs
    public struct BGMSystem : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        [SigPattern("4C 8B 15 * * * * 4D 85 D2 74 77 41 83 7A",
                    "48 8B 05 * * * * 48 85 C0 74 51 83 78 08 0B")]
        public static IntPtr InstancePtr { get; set; }

        public static BGMSystem Instance()
            => InstancePtr.ReadPtr().As<BGMSystem>();

        [SigPattern("4C 8B 1D ? ? ? ? 4D 85 DB 0F 84 ? ? ? ? 41 3B 53")]
        public static IntPtr SetBGMFuncPtr { get; set; }

        public static void SetBGM(
            ushort bgmId,
            uint sceneId,
            byte a3 = 0,
            bool enableCustomFade = false,
            uint fadeOutMs = 0,
            uint fadeInMs = 0,
            uint fadeInStartMs = 0,
            byte a8 = 0,
            byte a9 = 0,
            float initialVolume = 1f)
            => SetBGMFuncPtr.Call(bgmId, sceneId, a3, enableCustomFade, fadeOutMs, fadeInMs, fadeInStartMs, a8, a9, initialVolume);

        // 首条指令读取 BGM 单例指针，位移仅作通配（不是 *），保留函数入口。
        [SigPattern("4C 8B 05 ? ? ? ? 4D 85 C0 74 3D")]
        public static IntPtr ResetBGMFuncPtr { get; set; }

        public void ResetBGM(uint sceneId)
            => ResetBGMFuncPtr.Call(Ptr, sceneId);
    }
}
