using System;
using System.Numerics;

namespace Simulant.Game.FFCS.Client.Graphics.Render
{
    // Client::Graphics::Render::Light, FFCS size 0x130.
    //   Client::Graphics::Render::RenderObject
    //     Client::Graphics::ReferencedClassBase
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Graphics/Render/Light.cs
    internal struct Light : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        internal LightShape LightShape => (LightShape)Ptr.Read<uint>(0x1C);
        internal MemoryField<Vector4> ColorIntensity => Ptr.Field<Vector4>(0x28);
    }

    // Client::Graphics::Render::LightingManager::LightShape
    internal enum LightShape : uint
    {
        /// <summary>
        /// A uniform ambient light with no distinct shape that applies everywhere.
        /// </summary>
        /// <remarks>
        /// Only seems to function in exterior areas.
        /// </remarks>
        WorldLight = 1,

        /// <summary>
        /// A light that emits from its location equally in all directions.
        /// </summary>
        PointLight = 2, // 'area' light
    }
}
