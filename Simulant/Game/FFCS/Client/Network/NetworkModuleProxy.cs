using System;

namespace Simulant.Game.FFCS.Client.Network
{
    public struct NetworkModuleProxy : IMemoryObject
    {

        public IntPtr Ptr { get; set; }
        public NetworkModule NetworkModule => Ptr.ReadPtr(0x8).As<NetworkModule>();
    }
}

