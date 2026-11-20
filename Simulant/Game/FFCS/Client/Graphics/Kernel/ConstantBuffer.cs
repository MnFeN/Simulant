using System;

namespace Simulant.Game.FFCS.Client.Graphics.Kernel
{
    // Client::Graphics::Kernel::ConstantBuffer, FFCS size 0x70.
    //   Client::Graphics::Kernel::Buffer
    //     Client::Graphics::Kernel::Resource
    //       Client::Graphics::DelayedReleaseClassBase
    //         Client::Graphics::ReferencedClassBase
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Graphics/Kernel/ConstantBuffer.cs
    /// <summary>
    /// A constant buffer (or cbuffer), which is used to send values to shaders. Usually contains 4*n floats.
    /// </summary>
    internal struct ConstantBuffer : IMemoryObject
    {
        internal const byte DefaultLoadSourcePointerFlags = 2;

        [SigPattern("E8 * * * * 45 33 DB 48 85 C0")]
        internal static IntPtr LoadSourcePointerFuncPtr { get; set; }

        public IntPtr Ptr { get; set; }
        internal MemoryField<int> ByteSize => Ptr.Field<int>(0x20);
        internal MemoryField<int> Flags => Ptr.Field<int>(0x24);
        internal IntPtr UnsafeSourcePointer => Ptr.ReadPtr(0x28);

        internal IntPtr TryGetSourcePointer()
            => (Flags.Get() & 0x4003) == 0 ? UnsafeSourcePointer : IntPtr.Zero;

        internal IntPtr LoadSourcePointer(int byteOffset, int byteSize, byte flags = DefaultLoadSourcePointerFlags)
            => LoadSourcePointerFuncPtr.Call<IntPtr>(Ptr, byteOffset, byteSize, flags);
    }
}
