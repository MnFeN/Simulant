using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;
using Simulant.Game.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Core.Zone
{
    internal sealed class MapCenterSpawn
    {
        private const float GridStep = 2f;
        private const float MaxNeighborRise = 1f;
        private const int MinFloorSamples = 9;
        private const int MaxCenters = 8;
        private const float FootRadius = 0.55f;
        private const float Headroom = 2f;
        private static readonly (int X, int Z)[] Directions = { (-1, 0), (1, 0), (0, -1), (0, 1) };
        private static readonly Vector2[] Footprint =
        {
            Vector2.Zero, new Vector2(-FootRadius, 0), new Vector2(FootRadius, 0),
            new Vector2(0, -FootRadius), new Vector2(0, FootRadius)
        };

        private readonly List<Triangle> _triangles = new List<Triangle>();
        private readonly Dictionary<(int X, int Z), List<Sample>> _grid = new Dictionary<(int, int), List<Sample>>();

        internal static IEnumerable<Vector3> Find(GameDataService data, TerritoryType territory, TerritoryLayoutData layout,
            uint layerSetKey, LayerCommon.Layer[] layers)
        {
            var background = territory.Bg.ExtractText();
            if (string.IsNullOrWhiteSpace(background))
                return Array.Empty<Vector3>();

            var related = data.GetSheet<TerritoryType>().Where(row =>
                row.RowId != territory.RowId && row.IsInUse && row.Bg.ExtractText() == background).ToArray();
            var focus = FindActivityBounds(layerSetKey, layers);
            var scan = new MapCenterSpawn();
            if (related.Length == 0 && scan.Read(data, layout, layerSetKey, layers, null, 1600f))
                return scan.SelectCenters();

            // 大范围或复用背景的区域需要活动图层定位。同一张室内小地图的变体可共用静态地面。
            if (!focus.HasValue && (related.Length == 0 || territory.Map.RowId == 0 ||
                related.Any(row => row.Map.RowId != territory.Map.RowId || row.PlaceName.RowId != territory.PlaceName.RowId)))
                return Array.Empty<Vector3>();

            scan = new MapCenterSpawn();
            return scan.Read(data, layout, layerSetKey, layers, focus, focus.HasValue ? 1600f : 160f)
                ? scan.SelectCenters() : Array.Empty<Vector3>();
        }

        private static (Vector3 Min, Vector3 Max)? FindActivityBounds(uint layerSetKey, LayerCommon.Layer[] layers)
        {
            if (layerSetKey == 0)
                return null;

            var anchors = layers.Where(layer => layer.LayerSetReferencedList.ReferencedType == LayerSetReferencedType.Include)
                .SelectMany(layer => layer.InstanceObjects)
                .Where(item => item.Object is LayerCommon.EventInstanceObject || item.Object is LayerCommon.EventRangeInstanceObject)
                .Select(item => item.Transform.Translation)
                .Select(position => new Vector3(position.X, position.Y, position.Z))
                .Where(IsFinite)
                .Distinct().ToArray();
            if (anchors.Length == 0)
                return null;

            // 活动物件可能有零星落在活动区域之外。点位足够时取中间 80% 的范围。
            var trim = anchors.Length / 10;
            var x = anchors.Select(anchor => anchor.X).OrderBy(value => value).ToArray();
            var y = anchors.Select(anchor => anchor.Y).OrderBy(value => value).ToArray();
            var z = anchors.Select(anchor => anchor.Z).OrderBy(value => value).ToArray();
            var padding = new Vector3(12, 6, 12);
            var end = anchors.Length - 1 - trim;
            return (new Vector3(x[trim], y[trim], z[trim]) - padding, new Vector3(x[end], y[end], z[end]) + padding);
        }

        private bool Read(GameDataService data, TerritoryLayoutData layout, uint layerSetKey, LayerCommon.Layer[] layers,
            (Vector3 Min, Vector3 Max)? focus, float maxExtent)
        {
            var min = new Vector2(float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity);
            (Vector2 Min, Vector2 Max)? horizontalBounds = null;
            if (focus.HasValue)
                horizontalBounds = (new Vector2(focus.Value.Min.X, focus.Value.Min.Z),
                    new Vector2(focus.Value.Max.X, focus.Value.Max.Z));

            foreach (var (a, b, c, material) in TerritoryCollisionData.Read(data, layout, layerSetKey, layers, horizontalBounds))
            {
                if (!IsFinite(a) || !IsFinite(b) || !IsFinite(c))
                    continue;

                var triangle = new Triangle(a, b, c, material);
                var low = new Vector2(triangle.Min.X, triangle.Min.Z);
                var high = new Vector2(triangle.Max.X, triangle.Max.Z);
                if (focus.HasValue)
                {
                    low = Vector2.Max(low, horizontalBounds.Value.Min);
                    high = Vector2.Min(high, horizontalBounds.Value.Max);
                    if (triangle.Max.Y < focus.Value.Min.Y - 1 || triangle.Min.Y > focus.Value.Max.Y + Headroom)
                        continue;
                }
                if (low.X > high.X || low.Y > high.Y)
                    continue;

                _triangles.Add(triangle);
                if (!triangle.IsFloor)
                    continue;

                min = Vector2.Min(min, low);
                max = Vector2.Max(max, high);
                var size = max - min;
                // 限制无可靠活动范围的大地图扫描以及网格内存开销。
                if (size.X > maxExtent || size.Y > maxExtent || size.X * size.Y > 1_000_000)
                    return false;
            }

            if (max.X < min.X)
                return false;

            foreach (var triangle in _triangles.Where(item => item.IsFloor))
                AddSamples(triangle, min, max, focus?.Min.Y ?? float.NegativeInfinity, focus?.Max.Y ?? float.PositiveInfinity);
            return _grid.Count != 0;
        }

        private void AddSamples(Triangle triangle, Vector2 min, Vector2 max, float minHeight, float maxHeight)
        {
            var x0 = (int)Math.Ceiling(Math.Max(min.X, triangle.Min.X) / GridStep);
            var z0 = (int)Math.Ceiling(Math.Max(min.Y, triangle.Min.Z) / GridStep);
            var x1 = (int)Math.Floor(Math.Min(max.X, triangle.Max.X) / GridStep);
            var z1 = (int)Math.Floor(Math.Min(max.Y, triangle.Max.Z) / GridStep);
            for (var x = x0; x <= x1; x++)
                for (var z = z0; z <= z1; z++)
                {
                    if (!triangle.TryHeight(x * GridStep, z * GridStep, out var y) || y < minHeight || y > maxHeight)
                        continue;

                    if (!_grid.TryGetValue((x, z), out var floors))
                        _grid.Add((x, z), floors = new List<Sample>());
                    if (!floors.Any(other => Math.Abs(other.Height - y) < 0.1f))
                        floors.Add(new Sample(x, z, y));
                }
        }

        private IEnumerable<Vector3> SelectCenters()
        {
            var groups = new List<List<Sample>>();
            foreach (var start in _grid.Values.SelectMany(floors => floors))
            {
                if (start.Visited)
                    continue;
                var group = FloodFloor(start);
                if (group.Count >= MinFloorSamples)
                    groups.Add(group);
            }

            var emitted = 0;
            foreach (var group in groups.OrderByDescending(floor => floor.Count))
            {
                var x = group.Average(cell => cell.X);
                var z = group.Average(cell => cell.Z);
                foreach (var sample in group.Where(cell => cell.EdgeDistance >= 2).OrderByDescending(cell => cell.EdgeDistance)
                    .ThenBy(cell => (cell.X - x) * (cell.X - x) + (cell.Z - z) * (cell.Z - z)).Take(32))
                {
                    var position = new Vector3(sample.X * GridStep, sample.Height, sample.Z * GridStep);
                    if (!CanStand(position))
                        continue;

                    yield return new Vector3(position.X, position.Z, position.Y + 0.05f);
                    if (++emitted == MaxCenters)
                        yield break;
                    break;
                }
            }
        }

        private List<Sample> FloodFloor(Sample start)
        {
            var floor = new List<Sample>();
            var pending = new Queue<Sample>();
            var edge = new Queue<Sample>();
            start.Visited = true;
            pending.Enqueue(start);
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                floor.Add(cell);
                if (VisitNeighbors(cell, neighbor =>
                {
                    if (neighbor.Visited)
                        return;
                    neighbor.Visited = true;
                    pending.Enqueue(neighbor);
                }) == Directions.Length)
                    continue;
                cell.EdgeDistance = 1;
                edge.Enqueue(cell);
            }

            // 从地面边缘向内计算距离，同一位置的上下层按高度分别连通。
            while (edge.Count > 0)
            {
                var cell = edge.Dequeue();
                VisitNeighbors(cell, neighbor =>
                {
                    if (neighbor.EdgeDistance != 0)
                        return;
                    neighbor.EdgeDistance = cell.EdgeDistance + 1;
                    edge.Enqueue(neighbor);
                });
            }
            return floor;
        }

        private int VisitNeighbors(Sample cell, Action<Sample> visit)
        {
            var count = 0;
            foreach (var (x, z) in Directions)
            {
                if (!_grid.TryGetValue((cell.X + x, cell.Z + z), out var floors))
                    continue;
                var touches = false;
                foreach (var neighbor in floors)
                {
                    if (Math.Abs(neighbor.Height - cell.Height) > MaxNeighborRise)
                        continue;
                    visit(neighbor);
                    touches = true;
                }
                if (touches)
                    count++;
            }
            return count;
        }

        private bool CanStand(Vector3 position)
        {
            var supported = 0;
            foreach (var triangle in _triangles)
            {
                if (triangle.Max.X < position.X - FootRadius || triangle.Min.X > position.X + FootRadius ||
                    triangle.Max.Z < position.Z - FootRadius || triangle.Min.Z > position.Z + FootRadius ||
                    triangle.Max.Y < position.Y - 0.4f || triangle.Min.Y > position.Y + Headroom)
                    continue;

                for (var i = 0; i < Footprint.Length; i++)
                {
                    var offset = Footprint[i];
                    if (!triangle.TryHeight(position.X + offset.X, position.Z + offset.Y, out var y))
                        continue;

                    var delta = y - position.Y;
                    if (delta > 0.4f && delta < Headroom)
                        return false;
                    if (triangle.IsFloor && Math.Abs(delta) <= 0.4f)
                        supported |= 1 << i;
                }

                foreach (var (x, z) in Directions)
                {
                    var ray = new Vector3(x * FootRadius, 0, z * FootRadius);
                    if (triangle.Blocks(position + new Vector3(0, 0.4f, 0), ray)
                        || triangle.Blocks(position + new Vector3(0, 1.5f, 0), ray))
                        return false;
                }
            }
            return supported == (1 << Footprint.Length) - 1;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector3 value) => IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);

        private sealed class Sample
        {
            internal Sample(int x, int z, float height)
            {
                X = x;
                Z = z;
                Height = height;
            }

            internal readonly int X;
            internal readonly int Z;
            internal readonly float Height;
            internal bool Visited;
            internal int EdgeDistance;
        }

        private readonly struct Triangle
        {
            private readonly Vector3 _a;
            private readonly Vector3 _ab;
            private readonly Vector3 _ac;
            private readonly float _normalY;
            internal readonly Vector3 Min;
            internal readonly Vector3 Max;
            internal readonly bool IsFloor;

            internal Triangle(Vector3 a, Vector3 b, Vector3 c, ulong material)
            {
                _a = a;
                _ab = b - a;
                _ac = c - a;
                var normal = Vector3.Cross(_ab, _ac);
                _normalY = normal.Y;
                Min = Vector3.Min(a, Vector3.Min(b, c));
                Max = Vector3.Max(a, Vector3.Max(b, c));
                // 使用碰撞材质过滤孔洞、禁落地面、隐形边界和水体。
                // https://github.com/awgil/ffxiv_navmesh/blob/38d2a24c3306f24640e64efe13915a78d301e878/vnavmesh/SceneExtractor.cs
                const ulong excludedMaterials = 0x02000000 | 0x00200000 | 0x01000000 | 0x00800000 | 0x00100000 | 0x8000;
                IsFloor = (material & excludedMaterials) == 0 && (material & 0x1F) != 0x11
                    && _normalY > 0.72f * normal.Length();
            }

            internal bool TryHeight(float x, float z, out float height)
            {
                height = default;
                if (Math.Abs(_normalY) < 0.00001f)
                    return false;

                x -= _a.X;
                z -= _a.Z;
                var u = (z * _ac.X - x * _ac.Z) / _normalY;
                var v = (x * _ab.Z - z * _ab.X) / _normalY;
                const float tolerance = 0.00001f;
                if (u < -tolerance || v < -tolerance || u + v > 1 + tolerance)
                    return false;

                height = _a.Y + u * _ab.Y + v * _ac.Y;
                return true;
            }

            internal bool Blocks(Vector3 origin, Vector3 direction)
            {
                var cross = Vector3.Cross(direction, _ac);
                var determinant = Vector3.Dot(_ab, cross);
                if (Math.Abs(determinant) < 0.00001f)
                    return false;

                var start = origin - _a;
                var u = Vector3.Dot(start, cross) / determinant;
                var q = Vector3.Cross(start, _ab);
                var v = Vector3.Dot(direction, q) / determinant;
                var distance = Vector3.Dot(_ac, q) / determinant;
                return u >= 0 && v >= 0 && u + v <= 1 && distance >= 0 && distance <= 1;
            }
        }
    }
}
