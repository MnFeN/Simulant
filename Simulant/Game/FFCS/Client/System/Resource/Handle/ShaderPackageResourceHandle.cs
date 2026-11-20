using Simulant.Game.FFCS.Client.Graphics.Kernel;
using System;

namespace Simulant.Game.FFCS.Client.System.Resource.Handle
{
    // Client::System::Resource::Handle::ShaderPackageResourceHandle, FFCS size 0xC0.
    //   Client::System::Resource::Handle::DefaultResourceHandle
    //     Client::System::Resource::Handle::ResourceHandle
    //       Client::System::Common::NonCopyable
    // .shpk
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/System/Resource/Handle/ShaderPackageResourceHandle.cs
    internal struct ShaderPackageResourceHandle : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        internal ShaderPackage ShaderPackage => Ptr.As<DefaultResourceHandle>().Data.As<ShaderPackage>();
    }
}
