using System;
using System.Collections;
using System.Collections.Generic;
using Lumina.Data;
using Lumina.Data.Structs.Excel;

namespace Lumina.Excel
{
    public sealed class ExcelSheet<T> : IExcelSheet, ICollection<T>, IReadOnlyCollection<T> where T : struct, IExcelRow<T>
    {
        public RawExcelSheet RawSheet { get; private set; }
        public ExcelModule Module { get { return RawSheet.Module; } }
        public Language Language { get { return RawSheet.Language; } }
        public IReadOnlyList<ExcelColumnDefinition> Columns { get { return RawSheet.Columns; } }
        public int Count { get { return RawSheet.Rows.Count; } }
        public T this[uint rowId] { get { return GetRow(rowId); } }
        bool ICollection<T>.IsReadOnly { get { return true; } }
        internal ExcelSheet(RawExcelSheet sheet) { RawSheet = sheet; }
        private static T Create(RawExcelRow row) { return RowFactory<T>.Create(row.ExcelPage, row.RowOffset, row.RowId); }
        public T? GetRowOrDefault(uint rowId) { T value; return TryGetRow(rowId, out value) ? value : (T?)null; }
        public bool TryGetRow(uint rowId, out T row)
        {
            var rows = RawSheet.GetRows(rowId);
            if (rows.Count == 0) { row = default(T); return false; }
            row = Create(rows[0]); return true;
        }
        public T GetRow(uint rowId)
        { T row; if (!TryGetRow(rowId, out row)) throw new ArgumentOutOfRangeException(nameof(rowId), rowId, "Row does not exist."); return row; }
        public T GetRowAt(int rowIndex)
        { if (rowIndex < 0 || rowIndex >= Count) throw new ArgumentOutOfRangeException(nameof(rowIndex)); return Create(RawSheet.Rows[rowIndex]); }
        public bool HasRow(uint rowId) { return RawSheet.GetRows(rowId).Count != 0; }
        public ushort GetColumnOffset(int columnIdx) { return RawSheet.Columns[columnIdx].Offset; }
        public bool Contains(T item) { T row; return TryGetRow(item.RowId, out row) && EqualityComparer<T>.Default.Equals(item, row); }
        public void CopyTo(T[] array, int arrayIndex)
        { ReadOnlyCollectionHelpers.CheckCopy(array, arrayIndex, Count); foreach (var row in this) array[arrayIndex++] = row; }
        void ICollection<T>.Add(T item) { throw new NotSupportedException(); }
        void ICollection<T>.Clear() { throw new NotSupportedException(); }
        bool ICollection<T>.Remove(T item) { throw new NotSupportedException(); }
        public IEnumerator<T> GetEnumerator() { foreach (var row in RawSheet.Rows) yield return Create(row); }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }

    internal static class ReadOnlyCollectionHelpers
    {
        internal static void CheckCopy<T>(T[] array, int index, int count)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (index > array.Length || count > array.Length - index) throw new ArgumentException("Destination array is too small.");
        }
    }
}
