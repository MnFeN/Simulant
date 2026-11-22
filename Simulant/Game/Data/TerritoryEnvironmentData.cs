using Lumina.Data;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

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
            var layout = new TerritoryLayoutData(game, territory?.Bg.ExtractText());
            var weatherIds = ReadWeatherIds(layout.File, territory);

            var weatherSheet = game.GetSheet<Weather>();
            var weathers = weatherIds.OrderBy(id => id).Select(id =>
            {
                var row = weatherSheet.GetRowOrDefault(id);
                return new WeatherChoice(id, row?.Name.ExtractText(), row?.Icon ?? 0);
            }).ToArray();

            var resources = new Dictionary<int, MapEffectResource>();
            if (slots.Length > 0)
            {
                var paths = layout.ReadSharedGroups(slots.Select(slot => slot.LayoutId));
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

        internal static (byte Weather, ushort Bgm, int RequiredMapEffectCount) ReadDefaults(GameDataService game, uint territoryId)
        {
            if (!game.GetSheet<TerritoryType>().TryGetRow(territoryId, out var territory))
                return default;

            var layout = new TerritoryLayoutData(game, territory.Bg.ExtractText());
            var weatherIds = ReadWeatherIds(layout.File, territory);
            var weather = weatherIds.FirstOrDefault(id => id > 2); // 天气 2 有时存在但光照看起来不对
            if (weather == 0)
                weather = weatherIds.DefaultIfEmpty().Max();
            var instance = territory.ContentFinderCondition.ValueNullable?.Content.GetValueOrDefault<InstanceContent>();
            var bgm = instance?.BGM.RowId ?? 0;
            if (bgm == 0)
            {
                var music = territory.BGM;
                bgm = music.Is<BGM>() ? music.RowId
                    : music.GetValueOrDefault<BGMSituation>()?.DaytimeID.RowId ?? 0;
            }

            var groups = instance?.ContentDirectorManagedSG;
            var requiredCount = 0;
            if (groups.HasValue && groups.Value.RowId != 0 && groups.Value.TryGetValue(out var managedGroups))
            {
                foreach (var group in managedGroups)
                    if (group.LayoutId != 0)
                        requiredCount = Math.Max(requiredCount, group.SubrowId + 1);
            }

            return (weather, (ushort)bgm, requiredCount);
        }

        // LVB 的 SCN1 设置引用一张 32 项的 UInt16 天气表，筛选规则参考 Hyperborea
        private static byte[] ReadWeatherIds(LayoutFile file, TerritoryType? territory)
        {
            var scene = file.SceneBody;
            var settings = file.Follow(scene, scene + 0x08);
            var table = file.Follow(settings, settings + 0x40);
            var weatherIds = file.ContainsArray(table, 32, 2)
                ? Enumerable.Range(0, 32).Select(i => (uint)file.UInt16(table + i * 2)).Where(IsWeatherId)
                    .Select(id => (byte)id).Distinct().ToArray()
                : Array.Empty<byte>();

            if (weatherIds.Length == 0 && territory?.WeatherRate.ValueNullable is WeatherRate rate)
                return rate.Weather.Select(row => row.RowId).Where(IsWeatherId).Select(id => (byte)id).Distinct().ToArray();

            return weatherIds;
        }

        private static bool IsWeatherId(uint id) => id > 0 && id < byte.MaxValue;

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
