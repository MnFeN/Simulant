using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;
using Simulant.Core.Entity;
using Simulant.Game.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Core.Zone
{
    /// <summary>按场地资源绑定识别出生圈，不把同一 EObj 表行的其他机关当作入口。</summary>
    internal static class EntranceBarrier
    {
        // 已使用沙斯塔夏、假火神、各绝本、异闻下水道核对，均使用下述路径
        private const string BarrierPath = "bgcommon/world/lvd/shared/for_vfx/sgvf_w_lvd_b0095.sgb";

        internal static EventObjectSpawnSpec[] Load(GameDataService data, TerritoryType territory)
        {
            var bg = territory.Bg.ExtractText();
            var separator = bg.LastIndexOf('/');

            if (separator < 0)
                return Array.Empty<EventObjectSpawnSpec>();

            var file = data.GetFile<LgbFile>("bg/" + bg.Substring(0, separator + 1) + "planmap.lgb");

            if (file == null)
                return Array.Empty<EventObjectSpawnSpec>();

            using (file.Reader)
                return Find(file.Layers.SelectMany(layer => layer.InstanceObjects));
        }

        internal static EventObjectSpawnSpec[] Find(IEnumerable<LayerCommon.InstanceObject> instances)
        {
            var objects = instances.ToArray();
            var walls = new HashSet<uint>(objects.Where(instance =>
                instance.Object is LayerCommon.SharedGroupInstanceObject group &&
                string.Equals(group.AssetPath, BarrierPath, StringComparison.OrdinalIgnoreCase))
                .Select(instance => instance.InstanceId));
            var result = new List<EventObjectSpawnSpec>();

            foreach (var instance in objects)
            {
                if (!(instance.Object is LayerCommon.EventInstanceObject obj) || !walls.Contains(obj.BoundInstanceId))
                    continue;

                var position = instance.Transform.Translation;
                result.Add(new EventObjectSpawnSpec
                {
                    BaseId = obj.ParentData.BaseId,
                    LayoutId = instance.InstanceId,
                    GimmickId = obj.BoundInstanceId,
                    Pos = new Vector3(position.X, position.Z, position.Y),
                    Heading = instance.Transform.Rotation.Y
                });
            }

            return result.ToArray();
        }
    }
}
