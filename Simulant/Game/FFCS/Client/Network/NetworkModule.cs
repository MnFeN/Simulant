using System.Runtime.InteropServices;
using System;

namespace Simulant.Game.FFCS.Client.Network
{
    public struct NetworkModule : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        public NetworkModulePacketReceiverCallback PacketReceiverCallback
            => (Ptr + 0xAA8).ReadPtr().As<NetworkModulePacketReceiverCallback>();
    }
}
