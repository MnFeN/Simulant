using Lumina.Data;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Simulant.Game.Data
{
    /// <summary>从当前客户端的场景文件解析区域天气和槽位绑定的 MapEffect 时间轴。</summary>
    internal sealed class TerritoryEnvironmentData
    {
        internal TerritoryEnvironmentData(string name, WeatherChoice[] weather, Dictionary<int, MapEffectResource> mapEffects)
        {
            Name = name;
            Weather = weather;
            MapEffects = mapEffects;
        }

        internal string Name { get; }
        internal WeatherChoice[] Weather { get; }
        internal Dictionary<int, MapEffectResource> MapEffects { get; }

        internal static TerritoryEnvironmentData Load(GameDataService game, uint territoryId, (int Index, uint LayoutId)[] slots)
        {
            var territory = game.GetSheet<TerritoryType>().GetRowOrDefault(territoryId);
            var name = territory?.PlaceName.ValueNullable?.Name.ExtractText();
            var scene = ReadScene(game, territory?.Bg.ExtractText());
            var weatherIds = ReadWeatherIds(scene.File, scene.Body);
            if (weatherIds.Length == 0 && territory?.WeatherRate.ValueNullable is WeatherRate rate)
                weatherIds = rate.Weather.Select(row => row.RowId).Where(IsWeatherId).Select(id => (byte)id).Distinct().OrderBy(id => id).ToArray();

            var weatherSheet = game.GetSheet<Weather>();
            var weathers = weatherIds.Select(id =>
            {
                var row = weatherSheet.GetRowOrDefault(id);
                return new WeatherChoice(id, row?.Name.ExtractText(), row?.Icon ?? 0);
            }).ToArray();

            var resources = new Dictionary<int, MapEffectResource>();
            if (slots.Length > 0)
            {
                var paths = ReadSharedGroups(game, scene.File, scene.Body, new HashSet<uint>(slots.Select(slot => slot.LayoutId)));
                var timelines = new Dictionary<string, MapEffectTimeline[]>(StringComparer.OrdinalIgnoreCase);
                foreach (var slot in slots)
                {
                    if (!paths.TryGetValue(slot.LayoutId, out var path))
                        continue;

                    if (!timelines.TryGetValue(path, out var states))
                    {
                        var sgb = new LayoutFile(game.GetFile<FileResource>(path)?.Data);
                        states = ReadTimelines(sgb, sgb.SceneBody);
                        timelines.Add(path, states);
                    }

                    resources.Add(slot.Index, new MapEffectResource(path, states));
                }
            }

            return new TerritoryEnvironmentData(string.IsNullOrWhiteSpace(name) ? $"区域 {territoryId}" : name, weathers, resources);
        }

        private static (LayoutFile File, int Body) ReadScene(GameDataService game, string background)
        {
            var file = new LayoutFile(string.IsNullOrEmpty(background) ? null : game.GetFile<FileResource>($"bg/{background}.lvb")?.Data);
            return (file, file.SceneBody);
        }

        // LVB 的 SCN1 设置引用一张 32 项的 UInt16 天气表，筛选规则与 Hyperborea 一致。
        private static byte[] ReadWeatherIds(LayoutFile file, int scene)
        {
            if (scene < 0)
                return Array.Empty<byte>();

            var settings = file.Follow(scene, scene + 0x08);
            var table = file.Follow(settings, settings + 0x40);
            return file.ContainsArray(table, 32, 2)
                ? Enumerable.Range(0, 32).Select(i => (uint)file.UInt16(table + i * 2)).Where(IsWeatherId)
                    .Select(id => (byte)id).Distinct().OrderBy(id => id).ToArray()
                : Array.Empty<byte>();
        }

        private static bool IsWeatherId(uint id) => id > 0 && id < byte.MaxValue;

        // 运行时槽位给出 LayoutId。沿 LVB 引用的 LGB 查找同编号的 SharedGroup 实例即可定位 SGB 路径。
        private static Dictionary<uint, string> ReadSharedGroups(GameDataService game, LayoutFile sceneFile, int scene, HashSet<uint> wanted)
        {
            var matches = new Dictionary<uint, string>();
            if (scene < 0)
                return matches;

            var references = sceneFile.Follow(scene, scene + 0x14);
            var referenceCount = sceneFile.Int32(scene + 0x18);
            if (!sceneFile.ContainsArray(references, referenceCount, 4))
                return matches;

            for (var i = 0; i < referenceCount && wanted.Count > 0; i++)
            {
                var path = sceneFile.StringAt(sceneFile.Follow(references, references + i * 4));
                if (!path.EndsWith(".lgb", StringComparison.OrdinalIgnoreCase))
                    continue;

                var layerFile = new LayoutFile(game.GetFile<FileResource>(path)?.Data);
                if (!layerFile.HasTag(12, "LGP1"))
                    continue;

                const int group = 20;
                var layers = layerFile.Follow(group, group + 8);
                var layerCount = layerFile.Int32(group + 12);
                if (!layerFile.ContainsArray(layers, layerCount, 4))
                    continue;

                for (var layerIndex = 0; layerIndex < layerCount && wanted.Count > 0; layerIndex++)
                {
                    var layer = layerFile.Follow(layers, layers + layerIndex * 4);
                    var entries = layerFile.Follow(layer, layer + 8);
                    var count = layerFile.Int32(layer + 12);
                    if (!layerFile.ContainsArray(entries, count, 4))
                        continue;

                    for (var j = 0; j < count; j++)
                    {
                        var entry = layerFile.Follow(entries, entries + j * 4);
                        if (entry < 0 || layerFile.Int32(entry) != (int)Lumina.Data.Parsing.Layer.LayerEntryType.SharedGroup)
                            continue;

                        var id = unchecked((uint)layerFile.Int32(entry + 4));
                        if (!wanted.Contains(id))
                            continue;

                        var sgb = layerFile.StringAt(layerFile.Follow(entry, entry + 48));
                        if (sgb.EndsWith(".sgb", StringComparison.OrdinalIgnoreCase))
                        {
                            matches.Add(id, sgb);
                            wanted.Remove(id);
                        }
                    }
                }
            }

            return matches;
        }

        // SGB 的 SCN1 配置保存 16 个状态位对应的时间轴编号，每条时间轴记录都有相对名称偏移。
        private static MapEffectTimeline[] ReadTimelines(LayoutFile file, int scene)
        {
            if (scene < 0)
                return Array.Empty<MapEffectTimeline>();

            var mappings = file.Follow(scene, scene + 0x20);
            const int bitCount = 16;
            if (!file.ContainsArray(mappings, 5 + bitCount, 1))
                return Array.Empty<MapEffectTimeline>();

            const int timelineSize = 44;
            var timelineTable = file.Follow(scene, scene + 0x10);
            var records = file.Follow(timelineTable, timelineTable);
            var count = file.Int32(timelineTable + 4);
            var names = new Dictionary<int, string>();
            if (file.ContainsArray(records, count, timelineSize))
            {
                for (var i = 0; i < count; i++)
                {
                    var record = records + i * timelineSize;
                    names[file.Int32(record)] = file.StringAt(file.Follow(record, record + 4));
                }
            }

            var results = new List<MapEffectTimeline>();
            for (var bit = 0; bit < bitCount; bit++)
            {
                var subId = file.ByteAt(mappings + 5 + bit);
                if (subId != 0)
                {
                    names.TryGetValue(subId, out var name);
                    results.Add(new MapEffectTimeline((ushort)(1 << bit), subId, name));
                }
            }

            return results.ToArray();
        }

        private sealed class LayoutFile
        {
            private readonly byte[] _data;

            internal LayoutFile(byte[] data) => _data = data ?? Array.Empty<byte>();

            internal int SceneBody => HasTag(12, "SCN1") ? 20 : HasTag(32, "SCN1") ? 40 : -1;

            internal bool ContainsArray(int offset, int count, int size)
                => offset >= 0 && offset <= _data.Length && count >= 0 && count <= (_data.Length - offset) / size;

            internal int Int32(int offset) => ContainsArray(offset, 1, 4) ? BitConverter.ToInt32(_data, offset) : 0;
            internal ushort UInt16(int offset) => BitConverter.ToUInt16(_data, offset);
            internal byte ByteAt(int offset) => _data[offset];

            internal int Follow(int basis, int field)
            {
                var delta = Int32(field);
                var target = (long)basis + delta;
                return basis >= 0 && delta > 0 && target < _data.Length ? (int)target : -1;
            }

            internal bool HasTag(int offset, string tag)
                => ContainsArray(offset, tag.Length, 1) && Encoding.ASCII.GetString(_data, offset, tag.Length) == tag;

            internal string StringAt(int offset)
            {
                if (!ContainsArray(offset, 1, 1))
                    return string.Empty;

                var length = Array.IndexOf(_data, (byte)0, offset) - offset;
                return length > 0 ? Encoding.UTF8.GetString(_data, offset, length) : string.Empty;
            }
        }
    }

    internal readonly struct WeatherChoice
    {
        internal WeatherChoice(byte id, string name, int icon)
        {
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? $"天气 {id}" : name;
            Icon = icon;
        }

        internal byte Id { get; }
        internal string Name { get; }
        internal int Icon { get; }
    }

    internal sealed class MapEffectResource
    {
        internal MapEffectResource(string path, MapEffectTimeline[] timelines)
        {
            Path = path;
            Timelines = timelines;
        }

        internal string Path { get; }
        internal string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
        internal MapEffectTimeline[] Timelines { get; }
    }

    internal readonly struct MapEffectTimeline
    {
        internal MapEffectTimeline(ushort mask, byte subId, string name)
        {
            Mask = mask;
            Name = string.IsNullOrWhiteSpace(name) ? $"时间轴 #{subId}" : name;
        }

        internal ushort Mask { get; }
        internal string Name { get; }
    }
}
