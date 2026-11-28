using System;

namespace Simulant.Game.FFCS.Client.Game.UI
{
    // Client::Game::UI::WeaponState
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Game/UI/WeaponState.cs
    /// <summary>
    /// Represents the in-memory layout of the WeaponState structure.
    /// </summary>
    internal struct WeaponState : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // Offset 0x10: Originally, this offset was used for AutoAttackState.
        // In older versions, a simple bool (IsAutoAttacking) resided here.
        // In the current patch, analysis shows auto-attack state is handled globally via DAT_142921510.
        internal AutoAttackState AutoAttackState => Ptr.As<AutoAttackState>(0x10);
    }

    /// <summary>
    /// Represents the AutoAttackState structure.
    /// Originally, this might have been embedded in WeaponState at offset 0x10,
    /// but current analysis shows auto-attack is managed globally.
    /// </summary>
    internal struct AutoAttackState : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // Offset 0x00: A single byte that indicates if auto-attack is active.
        // 国服 7.58（2026.11.03.0000.0000）：从 HandleActorControl 的 AutoAttack 分支可核对，
        // OnActorControl 的 this 指向 UIState 内此字节。AutoAttackState.Set 调用 SetImpl 时也传入同一地址，
        // SetImpl 先更新此字节，再按参数决定是否发包。
        internal MemoryField<bool> IsAutoAttacking => Ptr.Field<bool>(0);
    }
}
