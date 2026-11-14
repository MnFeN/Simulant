using System;
using System.IO;
using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using Lumina.Data.Files;

namespace Lumina.Data.Parsing.Tex.Buffers
{
    public class BlockCompressionTextureBuffer : TextureBuffer
    {
        private int _version;
        private BlockCompressionTextureBuffer(int version, TexFile.Attribute attribute, int width, int height, int depth, int[] mipmapAllocations, byte[] buffer) : base(attribute, width, height, depth, mipmapAllocations, buffer, tb => ((BlockCompressionTextureBuffer)tb)._version = version)
        {
            _version = version;
        }

        public BlockCompressionTextureBuffer(TexFile.TextureFormat format, TexFile.Attribute attribute, int width, int height, int depth, int[] mipmapAllocations, byte[] buffer) : base(attribute, width, height, depth, mipmapAllocations, buffer, tb =>
        {
            ((BlockCompressionTextureBuffer)tb)._version = ((format == TexFile.TextureFormat.BC1) ? (int)(1) : ((format == TexFile.TextureFormat.BC2) ? (int)(2) : ((format == TexFile.TextureFormat.BC3) ? (int)(3) : ((format == TexFile.TextureFormat.BC5) ? (int)(5) : ((format == TexFile.TextureFormat.BC7) ? (int)(7) : throw new ArgumentException(null, nameof(format)))))));
        })
        {
            _version = ((format == TexFile.TextureFormat.BC1) ? (int)(1) : ((format == TexFile.TextureFormat.BC2) ? (int)(2) : ((format == TexFile.TextureFormat.BC3) ? (int)(3) : ((format == TexFile.TextureFormat.BC5) ? (int)(5) : ((format == TexFile.TextureFormat.BC7) ? (int)(7) : throw new ArgumentException(null, nameof(format)))))));
        }

        public override int NumBytesOfMipmapPerPlane(int mipmapIndex) => CalculateNumBytesPerPlane(WidthOfMipmap(mipmapIndex), HeightOfMipmap(mipmapIndex));
        protected override void ConvertToB8G8R8A8(byte[] buffer, int destOffset, int sourceOffset, int width, int height, int depth)
        {
            var cbPlane = CalculateNumBytesPerPlane(width, height);
            for (var i = 0; i < depth; i++)
            {
                var decoder = new BcDecoder();
                var stream = new MemoryStream(RawData, sourceOffset + cbPlane * i, cbPlane);
                var rgbaData = decoder.DecodeRaw(stream, width, height, ((_version == 1) ? CompressionFormat.Bc1 : ((_version == 2) ? CompressionFormat.Bc2 : ((_version == 3) ? CompressionFormat.Bc3 : ((_version == 5) ? CompressionFormat.Bc5 : ((_version == 7) ? CompressionFormat.Bc7 : throw new NotSupportedException("Unknown block compression version.")))))));
                // Write ColorRgba32[] to output byte[]
                for (var j = 0; j < rgbaData.Length; j++)
                {
                    var color = rgbaData[j];
                    buffer[destOffset + (i * width * height * 4) + (j * 4) + 0] = color.b;
                    buffer[destOffset + (i * width * height * 4) + (j * 4) + 1] = color.g;
                    buffer[destOffset + (i * width * height * 4) + (j * 4) + 2] = color.r;
                    buffer[destOffset + (i * width * height * 4) + (j * 4) + 3] = color.a;
                }
            }
        }

        protected override TextureBuffer CreateNew(TexFile.Attribute attribute, int width, int height, int depth, int[] mipmapAllocations, byte[] buffer) => new BlockCompressionTextureBuffer(_version, attribute, width, height, depth, mipmapAllocations, buffer);
        private int CalculateNumBytesPerPlane(int w, int h) => Math.Max(1, ((w + 3) / 4)) * Math.Max(1, ((h + 3) / 4)) * (_version == 1 ? 8 : 16);
    }
}
