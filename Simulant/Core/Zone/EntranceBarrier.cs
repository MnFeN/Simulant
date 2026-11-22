using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;
using Simulant.Game.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulant.Core.Zone
{
    internal static class EntranceBarrier
    {
        private const string BarrierDirectory = "bgcommon/world/lvd/shared/for_vfx/";
        private const string DeepDungeonBarrierPath = BarrierDirectory + "sgvf_w_lvd_b0945.sgb";
        private static readonly HashSet<string> BarrierPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            BarrierDirectory + "sgvf_w_lvd_b0095.sgb", // 多数副本入口圈
            BarrierDirectory + "sgvf_w_lvd_b0095_fly.sgb", // 个别地图使用的，如 656 云冠群岛的特殊版本
            BarrierDirectory + "sgvf_w_lvd_b0286.sgb" // 团本
        };

        internal static (uint[] LayoutIds, (uint LayoutId, LayerCommon.Layer Layer, LayerCommon.InstanceObject Object)[] Circles)
            Scan(TerritoryLayoutData layout, TerritoryType territory, uint layerSetKey, LayerCommon.Layer[] layers)
        {
            var use = (TerritoryIntendedUseEnum)territory.TerritoryIntendedUse.RowId;
            var content = territory.ContentFinderCondition.ValueNullable?.Content;
            var publicContent = content?.GetValueOrDefault<PublicContent>();
            var groups = new HashSet<uint>();
            if (use == TerritoryIntendedUseEnum.DeepDungeon)
            {
                // 这些圈属于房间模板，楼层生成后才有实际坐标。
                groups.UnionWith(layers.SelectMany(layer => layer.InstanceObjects)
                    .Where(item => item.Object is LayerCommon.SharedGroupInstanceObject group &&
                        string.Equals(group.AssetPath, DeepDungeonBarrierPath, StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.InstanceId));
            }

            var isRaid = use == TerritoryIntendedUseEnum.LargeScaleRaid || use == TerritoryIntendedUseEnum.LargeScaleSavageRaid;
            var isDuty = content?.Is<InstanceContent>() == true || content?.Is<PartyContent>() == true || isRaid;
            var isScenario = layerSetKey != 0 && use != TerritoryIntendedUseEnum.OpenWorld && !publicContent.HasValue;
            var eventObjects = layers.Where(layer => isDuty ||
                    (isScenario && layer.LayerSetReferencedList.ReferencedType == LayerSetReferencedType.Include))
                .SelectMany(layer => layer.InstanceObjects.Where(item =>
                    item.Object is LayerCommon.EventInstanceObject obj && obj.BoundInstanceId != 0)
                    .Select(item => (LayoutId: ((LayerCommon.EventInstanceObject)item.Object).BoundInstanceId, Layer: layer, Object: item)))
                .ToArray();

            var circles = new List<(uint LayoutId, LayerCommon.Layer Layer, LayerCommon.InstanceObject Object)>();
            if (eventObjects.Length != 0)
            {
                var paths = layout.ReadSharedGroups(eventObjects.Select(entry => entry.LayoutId));
                var circleIds = new HashSet<uint>();
                foreach (var entry in eventObjects)
                {
                    if (paths.TryGetValue(entry.LayoutId, out var resource) && BarrierPaths.Contains(resource) && circleIds.Add(entry.LayoutId))
                    {
                        groups.Add(entry.LayoutId);
                        circles.Add(entry);
                    }
                }
            }

            return (groups.ToArray(), circles.ToArray());
        }
    }
}
