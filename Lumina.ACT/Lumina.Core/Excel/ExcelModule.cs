using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lumina.Data;
using Lumina.Data.Files.Excel;
using Lumina.Data.Structs.Excel;

namespace Lumina.Excel
{
    /// <summary>Schema-independent EXD API for the ACT port; columns are indexed from zero.</summary>
    public sealed partial class ExcelModule
    {
        private readonly GameData gameData;
        private readonly Dictionary<string, RawExcelSheet> cache = new Dictionary<string, RawExcelSheet>();
        private readonly object gate = new object ();
        public IReadOnlyList<string> SheetNames { get; private set; }
        public Language Language { get { return gameData.Options.DefaultExcelLanguage; } }

        internal ExcelModule(GameData gameData)
        {
            this.gameData = gameData;
            var list = gameData.GetFile<ExcelListFile>("exd/root.exl");
            if (list == null)
                throw new FileNotFoundException("The selected installation does not contain exd/root.exl.");
            SheetNames = list.ExdMap.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public RawExcelSheet GetSheet(string name, Language? language = null)
        {
            if (!SheetNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                throw new FileNotFoundException("Unknown sheet: " + name);
            var requested = language ?? gameData.Options.DefaultExcelLanguage;
            string key = name.ToLowerInvariant() + ":" + requested;
            lock (gate)
            {
                RawExcelSheet sheet;
                if (cache.TryGetValue(key, out sheet))
                    return sheet;
                var header = gameData.GetFile<ExcelHeaderFile>("exd/" + name + ".exh");
                if (header == null)
                    throw new FileNotFoundException("Sheet header missing: " + name);
                var resolved = header.Languages.Contains(requested) ? requested : Language.None;
                if (!header.Languages.Contains(resolved))
                    throw new NotSupportedException("Sheet does not support language " + requested);
                sheet = new RawExcelSheet(gameData, name, resolved, header);
                cache.Add(key, sheet);
                return sheet;
            }
        }
    }

    public sealed class RawExcelSheet
    {
        private readonly List<RawExcelRow> rows = new List<RawExcelRow>();
        private readonly Dictionary<uint, List<RawExcelRow>> rowLookup = new Dictionary<uint, List<RawExcelRow>>();
        public string Name { get; private set; }
        public ExcelModule Module { get; private set; }
        public Language Language { get; private set; }
        public ExcelHeaderFile Header { get; private set; }

        public IReadOnlyList<RawExcelRow> Rows
        {
            get
            {
                return rows;
            }
        }

        public ExcelColumnDefinition[] Columns
        {
            get
            {
                return Header.ColumnDefinitions;
            }
        }

        public uint ColumnHash
        {
            get
            {
                return Header.GetColumnsHash();
            }
        }

        public ushort GetColumnOffset(int columnIdx) { return Columns[columnIdx].Offset; }

        public bool HasSubrows
        {
            get
            {
                return Header.Header.Variant == ExcelVariant.Subrows;
            }
        }

        internal RawExcelSheet(GameData gameData, string name, Language language, ExcelHeaderFile header)
        {
            Module = gameData.Excel;
            Name = name;
            Language = language;
            Header = header;
            if (header.Header.Variant != ExcelVariant.Default && header.Header.Variant != ExcelVariant.Subrows)
                throw new NotSupportedException("Unsupported sheet variant: " + header.Header.Variant);
            foreach (var pageInfo in header.DataPages)
            {
                string suffix = language == Language.None ? "" : "_" + LanguageUtil.GetLanguageStr(language);
                string path = "exd/" + name + "_" + pageInfo.StartId + suffix + ".exd";
                var page = gameData.GetFile<ExcelDataFile>(path);
                if (page == null)
                    throw new FileNotFoundException("Sheet page missing: " + path);
                var typedPage = new ExcelPage(this, page.Data);
                foreach (var pointer in page.RowData.Values.OrderBy(p => p.RowId))
                {
                    int offset = checked((int)pointer.Offset);
                    var data = page.Data;
                    CheckBounds(data, offset, 6);
                    int end = checked(offset + 6 + (int)ReadUInt32(data, offset));
                    if (end > data.Length)
                        throw new InvalidDataException("EXD row exceeds its page.");
                    int count = HasSubrows ? ReadUInt16(data, offset + 4) : 1;
                    int stride = checked(header.Header.DataOffset + 2);
                    int fixedStart = offset + 6;
                    int strings = checked(fixedStart + (HasSubrows ? count * stride : header.Header.DataOffset));
                    if (strings > end)
                        throw new InvalidDataException("EXD fixed data exceeds its row.");
                    var group = new List<RawExcelRow>();
                    for (int i = 0; i < count; i++)
                    {
                        int position = HasSubrows ? checked(fixedStart + i * stride) : fixedStart;
                        ushort subId = HasSubrows ? ReadUInt16(data, position) : (ushort)0;
                        var row = new RawExcelRow(this, pointer.RowId, subId, data, position + (HasSubrows ? 2 : 0), strings, end, gameData.Options.RsvResolver);
                        row.ExcelPage = typedPage;
                        typedPage.RegisterRow(row.RowOffset, strings, end, gameData.Options.RsvResolver);
                        rows.Add(row);
                        group.Add(row);
                    }

                    rowLookup.Add(pointer.RowId, group);
                }
            }
        }

        public IReadOnlyList<RawExcelRow> GetRows(uint rowId)
        {
            List<RawExcelRow> result;
            return rowLookup.TryGetValue(rowId, out result) ? result : (IReadOnlyList<RawExcelRow>)Array.Empty<RawExcelRow>();
        }

        internal static void CheckBounds(byte[] data, int offset, int count)
        {
            if (offset < 0 || count < 0 || offset > data.Length - count)
                throw new InvalidDataException("Invalid EXD offset or length.");
        }

        internal static ushort ReadUInt16(byte[] data, int offset)
        {
            CheckBounds(data, offset, 2);
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        internal static uint ReadUInt32(byte[] data, int offset)
        {
            CheckBounds(data, offset, 4);
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }
    }

    public sealed class RawExcelRow
    {
        private readonly RawExcelSheet sheet;
        private readonly byte[] data;
        private readonly int fixedOffset, stringOffset, rowEnd;
        private readonly Func<string, string> rsvResolver;
        public uint RowId { get; private set; }
        public ushort SubrowId { get; private set; }
        public ExcelPage ExcelPage { get; internal set; }
        public uint RowOffset { get { return checked((uint)fixedOffset); } }

        internal RawExcelRow(RawExcelSheet sheet, uint rowId, ushort subrowId, byte[] data, int fixedOffset, int stringOffset, int rowEnd, Func<string, string> resolver)
        {
            this.sheet = sheet;
            RowId = rowId;
            SubrowId = subrowId;
            this.data = data;
            this.fixedOffset = fixedOffset;
            this.stringOffset = stringOffset;
            this.rowEnd = rowEnd;
            rsvResolver = resolver;
        }

        public object ReadColumn(int index)
        {
            var column = sheet.Columns[index];
            int offset = checked(fixedOffset + column.Offset);
            var type = column.Type;
            int width;
            switch (type)
            {
                case ExcelColumnDataType.String:
                case ExcelColumnDataType.Int32:
                case ExcelColumnDataType.UInt32:
                case ExcelColumnDataType.Float32:
                    width = 4;
                    break;
                case ExcelColumnDataType.Int16:
                case ExcelColumnDataType.UInt16:
                    width = 2;
                    break;
                case ExcelColumnDataType.Int64:
                case ExcelColumnDataType.UInt64:
                    width = 8;
                    break;
                default:
                    width = 1;
                    break;
            }

            if (column.Offset + width > sheet.Header.Header.DataOffset)
                throw new InvalidDataException("Column exceeds the fixed row size.");
            RawExcelSheet.CheckBounds(data, offset, width);
            switch (type)
            {
                case ExcelColumnDataType.String:
                    int start = checked(stringOffset + (int)RawExcelSheet.ReadUInt32(data, offset));
                    if (start < stringOffset || start >= rowEnd)
                        throw new InvalidDataException("String offset is outside the row's shared string block.");
                    int end = Array.IndexOf(data, (byte)0, start, rowEnd - start);
                    if (end < 0)
                        throw new InvalidDataException("Unterminated EXD string.");
                    var bytes = new byte[end - start];
                    Array.Copy(data, start, bytes, 0, bytes.Length);
                    return new SeStringValue(bytes, rsvResolver);
                case ExcelColumnDataType.Bool:
                    return data[offset] != 0;
                case ExcelColumnDataType.Int8:
                    return unchecked((sbyte)data[offset]);
                case ExcelColumnDataType.UInt8:
                    return data[offset];
                case ExcelColumnDataType.Int16:
                    return unchecked((short)RawExcelSheet.ReadUInt16(data, offset));
                case ExcelColumnDataType.UInt16:
                    return RawExcelSheet.ReadUInt16(data, offset);
                case ExcelColumnDataType.Int32:
                    return unchecked((int)RawExcelSheet.ReadUInt32(data, offset));
                case ExcelColumnDataType.UInt32:
                    return RawExcelSheet.ReadUInt32(data, offset);
                case ExcelColumnDataType.Float32:
                    return BitConverter.ToSingle(BitConverter.GetBytes(RawExcelSheet.ReadUInt32(data, offset)), 0);
                case ExcelColumnDataType.Int64:
                    return unchecked((long)ReadUInt64(offset));
                case ExcelColumnDataType.UInt64:
                    return ReadUInt64(offset);
                default:
                    if (type >= ExcelColumnDataType.PackedBool0 && type <= ExcelColumnDataType.PackedBool7)
                        return (data[offset] & (1 << ((int)type - (int)ExcelColumnDataType.PackedBool0))) != 0;
                    throw new NotSupportedException("Unknown EXD column type: " + type);
            }
        }

        private ulong ReadUInt64(int offset)
        {
            return ((ulong)RawExcelSheet.ReadUInt32(data, offset) << 32) | RawExcelSheet.ReadUInt32(data, offset + 4);
        }
    }
}
