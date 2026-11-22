using System;
using System.Collections.Generic;
using System.Numerics;

namespace Simulant.Game.Data
{
    // 对应 FFCS 的 MeshPCB.FileHeader、FileNode 和 Mesh.Primitive 文件布局。
    // https://github.com/aers/FFXIVClientStructs/blob/7f509ce13d0e29a64cd24ec35ccc1452c1b1b7d2/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/Mesh.cs
    internal static class PcbCollisionMesh
    {
        private const int HeaderSize = 0x10;
        private const int NodeSize = 0x30;
        private const int TriangleSize = 0x0C;

        internal static IEnumerable<(Vector3 A, Vector3 B, Vector3 C, ulong Material)> Read(byte[] data, Matrix4x4 transform)
        {
            if (data == null || data.Length < HeaderSize + NodeSize)
                yield break;

            var version = BitConverter.ToInt32(data, 4);
            if (version != 1 && version != 4)
                yield break;

            var vertices = new Vector3[256];
            var seen = new HashSet<int>();
            var pending = new Stack<int>();
            pending.Push(HeaderSize);
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (!seen.Add(node))
                    continue;

                var packedCount = BitConverter.ToUInt16(data, node + 40);
                var triangleCount = BitConverter.ToUInt16(data, node + 42);
                var rawCount = BitConverter.ToUInt16(data, node + 44);
                var vertexCount = rawCount + packedCount;
                var offset = node + NodeSize;
                var vertexBytes = rawCount * 12 + packedCount * 6;
                if (vertexCount > vertices.Length || vertexBytes + triangleCount * TriangleSize > data.Length - offset)
                    continue;
                var triangles = offset + vertexBytes;

                var min = ReadVector(data, node + 16);
                var scale = (ReadVector(data, node + 28) - min) / ushort.MaxValue;
                for (var i = 0; i < rawCount; i++, offset += 12)
                    vertices[i] = Vector3.Transform(ReadVector(data, offset), transform);
                for (var i = rawCount; i < vertexCount; i++, offset += 6)
                {
                    var packed = new Vector3(BitConverter.ToUInt16(data, offset), BitConverter.ToUInt16(data, offset + 2),
                        BitConverter.ToUInt16(data, offset + 4));
                    vertices[i] = Vector3.Transform(min + packed * scale, transform);
                }

                for (var i = 0; i < triangleCount; i++, triangles += TriangleSize)
                {
                    var a = data[triangles];
                    var b = data[triangles + 1];
                    var c = data[triangles + 2];
                    if (a < vertexCount && b < vertexCount && c < vertexCount)
                        yield return (vertices[a], vertices[b], vertices[c], BitConverter.ToUInt64(data, triangles + 4));
                }

                for (var field = 8; field <= 12; field += 4)
                {
                    var child = BitConverter.ToInt32(data, node + field);
                    if (child > 0 && child <= data.Length - NodeSize - node)
                        pending.Push(node + child);
                }
            }
        }

        private static Vector3 ReadVector(byte[] data, int offset)
            => new Vector3(BitConverter.ToSingle(data, offset), BitConverter.ToSingle(data, offset + 4),
                BitConverter.ToSingle(data, offset + 8));
    }
}
