using Simulant.Game;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Game.FFCS.Client.Graphics.Vfx;
using Simulant.Game.FFCS.Client.LayoutEngine;
using System;
using System.Numerics;
using System.Threading;

namespace Simulant.Core.Environment
{
    internal sealed class StudioService
    {
        private const float FloorClearance = 0.03125f;
        private readonly PluginHost _host;
        private readonly object _decalFloorOwner = new object();
        private Timer _timer;
        private (IntPtr Ptr, uint Territory) _layout;
        private StudioSettings _material;
        private DateTime _materialDue;

        internal StudioService(PluginHost host)
        {
            _host = host;
            Settings = host.Configuration.Studio;
        }

        internal StudioSettings Settings { get; private set; }
        internal bool HasStudio => VfxManager.HasEffect(this);

        internal bool TryGetPlayerTransform(out Vector3 position, out float heading)
        {
            var player = _host.EntityProvider.GetMyself();
            position = player?.Pos3D ?? Vector3.Zero;
            heading = player?.Heading ?? 0f;
            return player != null;
        }

        internal void ChangeSettings(StudioSettings settings)
        {
            if (!Settings.SameMaterial(settings))
                _materialDue = DateTime.UtcNow.AddMilliseconds(180);

            Settings = settings;
        }

        internal void SetEnabled(bool enabled)
        {
            if (!enabled)
            {
                StopTracking();
                RemoveStudio();
                return;
            }

            if (HasStudio)
                return;

            _host.EnsureDebugAllowed();
            var layout = ReadActiveLayout();
            if (layout.Ptr == IntPtr.Zero)
                throw new InvalidOperationException("请等待当前地图加载完成。");

            if (!TryResolveTransform(out var position, out var rotation))
                throw new InvalidOperationException("找不到当前角色，无法确定摄影棚的位置。");

            RebuildModel(Settings, position, rotation);
            _layout = layout;
            _timer = new Timer(_ => _host.PollStudio(), null, 100, 100);
        }

        internal void Update()
        {
            if (_timer == null)
                return;

            if (!HasStudio)
            {
                StopTracking();
                VfxManager.Remove(_decalFloorOwner);
                return;
            }

            var layout = ReadActiveLayout();
            if (layout != _layout)
            {
                StopTracking();
                RemoveStudio();
                return;
            }

            if (!_host.CanRunDebug || !TryResolveTransform(out var position, out var rotation))
                return;

            if (!_material.SameMaterial(Settings) && DateTime.UtcNow >= _materialDue)
                RebuildModel(Settings, position, rotation);

            var model = ToNativeTransform(position, Settings.Size);
            var floor = ToDecalTransform(position, Settings.Size);
            if (!VfxManager.UpdateStatic(this, model.Position, rotation, model.Scale, Settings.Color) ||
                (_material.FloorType == StudioFloorType.Decal &&
                    !VfxManager.UpdateStatic(_decalFloorOwner, floor.Position, rotation, floor.Scale, Settings.Color)))
            {
                StopTracking();
                RemoveStudio();
            }
        }

        internal void StopTracking()
        {
            _timer?.Dispose();
            _timer = null;
        }

        private bool TryResolveTransform(out Vector3 position, out Quaternion rotation)
        {
            var player = Vector3.Zero;
            var playerHeading = 0f;
            if (Settings.FollowsPlayer && !TryGetPlayerTransform(out player, out playerHeading))
            {
                position = default;
                rotation = default;
                return false;
            }

            position = Settings.ResolvePosition(player);
            rotation = Settings.ResolveRotation(playerHeading);
            return true;
        }

        private static (Vector3 Position, Vector3 Scale) ToNativeTransform(Vector3 position, Vector3 size)
        {
            // 先把 ACT 坐标转换为游戏坐标。地面防闪烁的偏移以米为单位，独立于网格缩放。
            return (new Vector3(position.X, position.Z + FloorClearance, position.Y),
                new Vector3(size.X, size.Z, size.Y) / StudioAvfx.EdgeLength);
        }

        private static (Vector3 Position, Vector3 Scale) ToDecalTransform(Vector3 position, Vector3 size)
        {
            // 矩形贴花的水平缩放对应完整长宽。投影厚度独立于摄影棚高度。
            return (new Vector3(position.X, position.Z, position.Y), new Vector3(size.X, 0.5f, size.Y));
        }

        private void RebuildModel(StudioSettings settings, Vector3 position, Quaternion rotation)
        {
            GameFunctions.CheckStatus(VfxResourceInstance.SetColorFuncPtr);
            var model = ToNativeTransform(position, settings.Size);
            VfxManager.Remove(this);
            try
            {
                Place(StudioAvfx.Build(settings), model.Position, rotation, model.Scale, settings.Color, this);
                if (settings.FloorType == StudioFloorType.Model)
                {
                    VfxManager.Remove(_decalFloorOwner);
                }
                else if (!VfxManager.HasEffect(_decalFloorOwner))
                {
                    var floor = ToDecalTransform(position, settings.Size);
                    Place(StudioAvfx.BuildDecalFloor(), floor.Position, rotation, floor.Scale, settings.Color, _decalFloorOwner);
                }

                _material = settings;
            }
            catch
            {
                RemoveStudio();
                throw;
            }
        }

        private void RemoveStudio()
        {
            try
            {
                VfxManager.Remove(_decalFloorOwner);
            }
            finally
            {
                VfxManager.Remove(this);
            }
        }

        private static void Place(byte[] asset, Vector3 position, Quaternion rotation, Vector3 scale, Vector4 color, object owner)
        {
            var path = $"bgcommon/simulant/studio_{Guid.NewGuid():N}.avfx";
            var resource = AvfxResourceInterop.Load(path, asset, ValidateStudioSchedule);
            try
            {
                VfxManager.ShowStatic(path, resource, position, rotation, scale, owner, color);
            }
            finally
            {
                // 场景实例持有资源引用。释放加载时取得的临时引用。
                resource.DecRef();
            }
        }

        private static void ValidateStudioSchedule(AvfxResourceInterop.ParsedAvfx parsed)
        {
            if (parsed.SchedulerCount != 1)
                throw new InvalidOperationException("摄影棚资源需要一个调度表。");

            var scheduler = parsed.FirstScheduler;
            if (scheduler.IsNull() || scheduler.TriggerCount != StudioAvfx.TriggerCount || scheduler.Triggers.IsZero())
                throw new InvalidOperationException("摄影棚资源缺少完整的调度触发槽。");
        }

        private static (IntPtr Ptr, uint Territory) ReadActiveLayout()
        {
            var game = GameMain.Instance;
            if (!GameInterop.IsReady || game.IsNull() || LayoutWorld.InstancePtrPtr == IntPtr.Zero ||
                game.TerritoryLoadState.Get() != 2)
                return default;

            var world = LayoutWorld.Instance();
            if (world.IsNull())
                return default;

            var active = world.ActiveLayout;
            var territory = game.CurrentTerritoryTypeId.Get();
            return !active.IsNull() && !world.GlobalLayout.IsNull() && active.InitState.Get() == 7 &&
                active.TerritoryTypeId.Get() == territory ? (active.Ptr, territory) : default;
        }
    }
}
