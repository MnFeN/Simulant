using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Lumina.Data;
using Lumina.Data.Parsing;
using Lumina.Extensions;
using Lumina.Models.Materials;

namespace Lumina.Models.Models
{
    public class Mesh
    {
        public enum MeshType
        {
            Main,
            Water,
            Shadow,
            TerrainShadow,
            VerticalFog,
            LightShaft,
            Glass,
            MaterialChange,
            CrestChange
        }

        public Model Parent { get; private set; }
        public int MeshIndex { get; private set; }
        public MeshType[] Types { get; private set; }
        public string[] Attributes { get; private set; }
        // TODO: build submeshes from the index data
        // the information is available to build these, but it'd be nice
        // to have access to Mesh objects with pre-read submesh meshes
        // public Mesh SubmeshesAsMesh { get; private set; }
        public Submesh[] Submeshes { get; private set; }
        public ushort[] BoneTable { get; private set; }

        public Vertex[] Vertices;
        public ushort[] Indices;
        public Material Material { get; private set; }

        public Mesh(Model model, int index, MeshType[] types)
        {
            Parent = model;
            MeshIndex = index;
            Types = types;
            BuildMesh();
        }

        private void BuildMesh()
        {
            ReadBoneTable();
            ReadIndices();
            ReadVertices();
            ReadSubmeshes();
            var matIndex = Parent.File.Meshes[MeshIndex].MaterialIndex;
            Material = Parent.Materials[matIndex];
        }

        private void ReadSubmeshes()
        {
            var currentMesh = Parent.File.Meshes[MeshIndex];
            Submeshes = new Submesh[currentMesh.SubMeshCount];
            for (int i = 0; i < currentMesh.SubMeshCount; i++)
                Submeshes[i] = new Submesh(Parent, MeshIndex, i);
        }

        //TODO is this necessary?
        private void ReadBoneTable()
        {
            var currentMesh = Parent.File.Meshes[MeshIndex];
            // Copy over the bone table
            int boneTableIndex = currentMesh.BoneTableIndex;
            if (boneTableIndex != 255)
                BoneTable = Parent.File.BoneTables[boneTableIndex].BoneIndex;
        }

        private void ReadIndices()
        {
            var currentMesh = Parent.File.Meshes[MeshIndex];
            var reader = new LuminaBinaryReader(new MemoryStream(Parent.File.Data, (int)Parent.File.FileHeader.IndexOffset[(int)Parent.Lod], (int)Parent.File.FileHeader.IndexBufferSize[(int)Parent.Lod]));
            reader.Seek(currentMesh.StartIndex * 2);
            Indices = reader.ReadUInt16Array((int)currentMesh.IndexCount);
        }

        private void ReadVertices()
        {
            MdlStructs.MeshStruct currentMesh = Parent.File.Meshes[MeshIndex];
            MdlStructs.VertexDeclarationStruct currentDecl = Parent.File.VertexDeclarations[MeshIndex];
            // We have to go through these in order by offset with this implementation
            var orderedElements = currentDecl.VertexElements.OrderBy(e => e.Offset).ToList();
            Vertices = new Vertex[currentMesh.VertexCount];
            // Vertices may be defined across at most 3 streams defined by a Mesh's VertexDeclarations
            var vertexStreamReader = new LuminaBinaryReader[3];
            for (int i = 0; i < 3; i++)
            {
                vertexStreamReader[i] = new LuminaBinaryReader(new MemoryStream(Parent.File.Data, (int)Parent.File.FileHeader.VertexOffset[(int)Parent.Lod], (int)Parent.File.FileHeader.VertexBufferSize[(int)Parent.Lod]));
                vertexStreamReader[i].Seek(currentMesh.VertexBufferOffset[i]);
            }

            for (int i = 0; i < Vertices.Length; i++)
            {
                Vertices[i] = new Vertex();
                foreach (var element in orderedElements)
                    SetElementField(ref Vertices[i], element, vertexStreamReader[element.Stream]);
            }
        }

        private void SetElementField(ref Vertex v, MdlStructs.VertexElement element, LuminaBinaryReader br)
        {
            var type = (Vertex.VertexType)element.Type;
            var usage = (Vertex.VertexUsage)element.Usage;
            object data = ((type == Vertex.VertexType.Single3) ? (object)(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle())) : ((type == Vertex.VertexType.Single4) ? (object)(new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle())) : ((type == Vertex.VertexType.UInt) ? (object)(br.ReadBytes(4)) : ((type == Vertex.VertexType.ByteFloat4) ? (object)(new Vector4(br.ReadByte() / 255f, br.ReadByte() / 255f, br.ReadByte() / 255f, br.ReadByte() / 255f)) : ((type == Vertex.VertexType.Half2) ? (object)(new Vector2((float)br.ReadHalf(), (float)br.ReadHalf())) : ((type == Vertex.VertexType.Half4) ? (object)(new Vector4((float)br.ReadHalf(), (float)br.ReadHalf(), (float)br.ReadHalf(), (float)br.ReadHalf())) : throw new ArgumentOutOfRangeException()))))));
            switch (usage)
            {
                case Vertex.VertexUsage.Position:
                    v.Position = GetVector4(data);
                    break;
                case Vertex.VertexUsage.BlendWeights:
                    v.BlendWeights = GetVector4(data);
                    break;
                case Vertex.VertexUsage.BlendIndices:
                    v.BlendIndices = (byte[])data;
                    break;
                case Vertex.VertexUsage.Normal:
                    v.Normal = GetVector3(data);
                    break;
                case Vertex.VertexUsage.UV:
                    v.UV = GetVector4(data);
                    break;
                case Vertex.VertexUsage.Tangent2:
                    v.Tangent2 = GetVector4(data);
                    break;
                case Vertex.VertexUsage.Tangent1:
                    v.Tangent1 = GetVector4(data);
                    break;
                case Vertex.VertexUsage.Color:
                    v.Color = GetVector4(data);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static Vector3 GetVector3(object value)
        {
            switch (value)
            {
                case Vector2 v2:
                    return new Vector3(v2.X, v2.Y, 0);
                case Vector3 v3:
                    return v3;
                case Vector4 v4:
                    return new Vector3(v4.X, v4.Y, v4.Z);
            }

            throw new ArgumentOutOfRangeException();
        }

        private static Vector4 GetVector4(object value)
        {
            switch (value)
            {
                case Vector2 v2:
                    return new Vector4(v2.X, v2.Y, 0, 0);
                case Vector3 v3:
                    return new Vector4(v3.X, v3.Y, v3.Z, 1);
                case Vector4 v4:
                    return v4;
            }

            throw new ArgumentOutOfRangeException();
        }

        public string GetObjectFileText()
        {
            string nl = Environment.NewLine;
            var vsList = new List<Vector3>();
            var vcList = new List<Vector4>();
            var vtList = new List<Vector4>();
            var nmList = new List<Vector3>();
            var inList = new List<Vector3>();
            foreach (var vert in Vertices)
            {
                Vector3 vs = new Vector3();
                Vector4 vc = new Vector4();
                Vector4 vt = new Vector4();
                Vector3 nm = new Vector3();
                vs.X = vert.Position.Value.X;
                vs.Y = vert.Position.Value.Y;
                vs.Z = vert.Position.Value.Z;
                if (vert.Color == null)
                {
                    vc.X = 0;
                    vc.Y = 0;
                    vc.W = 0;
                    vc.Z = 0;
                }
                else
                {
                    vc.X = vert.Color.Value.X;
                    vc.Y = vert.Color.Value.Y;
                    vc.W = vert.Color.Value.W;
                    vc.Z = vert.Color.Value.Z;
                }

                if (vert.UV.HasValue)
                {
                    vt.X = vert.UV.Value.X;
                    vt.Y = -1 * vert.UV.Value.Y;
                    vt.W = (vert.UV.Value.W == 0) ? vt.X : vert.UV.Value.W;
                    vt.Z = (vert.UV.Value.Z == 0) ? vt.Y : vert.UV.Value.Z;
                }

                if (vert.Normal.HasValue)
                {
                    nm.X = vert.Normal.Value.X;
                    nm.Y = vert.Normal.Value.Y;
                    nm.Z = vert.Normal.Value.Z;
                }

                vsList.Add(vs);
                vcList.Add(vc);
                vtList.Add(vt);
                nmList.Add(nm);
            }

            for (int j = 0; j < Indices.Length; j += 3)
            {
                Vector3 ind = new Vector3
                {
                    X = Indices[j + 0] + 1,
                    Y = Indices[j + 1] + 1,
                    Z = Indices[j + 2] + 1
                };
                inList.Add(ind);
            }

            var sb = new StringBuilder();
            foreach (var vert in vsList)
                sb.Append($"v {(decimal)vert.X} {(decimal)vert.Y} {(decimal)vert.Z}{nl}");
            foreach (var color in vcList)
                sb.Append($"vc {(decimal)color.W} {(decimal)color.X} {(decimal)color.Y} {(decimal)color.Z}{nl}");
            foreach (var texCoord in vtList)
                sb.Append($"vt {(decimal)texCoord.X} {(decimal)texCoord.Y} {(decimal)texCoord.W} {(decimal)texCoord.Z}{nl}");
            foreach (var norm in nmList)
                sb.Append($"vn {(decimal)norm.X} {(decimal)norm.Y} {(decimal)norm.Z}{nl}");
            foreach (var ind in inList)
            {
                sb.Append(String.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}{3}", (ushort)ind.X, (ushort)ind.Y, (ushort)ind.Z, nl));
            }

            return sb.ToString();
        }
    }
}
