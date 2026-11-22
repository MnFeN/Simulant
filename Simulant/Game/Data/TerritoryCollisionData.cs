using Lumina.Data;
using Lumina.Data.Files.Pcb;
using Lumina.Data.Parsing;
using Lumina.Data.Parsing.Layer;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace Simulant.Game.Data
{
    internal static class TerritoryCollisionData
    {
        internal static IEnumerable<(Vector3 A, Vector3 B, Vector3 C, ulong Material)> Read(GameDataService game,
            TerritoryLayoutData layout, uint layerSetKey, LayerCommon.Layer[] placements, (Vector2 Min, Vector2 Max)? searchBounds)
        {
            foreach (var mesh in ReadMeshes(game, layout, layerSetKey, placements, searchBounds))
            {
                var file = game.GetFile<FileResource>(mesh.Path);
                if (file == null)
                    continue;
                using (file.Reader)
                    foreach (var triangle in PcbCollisionMesh.Read(file.Data, mesh.Transform))
                        yield return triangle;
            }
        }

        private static IEnumerable<(string Path, Matrix4x4 Transform)> ReadMeshes(GameDataService game,
            TerritoryLayoutData layout, uint layerSetKey, LayerCommon.Layer[] placements, (Vector2 Min, Vector2 Max)? searchBounds)
        {
            var level = layout.LevelDirectory;
            const string suffix = "level/";
            if (level.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var directory = level.Substring(0, level.Length - suffix.Length) + "collision/";
                var list = game.GetFile<PcbListFile>(directory + "list.pcb");
                if (list != null)
                {
                    using (list.Reader)
                        foreach (var tile in list.Nodes.Children)
                        {
                            if (searchBounds.HasValue &&
                                (tile.BoundingBox.Max.X < searchBounds.Value.Min.X || tile.BoundingBox.Min.X > searchBounds.Value.Max.X ||
                                 tile.BoundingBox.Max.Z < searchBounds.Value.Min.Y || tile.BoundingBox.Min.Z > searchBounds.Value.Max.Y))
                                continue;
                            yield return (directory + "tr" + tile.Id.ToString("D4", CultureInfo.InvariantCulture) + ".pcb", Matrix4x4.Identity);
                        }
                }
            }

            foreach (var layer in layout.ReadBackgroundLayers(layerSetKey).Concat(placements))
                foreach (var instance in layer.InstanceObjects)
                    if (instance.Object is LayerCommon.BGInstanceObject model && model.CollisionType == ModelCollisionType.Replace
                        && model.CollisionAssetPath.EndsWith(".pcb", StringComparison.OrdinalIgnoreCase))
                        yield return (model.CollisionAssetPath, ToMatrix(instance.Transform));
        }

        private static Matrix4x4 ToMatrix(Common.Transformation transform)
        {
            var s = transform.Scale;
            var r = transform.Rotation;
            var t = transform.Translation;
            return Matrix4x4.CreateScale(s.X, s.Y, s.Z) * Matrix4x4.CreateRotationX(r.X) * Matrix4x4.CreateRotationY(r.Y)
                * Matrix4x4.CreateRotationZ(r.Z) * Matrix4x4.CreateTranslation(t.X, t.Y, t.Z);
        }
    }
}
