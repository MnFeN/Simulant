using Lumina.Excel.Sheets;
using Simulant.Game.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ActionRow = Lumina.Excel.Sheets.Action;

namespace Simulant.ActionNpcTool
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("用法：ActionNpcIndex <客户端目录> <施法记录 CSV> <原始网络日志、ZIP 或目录> [更多输入]");
                return 1;
            }

            try
            {
                var output = Path.GetFullPath(args[1]);
                if (!output.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("请选择 CSV 文件作为输出路径。");

                var namesFile = string.Equals(Path.GetFileName(output), "ActionCasters.csv", StringComparison.OrdinalIgnoreCase)
                    ? "BNpcNames.csv" : Path.GetFileNameWithoutExtension(output) + ".Names.csv";
                var namesOutput = Path.Combine(Path.GetDirectoryName(output), namesFile);
                var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var input in args.Skip(2))
                {
                    var path = Path.GetFullPath(input);
                    if (Directory.Exists(path))
                    {
                        files.UnionWith(Directory.EnumerateFiles(path, "*.log", SearchOption.AllDirectories));
                        files.UnionWith(Directory.EnumerateFiles(path, "*.zip", SearchOption.AllDirectories));
                    }
                    else if (File.Exists(path))
                        files.Add(path);
                    else
                        throw new FileNotFoundException("未找到日志或目录。", path);
                }
                var logs = EnumerateLogs(files).OrderByDescending(log => log.Length).ToArray();
                if (logs.Length == 0)
                    throw new InvalidOperationException("未找到要扫描的日志。");

                var watch = Stopwatch.StartNew();
                var (wantedActions, validNameIds) = LoadGameIndexes(args[0]);
                var casters = new Dictionary<uint, HashSet<uint>>();
                var names = new Dictionary<uint, HashSet<uint>>();
                var gate = new object();
                var completed = 0;
                Parallel.ForEach(logs,
                    new ParallelOptions { MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) },
                    () => new ActNpcScanner(wantedActions, validNameIds), (log, _, scanner) =>
                    {
                        scanner.Scan(log.Path, log.EntryIndex);
                        var count = Interlocked.Increment(ref completed);
                        if (count % 25 == 0)
                            Console.WriteLine($"已扫描 {count}/{logs.Length} 份日志。");

                        return scanner;
                    }, scanner =>
                    {
                        lock (gate)
                        {
                            Merge(casters, scanner.Casters);
                            Merge(names, scanner.Names);
                        }
                    });

                var scannedActions = casters.Count;
                using (new FileStream(output + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096,
                    FileOptions.DeleteOnClose))
                {
                    ReadExisting(output, NpcIndexCsv.ActionHeader, casters, (actionId, baseId) =>
                        actionId < wantedActions.Length && wantedActions[actionId] && baseId != ActNpcScanner.DummyBaseId);
                    ReadExisting(namesOutput, NpcIndexCsv.NameHeader, names, (baseId, nameId) =>
                        baseId != ActNpcScanner.DummyBaseId && validNameIds.Contains(nameId));
                    WriteIndex(namesOutput, NpcIndexCsv.NameHeader, names.OrderBy(entry => entry.Key)
                        .Select(entry => (entry.Key, entry.Value.Min())));
                    WriteIndex(output, NpcIndexCsv.ActionHeader, casters.OrderBy(entry => entry.Key)
                        .SelectMany(entry => entry.Value.OrderBy(id => id).Select(id => (entry.Key, id))));
                }
                Console.WriteLine($"已扫描 {logs.Length} 份日志，匹配 {scannedActions} 个技能，索引共 {casters.Count} 个技能和 {names.Count} 个 BNpcBase 的名称，耗时 {watch.Elapsed.TotalSeconds:F2} 秒。");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                return 1;
            }
        }

        private static IEnumerable<(string Path, int EntryIndex, long Length)> EnumerateLogs(IEnumerable<string> files)
        {
            foreach (var path in files)
            {
                if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    yield return (path, -1, new FileInfo(path).Length);
                    continue;
                }

                using (var file = File.OpenRead(path))
                using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
                {
                    for (var i = 0; i < archive.Entries.Count; i++)
                    {
                        var entry = archive.Entries[i];
                        if (entry.Name.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                            yield return (path, i, entry.Length);
                    }
                }
            }
        }

        private static (bool[] Actions, HashSet<uint> NameIds) LoadGameIndexes(string game)
        {
            using (var data = new GameDataService(game))
            {
                var ids = data.GetSheet<ActionRow>()
                    .Where(row => row.AnimationEnd.ValueNullable?.Key.ExtractText().IndexOf("[SKL_ID]", StringComparison.Ordinal) >= 0)
                    .Select(row => row.RowId).ToArray();
                if (ids.Length == 0)
                    throw new InvalidOperationException("当前客户端未找到含 [SKL_ID] 的技能。");

                var wanted = new bool[checked(ids.Max() + 1)];
                foreach (var id in ids)
                    wanted[id] = true;
                var nameIds = new HashSet<uint>(data.GetSheet<BNpcName>().Select(row => row.RowId));
                nameIds.Remove(0);
                return (wanted, nameIds);
            }
        }

        private static void Merge(Dictionary<uint, HashSet<uint>> target, Dictionary<uint, HashSet<uint>> source)
        {
            foreach (var entry in source)
            {
                if (target.TryGetValue(entry.Key, out var existing))
                    existing.UnionWith(entry.Value);
                else
                    target.Add(entry.Key, entry.Value);
            }
        }

        private static void ReadExisting(string path, string header, Dictionary<uint, HashSet<uint>> index, Func<uint, uint, bool> keep)
        {
            if (!File.Exists(path))
                return;

            using (var reader = File.OpenText(path))
            {
                foreach (var pair in NpcIndexCsv.Read(reader, header))
                {
                    if (!keep(pair.First, pair.Second))
                        continue;

                    if (!index.TryGetValue(pair.First, out var entries))
                        index.Add(pair.First, entries = new HashSet<uint>());
                    entries.Add(pair.Second);
                }
            }
        }

        private static void WriteIndex(string path, string header, IEnumerable<(uint Key, uint Id)> entries)
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var writer = new StreamWriter(temporary, false, new UTF8Encoding(false)))
                {
                    writer.NewLine = "\n";
                    writer.WriteLine(header);
                    foreach (var entry in entries)
                        writer.WriteLine(FormattableString.Invariant($"{entry.Key},{entry.Id}"));
                }

                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }
}
