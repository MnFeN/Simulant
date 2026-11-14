using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Data.Parsing;
using Lumina.Extensions;
using Lumina.Models.Models;

namespace Lumina.Models.Materials
{
    public partial class Material
    {
        public string MaterialPath { get; private set; }
        public string ResolvedPath { get; private set; }
        public Model Parent { get; private set; }
        public MtrlFile File { get; private set; }
        public Texture[] Textures { get; private set; }
        public string ShaderPack { get; private set; }
        public int VariantId { get; set; }

        private Dictionary<int, string> StringOffsetToStringMap;
        public Material(string path, int variantId = 1)
        {
            MaterialPath = path;
            VariantId = !path.StartsWith("/") ? GetVariantIdFromPath(path) : variantId;
        }

        public Material(Model parent, string path, int variantId = 1)
        {
            Parent = parent;
            MaterialPath = path;
            VariantId = variantId;
        }

        public Material(MtrlFile file)
        {
            File = file;
            VariantId = GetVariantIdFromPath(file.FilePath);
            BuildMaterial();
        }

        public Material(GameData data, string path, int variantId = 1)
        {
            MaterialPath = path;
            BuildMaterial();
            Update(data);
        }

        public Material Update(GameData data)
        {
            if (MaterialPath.StartsWith("/"))
            {
                ResolvedPath = ResolveRelativeMaterialPath(MaterialPath, VariantId);
                if (ResolvedPath == null)
                {
                    return this;
                }

                File = data.GetFile<MtrlFile>(ResolvedPath);
            }
            else
            {
                File = data.GetFile<MtrlFile>(MaterialPath);
            }

            if (File != null)
                BuildMaterial();
            return this;
        }

        public static string ResolveRelativeMaterialPath(string relativePath, int variantId) => ResolveRelativeMaterialPath(relativePath, variantId, true);
        public static string ResolveRelativeMaterialPath(string relativePath, int variantId, bool strictSuffixValidation = true)
        {
            Regex rx = strictSuffixValidation ? MatNameRegexStrictSuffix() : MatNameRegexLaxSuffix();
            var result = rx.Match(relativePath);
            if (!result.Success)
                return null;
            var id1 = result.Groups["id1"].Value[0];
            var val1 = result.Groups["val1"].Value;
            var id2 = result.Groups["id2"].Value[0];
            var val2 = result.Groups["val2"].Value;
            return (((((id1, id2)).Item1 == 'c') && (((id1, id2)).Item2 == 'a')) ? (string)($"chara/accessory/a{val2}/material/v{variantId:D4}{relativePath}") : (((((id1, id2)).Item1 == 'c') && (((id1, id2)).Item2 == 'b')) ? (string)($"chara/human/c{val1}/obj/body/b{val2}/material/v{variantId:D4}{relativePath}") : (((((id1, id2)).Item1 == 'c') && (((id1, id2)).Item2 == 'e')) ? (string)($"chara/equipment/e{val2}/material/v{variantId:D4}{relativePath}") : (((((id1, id2)).Item1 == 'c') && (((id1, id2)).Item2 == 'f')) ? (string)($"chara/human/c{val1}/obj/face/f{val2}/material{relativePath}") : (((((id1, id2)).Item1 == 'c') && (((id1, id2)).Item2 == 'h')) ? (string)($"chara/human/c{val1}/obj/hair/h{val2}/material/v{variantId:D4}{relativePath}") : (((((id1, id2)).Item1 == 'c') && (((id1, id2)).Item2 == 't')) ? (string)($"chara/human/c{val1}/obj/tail/t{val2}/material/v{variantId:D4}{relativePath}") : (((((id1, id2)).Item1 == 'c') && (((id1, id2)).Item2 == 'z')) ? (string)($"chara/human/c{val1}/obj/zear/z{val2}/material{relativePath}") : (((((id1, id2)).Item1 == 'd') && (((id1, id2)).Item2 == 'e')) ? (string)($"chara/demihuman/d{val1}/obj/equipment/e{val2}/material/v{variantId:D4}{relativePath}") : (((((id1, id2)).Item1 == 'm') && (((id1, id2)).Item2 == 'b')) ? (string)($"chara/monster/m{val1}/obj/body/b{val2}/material/v{variantId:D4}{relativePath}") : (((((id1, id2)).Item1 == 'w') && (((id1, id2)).Item2 == 'b')) ? (string)($"chara/weapon/w{val1}/obj/body/b{val2}/material/v{variantId:D4}{relativePath}") : ((true && true) ? (string)(null) : throw new System.InvalidOperationException())))))))))));
        }

        private static readonly Regex StrictName = new Regex(@"/mt_(?'id1'[cdmw])(?'val1'\d{4})(?'id2'[abefhtze])(?'val2'\d{4})_(?:\w{3}_\w|\w)\.mtrl$", RegexOptions.CultureInvariant);
        private static readonly Regex LaxName = new Regex(@"/mt_(?'id1'[cdmw])(?'val1'\d{4})(?'id2'[abefhtze])(?'val2'\d{4})_\w+\.mtrl$", RegexOptions.CultureInvariant);
        private static Regex MatNameRegexStrictSuffix()
        {
            return StrictName;
        }

        private static Regex MatNameRegexLaxSuffix()
        {
            return LaxName;
        }

        public static int GetVariantIdFromPath(string matPath)
        {
            var v = 1;
            var vStart = matPath.IndexOf("/v", StringComparison.Ordinal);
            if (vStart == -1)
                return v;
            var vSub = matPath.Substring(vStart + 2, 4);
            try
            {
                v = int.Parse(vSub);
            }
            catch (FormatException)
            {
            }

            return v;
        }

        private void BuildMaterial()
        {
            ReadStrings();
            ReadTextures();
            ShaderPack = StringOffsetToStringMap[File.FileHeader.ShaderPackageNameOffset];
        }

        private void ReadTextures()
        {
            Textures = new Texture[File.TextureOffsets.Length];
            for (int i = 0; i < File.TextureOffsets.Length; i++)
            {
                TextureUsage raw = (TextureUsage)File.Samplers[i].SamplerId;
                var texIndex = File.Samplers[i].TextureIndex;
                var texOffset = File.TextureOffsets[texIndex].Offset;
                var texPath = StringOffsetToStringMap[texOffset];
                Textures[i] = new Texture(this, raw, texPath);
            }
        }

        private void ReadStrings()
        {
            StringOffsetToStringMap = new Dictionary<int, string>();
            var br = new LuminaBinaryReader(File.Strings);
            // They re-use offsets, so the number of offsets is not equal to the number of unique members
            var uniqueTextureCount = File.TextureOffsets.Select(t => t.Offset).Distinct().Count();
            var uniqueUvColorSetCount = File.UvColorSets.Select(t => t.NameOffset).Distinct().Count();
            var uniqueColorOffsetCount = File.ColorSets.Select(t => t.NameOffset).Distinct().Count();
            // Add one for the shader package name at the end
            var stringCount = uniqueTextureCount + uniqueUvColorSetCount + uniqueColorOffsetCount + 1;
            for (int i = 0; i < stringCount; i++)
            {
                long startOffset = br.BaseStream.Position;
                string tmp = br.ReadStringData();
                StringOffsetToStringMap[(int)startOffset] = tmp;
            }

            br.Dispose();
        }
    }
}
