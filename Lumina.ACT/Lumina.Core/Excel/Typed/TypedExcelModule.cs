using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Lumina.Data;
using Lumina.Data.Files.Excel;
using Lumina.Excel.Exceptions;

namespace Lumina.Excel
{
    public sealed partial class ExcelModule
    {
        private readonly Dictionary<string, IExcelSheet> typedSheets = new Dictionary<string, IExcelSheet>();
        private readonly Dictionary<string, HashSet<uint>> rowIds = new Dictionary<string, HashSet<uint>>();

        internal bool HasRawRow(Type type, uint id, Language? language)
        {
            var attribute = (SheetAttribute)Attribute.GetCustomAttribute(type, typeof(SheetAttribute));
            if (attribute == null || attribute.Name == null) return false;
            lock (gate)
            {
                string name = attribute.Name;
                string key = name + ":" + (language ?? Language);
                HashSet<uint> ids;
                if (!rowIds.TryGetValue(key, out ids))
                {
                    var header = gameData.GetFile<ExcelHeaderFile>("exd/" + name + ".exh");
                    if (header == null) return false;
                    var requested = language ?? Language;
                    var resolved = header.Languages.Contains(requested) ? requested : Data.Language.None;
                    if (!header.Languages.Contains(resolved)) throw new NotSupportedException("Sheet does not support language " + requested);
                    string suffix = resolved == Data.Language.None ? "" : "_" + LanguageUtil.GetLanguageStr(resolved);
                    ids = new HashSet<uint>();
                    foreach (var page in header.DataPages)
                    {
                        var file = gameData.GetFile<ExcelDataFile>("exd/" + name + "_" + page.StartId + suffix + ".exd");
                        if (file == null) throw new FileNotFoundException("Missing EXD page for " + name);
                        foreach (var rowId in file.RowData.Keys) ids.Add(rowId);
                    }
                    rowIds.Add(key, ids);
                }
                return ids.Contains(id);
            }
        }

        public ExcelSheet<T> GetSheet<T>(Language? language = null, string name = null) where T : struct, IExcelRow<T>
        { return (ExcelSheet<T>)GetTypedSheet(typeof(T), false, language, name, raw => new ExcelSheet<T>(raw)); }

        public SubrowExcelSheet<T> GetSubrowSheet<T>(Language? language = null, string name = null) where T : struct, IExcelSubrow<T>
        { return (SubrowExcelSheet<T>)GetTypedSheet(typeof(T), true, language, name, raw => new SubrowExcelSheet<T>(raw)); }

        public IExcelSheet GetSheet(Type type, Language? language = null, string name = null)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            bool subrows = typeof(IExcelSubrow<>).MakeGenericType(type).IsAssignableFrom(type);
            bool rows = typeof(IExcelRow<>).MakeGenericType(type).IsAssignableFrom(type);
            if (!rows && !subrows) throw new ArgumentException("Type must implement IExcelRow<T> or IExcelSubrow<T>.", nameof(type));
            var method = typeof(ExcelModule).GetMethods().Single(m => m.Name == (subrows ? "GetSubrowSheet" : "GetSheet") && m.IsGenericMethodDefinition);
            try { return (IExcelSheet)method.MakeGenericMethod(type).Invoke(this, new object[] { language, name }); }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
        }

        private IExcelSheet GetTypedSheet(Type type, bool subrows, Language? language, string name, Func<RawExcelSheet, IExcelSheet> create)
        {
            lock (gate)
            {
                var attribute = (SheetAttribute)Attribute.GetCustomAttribute(type, typeof(SheetAttribute));
                if (attribute == null) throw new InvalidOperationException(type.FullName + " has no SheetAttribute.");
                name = name ?? attribute.Name;
                if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A sheet name is required.", nameof(name));
                string key = type.AssemblyQualifiedName + ":" + (language ?? Language) + ":" + name;
                IExcelSheet found;
                if (typedSheets.TryGetValue(key, out found)) return found;
                var header = gameData.GetFile<ExcelHeaderFile>("exd/" + name + ".exh");
                if (header == null) throw new FileNotFoundException("Sheet header missing: " + name);
                uint actual = header.GetColumnsHash();
                // Never interpret an incompatible layout, even if the table name is unchanged.
                if (attribute.ColumnHash.HasValue && attribute.ColumnHash.Value != actual)
                    throw new MismatchedColumnHashException(name, attribute.ColumnHash.Value, actual);
                bool actualSubrows = header.Header.Variant == Data.Structs.Excel.ExcelVariant.Subrows;
                if (actualSubrows != subrows) throw new NotSupportedException("The requested schema has the wrong row/subrow variant: " + name);
                var sheet = create(GetSheet(name, language));
                typedSheets.Add(key, sheet);
                return sheet;
            }
        }
    }
}
