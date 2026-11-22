using Simulant.Game.FFCS.Client.Network;
using Simulant.Game.FFCS.Client.UI;
using Simulant.Game.FFCS.Common.Component.BGCollision;
using System;

namespace Simulant.Game.FFCS.Client.System.Framework
{
    // Client::System::Framework::Framework
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/System/Framework/Framework.cs
    public struct Framework : IMemoryObject
    {
        [SigPattern("48 8B 1D * * * * 8B 7C 24")]
        public static IntPtr InstancePtrPtr { get; set; }

        public static Framework Instance
            => InstancePtrPtr.ReadPtr().As<Framework>();

        public IntPtr Ptr { get; set; }

        public NetworkModuleProxy NetworkModuleProxy
            => Ptr.ReadPtr(0x1678).As<NetworkModuleProxy>();

        public UIModule UIModule
            => Ptr.ReadPtr(0x2B68).As<UIModule>();

        internal BGCollisionModule BGCollisionModule
            => Ptr.ReadPtr(0x2B58).As<BGCollisionModule>();
    }
}
