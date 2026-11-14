using System;
using System.Collections.Generic;
using System.Reflection;
using Lumina.Data;
using Lumina.Data.Structs.Excel;

namespace Lumina.Excel
{
    public interface IExcelRow<out T> where T : struct
    {
        ExcelPage ExcelPage { get; }
        uint RowOffset { get; }
        uint RowId { get; }
    }

    public interface IExcelSubrow<out T> where T : struct
    {
        ExcelPage ExcelPage { get; }
        uint RowOffset { get; }
        uint RowId { get; }
        ushort SubrowId { get; }
    }

    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class SheetAttribute : Attribute
    {
        public string Name { get; private set; }
        public uint? ColumnHash { get; private set; }
        public SheetAttribute() { }
        public SheetAttribute(string name) { Name = name; }
        public SheetAttribute(uint columnHash) { ColumnHash = columnHash; }
        public SheetAttribute(string name, uint columnHash) { Name = name; ColumnHash = columnHash; }
    }

    public interface IExcelSheet
    {
        ExcelModule Module { get; }
        Language Language { get; }
        IReadOnlyList<ExcelColumnDefinition> Columns { get; }
        int Count { get; }
        bool HasRow(uint rowId);
        ushort GetColumnOffset(int columnIdx);
    }

    public interface ISubrowExcelSheet : IExcelSheet
    {
        int TotalSubrowCount { get; }
        bool HasSubrow(uint rowId, ushort subrowId);
        bool TryGetSubrowCount(uint rowId, out ushort subrowCount);
        ushort GetSubrowCount(uint rowId);
    }

    // Delegate binding is performed once per schema. No reflection is used while reading fields.
    internal static class RowFactory<T> where T : struct, IExcelRow<T>
    {
        internal static readonly Func<ExcelPage, uint, uint, T> Create = Bind();
        private static Func<ExcelPage, uint, uint, T> Bind()
        {
            var method = typeof(T).GetMethod("Create", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ExcelPage), typeof(uint), typeof(uint) }, null);
            if (method == null) throw new InvalidOperationException(typeof(T).FullName + " must declare public static T Create(ExcelPage, uint, uint).");
            return (Func<ExcelPage, uint, uint, T>)Delegate.CreateDelegate(typeof(Func<ExcelPage, uint, uint, T>), method);
        }
    }

    internal static class SubrowFactory<T> where T : struct, IExcelSubrow<T>
    {
        internal static readonly Func<ExcelPage, uint, uint, ushort, T> Create = Bind();
        private static Func<ExcelPage, uint, uint, ushort, T> Bind()
        {
            var method = typeof(T).GetMethod("Create", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ExcelPage), typeof(uint), typeof(uint), typeof(ushort) }, null);
            if (method == null) throw new InvalidOperationException(typeof(T).FullName + " must declare public static T Create(ExcelPage, uint, uint, ushort).");
            return (Func<ExcelPage, uint, uint, ushort, T>)Delegate.CreateDelegate(typeof(Func<ExcelPage, uint, uint, ushort, T>), method);
        }
    }
}

namespace Lumina.Excel.Exceptions
{
    public sealed class MismatchedColumnHashException : InvalidOperationException
    {
        public string SheetName { get; private set; }
        public uint ExpectedHash { get; private set; }
        public uint ActualHash { get; private set; }
        public MismatchedColumnHashException(string sheet, uint expected, uint actual)
            : base(string.Format("Sheet {0} schema mismatch: expected {1:X8}, actual {2:X8}. Use a matching schema or the raw EXD API.", sheet, expected, actual))
        { SheetName = sheet; ExpectedHash = expected; ActualHash = actual; }
    }
}
