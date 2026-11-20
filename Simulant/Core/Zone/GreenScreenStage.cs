using Simulant.Game;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.LayoutEngine;
using Simulant.Game.FFCS.Client.System.Resource.Handle;
using System;
using System.Collections.Generic;
using System.IO;

namespace Simulant.Core.Zone
{
    internal sealed partial class GreenScreenStage : IDisposable
    {
        internal const int TerritoryId = 1;
        internal const string Name = "绿幕";

        private readonly GameConnection _connection;
        private readonly string _directory;
        private readonly List<ResourceHandle> _resources = new List<ResourceHandle>();
        private readonly List<string> _files = new List<string>();
        private TerritorySceneInterop _scenePath;

        private GreenScreenStage(GameConnection connection, string directory)
        {
            _connection = connection;
            _directory = directory;
        }

        internal static GreenScreenStage Load()
        {
            var connection = GameInterop.Connection ?? throw new InvalidOperationException("游戏尚未连接。");
            var id = Guid.NewGuid().ToString("N");
            var root = $"ffxiv/ss{id.Substring(0, 8)}/";
            var prefix = "bg/" + root;
            var assets = GreenScreenScene.Build(GameDataService.Instance, prefix);
            var stage = new GreenScreenStage(connection, Path.Combine(Path.GetTempPath(), "Simulant-GreenScreen-" + id));
            try
            {
                Directory.CreateDirectory(stage._directory);
                foreach (var asset in assets)
                {
                    var file = Path.Combine(stage._directory, Path.GetFileName(asset.Name));
                    stage._files.Add(file);
                    File.WriteAllBytes(file, asset.Data);
                    connection.RunInFrame(() =>
                    {
                        var resource = UnpackedResourceInterop.Load(connection, prefix + asset.Name, file);
                        stage._resources.Add(resource);
                        if (asset.Name == GreenScreenScene.FloorMaterial)
                        {
                            stage._floorMaterial = resource.Ptr.As<MaterialResourceHandle>();
                        }
                    });
                }

                stage._scenePath = TerritorySceneInterop.SetScenePath(connection, TerritoryId, root + "level/stage");
                return stage;
            }
            catch
            {
                stage.Dispose();
                throw;
            }
        }

        internal void ActivateFloor()
        {
            _connection.RunInFrame(() =>
            {
                var world = LayoutWorld.Instance().ThrowIfNull();
                var layout = world.ActiveLayout.ThrowIfNull();
                var actual = layout.ManagerBase.Id.Get();
                var expected = _resources[_resources.Count - 1].Id.Get();
                if (layout.TerritoryTypeId.Get() != TerritoryId || actual != expected)
                    throw new InvalidOperationException($"区域 1 未使用绿幕场景。当前 LVB {actual:X8}，预期 {expected:X8}。");

                var floor = world.GetLayoutInstance(InstanceType.BgPart, GreenScreenScene.FloorInstanceId);
                if (floor.IsNull() || floor.Layout.Ptr != layout.Ptr)
                    throw new InvalidOperationException("绿幕场景已加载，地板实例未创建。");

                if (!floor.IsActive)
                {
                    floor.SetActive(true);
                }

                if (!_ready)
                {
                    var current = Appearance;
                    current.PointLight = GreenScreenAppearance.Default.PointLight;
                    current.WorldLight = GreenScreenAppearance.Default.WorldLight;
                    Appearance = current;
                }

                _layoutId = expected;
                _ready = true;
            });
        }

        public void Dispose()
        {
            BeginTransition();
            try
            {
                _scenePath?.Dispose();
                _scenePath = null;
            }
            finally
            {
                try
                {
                    if (GameInterop.Connection == _connection && GameInterop.IsReady)
                    {
                        for (var index = _resources.Count - 1; index >= 0; index--)
                        {
                            _resources[index].DecRef();
                            _resources.RemoveAt(index);
                        }
                    }
                }
                finally
                {
                    foreach (var file in _files)
                    {
                        File.Delete(file);
                    }

                    _files.Clear();
                    if (Directory.Exists(_directory))
                    {
                        Directory.Delete(_directory);
                    }
                }
            }
        }
    }
}
