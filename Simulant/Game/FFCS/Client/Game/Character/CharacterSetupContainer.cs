using System;

namespace Simulant.Game.FFCS.Client.Game.Character
{
    // Client::Game::Character::CharacterSetupContainer
    //   Client::Game::Character::ContainerInterface
    public struct CharacterSetupContainer : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // Inherits<ContainerInterface>
        private ContainerInterface ContainerInterface => Ptr.As<ContainerInterface>();
        #region ContainerInterface
        public Character OwnerObject => ContainerInterface.OwnerObject;

        #endregion
        [SigPattern("E8 * * * * 8B 87 ? ? ? ? 85 C0 74 ? 83 F8")]
        public static IntPtr CopyFromCharacterFuncPtr { get; set; }

        public ulong CopyFromCharacter(Character source, CopyFlags flags)
            => CopyFromCharacterFuncPtr.Call<ulong>(Ptr, source.Ptr, (uint)flags);

        [SigPattern("E8 * * * * 45 0F B6 86 ? ? ? ? 48 8D 8F")]
        public static IntPtr SetupBNpcFuncPtr { get; set; }

        public void SetupBNpc(uint bNpcBaseId, uint bNpcNameId = 0)
            => SetupBNpcFuncPtr.Call(Ptr, bNpcBaseId, bNpcNameId);

        [Flags]
        public enum CopyFlags : uint
        {
            None = 0x00,
            Mode = 0x1, // emote loop etc
            Mount = 0x2,
            ClassJob = 0x4,
            Companion = 0x20,
            WeaponHiding = 0x80,
            Target = 0x400,
            Name = 0x1000,
            LastAnimation = 0x8000,
            Position = 0x10000, // includes rotation
            UseSecondaryCharaId = 0x200000,
            Ornament = 0x400000
        }
    }
}
