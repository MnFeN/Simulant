using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Simulant.Game.ExtractedCsv
{
    /// <summary>
    /// Central manager for loading and caching CSV tables and typed row maps.
    /// </summary>
    public class CsvManager
    {
        /// <summary>
        /// Default CsvManager instance used by the plugin.
        /// </summary>
        public static CsvManager Instance { get; } = new CsvManager();

        /// <summary>
        /// Global cache of raw tables (table name → CsvTable).
        /// </summary>
        private readonly Dictionary<string, CsvTable> _tables = new Dictionary<string, CsvTable>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lockTables = new object();

        /// <summary>
        /// Global cache of typed tables (row type → IReadOnlyDictionary&lt;RowIndexKey, TRow&gt; where TRow : TypedCsvRow).
        /// </summary>
        private readonly Dictionary<Type, object> _typedTables = new Dictionary<Type, object>();
        private readonly object _lockTypedTables = new object();
        private readonly object _loadAllGate = new object();
        private bool _allTablesLoaded;

        /// <summary>
        /// Cached mapping from table name to the corresponding TypedCsvRow type.
        /// </summary>
        private static readonly Dictionary<string, Type> _rowTypeByName = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        private const string CsvResourcePrefix = "Simulant.Game.ExtractedCsv.RawExd.";
        private static readonly Assembly _csvAssembly = typeof(CsvManager).Assembly;

        static CsvManager()
        {
            var baseType = typeof(TypedCsvRow);
            var asm = baseType.Assembly;

            foreach (var t in asm.GetTypes())
            {
                if (t.Namespace == "Simulant.Game.ExtractedCsv.Rows" &&
                    !t.IsAbstract &&
                    baseType.IsAssignableFrom(t))
                {
                    _rowTypeByName[t.Name] = t;
                }
            }
        }

        /// <summary>
        /// Get a loaded raw CSV table by name (without .csv extension, ignore case).
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown if the table has not been loaded.</exception>
        public CsvTable this[string tableName]
            => TryGetTable(tableName, out CsvTable table) ? table
                : throw new KeyNotFoundException($"Table '{tableName}.csv' was not loaded.");

        /// <summary>
        /// Try to get a loaded raw CSV table by name (without .csv extension, ignore case).
        /// </summary>
        /// <returns><c>true</c> if the table is found; otherwise <c>false</c>.</returns>
        public bool TryGetTable(string tableName, out CsvTable table)
        {
            lock (_lockTables)
                return _tables.TryGetValue(tableName, out table);
        }

        /// <summary>
        /// Get a loaded typed table for the specified row type (subclass of TypedCsvRow).
        /// </summary>
        /// <returns>Read-only dictionary of row index to typed row.</returns>
        /// <exception cref="InvalidCastException">The cached entry does not match the expected type.</exception>
        /// <exception cref="KeyNotFoundException">The typed table has not been loaded.</exception>
        public IReadOnlyDictionary<RowIndexKey, T> Get<T>() where T : TypedCsvRow
        {
            lock (_lockTypedTables)
            {
                if (_typedTables.TryGetValue(typeof(T), out var o))
                {
                    if (o is IReadOnlyDictionary<RowIndexKey, T> dict)
                        return dict;

                    throw new InvalidCastException(
                        $"Cached table of '{typeof(T)}' is not IReadOnlyDictionary<RowIndexKey, {typeof(T).Name}>."
                    );
                }
            }

            throw new KeyNotFoundException($"Table '{typeof(T)}.csv' was not loaded.");
        }

        public void LoadTable(string csvNameWithoutExt, Type type = null)
        {
            var resourcePath = CsvResourcePrefix + csvNameWithoutExt + ".csv";
            var table = CsvTable.CreateFromResourcePath(this, resourcePath);

            lock (_lockTables)
            {
                _tables[csvNameWithoutExt] = table;
            }

            if (type == null)
            {
                if (!_rowTypeByName.TryGetValue(csvNameWithoutExt, out type))
                    return;
            }
            else if (!typeof(TypedCsvRow).IsAssignableFrom(type))
            {
                throw new ArgumentException(
                    $"The provided type '{type.FullName}' is not a subclass of {typeof(TypedCsvRow).FullName}.",
                    nameof(type));
            }

            var genericMethod = _createTypedDictMethod.MakeGenericMethod(type);
            var dictObj = genericMethod.Invoke(this, new object[] { table });

            lock (_lockTypedTables)
            {
                _typedTables[type] = dictObj;
            }
        }

        /// <summary>
        /// Attempts to load a single CSV table, wrapping exceptions with additional context.
        /// </summary>
        /// <param name="csvNameWithoutExt">CSV file name without extension.</param>
        private void TryLoadTable(string csvNameWithoutExt)
        {
            try
            {
                LoadTable(csvNameWithoutExt);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load CSV file '{csvNameWithoutExt}.csv': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads all embedded CSV files.
        /// </summary>
        /// <param name="multiThread">
        /// If true, tables are loaded in parallel; otherwise loaded sequentially.
        /// </param>
        public void LoadAllTables(bool multiThread = true)
        {
            // 多个宿主重叠加载时只解析一次；完整成功后复用缓存，失败保留重试机会。
            lock (_loadAllGate)
            {
                if (_allTablesLoaded) return;
                LoadAllTablesCore(multiThread);
                _allTablesLoaded = true;
            }
        }

        private void LoadAllTablesCore(bool multiThread)
        {
            var files = _csvAssembly
                .GetManifestResourceNames()
                .Where(name => name.StartsWith(CsvResourcePrefix, StringComparison.OrdinalIgnoreCase))
                .Where(name => name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                .Select(name => name.Substring(CsvResourcePrefix.Length, name.Length - CsvResourcePrefix.Length - 4))
                .ToArray();

            if (multiThread)
            {
                Parallel.ForEach(
                    files,
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    TryLoadTable);
            }
            else
            {
                foreach (var csvNameWithoutExt in files)
                {
                    TryLoadTable(csvNameWithoutExt);
                }
            }
        }

        /// <summary>
        /// Clears all loaded raw and typed tables.
        /// </summary>
        public void Clear()
        {
            // 与完整加载互斥，清空后下一次 LoadAllTables 会重新读取嵌入数据。
            lock (_loadAllGate)
            {
                _allTablesLoaded = false;
                lock (_lockTables)
                    _tables.Clear();
                lock (_lockTypedTables)
                    _typedTables.Clear();
            }
        }

        /// <summary>
        /// Cached MethodInfo for the generic CreateTypedDict&lt;TRow&gt; method.
        /// </summary>
        private static readonly MethodInfo _createTypedDictMethod =
            typeof(CsvManager).GetMethod("CreateTypedDict", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Cannot find method CreateTypedDict in CsvManager.");

        /// <summary>
        /// Creates a strongly-typed dictionary for a given CsvTable and row type.
        /// </summary>
        /// <typeparam name="TRow">TypedCsvRow subclass for this table.</typeparam>
        /// <param name="table">Source CsvTable instance.</param>
        /// <returns>Dictionary mapping RowIndexKey to TRow.</returns>
        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Used via reflection by _createTypedDictMethod.")]
        private Dictionary<RowIndexKey, TRow> CreateTypedDict<TRow>(CsvTable table) where TRow : TypedCsvRow
        {
            var rowFactory = CsvRow.GetOrCreateFactory(typeof(TRow));

            return table.Rows.Values
                .Select(csvRow => (TRow)rowFactory(csvRow))
                .ToDictionary(
                    row => row.Index,
                    row => row
                );
        }
    }
}
