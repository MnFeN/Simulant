using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Data.Parsing;
using Lumina.Data.Parsing.Layer;
using Lumina.Extensions;
using Simulant.Game.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Simulant.Core.Zone
{
    internal static class GreenScreenScene
    {
        private const string TemplateRoot = "bg/ex4/05_zon_z5/fld/z5fd/";
        private const string ModelTemplate = TemplateRoot + "bgparts/z5fd_a3_stg00.mdl";
        private const string MaterialTemplate = TemplateRoot + "material/z5fd_a3_emv01a.mtrl";
        private const string LayoutTemplate = "bg/ex1/03_abr_a2/fld/a2fb/level/a2fb.lvb";
        private const float HalfSize = 500f;
        private const ulong FloorCollisionMaterial = 0x7004; // Walkable triangles in 448's native floor.
        private const string FloorModel = "floor.mdl";
        internal const string FloorMaterial = "floor.mtrl";
        internal const uint FloorInstanceId = 1;
        internal const uint PointLightInstanceId = 2;
        internal const uint WorldLightInstanceId = 3;
        private static readonly Vector3[] Corners =
        {
            new Vector3(-HalfSize, 0, -HalfSize), new Vector3(-HalfSize, 0, HalfSize),
            new Vector3(HalfSize, 0, HalfSize), new Vector3(HalfSize, 0, -HalfSize)
        };
        private static readonly byte[] Indices = { 0, 1, 2, 0, 2, 3 };

        internal static (string Name, byte[] Data)[] Build(GameDataService game, string path)
        {
            var material = ReadTemplate<MtrlFile>(game, MaterialTemplate);
            var model = ReadTemplate<MdlFile>(game, ModelTemplate);
            var scene = ReadTemplate<FileResource>(game, LayoutTemplate);
            return new[]
            {
                (FloorMaterial, BuildMaterial(material)),
                (FloorModel, BuildModel(model, path + FloorMaterial)),
                ("floor.pcb", BuildCollision()),
                ("level/bg.lgb", BuildBackground(path)),
                ("level/stage.lvb", BuildLayout(scene.Data, path + "level/bg.lgb"))
            };
        }

        private static T ReadTemplate<T>(GameDataService game, string path) where T : FileResource
            => game.GetFile<T>(path) ?? throw new FileNotFoundException("找不到生成绿幕场景所需的模板。", path);

        private static byte[] BuildLayout(byte[] source, string background)
        {
            const int scene = 20;
            var bytes = (byte[])source.Clone();
            if (Encoding.ASCII.GetString(bytes, 0, 4) != "LVB1" || Encoding.ASCII.GetString(bytes, 12, 4) != "SCN1"
                || BitConverter.ToInt32(bytes, scene + 4) != 0 || BitConverter.ToInt32(bytes, scene + 0x18) < 1)
            {
                throw new InvalidDataException("绿幕使用的 LVB 模板结构不符。");
            }

            var references = scene + BitConverter.ToInt32(bytes, scene + 0x14);
            var first = references + BitConverter.ToInt32(bytes, references);
            var end = Array.IndexOf(bytes, (byte)0, first);
            if (end < first || !Encoding.ASCII.GetString(bytes, first, end - first).EndsWith("/level/bg.lgb", StringComparison.Ordinal))
            {
                throw new InvalidDataException("LVB 模板的首个场景层路径不符。");
            }

            ReplaceString(bytes, first, end - first, background);
            Buffer.BlockCopy(BitConverter.GetBytes(1), 0, bytes, scene + 0x18, sizeof(int));
            return bytes;
        }

        private static byte[] BuildBackground(string path)
        {
            const int group = 20;
            const int layer = 40;
            const int instanceTable = layer + 52;
            const int count = 3;
            const int entry = instanceTable + count * sizeof(int);
            const int lightSize = 108;
            const int point = entry + 80;
            const int world = point + lightSize;
            const int layerSet = world + lightSize;

            return BuildBytes(writer =>
            {
                writer.Write(Encoding.ASCII.GetBytes("LGB1"));
                writer.Write(0); // File size, filled below.
                writer.Write(1);
                writer.Write(Encoding.ASCII.GetBytes("LGP1"));
                writer.Write(24);
                writer.Write(256);
                writer.Write(0); // Group name offset.
                writer.Write(layer - sizeof(int) - group);
                writer.Write(1);
                writer.Write(sizeof(int));

                writer.Write(1u); // Layer id.
                writer.Write(0); // Layer name offset.
                writer.Write(instanceTable - layer);
                writer.Write(count);
                writer.Write(new byte[] { 1, 0, 0, 1 });
                writer.Write(layerSet - layer);
                writer.Write(new byte[28]);

                writer.Write(entry - instanceTable);
                writer.Write(point - instanceTable);
                writer.Write(world - instanceTable);
                writer.Write((int)LayerEntryType.BG);
                writer.Write(FloorInstanceId);
                writer.Write(0); // Instance name offset.
                WriteVector(writer, Vector3.Zero);
                WriteVector(writer, Vector3.Zero);
                WriteVector(writer, Vector3.One);
                writer.Write(0); // Model path offset.
                writer.Write(0); // Collision path offset.
                writer.Write((int)ModelCollisionType.Replace);
                writer.Write(new byte[12]);
                writer.Write(new byte[] { 1, 0, 0, 0 }); // Visible, both shadow flags off.
                writer.Write(1500f);

                var defaults = GreenScreenAppearance.Default;
                WriteLight(writer, PointLightInstanceId, LightType.Point, new Vector3(0, 12, -8), defaults.PointLight.W, true);
                WriteLight(writer, WorldLightInstanceId, LightType.Directional, Vector3.Zero, defaults.WorldLight.W, false);

                writer.Write(0); // Layer set applies to all.
                writer.Write(12);
                writer.Write(0);
                AppendString(writer, group + 4, group, "background");
                AppendString(writer, layer + 4, layer, "stage");
                AppendString(writer, entry + 8, entry, "floor");
                AppendString(writer, entry + 48, entry, path + FloorModel);
                AppendString(writer, entry + 52, entry, path + "floor.pcb");
                AppendString(writer, point + 8, point, "point_light");
                AppendString(writer, world + 8, world, "world_light");
                AppendString(writer, point + 72, point, string.Empty);
                AppendString(writer, world + 72, world, string.Empty);
                WriteAt(writer, 4, w => w.Write(checked((int)w.BaseStream.Length)));
            });
        }

        private static void WriteLight(BinaryWriter writer, uint id, LightType type, Vector3 position, float intensity, bool specular)
        {
            writer.Write((int)LayerEntryType.LayLight);
            writer.Write(id);
            writer.Write(0); // Instance name offset.
            WriteVector(writer, position);
            WriteVector(writer, Vector3.Zero);
            WriteVector(writer, Vector3.One);
            writer.Write((int)type);
            writer.Write(2f); // Attenuation.
            writer.Write(1f); // Range rate.
            writer.Write((int)PointLightType.Sphere);
            writer.Write(0.5f); // Cone attenuation.
            writer.Write(45f);
            writer.Write(0); // Texture path offset.
            writer.Write(uint.MaxValue); // White RGBA.
            writer.Write(intensity);
            writer.Write(0); // Directional tracking and padding.
            writer.Write((byte)(specular ? 1 : 0));
            writer.Write(new byte[3]); // Shadow flags and padding.
            writer.Write(0.01f); // Shadow clip range.
            writer.Write(new byte[12]); // Plane light angles, merge group and padding.
        }

        private static byte[] BuildModel(MdlFile model, string material)
        {
            const int fileHeaderSize = 68;
            const int vertexDeclarationSize = 136;
            const int modelHeaderSize = 56;
            const int lodSize = 60;
            var file = model.FileHeader;
            var header = model.ModelHeader;
            if (file.VertexDeclarationCount != 1 || file.StackSize != vertexDeclarationSize || file.LodCount != 1
                || header.MeshCount != 1 || header.MaterialCount != 1 || header.SubmeshCount != 0 || header.BoneCount != 0
                || header.ElementIdCount != 0 || header.ExtraLodEnabled || header.ShapeCount != 0)
            {
                throw new InvalidDataException("绿幕使用的 MDL 模板结构不符。");
            }

            var mesh = model.Meshes[0];
            if (mesh.VertexStreamCount != 2 || mesh.VertexBufferStride[0] != 8 || mesh.VertexBufferStride[1] != 20
                || mesh.StartIndex != 0 || mesh.MaterialIndex != 0)
            {
                throw new InvalidDataException("绿幕 MDL 模板的缓冲布局不符。");
            }

            var bytes = (byte[])model.Data.Clone();
            var strings = fileHeaderSize + vertexDeclarationSize + 8;
            var materialOffset = checked((int)model.MaterialNameOffsets.Single());
            var materialEnd = Array.IndexOf(model.Strings, (byte)0, materialOffset);
            if (materialEnd < materialOffset)
            {
                throw new InvalidDataException("绿幕 MDL 模板的材质路径不完整。");
            }

            ReplaceString(bytes, strings + materialOffset, materialEnd - materialOffset, material);
            var modelHeader = strings + model.Strings.Length;
            var lod = modelHeader + modelHeaderSize;
            var meshHeader = lod + lodSize * model.Lods.Length;
            var vertices = checked((int)file.VertexOffset[0]);
            var indices = checked((int)file.IndexOffset[0]);
            Array.Clear(bytes, vertices, bytes.Length - vertices);
            bytes[modelHeader + 23] = (byte)((bytes[modelHeader + 23] | 3) & ~8); // Shadow, light shadow and reflection.
            bytes[65] = 0; // The generated floor needs no index streaming.

            using (var stream = new MemoryStream(bytes))
            using (var writer = new BinaryWriter(stream))
            {
                WriteAt(writer, modelHeader, w => w.Write(Corners[0].Length()));
                WriteAt(writer, lod + 36, w => w.Write(Indices.Length / 3));
                WriteAt(writer, meshHeader, w => w.Write((ushort)Corners.Length));
                WriteAt(writer, meshHeader + 4, w => w.Write(Indices.Length));
                WriteAt(writer, vertices - 128, w =>
                {
                    for (var box = 0; box < 2; box++)
                    {
                        WriteVector(w, Corners[0]);
                        w.Write(1f);
                        WriteVector(w, Corners[2]);
                        w.Write(1f);
                    }
                });

                WriteAt(writer, vertices + mesh.VertexBufferOffset[0], w =>
                {
                    foreach (var corner in Corners)
                    {
                        w.Write(corner.X.Pack());
                        w.Write(corner.Y.Pack());
                        w.Write(corner.Z.Pack());
                        w.Write(1f.Pack());
                    }
                });
                WriteAt(writer, vertices + mesh.VertexBufferOffset[1], w =>
                {
                    foreach (var corner in Corners)
                    {
                        w.Write(0f.Pack());
                        w.Write(1f.Pack());
                        w.Write(0f.Pack());
                        w.Write(0f.Pack());
                        w.Write(uint.MaxValue); // Vertex color.
                        w.Write((corner.X > 0 ? 1f : 0f).Pack());
                        w.Write((corner.Z > 0 ? 1f : 0f).Pack());
                        w.Write(0u);
                    }
                });
                WriteAt(writer, indices, w =>
                {
                    foreach (var index in Indices)
                    {
                        w.Write((ushort)index);
                    }
                });
            }

            return bytes;
        }

        private static byte[] BuildCollision()
        {
            return BuildBytes(writer =>
            {
                var count = Indices.Length / 3;
                writer.Write(0u);
                writer.Write(1u); // PCB version.
                writer.Write(0u); // Child node count, excluding the root.
                writer.Write(count);
                writer.Write(0ul); // Root node header.
                writer.Write(0u); // First child offset, zero for a leaf.
                writer.Write(0u); // Second child offset.
                var boundsPadding = new Vector3(0, 0.001f, 0);
                WriteVector(writer, Corners[0] - boundsPadding);
                WriteVector(writer, Corners[2] + boundsPadding);
                writer.Write((ushort)0);
                writer.Write((ushort)count);
                writer.Write((ushort)Corners.Length);
                writer.Write((ushort)0);
                foreach (var corner in Corners)
                {
                    WriteVector(writer, corner);
                }

                for (var triangle = 0; triangle < Indices.Length; triangle += 3)
                {
                    writer.Write(Indices, triangle, 3);
                    writer.Write((byte)0);
                    writer.Write(FloorCollisionMaterial);
                }
            });
        }

        private static byte[] BuildMaterial(MtrlFile material)
        {
            const string white = "bgcommon/texture/dummy_d.tex";
            const uint textureLayersKey = 0xB616DC5A;
            const uint singleTextureLayer = 0x669A451B;
            var shader = Encoding.ASCII.GetBytes("bg.shpk\0");
            var header = material.FileHeader;
            var shaderOffset = header.ShaderPackageNameOffset;
            if (!material.Strings.Skip(shaderOffset).Take(shader.Length).SequenceEqual(shader))
            {
                throw new InvalidDataException("绿幕使用的材质着色器不符。");
            }

            if (header.FileSize != material.Data.Length)
            {
                throw new InvalidDataException("绿幕材质的文件大小不符。");
            }

            var textureLayers = Array.FindIndex(material.ShaderKeys, key => key.Category == textureLayersKey);
            if (textureLayers < 0)
            {
                throw new InvalidDataException("绿幕材质缺少贴图层着色器键。");
            }

            // 保留着色器需要的采样器，颜色与高光强度由常量控制，采样用纯白占位图。
            var bytes = (byte[])material.Data.Clone();
            var strings = 16 + sizeof(uint) * (header.TextureCount + header.UvSetCount + header.ColorSetCount);
            var shaderKeys = strings + header.StringTableSize + header.AdditionalDataSize + header.DataSetSize + 12;
            var values = bytes.Length - material.MaterialHeader.ShaderValueListSize;
            var whiteInputs = material.Samplers
                .Where(sampler => sampler.SamplerId == (uint)TextureUsage.SamplerColorMap0
                    || sampler.SamplerId == (uint)TextureUsage.SamplerColorMap1
                    || sampler.SamplerId == (uint)TextureUsage.SamplerSpecularMap0
                    || sampler.SamplerId == (uint)TextureUsage.SamplerSpecularMap1)
                .Select(sampler => sampler.TextureIndex).Distinct();
            foreach (var index in whiteInputs)
            {
                var offset = material.TextureOffsets[index].Offset;
                var end = Array.IndexOf(material.Strings, (byte)0, offset);
                if (end < offset)
                {
                    throw new InvalidDataException("绿幕材质的贴图路径不完整。");
                }

                ReplaceString(bytes, strings + offset, end - offset, white);
            }
            using (var stream = new MemoryStream(bytes))
            using (var writer = new BinaryWriter(stream))
            {
                void SetConstant(uint id, params float[] components)
                {
                    var constant = material.Constants.Single(entry => entry.ConstantId == id);
                    if (constant.ValueSize != sizeof(float) * components.Length
                        || constant.ValueOffset + constant.ValueSize > material.MaterialHeader.ShaderValueListSize)
                    {
                        throw new InvalidDataException($"绿幕材质的常量 {id:X8} 大小异常。");
                    }

                    WriteAt(writer, values + constant.ValueOffset, w =>
                    {
                        foreach (var component in components)
                        {
                            w.Write(component);
                        }
                    });
                }

                WriteAt(writer, shaderKeys + textureLayers * 8 + 4, w => w.Write(singleTextureLayer));
                GreenScreenAppearance.Default.WriteMaterialConstants(SetConstant);
                SetConstant(0xBCEA8C11, 0f); // g_EnableShadow
                SetConstant(0x5095E770, 0f); // g_EnableLightShadow
            }

            return bytes;
        }

        private static void ReplaceString(byte[] bytes, int start, int length, string value)
        {
            var replacement = Encoding.ASCII.GetBytes(value);
            if (replacement.Length > length)
            {
                throw new InvalidDataException("绿幕资源路径超出模板字符串的容量。");
            }

            Array.Clear(bytes, start, length);
            Buffer.BlockCopy(replacement, 0, bytes, start, replacement.Length);
        }

        private static void AppendString(BinaryWriter writer, int field, int basis, string value)
        {
            var offset = checked((int)writer.BaseStream.Position - basis);
            WriteAt(writer, field, w => w.Write(offset));
            writer.Write(Encoding.ASCII.GetBytes(value));
            writer.Write((byte)0);
        }

        private static byte[] BuildBytes(Action<BinaryWriter> build)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                build(writer);
                return stream.ToArray();
            }
        }

        private static void WriteAt(BinaryWriter writer, long offset, Action<BinaryWriter> write)
        {
            var original = writer.BaseStream.Position;
            writer.BaseStream.Position = offset;
            write(writer);
            writer.BaseStream.Position = original;
        }

        private static void WriteVector(BinaryWriter writer, Vector3 vector)
        {
            writer.Write(vector.X);
            writer.Write(vector.Y);
            writer.Write(vector.Z);
        }
    }
}
