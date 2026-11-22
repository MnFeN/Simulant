using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulant.Game.Data
{
    internal sealed class TerritoryLayoutData
    {
        private static readonly string[] PlacementFiles = { "planmap.lgb", "planevent.lgb", "planlive.lgb" };
        private readonly GameDataService _game;

        internal TerritoryLayoutData(GameDataService game, string background)
        {
            _game = game;
            background = background ?? string.Empty;
            var separator = background.LastIndexOf('/');
            LevelDirectory = separator < 0 ? string.Empty : "bg/" + background.Substring(0, separator + 1);
            File = new LayoutFile(background.Length == 0 ? null : game.GetFile<FileResource>($"bg/{background}.lvb")?.Data);
        }

        internal LayoutFile File { get; }

        internal string LevelDirectory { get; }

        internal LayerCommon.Layer[] ReadPlacementLayers(uint layerSetKey)
            => ReadLayers(PlacementFiles.Select(name => LevelDirectory + name), layerSetKey).ToArray();

        internal IEnumerable<LayerCommon.Layer> ReadBackgroundLayers(uint layerSetKey)
        {
            var placementPaths = PlacementFiles.Select(name => LevelDirectory + name);
            var paths = ReadReferencedLayers().Except(placementPaths, StringComparer.OrdinalIgnoreCase)
                .DefaultIfEmpty(LevelDirectory + "bg.lgb");
            return ReadLayers(paths, layerSetKey);
        }

        private IEnumerable<LayerCommon.Layer> ReadLayers(IEnumerable<string> paths, uint layerSetKey)
        {
            if (LevelDirectory.Length == 0)
                yield break;

            foreach (var path in paths)
            {
                var file = _game.GetFile<LgbFile>(path);
                if (file == null)
                    continue;
                using (file.Reader)
                    foreach (var layer in file.Layers.Where(layer => layer.FestivalID == 0 && UsesLayer(layer, layerSetKey)))
                        yield return layer;
            }
        }

        private static bool UsesLayer(LayerCommon.Layer layer, uint key)
        {
            if (layer.LayerSetReferencedList.ReferencedType == LayerSetReferencedType.Include)
                return layer.LayerSetReferences.Any(reference => reference.LayerSetId == key);
            if (layer.LayerSetReferencedList.ReferencedType == LayerSetReferencedType.Exclude)
                return layer.LayerSetReferences.All(reference => reference.LayerSetId != key);
            return true;
        }

        internal uint ReadLayerSetKey(TerritoryType territory)
        {
            var file = File;
            var scene = file.SceneBody;
            var table = file.Follow(scene, scene + 0x0C);
            var count = file.Int32(table + 4);
            var records = file.Follow(table + 4, table);
            const int recordSize = 28;
            if (!file.ContainsArray(records, count, recordSize))
                return 0;

            uint territoryKey = 0;
            for (var i = 0; i < count; i++)
            {
                var record = records + i * recordSize;
                // 区域和副本条件各占 16 位，跳跳乐等场景按二者区分版型。
                if (file.UInt16(record + 12) != territory.RowId)
                    continue;

                var key = unchecked((uint)file.Int32(record));
                var cfc = file.UInt16(record + 14);
                if (cfc == territory.ContentFinderCondition.RowId)
                    return key;
                if (cfc == 0 && territoryKey == 0)
                    territoryKey = key;
            }
            return territoryKey;
        }

        private IEnumerable<string> ReadReferencedLayers()
        {
            var sceneFile = File;
            var scene = sceneFile.SceneBody;
            if (scene < 0)
                yield break;

            var references = sceneFile.Follow(scene, scene + 0x14);
            var referenceCount = sceneFile.Int32(scene + 0x18);
            if (!sceneFile.ContainsArray(references, referenceCount, 4))
                yield break;

            for (var i = 0; i < referenceCount; i++)
            {
                var path = sceneFile.StringAt(sceneFile.Follow(references, references + i * 4));
                if (path.EndsWith(".lgb", StringComparison.OrdinalIgnoreCase))
                    yield return path;
            }
        }

        // 沿 LVB 引用的 LGB 按 LayoutId 查找 SharedGroup 的 SGB 路径。
        internal Dictionary<uint, string> ReadSharedGroups(IEnumerable<uint> layoutIds)
        {
            var matches = new Dictionary<uint, string>();
            var wanted = new HashSet<uint>(layoutIds);
            foreach (var path in ReadReferencedLayers())
            {
                if (wanted.Count == 0)
                    break;
                var layerFile = new LayoutFile(_game.GetFile<FileResource>(path)?.Data);
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
                        if (entry < 0 || layerFile.Int32(entry) != (int)LayerEntryType.SharedGroup)
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
    }
}
