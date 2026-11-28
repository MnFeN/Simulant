using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Simulant.Content.Local
{
    internal sealed class LocalEncounterLibrary
    {
        private readonly string _directory;
        private readonly Dictionary<string, LocalPreset> _presets = new Dictionary<string, LocalPreset>();
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        internal IEnumerable<LocalPreset> Presets => _presets.Values;

        internal LocalEncounterLibrary(string directory) => _directory = directory;

        internal static string Read(string path)
        {
            using (var file = File.OpenRead(path))
            {
                if (file.Length > 1024 * 1024)
                    throw new InvalidDataException("描述文件超过 1 MiB。");
                using (var reader = new StreamReader(file, Utf8, detectEncodingFromByteOrderMarks: false))
                {
                    if (reader.Peek() == 0xfeff)
                        reader.Read();
                    return reader.ReadToEnd();
                }
            }
        }

        private static string FileName(string id) => "enc-" + id + ".json";

        internal void Load(Action<LocalEncounter> validateResources, Action<string> warn)
        {
            _presets.Clear();
            if (!Directory.Exists(_directory))
                return;
            foreach (var file in Directory.GetFiles(_directory, "enc-*.json"))
            {
                try
                {
                    var encounter = LocalEncounter.Parse(Read(file));
                    if (Path.GetFileName(file) != FileName(encounter.Id))
                        throw new InvalidDataException("文件名与内容 ID 不符。");
                    validateResources(encounter);
                    _presets.Add(encounter.Id, new LocalPreset(encounter, File.GetLastWriteTime(file)));
                }
                catch (Exception error)
                {
                    warn($"跳过本地描述文件 {Path.GetFileName(file)}：{error.Message}");
                }
            }
        }

        internal LocalPreset Import(string json, Action<LocalEncounter> validateResources, Func<string, bool> confirmReplace)
        {
            var encounter = LocalEncounter.Parse(json);
            validateResources(encounter);
            var path = Path.Combine(_directory, FileName(encounter.Id));
            Directory.CreateDirectory(_directory);
            var stored = new FileInfo(path);
            var bytes = Utf8.GetBytes(json);
            if (!stored.Exists || stored.Length != bytes.Length || !File.ReadAllBytes(path).SequenceEqual(bytes))
            {
                if (stored.Exists && !confirmReplace(_presets.TryGetValue(encounter.Id, out var old) ? old.Name : encounter.Id))
                    return null;
                var temporary = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    File.WriteAllBytes(temporary, bytes);
                    if (stored.Exists)
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
            var preset = new LocalPreset(encounter, File.GetLastWriteTime(path));
            _presets[encounter.Id] = preset;
            return preset;
        }
    }
}
