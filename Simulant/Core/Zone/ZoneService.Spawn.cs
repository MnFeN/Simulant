using Simulant.Game;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Game.FFCS.Client.LayoutEngine;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using TerritoryType = Lumina.Excel.Sheets.TerritoryType;

namespace Simulant.Core.Zone
{
    internal sealed partial class ZoneService
    {
        internal uint CurrentTerritoryId
        {
            get
            {
                var tracked = _host.CurrentTerritoryId;
                if (tracked != 0)
                    return tracked;

                var connection = GameInterop.Connection;
                var game = GameMain.Instance;
                if (connection == null || game.IsNull())
                    return 0;

                try
                {
                    return game.CurrentTerritoryTypeId.Get();
                }
                catch (Exception) when (!ReferenceEquals(connection, GameInterop.Connection))
                {
                    return 0;
                }
            }
        }

        internal Task<SpawnPoint[]> GetSpawnPointsAsync(uint territoryId)
        {
            lock (_stateGate)
            {
                if (_isInSimulatedTerritory && territoryId == _simulatedTerritoryId && _spawnData != null)
                    return Task.FromResult(_spawnData.Points);
            }

            var data = GameDataService.Instance;
            return Task.Run(() => data.GetSheet<TerritoryType>().TryGetRow(territoryId, out var territory)
                ? TerritorySpawnData.Scan(data, territory).Points : Array.Empty<SpawnPoint>());
        }

        internal bool TryMoveToSpawnPoint(uint territoryId, int worldGeneration, SpawnPoint point)
        {
            void CheckCurrent()
            {
                if (!_host.FirewallEnabled || _host.WorldGeneration != worldGeneration || !IsMapReady(territoryId, out _))
                    throw new OperationCanceledException("请等待当前地图加载完成，再前往出生点。");
            }

            try
            {
                CheckCurrent();
                var connection = GameInterop.Connection ?? throw new InvalidOperationException("游戏已断开连接。");
                connection.RunInFrame(() => PlacePlayer(point.Position, CheckCurrent));
                _host.LogSim($"已前往出生点：{point}");
                return true;
            }
            catch (Exception error)
            {
                _host.LogWarning("前往出生点失败：" + error.Message);
                return false;
            }
        }

        private void PlacePlayer(Vector3 position, Action checkCurrent)
        {
            checkCurrent();
            var me = _host.EntityProvider.GetMyself() ?? throw new InvalidOperationException("无法获取玩家实体，不能设置出生点。");
            checkCurrent();
            me.Pos3D = position;
        }

        private Task PrepareSpawns(TerritoryType? territory, int worldVersion)
        {
            if (!territory.HasValue)
                return Task.CompletedTask;

            try
            {
                var spawns = TerritorySpawnData.Scan(GameDataService.Instance, territory.Value);
                lock (_stateGate)
                {
                    EnsureWorld(worldVersion);
                    _spawnData = spawns;
                }

                return OpenEntranceAsync(territory.Value.RowId, spawns.BarrierLayoutIds, worldVersion);
            }
            catch (Exception error)
            {
                if (!(error is OperationCanceledException))
                    _host.LogWarning("读取出生点失败：" + error.Message);
                return Task.CompletedTask;
            }
        }

        private async Task OpenEntranceAsync(uint territoryId, uint[] layoutIds, int worldVersion)
        {
            if (layoutIds.Length == 0)
                return;

            var clock = Stopwatch.StartNew();
            try
            {
                await _entranceApplication.ApplyAsync(async token =>
                {
                    await _entranceApplication.WaitUntilAsync(current =>
                    {
                        EnsureWorld(worldVersion);
                        return IsMapReady(territoryId, out _);
                    }, token, "出生圈所在的地图布局就绪");

                    var connection = GameInterop.Connection ?? throw new OperationCanceledException("游戏已断开连接。");
                    var cleared = 0;
                    connection.RunInFrame(() =>
                    {
                        token.ThrowIfCancellationRequested();
                        EnsureWorld(worldVersion);
                        if (!IsMapReady(territoryId, out _))
                            throw new OperationCanceledException("出生圈所属的地图布局已切换。");

                        var world = LayoutWorld.Instance();
                        var layout = world.ActiveLayout;
                        foreach (var id in layoutIds)
                        {
                            token.ThrowIfCancellationRequested();
                            var group = world.GetLayoutInstance(InstanceType.SharedGroup, id);
                            if (group.IsNull() || group.Layout.Ptr != layout.Ptr)
                                continue;

                            group.SetActive(false);
                            group.SetColliderActive(false);
                            cleared++;
                        }
                    });

                    _host.LogVerbose(FormattableString.Invariant(
                        $"[出生圈计时] 区域 {territoryId}，{clock.Elapsed.TotalSeconds:F3} 秒：关闭 {cleared}/{layoutIds.Length} 个场地组。"));
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                _host.LogWarning("解除出生圈失败：" + error.Message);
            }
        }
    }
}
