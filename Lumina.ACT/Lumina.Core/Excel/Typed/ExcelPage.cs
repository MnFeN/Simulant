using System;
using System.Collections.Generic;
using System.IO;
using Lumina.Data;
using Lumina.Text.ReadOnly;

namespace Lumina.Excel
{
    /// <summary>Safe C# 7.3 page reader used by the complete generated schemas.</summary>
    public sealed class ExcelPage
    {
        private readonly byte[] data;
        private readonly Dictionary<uint, StringBlock> strings = new Dictionary<uint, StringBlock>();
        public RawExcelSheet Sheet { get; private set; }
        public ExcelModule Module { get { return Sheet.Module; } }
        public Language Language { get { return Sheet.Language; } }
        internal ExcelPage(RawExcelSheet sheet, byte[] bytes) { Sheet = sheet; data = bytes; }
        internal void RegisterRow(uint rowOffset, int start, int end, Func<string, string> resolver)
        { strings.Add(rowOffset, new StringBlock { Start = start, End = end, Resolver = resolver }); }
        private sealed class StringBlock { internal int Start, End; internal Func<string, string> Resolver; }
        private int Check(uint offset, int size)
        {
            int position = checked((int)offset);
            RawExcelSheet.CheckBounds(data, position, size);
            return position;
        }
        public ReadOnlySeString ReadString(uint offset, uint structOffset)
        {
            StringBlock block;
            if (!strings.TryGetValue(structOffset, out block)) throw new InvalidDataException("Unknown EXD row string block.");
            int start = checked(block.Start + (int)ReadUInt32(offset));
            if (start < block.Start || start >= block.End) throw new InvalidDataException("String exceeds the row's shared string block.");
            int end = Array.IndexOf(data, (byte)0, start, block.End - start);
            if (end < 0) throw new InvalidDataException("Unterminated EXD string.");
            return new ReadOnlySeString(new ReadOnlyMemory<byte>(data, start, end - start), block.Resolver);
        }
        public bool ReadBool(uint offset) { return ReadUInt8(offset) != 0; }
        public byte ReadUInt8(uint offset) { return data[Check(offset, 1)]; }
        public sbyte ReadInt8(uint offset) { return unchecked((sbyte)ReadUInt8(offset)); }
        public ushort ReadUInt16(uint offset) { return RawExcelSheet.ReadUInt16(data, Check(offset, 2)); }
        public short ReadInt16(uint offset) { return unchecked((short)ReadUInt16(offset)); }
        public uint ReadUInt32(uint offset) { return RawExcelSheet.ReadUInt32(data, Check(offset, 4)); }
        public int ReadInt32(uint offset) { return unchecked((int)ReadUInt32(offset)); }
        public ulong ReadUInt64(uint offset) { Check(offset, 8); return ((ulong)ReadUInt32(offset) << 32) | ReadUInt32(offset + 4); }
        public long ReadInt64(uint offset) { return unchecked((long)ReadUInt64(offset)); }
        public float ReadFloat32(uint offset) { return BitConverter.ToSingle(BitConverter.GetBytes(ReadUInt32(offset)), 0); }
        public bool ReadPackedBool(uint offset, byte bit)
        {
            if (bit > 7) throw new ArgumentOutOfRangeException(nameof(bit));
            return (ReadUInt8(offset) & (1 << bit)) != 0;
        }
    }
}
