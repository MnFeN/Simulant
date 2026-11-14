using System;

namespace Simulant.Game.FFCS.Client.Game.Character
{
    // Client::Game::Character::ContainerInterface
    public struct ContainerInterface : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        public Character OwnerObject => Ptr.ReadPtr(0x08).As<Character>();
    }
}
