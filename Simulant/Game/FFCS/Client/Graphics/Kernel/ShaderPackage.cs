using System;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.Graphics.Kernel
{
    // Client::Graphics::Kernel::ShaderPackage, FFCS size 0x178.
    //   Client::Graphics::ReferencedClassBase
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Graphics/Kernel/ShaderPackage.cs
    internal struct ShaderPackage : IMemoryObject
    {
        [StructLayout(LayoutKind.Explicit, Size = 0x8)]
        internal struct MaterialElement
        {
            [FieldOffset(0x0)] internal uint CRC;
            [FieldOffset(0x4)] internal ushort Offset;
            [FieldOffset(0x6)] internal ushort Size;
        }

        public IntPtr Ptr { get; set; }
        internal MemoryField<ushort> MaterialConstantBufferSize => Ptr.Field<ushort>(0xD0);

        // the following counts are totals shared by shaders in the shpk, they are not all used for all shaders
        internal MemoryField<ushort> MaterialElementCount => Ptr.Field<ushort>(0xD4); // these are individual elements within the material constant buffer

        // following are arrays
        // these are all pre-allocated with the ShaderPackage object in one block of memory
        internal IntPtr MaterialElements => Ptr.ReadPtr(0xF8);
    }
}
