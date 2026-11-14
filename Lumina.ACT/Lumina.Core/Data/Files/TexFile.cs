using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Lumina.Data.Attributes;
using Lumina.Data.Parsing.Tex.Buffers;

// ReSharper disable InconsistentNaming
namespace Lumina.Data.Files
{
    [FileExtension(".tex")]
    public class TexFile : FileResource
    {
        [Flags]
        public enum Attribute : uint
        {
            DiscardPerFrame = 0x1,
            DiscardPerMap = 0x2,
            Managed = 0x4,
            UserManaged = 0x8,
            CpuRead = 0x10,
            LocationMain = 0x20,
            NoGpuRead = 0x40,
            AlignedSize = 0x80,
            EdgeCulling = 0x100,
            LocationOnion = 0x200,
            ReadWrite = 0x400,
            Immutable = 0x800,
            TextureRenderTarget = 0x100000,
            TextureDepthStencil = 0x200000,
            TextureType1D = 0x400000,
            TextureType2D = 0x800000,
            TextureType3D = 0x1000000,
            TextureType2DArray = 0x10000000,
            TextureTypeCube = 0x2000000,
            TextureTypeMask = 0x13C00000,
            TextureSwizzle = 0x4000000,
            TextureNoTiled = 0x8000000,
            TextureNoSwizzle = 0x80000000,
        }

        [Flags]
        public enum TextureFormat
        {
            TypeShift = 0xC,
            TypeMask = 0xF000,
            ComponentShift = 0x8,
            ComponentMask = 0xF00,
            BppShift = 0x4,
            BppMask = 0xF0,
            EnumShift = 0x0,
            EnumMask = 0xF,
            TypeInteger = 0x1,
            TypeFloat = 0x2,
            TypeDxt = 0x3,
            TypeBc123 = 0x3,
            TypeDepthStencil = 0x4,
            TypeSpecial = 0x5,
            TypeBc57 = 0x6,
            Unknown = 0x0,
            // Integer types
            L8 = 0x1130,
            A8 = 0x1131,
            B4G4R4A4 = 0x1440,
            B5G5R5A1 = 0x1441,
            B8G8R8A8 = 0x1450,
            B8G8R8X8 = 0x1451,
            [Obsolete("Use B4G4R4A4 instead.")]
            R4G4B4A4 = 0x1440,
            [Obsolete("Use B5G5R5A1 instead.")]
            R5G5B5A1 = 0x1441,
            [Obsolete("Use B8G8R8A8 instead.")]
            A8R8G8B8 = 0x1450,
            [Obsolete("Use B8G8R8X8 instead.")]
            R8G8B8X8 = 0x1451,
            [Obsolete("Not supported by Windows DirectX 11 version of the game, nor have any mention of the value, as of 6.15.")]
            A8R8G8B82 = 0x1452,
            // Floating point types
            R32F = 0x2150,
            R16G16F = 0x2250,
            R32G32F = 0x2260,
            R16G16B16A16F = 0x2460,
            R32G32B32A32F = 0x2470,
            // Block compression types (DX9 names)
            DXT1 = 0x3420,
            DXT3 = 0x3430,
            DXT5 = 0x3431,
            ATI2 = 0x6230,
            // Block compression types (DX11 names)
            BC1 = 0x3420,
            BC2 = 0x3430,
            BC3 = 0x3431,
            BC4 = 0x6120,
            BC5 = 0x6230,
            BC6H = 0x6330,
            BC7 = 0x6432,
            // Depth stencil types
            // Does not exist in ffxiv_dx11.exe: RGBA8 0x4401
            D16 = 0x4140,
            D24S8 = 0x4250,
            // Special types
            Null = 0x5100,
            Shadow16 = 0x5140,
            Shadow24 = 0x5150,
        }

        [StructLayout(LayoutKind.Explicit, Size = 80)]
        public unsafe struct TexHeader
        {
            [FieldOffset(0)]
            public Attribute Type;
            [FieldOffset(4)]
            public TextureFormat Format;
            [FieldOffset(8)]
            public ushort Width;
            [FieldOffset(10)]
            public ushort Height;
            [FieldOffset(12)]
            public ushort Depth;
            [FieldOffset(14)]
            [Obsolete("Use the current texture format instead.")]
            public byte MipLevelsCount;
            [FieldOffset(15)]
            public byte ArraySize;
            [FieldOffset(16)]
            public fixed uint LodOffset[3];
            [FieldOffset(28)]
            public fixed uint OffsetToSurface[13];
            public int MipCount {[MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => MipLevelsCount & 0x7F; set => MipLevelsCount = (byte)((MipLevelsCount & 0x80) | (((value >= 0) && (value <= 0x7F)) ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "Value is out of supported range from the value type."))); }
            public bool MipUnknownFlag {[MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (MipLevelsCount & 0x80) != 0; set => MipLevelsCount = (byte)((MipLevelsCount & 0x7F) | (value ? 0x80 : 0)); }
        }

        public enum DxgiFormatConversion
        {
            NoConversion,
            FromL8ToB8G8R8A8,
            FromB4G4R4A4ToB8G8R8A8,
            FromB5G5R5A1ToB8G8R8A8,
        }

        private byte[] _imageDataDefault;
        public TexHeader Header;
        public int HeaderLength => Unsafe.SizeOf<TexHeader>();

        public TextureBuffer TextureBuffer = null;
        public byte[] ImageData
        {
            get
            {
                _imageDataDefault = _imageDataDefault ?? TextureBuffer.Filter(mip: 0, z: 0, format: TextureFormat.B8G8R8A8).RawData;
                return _imageDataDefault;
            }
        }

        public override void LoadFile()
        {
            Reader.BaseStream.Position = 0;
            Header = Reader.ReadStructure<TexHeader>();
            if ((Header.Type & Attribute.TextureTypeCube) != 0 && Header.Depth != 1)
                throw new NotSupportedException("Cube map texture with depth value above 1 is currently not supported.");
            TextureBuffer = TextureBuffer.FromStream(Header, Reader);
        }

        public int SliceSize(int mipmapIndex, out int width, out int height)
        {
            if (mipmapIndex < 0 || mipmapIndex >= Math.Max(Header.MipCount, 1))
                throw new ArgumentOutOfRangeException(nameof(mipmapIndex), mipmapIndex, null);
            var bpp = 1 << ((int)(Header.Format & TextureFormat.BppMask) >> (int)TextureFormat.BppShift);
            width = Math.Max(1, Header.Width >> mipmapIndex);
            height = Math.Max(1, Header.Height >> mipmapIndex);
            switch ((TextureFormat)((int)(Header.Format & TextureFormat.TypeMask) >> (int)TextureFormat.TypeShift))
            {
                case TextureFormat.TypeBc123:
                case TextureFormat.TypeBc57:
                    var nbw = Math.Max(1, (width + 3) / 4);
                    var nbh = Math.Max(1, (height + 3) / 4);
                    return nbw * nbh * bpp * 2;
                case TextureFormat.TypeInteger:
                case TextureFormat.TypeFloat:
                case TextureFormat.TypeDepthStencil:
                case TextureFormat.TypeSpecial:
                    return width * height * bpp / 8;
                default:
                    throw new NotSupportedException();
            }
        }

        public unsafe Span<byte> SliceSpan(int mipmapIndex, int sliceIndex, out int sliceSize, out int width, out int height)
        {
            sliceSize = SliceSize(mipmapIndex, out width, out height);
            if (mipmapIndex < 0 || mipmapIndex >= Math.Max(Header.MipCount, 1))
                throw new ArgumentOutOfRangeException(nameof(mipmapIndex), mipmapIndex, null);
            switch (Header.Type & Attribute.TextureTypeMask)
            {
                case var _ when sliceIndex < 0:
                case Attribute.TextureType1D when sliceIndex != 0:
                case Attribute.TextureType2D when sliceIndex != 0:
                case Attribute.TextureType3D when sliceIndex >= Header.Depth:
                case Attribute.TextureTypeCube when sliceIndex >= 6:
                case Attribute.TextureType2DArray when sliceIndex >= Header.ArraySize:
                    throw new ArgumentOutOfRangeException(nameof(sliceIndex), sliceIndex, null);
            }

            var offset = (int)Header.OffsetToSurface[mipmapIndex] + sliceIndex * sliceSize;
            return offset >= Data.Length ? new global::System.Span<byte>() : Data.AsSpan(offset, Math.Min(Data.Length - offset, sliceSize));
        }

        public static Tuple<int, DxgiFormatConversion> GetDxgiFormatFromTextureFormat(TextureFormat format, bool useGameCompatible = true)
        {
            return ((format == TextureFormat.Unknown) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x00, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.Null) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x00, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.R32G32B32A32F) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x02, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.R16G16B16A16F) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x0a, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.R32G32F) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x10, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.R16G16F) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x22, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.R32F) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x29, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.D24S8) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x2c, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.Shadow24) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x2c, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.D16) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x35, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.Shadow16) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x35, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.A8) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x41, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.BC1) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x47, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.BC2) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x4a, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.BC3) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x4d, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.BC5) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x53, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.L8) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x57, DxgiFormatConversion.FromL8ToB8G8R8A8)) : ((format == TextureFormat.B4G4R4A4) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(useGameCompatible ? Tuple.Create(0x57, DxgiFormatConversion.FromB4G4R4A4ToB8G8R8A8) // DXGI_FORMAT_B8G8R8A8_UNORM
 : Tuple.Create(0x73, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.B5G5R5A1) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(useGameCompatible ? Tuple.Create(0x57, DxgiFormatConversion.FromB5G5R5A1ToB8G8R8A8) // DXGI_FORMAT_B8G8R8A8_UNORM
 : Tuple.Create(0x56, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.B8G8R8A8) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x57, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.B8G8R8X8) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x58, DxgiFormatConversion.NoConversion)) : ((format == TextureFormat.BC7) ? (global::System.Tuple<int, global::Lumina.Data.Files.TexFile.DxgiFormatConversion>)(Tuple.Create(0x62, DxgiFormatConversion.NoConversion)) : throw new NotSupportedException($"TextureFormat {(int)format:X04} is not supported.")))))))))))))))))))))));
        }
    }
}
