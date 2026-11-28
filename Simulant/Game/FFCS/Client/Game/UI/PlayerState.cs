using System;

namespace Simulant.Game.FFCS.Client.Game.UI
{
    // 原生 Client::Game::UI::PlayerState，无基类，大小 0x928。
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Game/UI/PlayerState.cs
    internal struct PlayerState : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        [SigPattern("48 8D 0D * * * * E8 ?? ?? ?? ?? 84 C0 75 06 F6 43 18 02")]
        internal static IntPtr InstancePtr { get; set; }
        internal static PlayerState Instance() => InstancePtr.As<PlayerState>();

        internal MemoryField<short> CurrentLevel => Ptr.Field<short>(0x88);

        // FFCS 未声明。国服 7.58（2026.11.03.0000.0000）：从处理本机职业和等级更新的收包函数出发，
        // 沿 PlayerState.GetClassJobLevel 的同步等级比较分支找到接收该等级的调用。
        // 函数写入 SyncedLevel、按真实职业等级更新 IsLevelSynced，并通知界面刷新。
        [SigPattern("0F B7 53 06 48 8D 0D ?? ?? ?? ?? E8 * * * * 0F B6 13 48 8D 0D ?? ?? ?? ?? 44 8B 43 08")]
        internal static IntPtr SetSyncedLevelFuncPtr { get; set; }

        internal void SetSyncedLevel(ushort level)
            => SetSyncedLevelFuncPtr.Call(Ptr, level);
    }
}
