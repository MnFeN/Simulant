using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lumina.Data;
using Lumina.Data.Structs.Excel;

namespace Lumina.Excel
{
    public sealed class SubrowExcelSheet<T> : ISubrowExcelSheet, ICollection<SubrowCollection<T>>, IReadOnlyCollection<SubrowCollection<T>> where T : struct, IExcelSubrow<T>
    {
        private readonly uint[] rowIds;
        public RawExcelSheet RawSheet { get; private set; }
        public ExcelModule Module { get { return RawSheet.Module; } }
        public Language Language { get { return RawSheet.Language; } }
        public IReadOnlyList<ExcelColumnDefinition> Columns { get { return RawSheet.Columns; } }
        public int Count { get { return rowIds.Length; } }
        public int TotalSubrowCount { get { return RawSheet.Rows.Count; } }
        public SubrowCollection<T> this[uint rowId] { get { return GetRow(rowId); } }
        public T this[uint rowId, ushort subrowId] { get { return GetSubrow(rowId, subrowId); } }
        bool ICollection<SubrowCollection<T>>.IsReadOnly { get { return true; } }
        internal SubrowExcelSheet(RawExcelSheet sheet) { RawSheet = sheet; rowIds = sheet.Rows.Select(r => r.RowId).Distinct().OrderBy(id => id).ToArray(); }
        internal static T Create(RawExcelRow row) { return SubrowFactory<T>.Create(row.ExcelPage, row.RowOffset, row.RowId, row.SubrowId); }
        public SubrowCollection<T>? GetRowOrDefault(uint rowId)
        { SubrowCollection<T> rows; return TryGetRow(rowId, out rows) ? rows : (SubrowCollection<T>?)null; }
        public bool TryGetRow(uint rowId, out SubrowCollection<T> row)
        {
            var rows = RawSheet.GetRows(rowId);
            if (rows.Count == 0) { row = default(SubrowCollection<T>); return false; }
            row = new SubrowCollection<T>(this, rowId, rows); return true;
        }
        public SubrowCollection<T> GetRow(uint rowId)
        { SubrowCollection<T> row; if (!TryGetRow(rowId, out row)) throw new ArgumentOutOfRangeException(nameof(rowId)); return row; }
        public SubrowCollection<T> GetRowAt(int rowIndex)
        { if (rowIndex < 0 || rowIndex >= Count) throw new ArgumentOutOfRangeException(nameof(rowIndex)); return GetRow(rowIds[rowIndex]); }
        public T? GetSubrowOrDefault(uint rowId, ushort subrowId)
        { T row; return TryGetSubrow(rowId, subrowId, out row) ? row : (T?)null; }
        public bool TryGetSubrow(uint rowId, ushort subrowId, out T subrow)
        {
            foreach (var raw in RawSheet.GetRows(rowId))
                if (raw.SubrowId == subrowId) { subrow = Create(raw); return true; }
            subrow = default(T); return false;
        }
        public T GetSubrow(uint rowId, ushort subrowId)
        { T row; if (!TryGetSubrow(rowId, subrowId, out row)) throw new ArgumentOutOfRangeException(nameof(subrowId)); return row; }
        public T GetSubrowAt(int rowIndex, ushort subrowId)
        { return GetSubrow(GetRowAt(rowIndex).RowId, subrowId); }
        public bool HasRow(uint rowId) { return RawSheet.GetRows(rowId).Count != 0; }
        public bool HasSubrow(uint rowId, ushort subrowId) { return RawSheet.GetRows(rowId).Any(r => r.SubrowId == subrowId); }
        public bool TryGetSubrowCount(uint rowId, out ushort count)
        { count = checked((ushort)RawSheet.GetRows(rowId).Count); return count != 0; }
        public ushort GetSubrowCount(uint rowId)
        { ushort count; if (!TryGetSubrowCount(rowId, out count)) throw new ArgumentOutOfRangeException(nameof(rowId)); return count; }
        public ushort GetColumnOffset(int columnIdx) { return RawSheet.Columns[columnIdx].Offset; }
        public bool Contains(SubrowCollection<T> item) { return ReferenceEquals(item.Sheet, this) && HasRow(item.RowId); }
        public void CopyTo(SubrowCollection<T>[] array, int index)
        { ReadOnlyCollectionHelpers.CheckCopy(array, index, Count); foreach (var row in this) array[index++] = row; }
        void ICollection<SubrowCollection<T>>.Add(SubrowCollection<T> item) { throw new NotSupportedException(); }
        void ICollection<SubrowCollection<T>>.Clear() { throw new NotSupportedException(); }
        bool ICollection<SubrowCollection<T>>.Remove(SubrowCollection<T> item) { throw new NotSupportedException(); }
        public IEnumerator<SubrowCollection<T>> GetEnumerator() { foreach (uint id in rowIds) yield return GetRow(id); }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }

    public readonly struct SubrowCollection<T> : IList<T>, IReadOnlyList<T> where T : struct, IExcelSubrow<T>
    {
        private readonly IReadOnlyList<RawExcelRow> rows;
        public SubrowExcelSheet<T> Sheet { get; }
        public uint RowId { get; }
        public int Count { get { return rows == null ? 0 : rows.Count; } }
        internal SubrowCollection(SubrowExcelSheet<T> sheet, uint id, IReadOnlyList<RawExcelRow> data) { Sheet = sheet; RowId = id; rows = data; }
        public T this[int index]
        { get { if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index)); return SubrowExcelSheet<T>.Create(rows[index]); } }
        T IList<T>.this[int index] { get { return this[index]; } set { throw new NotSupportedException(); } }
        bool ICollection<T>.IsReadOnly { get { return true; } }
        public int IndexOf(T item) { for (int i = 0; i < Count; i++) if (EqualityComparer<T>.Default.Equals(this[i], item)) return i; return -1; }
        public bool Contains(T item) { return IndexOf(item) >= 0; }
        public void CopyTo(T[] array, int index) { ReadOnlyCollectionHelpers.CheckCopy(array, index, Count); for (int i = 0; i < Count; i++) array[index++] = this[i]; }
        void IList<T>.Insert(int index, T item) { throw new NotSupportedException(); }
        void IList<T>.RemoveAt(int index) { throw new NotSupportedException(); }
        void ICollection<T>.Add(T item) { throw new NotSupportedException(); }
        void ICollection<T>.Clear() { throw new NotSupportedException(); }
        bool ICollection<T>.Remove(T item) { throw new NotSupportedException(); }
        public IEnumerator<T> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }
}
