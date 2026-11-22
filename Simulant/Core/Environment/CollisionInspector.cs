using Simulant.Game;
using Simulant.Game.FFCS.Client.LayoutEngine;
using Simulant.Game.FFCS.Client.System.Framework;
using Simulant.Game.FFCS.Common.Component.BGCollision;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Simulant.Core.Environment
{
    internal sealed class CollisionInspector
    {
        internal const int RayCount = 24;
        internal const float Range = 30f;
        private const ulong CollisionLayer = 1;
        private static readonly float[] RayHeights = { 0.2f, 1f };
        private static readonly InstanceType[] LayoutTypes = { InstanceType.CollisionBox, InstanceType.BgPart, InstanceType.SharedGroup };

        private readonly PluginHost _host;
        private readonly Dictionary<IntPtr, (CollisionHit Hit, ulong Mask)> _disabled = new Dictionary<IntPtr, (CollisionHit, ulong)>();
        private (GameConnection Connection, int Generation, uint Territory, IntPtr Layout) _scope;

        internal CollisionInspector(PluginHost host) => _host = host;

        internal (CollisionHit[] Hits, bool GroundHit, Vector3[] Points) Scan(LiveEnvironment shown, int generation)
        {
            var connection = GameInterop.Connection ?? throw new InvalidOperationException("游戏尚未连接。");
            (CollisionHit[] Hits, bool GroundHit, Vector3[] Points) result = default;
            connection.RunInFrame(() =>
            {
                RequireDeveloperMode();
                if (!ReferenceEquals(connection, GameInterop.Connection) || generation != _host.WorldGeneration ||
                    !_host.EnvironmentService.TryReadCurrent(out var current) || !shown.IsSameRegion(current))
                    throw new InvalidOperationException("区域已变化，请等待加载后再扫描。");

                var player = _host.EntityProvider.GetMyself() ?? throw new InvalidOperationException("找不到当前玩家。");
                var world = LayoutWorld.Instance();
                var active = ReadActiveLayout(world, current.TerritoryId);
                if (active.IsNull())
                    throw new InvalidOperationException("当前场地尚未加载完成。");

                var scope = (connection, generation, current.TerritoryId, active.Ptr);
                if (_scope != scope)
                {
                    _disabled.Clear();
                    _scope = scope;
                }

                var scenes = ReadScenes();
                result = ScanRays(connection, scenes, world, player.Native.Position.Get());
                foreach (var hit in result.Hits)
                {
                    _disabled.Remove(hit.Collider.Ptr);
                }

                foreach (var entry in _disabled.Values.ToArray())
                {
                    var collider = FindCollider(scenes, entry.Hit);
                    if (collider.IsNull() || collider.LayerMask.Get() != 0)
                    {
                        _disabled.Remove(entry.Hit.Collider.Ptr);
                    }
                }

                result.Hits = result.Hits.Concat(_disabled.Values.Select(entry => entry.Hit)).ToArray();
            });
            return result;
        }

        internal void Toggle(CollisionHit hit)
        {
            var connection = GameInterop.Connection ?? throw new InvalidOperationException("游戏尚未连接。");
            connection.RunInFrame(() =>
            {
                RequireDeveloperMode();
                if (!IsCurrentScope(connection))
                {
                    throw new InvalidOperationException("区域已变化，请重新扫描。");
                }

                var collider = FindCollider(ReadScenes(), hit).ThrowIfNull("碰撞体已卸载，请重新扫描。");
                var mask = collider.LayerMask.Get();
                if (_disabled.TryGetValue(collider.Ptr, out var disabled))
                {
                    if (mask == 0)
                    {
                        collider.LayerMask.Set(disabled.Mask);
                    }

                    _disabled.Remove(collider.Ptr);
                    hit.Enabled = true;
                }
                else if (mask != 0)
                {
                    collider.LayerMask.Set(0);
                    _disabled.Add(collider.Ptr, (hit, mask));
                    hit.Enabled = false;
                }
                else
                {
                    throw new InvalidOperationException("该碰撞体已由游戏关闭，请重新扫描。");
                }
            });
        }

        internal void Restore()
        {
            var connection = GameInterop.Connection;
            if (_disabled.Count == 0 || connection == null)
            {
                return;
            }

            connection.RunInFrame(() =>
            {
                if (IsCurrentScope(connection))
                {
                    var scenes = ReadScenes();
                    foreach (var entry in _disabled.Values)
                    {
                        var collider = FindCollider(scenes, entry.Hit);
                        if (!collider.IsNull() && collider.LayerMask.Get() == 0)
                        {
                            collider.LayerMask.Set(entry.Mask);
                        }
                    }
                }

                _disabled.Clear();
            });
        }

        private void RequireDeveloperMode()
        {
            if (!_host.Configuration.DeveloperMode)
            {
                throw new InvalidOperationException("碰撞体检查仅限开发者模式使用。");
            }
        }

        private bool IsCurrentScope(GameConnection connection)
        {
            if (_scope.Connection != connection || !ReferenceEquals(connection, GameInterop.Connection) ||
                _scope.Generation != _host.WorldGeneration || LayoutWorld.InstancePtrPtr == IntPtr.Zero)
            {
                return false;
            }

            var game = Game.FFCS.Client.Game.GameMain.Instance;
            return !game.IsNull() && game.TerritoryLoadState.Get() == 2 && game.CurrentTerritoryTypeId.Get() == _scope.Territory &&
                ReadActiveLayout(LayoutWorld.Instance(), _scope.Territory).Ptr == _scope.Layout;
        }

        private static LayoutManager ReadActiveLayout(LayoutWorld world, uint territory)
        {
            var layout = world.IsNull() ? default : world.ActiveLayout;
            return !layout.IsNull() && layout.InitState.Get() == 7 && layout.TerritoryTypeId.Get() == territory ? layout : default;
        }

        private static SceneManager ReadScenes() => Framework.Instance.ThrowIfNull().BGCollisionModule.ThrowIfNull().SceneManager.ThrowIfNull();

        private static Collider FindCollider(SceneManager scenes, CollisionHit hit)
        {
            foreach (var wrapper in scenes.EnumerateScenes())
            {
                var scene = wrapper.Scene;
                if (scene.IsNull() || scene.Ptr != hit.Scene)
                {
                    continue;
                }

                var collider = scene.FirstCollider;
                var count = scene.NumColliders.Get();
                for (var i = 0; i < count && !collider.IsNull(); i++, collider = collider.Next)
                {
                    if (collider.Ptr == hit.Collider.Ptr && collider.Ptr.ReadPtr() == hit.VTable && collider.LayoutObjectId.Get() == hit.LayoutId)
                    {
                        return collider;
                    }
                }
            }

            return default;
        }

        private static (CollisionHit[] Hits, bool GroundHit, Vector3[] Points) ScanRays(GameConnection connection, SceneManager scenes, LayoutWorld world, Vector3 playerPosition)
        {
            var wrappers = scenes.EnumerateScenes().Where(scene => !scene.Scene.IsNull()).ToArray();
            if (wrappers.Length == 0)
            {
                throw new InvalidOperationException("当前没有已加载的碰撞场景。");
            }

            // 回传结构和射线输入共用一块远程缓冲区，各自按 16 字节对齐。
            const int originOffset = 0x60;
            const int directionOffset = 0x70;
            const int distanceOffset = 0x80;
            const int argsOffset = 0x90;
            var memory = connection.Memory;
            var buffer = memory.AllocateMemory(argsOffset + 0x40);
            if (buffer == IntPtr.Zero)
                throw new InvalidOperationException("无法分配碰撞查询缓冲区。");

            try
            {
                var originAddress = buffer + originOffset;
                var directionAddress = buffer + directionOffset;
                var distanceAddress = buffer + distanceOffset;
                var argsAddress = buffer + argsOffset;
                // 普通射线的 Algorithm 和 MaterialFilter 均为零。
                memory.Write(argsAddress, new RaycastParams
                {
                    Origin = originAddress,
                    Direction = directionAddress,
                    MaxDistance = distanceAddress,
                });

                var nearest = new Dictionary<IntPtr, RaycastHit>();
                var points = new HashSet<Vector3>();
                var groundHit = false;
                foreach (var scene in wrappers)
                {
                    if (!groundHit)
                    {
                        memory.Write(originAddress, new Vector4(playerPosition + Vector3.UnitY, 1f));
                        groundHit = TryCast(scene, -Vector3.UnitY, 5f, out _);
                    }

                    foreach (var height in RayHeights)
                    {
                        memory.Write(originAddress, new Vector4(playerPosition + Vector3.UnitY * height, 1f));
                        for (var ray = 0; ray < RayCount; ray++)
                        {
                            var angle = ray * 2 * Math.PI / RayCount;
                            var direction = new Vector3((float)Math.Sin(angle), 0, (float)Math.Cos(angle));
                            if (TryCast(scene, direction, Range, out var hit) &&
                                (!nearest.TryGetValue(hit.Object, out var prior) || hit.Distance < prior.Distance))
                            {
                                nearest[hit.Object] = hit;
                            }
                        }
                    }
                }

                return (nearest.Values.OrderBy(hit => hit.Distance).Select(hit => TraceHit(world, hit)).ToArray(), groundHit, points.ToArray());

                bool TryCast(SceneWrapper target, Vector3 direction, float maxDistance, out RaycastHit hit)
                {
                    memory.Write(directionAddress, direction);
                    memory.Write(distanceAddress, maxDistance);
                    memory.Write(buffer, default(RaycastHit));
                    hit = target.Raycast(buffer, CollisionLayer, argsAddress) ? memory.Read<RaycastHit>(buffer) : default;
                    if (hit.Object == IntPtr.Zero)
                    {
                        return false;
                    }

                    points.Add(hit.Point);
                    return true;
                }
            }
            finally
            {
                memory.FreeMemory(buffer);
            }
        }

        private static CollisionHit TraceHit(LayoutWorld world, RaycastHit hit)
        {
            var collider = hit.Object.As<Collider>();
            var layoutId = collider.LayoutObjectId.Get();
            var type = collider.GetColliderType().ToString();
            var instanceId = unchecked((uint)layoutId);
            if (instanceId != 0)
            {
                var subId = (uint)(layoutId >> 32);
                foreach (var instanceType in LayoutTypes)
                {
                    var instance = world.GetLayoutInstance(instanceType, instanceId, subId);
                    if (instance.IsNull() ||
                        (instance.GetCollider().Ptr != collider.Ptr && instance.GetCollider2().Ptr != collider.Ptr))
                        continue;

                    var layer = instance.Layer;
                    var path = ReadPath(instance.GetSecondaryPath());
                    if (path.Length == 0)
                        path = ReadPath(instance.GetPrimaryPath());
                    return new CollisionHit(collider, hit.Distance, type, hit.Material, layoutId, instanceType.ToString(),
                        layer.IsNull() ? (uint?)null : layer.ManagerBase.Id.Get(), path);
                }
            }

            return new CollisionHit(collider, hit.Distance, type, hit.Material, layoutId, null, null, null);
        }

        private static string ReadPath(IntPtr address)
        {
            if (address == IntPtr.Zero)
                return string.Empty;

            var bytes = address.ReadBytes(260);
            var end = Array.IndexOf(bytes, (byte)0);
            return Encoding.UTF8.GetString(bytes, 0, end >= 0 ? end : bytes.Length);
        }
    }

    internal sealed class CollisionHit
    {
        internal CollisionHit(Collider collider, float distance, string colliderType, ulong material, ulong layoutId, string instanceType, uint? layerId, string path)
        {
            Collider = collider;
            Scene = collider.Scene.Ptr;
            VTable = collider.Ptr.ReadPtr();
            Distance = distance;
            ColliderType = colliderType;
            Material = material;
            LayoutId = layoutId;
            InstanceType = instanceType;
            LayerId = layerId;
            Path = path;
        }

        internal Collider Collider { get; }
        internal IntPtr Scene { get; }
        internal IntPtr VTable { get; }
        internal bool Enabled { get; set; } = true;
        internal float Distance { get; }
        internal string ColliderType { get; }
        internal ulong Material { get; }
        internal ulong LayoutId { get; }
        internal string InstanceType { get; }
        internal uint? LayerId { get; }
        internal string Path { get; }
    }
}
