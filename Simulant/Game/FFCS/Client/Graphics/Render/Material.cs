using Simulant.Game.FFCS.Client.Graphics.Kernel;
using Simulant.Game.FFCS.Client.System.Resource.Handle;
using System;

namespace Simulant.Game.FFCS.Client.Graphics.Render
{
    // Client::Graphics::Render::Material, FFCS size 0x58.
    //   Client::Graphics::ReferencedClassBase
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Graphics/Render/Material.cs
    /// <summary>
    /// Represents a renderer material.
    /// </summary>
    /// <remarks>
    /// This structure is the header of a memory block of size sizeof(Material) + sizeof(uint) * ShaderKeyCount + sizeof(Material.TextureEntry) * TextureCount.
    /// On construction, ShaderKeyCount is determined by <see cref="ShaderPackage.MaterialKeyCount"/>, and TextureCount is the number of
    /// <see cref="ShaderPackage.Samplers"/> with <see cref="ShaderPackage.ConstantSamplerUnknown.Slot"/> == <see cref="ShaderPackage.SamplerSlotMaterial"/>.
    /// </remarks>
    internal struct Material : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        /// <summary>
        /// The resource handle this material was instantiated from. Its <see cref="MaterialResourceHandle.Material"/> will be the current structure.
        /// </summary>
        internal MaterialResourceHandle MaterialResourceHandle => Ptr.ReadPtr(0x10).As<MaterialResourceHandle>();
        internal ConstantBuffer MaterialParameterCBuffer => Ptr.ReadPtr(0x28).As<ConstantBuffer>(); // arbitrary size and contents, defined by the shader package
    }
}
