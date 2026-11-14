using System;
using Lumina.Data.Files;
using Lumina.Data.Parsing;

namespace Lumina.Models.Materials
{
    public class Texture
    {
        public enum Usage
        {
            Catchlight,
            Diffuse,
            Envmap,
            Mask,
            Normal,
            Reflection,
            Specular,
            Wave,
            Whitecap,
        }

        public Material Parent { get; private set; }
        public TextureUsage TextureUsageRaw { get; private set; }
        public Usage TextureUsageSimple { get; private set; }
        public string TexturePath { get; private set; }

        private TexFile _tex;
        public Texture(Material parent, TextureUsage raw, string texturePath)
        {
            Parent = parent;
            TextureUsageRaw = raw;
            TextureUsageSimple = GetUsage(raw);
            TexturePath = texturePath;
        }

        public Texture(TextureUsage raw, string texturePath) : this(null, raw, texturePath)
        {
        }

        public TexFile GetTexture(GameData data)
        {
            if (_tex == null)
            {
                return null;
            }

            return _tex = data.GetFile<TexFile>(TexturePath);
            ;
        }

        public TexFile GetTextureNc(GameData data)
        {
            return data.GetFile<TexFile>(TexturePath);
        }

        private static Usage GetUsage(TextureUsage usage)
        {
            return ((usage == TextureUsage.Sampler) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Diffuse) : ((usage == TextureUsage.Sampler0) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Diffuse) : ((usage == TextureUsage.Sampler1) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Diffuse) : ((usage == TextureUsage.SamplerCatchlight) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Catchlight) : ((usage == TextureUsage.SamplerColorMap0) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Diffuse) : ((usage == TextureUsage.SamplerColorMap1) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Diffuse) : ((usage == TextureUsage.SamplerDiffuse) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Diffuse) : ((usage == TextureUsage.SamplerEnvMap) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Envmap) : ((usage == TextureUsage.SamplerMask) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Mask) : ((usage == TextureUsage.SamplerNormal) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Normal) : ((usage == TextureUsage.SamplerNormalMap0) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Normal) : ((usage == TextureUsage.SamplerNormalMap1) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Normal) : ((usage == TextureUsage.SamplerReflection) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Reflection) : ((usage == TextureUsage.SamplerSpecular) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Specular) : ((usage == TextureUsage.SamplerSpecularMap0) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Specular) : ((usage == TextureUsage.SamplerSpecularMap1) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Specular) : ((usage == TextureUsage.SamplerWaveMap) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Wave) : ((usage == TextureUsage.SamplerWaveMap1) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Wave) : ((usage == TextureUsage.SamplerWaveletMap0) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Wave) : ((usage == TextureUsage.SamplerWaveletMap1) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Wave) : ((usage == TextureUsage.SamplerWhitecapMap) ? (global::Lumina.Models.Materials.Texture.Usage)(Usage.Whitecap) : throw new ArgumentOutOfRangeException(nameof(usage), usage, null))))))))))))))))))))));
        }
    }
}
