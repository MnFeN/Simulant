using System;

namespace Simulant.Game.FFCS.Client.System.Resource.Handle
{
    // Client::System::Resource::Handle::DefaultResourceHandle, FFCS size 0xC0.
    //   Client::System::Resource::Handle::ResourceHandle
    //     Client::System::Common::NonCopyable
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/System/Resource/Handle/DefaultResouceHandle.cs
    internal struct DefaultResourceHandle : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        internal IntPtr Data => Ptr.ReadPtr(0xB0);
    }
}
