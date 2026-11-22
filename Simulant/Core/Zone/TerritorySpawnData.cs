using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;
using Simulant.Game.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Core.Zone
{
    internal sealed class TerritorySpawnData
    {
        private const uint GuildhestContentType = 3;
        internal static readonly Vector3 GreenScreenPosition = new Vector3(0, 0, 0.1f);

        private TerritorySpawnData(uint[] barrierLayoutIds, SpawnPoint[] points)
        {
            BarrierLayoutIds = barrierLayoutIds;
            Points = points;
        }

        internal uint[] BarrierLayoutIds { get; }
        internal SpawnPoint[] Points { get; }
        internal SpawnPoint Recommended => Points.Length == 0 ? null : Points[0];

        /// <summary>Scans the active zone layers and ranks the available player spawn points.</summary>
        internal static TerritorySpawnData Scan(GameDataService data, TerritoryType territory)
        {
            if (territory.RowId == GreenScreenStage.TerritoryId)
                return new TerritorySpawnData(Array.Empty<uint>(), new[] { new SpawnPoint(GreenScreenPosition, SpawnPointSource.GreenScreen) });

            var layout = new TerritoryLayoutData(data, territory.Bg.ExtractText());
            var layerSetKey = layout.ReadLayerSetKey(territory);
            var layers = layout.ReadPlacementLayers(layerSetKey);
            var (barrierIds, circles) = EntranceBarrier.Scan(layout, territory, layerSetKey, layers);
            var content = territory.ContentFinderCondition.ValueNullable?.Content;
            var publicContent = content?.GetValueOrDefault<PublicContent>();
            var isGuildhest = content?.GetValueOrDefault<InstanceContent>()?.InstanceContentType.RowId == GuildhestContentType;
            var points = new List<SpawnPoint>();
            var positions = new HashSet<Vector3>();
            var popRanges = layers.SelectMany(layer => layer.InstanceObjects).Where(IsPlayerPop).ToArray();

            void Add(Vector3? position, SpawnPointSource source, EntranceCircle circle = null)
            {
                if (position.HasValue && (positions.Add(position.Value) || circle != null))
                    points.Add(new SpawnPoint(position.Value, source, circle));
            }

            Vector3? FindPop(uint id)
            {
                var pop = popRanges.FirstOrDefault(item => item.InstanceId == id);
                return pop.InstanceId != 0 ? (Vector3?)Position(pop) : null;
            }

            Add(FindPop(publicContent?.LGBPopRange ?? 0), SpawnPointSource.PublicContent);
            foreach (var circle in circles)
            {
                var position = Position(circle.Object);
                // 行会令的圈和玩家出生范围都在各自的事件层。
                var spawn = isGuildhest ? NearestPop(circle.Layer.InstanceObjects, position) ?? position : position;
                Add(spawn, SpawnPointSource.EntranceCircle, new EntranceCircle(circle.LayoutId, position));
            }

            // 深层迷宫的房间模板坐标无法直接用作当前楼层的落点。
            if (territory.TerritoryIntendedUse.RowId == (uint)TerritoryIntendedUseEnum.DeepDungeon)
                return new TerritorySpawnData(barrierIds, points.ToArray());

            var exitId = publicContent?.LGBEventRange ?? 0;
            foreach (var layer in layers)
            {
                var exit = layer.InstanceObjects.FirstOrDefault(item => item.InstanceId == exitId);
                if (exit.InstanceId != 0)
                    Add(NearestPop(layer.InstanceObjects, Position(exit)) ?? NearestPop(popRanges, Position(exit)), SpawnPointSource.PublicContentExit);
            }

            var crystalId = territory.Aetheryte.RowId;
            var crystals = layers.SelectMany(layer => layer.InstanceObjects
                .Where(item => item.Object is LayerCommon.AetheryteInstanceObject)
                .Select(item => (Layer: layer, Object: item)))
                .OrderBy(entry => ((LayerCommon.AetheryteInstanceObject)entry.Object.Object).ParentData.BaseId != crystalId);
            foreach (var entry in crystals)
                Add(NearestPop(entry.Layer.InstanceObjects, Position(entry.Object)), SpawnPointSource.Aetheryte);

            foreach (var warp in data.GetSheet<Warp>().Where(row => row.TerritoryType.RowId == territory.RowId && row.PopRange.RowId != 0))
                Add(FindPop(warp.PopRange.RowId), SpawnPointSource.Warp);

            // 有些独立场景（包括任务战斗）直接把玩家落点存在 Level 表里。
            var levelSpawns = data.GetSheet<Level>().Where(row =>
                    row.Type == (byte)LayerEntryType.PopRange && row.Territory.RowId == territory.RowId)
                .Select(row => new Vector3(row.X, row.Z, row.Y)).Distinct().ToArray();
            if (levelSpawns.Length == 1)
                Add(levelSpawns[0], SpawnPointSource.Level);

            foreach (var layer in layers.Where(layer =>
                layer.Name.StartsWith("LVD_entrance_", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(layer.Name, "PC_POPRANGE", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var pop in layer.InstanceObjects.Where(IsPlayerPop))
                    Add(Position(pop), SpawnPointSource.EntranceRange);
            }

            foreach (var position in levelSpawns)
                Add(position, SpawnPointSource.Level);
            foreach (var pop in popRanges)
                Add(Position(pop), SpawnPointSource.PlayerRange);

            return new TerritorySpawnData(barrierIds, points.ToArray());
        }

        private static bool IsPlayerPop(LayerCommon.InstanceObject item)
            => item.Object is LayerCommon.PopRangeInstanceObject pop && pop.PopType == PopType.PC;

        private static Vector3? NearestPop(IEnumerable<LayerCommon.InstanceObject> objects, Vector3 origin)
            => objects.Where(IsPlayerPop).Select(Position).OrderBy(position => Vector3.DistanceSquared(position, origin))
                .Select(position => (Vector3?)position).FirstOrDefault();

        private static Vector3 Position(LayerCommon.InstanceObject item)
        {
            var position = item.Transform.Translation;
            return new Vector3(position.X, position.Z, position.Y);
        }
    }
}
