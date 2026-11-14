using System;
using Lumina.Data;

namespace Lumina.Excel
{
    public readonly struct RowRef<T> where T : struct, IExcelRow<T>
    {
        private readonly ExcelModule module;
        private readonly Language? language;
        public uint RowId { get; }
        public Type RowType { get { return typeof(T); } }
        public Language? Language { get { return language ?? (module == null ? (Language?)null : module.Language); } }
        private ExcelSheet<T> Sheet { get { return module == null ? null : module.GetSheet<T>(language == Data.Language.None ? null : language); } }
        public RowRef(ExcelModule module, uint rowId, Language? language = null) { this.module = module; RowId = rowId; this.language = language; }
        public bool IsValid { get { var sheet = Sheet; return sheet != null && sheet.HasRow(RowId); } }
        public T? ValueNullable { get { var sheet = Sheet; return sheet == null ? null : sheet.GetRowOrDefault(RowId); } }
        public T Value { get { return ValueNullable ?? throw new InvalidOperationException("Referenced row does not exist."); } }
        public bool TryGetValue(out T row) { var value = ValueNullable; row = value.GetValueOrDefault(); return value.HasValue; }
        public static explicit operator RowRef(RowRef<T> row) { return RowRef.Create<T>(row.module, row.RowId, row.language); }
    }

    public readonly struct SubrowRef<T> where T : struct, IExcelSubrow<T>
    {
        private readonly ExcelModule module;
        private readonly Language? language;
        public uint RowId { get; }
        public Type RowType { get { return typeof(T); } }
        public Language? Language { get { return language ?? (module == null ? (Language?)null : module.Language); } }
        private SubrowExcelSheet<T> Sheet { get { return module == null ? null : module.GetSubrowSheet<T>(language == Data.Language.None ? null : language); } }
        public SubrowRef(ExcelModule module, uint rowId, Language? language = null) { this.module = module; RowId = rowId; this.language = language; }
        public bool IsValid { get { var sheet = Sheet; return sheet != null && sheet.HasRow(RowId); } }
        public SubrowCollection<T>? ValueNullable { get { var sheet = Sheet; return sheet == null ? null : sheet.GetRowOrDefault(RowId); } }
        public SubrowCollection<T> Value { get { return ValueNullable ?? throw new InvalidOperationException("Referenced subrow collection does not exist."); } }
        public bool TryGetValue(out SubrowCollection<T> row) { var value = ValueNullable; row = value.GetValueOrDefault(); return value.HasValue; }
        public static explicit operator RowRef(SubrowRef<T> row) { return RowRef.CreateSubrow<T>(row.module, row.RowId, row.language); }
    }

    public readonly struct RowRef
    {
        private readonly ExcelModule module;
        private readonly Language? language;
        public uint RowId { get; }
        public Type RowType { get; }
        public bool IsUntyped { get { return RowType == null; } }
        public Language? Language { get { return language ?? (module == null ? (Language?)null : module.Language); } }
        public RowRef(ExcelModule module, uint rowId, Type rowType, Language? language = null)
        { this.module = module; RowId = rowId; RowType = rowType; this.language = language; }
        public bool Is<T>() where T : struct, IExcelRow<T> { return RowType == typeof(T); }
        public bool IsSubrow<T>() where T : struct, IExcelSubrow<T> { return RowType == typeof(T); }
        public T? GetValueOrDefault<T>() where T : struct, IExcelRow<T>
        { return Is<T>() && module != null ? new RowRef<T>(module, RowId, language).ValueNullable : null; }
        public SubrowCollection<T>? GetValueOrDefaultSubrow<T>() where T : struct, IExcelSubrow<T>
        { return IsSubrow<T>() && module != null ? new SubrowRef<T>(module, RowId, language).ValueNullable : null; }
        public bool TryGetValue<T>(out T row) where T : struct, IExcelRow<T>
        { var value = GetValueOrDefault<T>(); row = value.GetValueOrDefault(); return value.HasValue; }
        public bool TryGetValueSubrow<T>(out SubrowCollection<T> row) where T : struct, IExcelSubrow<T>
        { var value = GetValueOrDefaultSubrow<T>(); row = value.GetValueOrDefault(); return value.HasValue; }
        public static RowRef Create<T>(ExcelModule module, uint id, Language? language = null) where T : struct, IExcelRow<T>
        { return new RowRef(module, id, typeof(T), language); }
        public static RowRef CreateSubrow<T>(ExcelModule module, uint id, Language? language = null) where T : struct, IExcelSubrow<T>
        { return new RowRef(module, id, typeof(T), language); }
        public static RowRef CreateUntyped(uint id, Language? language = null) { return new RowRef(null, id, null, language); }
        public static RowRef GetFirstValidRowOrUntyped(ExcelModule module, uint rowId, ReadOnlySpan<Type> types, int typeHash, Language? language = null)
        {
            // Candidate order is significant; a hash collision must never change the selected target.
            foreach (var type in types)
                if (module.HasRawRow(type, rowId, language == Data.Language.None ? null : language))
                    return new RowRef(module, rowId, type, language);
            return CreateUntyped(rowId, language ?? module.Language);
        }
        public static RowRef GetFirstValidRowOrUntyped(ExcelModule module, uint id, ReadOnlySpan<Type> types, Language? language = null)
        { return GetFirstValidRowOrUntyped(module, id, types, CreateTypeHash(types), language); }
        public static int CreateTypeHash(ReadOnlySpan<Type> types)
        { unchecked { int hash = 17; foreach (var type in types) hash = hash * 31 + type.GetHashCode(); return hash; } }
    }
}
