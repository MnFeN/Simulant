using System;

namespace Simulant.Game.FFCS.Client.Game.Object
{
    // Client::Game::Object::ClientObjectManager
    public struct EventObjectManager : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        [SigPattern("48 8D 0D * * * * E8 ? ? ? ? 48 85 C0 74 ? 48 39 B0 ? ? ? ? 74 ? FF C3")]
        public static IntPtr InstancePtr { get; set; }
        public static EventObjectManager Instance
            => InstancePtr.As<EventObjectManager>();

    }
}
