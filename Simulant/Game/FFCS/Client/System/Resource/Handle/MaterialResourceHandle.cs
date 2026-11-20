using Simulant.Game.FFCS.Client.Graphics.Render;
using System;

namespace Simulant.Game.FFCS.Client.System.Resource.Handle
{
    // Client::System::Resource::Handle::MaterialResourceHandle, FFCS size 0x108.
    //   Client::System::Resource::Handle::DefaultResourceHandle
    //     Client::System::Resource::Handle::ResourceHandle
    //       Client::System::Common::NonCopyable
    // .mtrl
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/System/Resource/Handle/MaterialResourceHandle.cs
    internal struct MaterialResourceHandle : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        /// <summary>
        /// The instantiated material. Its <see cref="Material.MaterialResourceHandle"/> will be the current structure.
        /// </summary>
        internal Material Material => Ptr.ReadPtr(0xC0).As<Material>();
        internal ShaderPackageResourceHandle ShaderPackageResourceHandle => Ptr.ReadPtr(0xC8).As<ShaderPackageResourceHandle>();
    }
}
