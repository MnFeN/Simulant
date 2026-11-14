using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Lumina.Data.Parsing;

// ReSharper disable RedundantAssignment
namespace Lumina.Extensions
{
    public static class BinaryReaderExtensions
    {
        public static T ReadStructure<T>(this BinaryReader br)
            where T : struct
        {
            ReadOnlySpan<byte> data = br.ReadBytes(Unsafe.SizeOf<T>());
            return MemoryMarshal.Read<T>(data);
        }

        public static List<T> ReadStructures<T>(this BinaryReader br, int count)
            where T : struct
        {
            var structs = br.ReadStructuresAsSpan<T>(count);
            var list = new List<T>(structs.Length);
            for (var i = 0; i < structs.Length; i++)
            {
                list.Add(structs[i]);
            }

            return list;
        }

        public static T[] ReadStructuresAsArray<T>(this BinaryReader br, int count)
            where T : struct
        {
            return br.ReadStructuresAsSpan<T>(count).ToArray();
        }

        public static Span<T> ReadStructuresAsSpan<T>(this BinaryReader br, int count)
            where T : struct
        {
            var data = br.ReadBytes(Unsafe.SizeOf<T>() * count);
            return MemoryMarshal.Cast<byte, T>(data.AsSpan());
        }

        public static string ReadStringOffsetData(this BinaryReader br, long offset)
        {
            return Encoding.UTF8.GetString(ReadRawOffsetData(br, offset));
        }

        public static byte[] ReadRawOffsetData(this BinaryReader br, long offset)
        {
            var originalPosition = br.BaseStream.Position;
            br.BaseStream.Position = offset;
            var chars = new List<byte>();
            byte current;
            while ((current = br.ReadByte()) != 0)
            {
                chars.Add(current);
            }

            br.BaseStream.Position = originalPosition;
            return chars.ToArray();
        }

        public static string ReadStringData(this BinaryReader br)
        {
            var chars = new List<byte>();
            byte current;
            while ((current = br.ReadByte()) != 0)
            {
                chars.Add(current);
            }

            return Encoding.UTF8.GetString(chars.ToArray(), 0, chars.Count);
        }

        public static void Seek(this BinaryReader br, long offset)
        {
            br.BaseStream.Position = offset;
        }

        public static StringOffset ReadStringOffset(this BinaryReader br, long offset)
        {
            return new StringOffset(br, offset);
        }

        public static BinaryReader ReadStringOffset(this BinaryReader br, long offset, ref string value)
        {
            value = new StringOffset(br, offset);
            return br;
        }

        public static BinaryReader ReadStringOffset(this BinaryReader br, long offset, ref StringOffset value)
        {
            value = new StringOffset(br, offset);
            return br;
        }

        public static BinaryReader Read(this BinaryReader br, ref ulong field)
        {
            field = br.ReadUInt64();
            return br;
        }

        public static BinaryReader Read(this BinaryReader br, ref long field)
        {
            field = br.ReadInt64();
            return br;
        }

        public static BinaryReader Read(this BinaryReader br, ref uint field)
        {
            field = br.ReadUInt32();
            return br;
        }

        public static BinaryReader Read(this BinaryReader br, ref int field)
        {
            field = br.ReadInt32();
            return br;
        }

        public static BinaryReader Read(this BinaryReader br, ref ushort field)
        {
            field = br.ReadUInt16();
            return br;
        }

        public static BinaryReader Read(this BinaryReader br, ref short field)
        {
            field = br.ReadInt16();
            return br;
        }

        public static BinaryReader Read(this BinaryReader br, ref byte field)
        {
            field = br.ReadByte();
            return br;
        }

        public static BinaryReader Read(this BinaryReader br, ref float field)
        {
            field = br.ReadSingle();
            return br;
        }

        public static BinaryReader Read(this BinaryReader br, ref double field)
        {
            field = br.ReadDouble();
            return br;
        }

        public static byte PeekByte(this BinaryReader br)
        {
            var data = br.ReadByte();
            br.BaseStream.Position--;
            return data;
        }

        public static byte[] PeekBytes(this BinaryReader br, int count)
        {
            var data = br.ReadBytes(count);
            br.BaseStream.Position -= count;
            return data;
        }
    }
}
