using Lumina.Data.Structs.Excel;
using Lumina.Text.ReadOnly;
using System;
using System.Collections.Generic;

namespace Lumina.Excel
{
    /// <summary>
    /// An <see cref = "IExcelSubrow{T}"/> type for explicitly reading data from an <see cref = "Excel.ExcelPage"/>.
    /// </summary>
    /// <inheritdoc cref = "RawRow"/>
    [Sheet]
    public readonly struct RawSubrow : IExcelSubrow<RawSubrow>
    {
        private readonly ExcelPage page;
        private readonly uint offset;
        private readonly uint row;
        private readonly ushort subrow;
        public RawSubrow(ExcelPage page, uint offset, uint row, ushort subrow)
        {
            this.page = page;
            this.offset = offset;
            this.row = row;
            this.subrow = subrow;
        }

        /// <inheritdoc/>
        public ExcelPage ExcelPage => page;
        /// <inheritdoc/>
        public uint RowOffset => offset;
        /// <inheritdoc/>
        public uint RowId => row;
        /// <inheritdoc/>
        public ushort SubrowId => subrow;
        /// <inheritdoc cref = "RawRow.Columns"/>
        public IReadOnlyList<ExcelColumnDefinition> Columns => page.Sheet.Columns;

        /// <inheritdoc cref = "RawRow.GetColumnOffset(int)"/>
        public ushort GetColumnOffset(int columnIdx) => page.Sheet.GetColumnOffset(columnIdx);
        /// <summary>
        /// Reads the value of the specified column.
        /// </summary>
        /// <param name = "columnIdx"></param>
        /// <returns>Returns the value as a boxed <see langword="object"/>.</returns>
        /// <exception cref = "InvalidOperationException">Thrown when the column is of an unknown type.</exception>
        public object ReadColumn(int columnIdx)
        {
            var column = page.Sheet.Columns[columnIdx];
            return (((column.Type) == ExcelColumnDataType.String) ? (object)(ReadString(column.Offset)) : (((column.Type) == ExcelColumnDataType.Bool) ? (object)(ReadBool(column.Offset)) : (((column.Type) == ExcelColumnDataType.Int8) ? (object)(ReadInt8(column.Offset)) : (((column.Type) == ExcelColumnDataType.UInt8) ? (object)(ReadUInt8(column.Offset)) : (((column.Type) == ExcelColumnDataType.Int16) ? (object)(ReadInt16(column.Offset)) : (((column.Type) == ExcelColumnDataType.UInt16) ? (object)(ReadUInt16(column.Offset)) : (((column.Type) == ExcelColumnDataType.Int32) ? (object)(ReadInt32(column.Offset)) : (((column.Type) == ExcelColumnDataType.UInt32) ? (object)(ReadUInt32(column.Offset)) : (((column.Type) == ExcelColumnDataType.Float32) ? (object)(ReadFloat32(column.Offset)) : (((column.Type) == ExcelColumnDataType.Int64) ? (object)(ReadInt64(column.Offset)) : (((column.Type) == ExcelColumnDataType.UInt64) ? (object)(ReadUInt64(column.Offset)) : ((((column.Type) >= ExcelColumnDataType.PackedBool0) && ((column.Type) <= ExcelColumnDataType.PackedBool7)) ? (object)(ReadPackedBool(column.Offset, (byte)(column.Type - ExcelColumnDataType.PackedBool0))) : throw new InvalidOperationException($"Unknown column type {column.Type}")))))))))))));
        }

        /// <inheritdoc cref = "RawRow.ReadString(nuint)"/>
        public ReadOnlySeString ReadString(uint offset) => page.ReadString(RowOffset + offset, RowOffset);
        /// <inheritdoc cref = "RawRow.ReadBool(nuint)"/>
        public bool ReadBool(uint offset) => page.ReadBool(RowOffset + offset);
        /// <inheritdoc cref = "RawRow.ReadInt8(nuint)"/>
        public sbyte ReadInt8(uint offset) => page.ReadInt8(RowOffset + offset);
        /// <inheritdoc cref = "RawRow.ReadUInt8(nuint)"/>
        public byte ReadUInt8(uint offset) => page.ReadUInt8(RowOffset + offset);
        /// <inheritdoc cref = "RawRow.ReadInt16(nuint)"/>
        public short ReadInt16(uint offset) => page.ReadInt16(RowOffset + offset);
        /// <inheritdoc cref = "RawRow.ReadUInt16(nuint)"/>
        public ushort ReadUInt16(uint offset) => page.ReadUInt16(RowOffset + offset);
        /// <inheritdoc cref = "RawRow.ReadInt32(nuint)"/>
        public int ReadInt32(uint offset) => page.ReadInt32(RowOffset + offset);
        /// <inheritdoc cref = "RawRow.ReadUInt32(nuint)"/>
        public uint ReadUInt32(uint offset) => page.ReadUInt32(RowOffset + offset);
        /// <inheritdoc cref = "RawRow.ReadFloat32(nuint)"/>
        public float ReadFloat32(uint offset) => page.ReadFloat32(RowOffset + offset);
        /// <inheritdoc cref = "RawRow.ReadInt64(nuint)"/>
        public long ReadInt64(uint offset) => page.ReadInt64(RowOffset + offset);
        /// <inheritdoc cref = "RawRow.ReadUInt64(nuint)"/>
        public ulong ReadUInt64(uint offset) => page.ReadUInt64(RowOffset + offset);
        /// <inheritdoc cref = "RawRow.ReadPackedBool(nuint, byte)"/>
        public bool ReadPackedBool(uint offset, byte bit) => page.ReadPackedBool(RowOffset + offset, bit);
        /// <inheritdoc cref = "RawRow.ReadStringColumn(int)"/>
        public ReadOnlySeString ReadStringColumn(int columnIdx) => ReadString(GetColumnOffset(columnIdx));
        /// <inheritdoc cref = "RawRow.ReadBoolColumn(int)"/>
        public bool ReadBoolColumn(int columnIdx) => ReadBool(GetColumnOffset(columnIdx));
        /// <inheritdoc cref = "RawRow.ReadInt8Column(int)"/>
        public sbyte ReadInt8Column(int columnIdx) => ReadInt8(GetColumnOffset(columnIdx));
        /// <inheritdoc cref = "RawRow.ReadUInt8Column(int)"/>
        public byte ReadUInt8Column(int columnIdx) => ReadUInt8(GetColumnOffset(columnIdx));
        /// <inheritdoc cref = "RawRow.ReadInt16Column(int)"/>
        public short ReadInt16Column(int columnIdx) => ReadInt16(GetColumnOffset(columnIdx));
        /// <inheritdoc cref = "RawRow.ReadUInt16Column(int)"/>
        public ushort ReadUInt16Column(int columnIdx) => ReadUInt16(GetColumnOffset(columnIdx));
        /// <inheritdoc cref = "RawRow.ReadInt32Column(int)"/>
        public int ReadInt32Column(int columnIdx) => ReadInt32(GetColumnOffset(columnIdx));
        /// <inheritdoc cref = "RawRow.ReadUInt32Column(int)"/>
        public uint ReadUInt32Column(int columnIdx) => ReadUInt32(GetColumnOffset(columnIdx));
        /// <inheritdoc cref = "RawRow.ReadFloat32Column(int)"/>
        public float ReadFloat32Column(int columnIdx) => ReadFloat32(GetColumnOffset(columnIdx));
        /// <inheritdoc cref = "RawRow.ReadInt64Column(int)"/>
        public long ReadInt64Column(int columnIdx) => ReadInt64(GetColumnOffset(columnIdx));
        /// <inheritdoc cref = "RawRow.ReadUInt64Column(int)"/>
        public ulong ReadUInt64Column(int columnIdx) => ReadUInt64(GetColumnOffset(columnIdx));
        /// <inheritdoc cref = "RawRow.ReadPackedBoolColumn(int, byte)"/>
        public bool ReadPackedBoolColumn(int columnIdx, byte bit) => ReadPackedBool(GetColumnOffset(columnIdx), bit);
        /// <inheritdoc cref = "RawRow.ReadPackedBoolColumn(int)"/>
        public bool ReadPackedBoolColumn(int columnIdx) => ((true && ((Columns[columnIdx].Type) < ExcelColumnDataType.PackedBool0 || (Columns[columnIdx].Type) > ExcelColumnDataType.PackedBool7)) ? throw new InvalidOperationException($"Unknown column type {(Columns[columnIdx].Type)}") : (ReadPackedBoolColumn(columnIdx, (byte)((Columns[columnIdx].Type) - ExcelColumnDataType.PackedBool0))));
        static public RawSubrow Create(ExcelPage page, uint offset, uint row, ushort subrow) => new RawSubrow(page, offset, row, subrow);
    }
}
