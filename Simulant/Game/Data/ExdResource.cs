using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Exceptions;
using Lumina.Text.ReadOnly;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace Simulant.Game.Data
{
    /// <summary>独立读取窗口所需的一页快照，不在界面绘制时访问游戏文件。</summary>
    internal sealed class ExdResource
    {
        public const int PageSize = 200;
        public string[] Columns { get; private set; }
        public string[][] Rows { get; private set; }
        public int Total { get; private set; }
        public int Matches { get; private set; }
        public int Page { get; private set; }
        public string Notice { get; private set; }

        private static readonly Dictionary<string, Type> Schemas = typeof(SheetAttribute).Assembly.GetTypes()
            .Select(type => new
            {
                Type = type,
                Sheet = type.GetCustomAttribute<SheetAttribute>()
            })
            .Where(item => item.Sheet?.Name != null)
            .ToDictionary(item => item.Sheet.Name, item => item.Type, StringComparer.OrdinalIgnoreCase);

        public static string[] ListSheets(string path, Language language, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            using (var data = Open(path, language))
                return data.Excel.SheetNames.ToArray();
        }

        private static GameData Open(string path, Language language) =>
            new GameData(path, new LuminaOptions
            {
                DefaultExcelLanguage = language
            });

        public static ExdResource Read(string path, Language language, string name, string query, int page, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            using (var data = Open(path, language))
            {
                var sheet = data.Excel.GetSheet(name);
                token.ThrowIfCancellationRequested();
                var reader = new Reader(data.Excel, token);
                Schemas.TryGetValue(name, out var type);
                string notice = "";

                if (type != null)
                {
                    try
                    {
                        data.Excel.GetSheet(type);
                    }
                    catch (MismatchedColumnHashException)
                    {
                        type = null;
                        notice = "字段定义与客户端不匹配，显示原始列。";
                    }
                }
                else
                    notice = "暂无强类型定义，显示原始列。";

                var fields = type == null ? null : Fields(type);
                var factory = type == null ? null : Factory(type);
                var headers = fields == null
                    ? new[]
                {
                    "RowId"
                }.Concat(sheet.HasSubrows ? new[]
                {
                    "SubrowId"
                } : Array.Empty<string>())
                        .Concat(sheet.Columns.Select((column, i) => $"列 {i} ({column.Type})")).ToArray()
                    : fields.Select(field => field.Name).ToArray();
                Func<RawExcelRow, string[]> format = row => fields == null
                    ? new[]
                {
                    row.RowId.ToString(CultureInfo.InvariantCulture)
                }
                        .Concat(sheet.HasSubrows ? new[]
                {
                    row.SubrowId.ToString(CultureInfo.InvariantCulture)
                } : Array.Empty<string>())
                        .Concat(sheet.Columns.Select((_, i) => reader.Format(row.ReadColumn(i)))).ToArray()
                    : reader.Cells(factory(row), fields);
                var rows = new List<string[]>();
                var matches = 0;
                page = Math.Max(0, page);
                query = query?.Trim() ?? "";

                if (query.Length == 0)
                {
                    matches = sheet.Rows.Count;
                    page = Math.Min(page, Math.Max(0, (matches - 1) / PageSize));

                    foreach (var row in sheet.Rows.Skip(page * PageSize).Take(PageSize))
                    {
                        token.ThrowIfCancellationRequested();
                        rows.Add(format(row));
                    }
                }
                else
                {
                    foreach (var row in sheet.Rows)
                    {
                        token.ThrowIfCancellationRequested();
                        var cells = format(row);

                        if (!cells.Any(cell => cell.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                            continue;

                        if (matches / PageSize == page)
                            rows.Add(cells);

                        matches++;
                    }
                }

                return new ExdResource
                {
                    Columns = headers,
                    Rows = rows.ToArray(),
                    Total = sheet.Rows.Count,
                    Matches = matches,
                    Page = page,
                    Notice = notice
                };
            }
        }

        private static PropertyInfo[] Fields(Type type) => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0 && property.Name != "ExcelPage" && property.Name != "RowOffset")
            .OrderBy(property => property.Name == "RowId" ? 0 : property.Name == "SubrowId" ? 1 : 2)
            .ThenBy(property => property.MetadataToken).ToArray();

        private static Func<RawExcelRow, object> Factory(Type type)
        {
            var row = Expression.Parameter(typeof(RawExcelRow), "row");
            var method = type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
            var names = new[]
            {
                "ExcelPage",
                "RowOffset",
                "RowId",
                "SubrowId"
            };
            return Expression.Lambda<Func<RawExcelRow, object>>(Expression.Convert(Expression.Call(method,
                names.Take(method.GetParameters().Length).Select(name => Expression.Property(row, name))), typeof(object)), row).Compile();
        }

        /// <summary>按字段类型解析文本和关联；名称优先级适用于所有表，不维护逐表映射。</summary>
        private sealed class Reader
        {
            private readonly ExcelModule _excel;
            private readonly CancellationToken _token;
            private readonly Dictionary<Type, PropertyInfo[]> _fields = new Dictionary<Type, PropertyInfo[]>();
            private readonly Dictionary<Type, Func<RawExcelRow, object>> _factories = new Dictionary<Type, Func<RawExcelRow, object>>();
            private readonly Dictionary<string, string> _references = new Dictionary<string, string>();
            private readonly HashSet<Type> _incompatible = new HashSet<Type>();
            private static readonly string[] NameFields =
            {
                "Name",
                "Singular",
                "Key",
                "Path",
                "Text",
                "Description"
            };

            public Reader(ExcelModule excel, CancellationToken token)
            {
                _excel = excel;
                _token = token;
            }

            private PropertyInfo[] GetFields(Type type)
            {
                if (!_fields.TryGetValue(type, out var fields))
                    _fields[type] = fields = Fields(type);

                return fields;
            }

            public string[] Cells(object row, PropertyInfo[] fields) => fields.Select(field => ReadField(row, field, 0)).ToArray();

            private string ReadField(object row, PropertyInfo field, int depth)
            {
                _token.ThrowIfCancellationRequested();

                try
                {
                    return Format(field.GetValue(row), depth);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception error)
                {
                    return "读取失败：" + (error is TargetInvocationException ? error.InnerException ?? error : error).Message;
                }
            }

            public string Format(object value, int depth = 0)
            {
                _token.ThrowIfCancellationRequested();

                if (value == null)
                    return "";

                if (value is ReadOnlySeString text)
                    return text.ExtractText();

                if (value is SeStringValue rawText)
                    return rawText.Text;

                if (value is string str)
                    return str;

                var type = value.GetType();

                if (type.IsPrimitive || type.IsEnum || value is decimal)
                    return Convert.ToString(value, CultureInfo.InvariantCulture);

                if (value is RowRef reference)
                    return Reference(reference.RowType, reference.RowId);

                if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(RowRef<>) || type.GetGenericTypeDefinition() == typeof(SubrowRef<>)))
                    return Reference(type.GetGenericArguments()[0], (uint)type.GetProperty("RowId").GetValue(value));

                if (depth >= 4)
                    return "（嵌套内容）";

                if (value is IEnumerable items)
                    return "[" + string.Join("; ", items.Cast<object>().Select(item => Format(item, depth + 1))) + "]";

                return string.Join("; ", GetFields(type).Select(field => field.Name + "=" + ReadField(value, field, depth + 1)));
            }

            private string Reference(Type type, uint id)
            {
                if (type == null)
                    return id.ToString(CultureInfo.InvariantCulture);

                var name = type.GetCustomAttribute<SheetAttribute>()?.Name ?? type.Name;
                var key = name + "#" + id.ToString(CultureInfo.InvariantCulture);

                if (_references.TryGetValue(key, out var found))
                    return found;

                if (_incompatible.Contains(type))
                    return key + "（字段定义不匹配）";

                try
                {
                    // 必须先验证布局，不能把新版本的数据套入旧定义。
                    if (!_factories.TryGetValue(type, out var create))
                    {
                        _excel.GetSheet(type);
                        _factories[type] = create = Factory(type);
                    }

                    var rows = _excel.GetSheet(name).GetRows(id);

                    if (rows.Count == 0)
                        return _references[key] = key + "（无对应行）";

                    var fields = GetFields(type).Where(field => field.PropertyType == typeof(ReadOnlySeString) || field.PropertyType == typeof(string))
                        .OrderBy(field =>
                    {
                        var index = Array.IndexOf(NameFields, field.Name);
                        return index < 0 ? NameFields.Length : index;
                    });
                    var row = create(rows[0]);

                    foreach (var field in fields)
                    {
                        var text = Format(field.GetValue(row));

                        if (!string.IsNullOrWhiteSpace(text))
                            return _references[key] = key + " → " + text;
                    }

                    return _references[key] = key;
                }
                catch (MismatchedColumnHashException)
                {
                    _incompatible.Add(type);
                    return key + "（字段定义不匹配）";
                }
                catch (System.IO.FileNotFoundException)
                {
                    return _references[key] = key + "（客户端没有此表）";
                }
            }
        }
    }
}
