using Simulant.Core.Environment;
using Simulant.Game;
using Simulant.Game.Data;
using Lumina.Excel.Sheets;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Game.FFCS.Client.Game.Event;
using Simulant.Game.FFCS.Client.Game.Object;
using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Simulant.Core.Zone
{
    internal sealed class ZoneService
    {
        private readonly PluginHost _host;
        private readonly object _stateGate = new object();
        private readonly PhaseApplication _phaseApplication = new PhaseApplication();
        private int _worldVersion;
        public ZoneService(PluginHost host)
        {
            _host = host;
        }

        // 用于下次切换区域时清理旧 instance
        private ushort? _loadedInstanceContentId;

        // 进入模拟区域前记录真实地图和位置，退出时返回
        private uint? _initialTerritoryId;
        private Vector3? _initialPosition;

        private bool _isInSimulatedTerritory;
        public bool IsInSimulatedTerritory
        {
            get { lock (_stateGate) return _isInSimulatedTerritory; }
        }

        /// <summary>包括进入中途失败的情况；恢复通信前仍需返回已记录的真实地图。</summary>
        internal bool HasPendingReturn
        {
            get { lock (_stateGate) return _initialTerritoryId.HasValue; }
        }

        internal void CancelPendingPhase() => _phaseApplication.Cancel();

        // 外部换图/进程退出时只丢弃归属，不在新世界中清理旧副本实例。
        internal void InvalidateWorld()
        {
            lock (_stateGate)
            {
                _worldVersion++;
                _isInSimulatedTerritory = false;
                _initialTerritoryId = null;
                _initialPosition = null;
                _loadedInstanceContentId = null;
            }
            CancelPendingPhase();
        }

        private void EnsureWorld(int worldVersion)
        {
            lock (_stateGate)
                if (_worldVersion != worldVersion)
                    throw new OperationCanceledException("地图操作所属的世界已经失效。");
        }

        public bool TryEnterTerritory(int territoryId, int storyProgress = 0)
        {
            CancelPendingPhase();
            try
            {
                int worldVersion;
                bool recordInitialState;
                lock (_stateGate)
                {
                    worldVersion = _worldVersion;
                    recordInitialState = !_initialTerritoryId.HasValue;
                }
                var gameMain = GameMain.Instance;
                if (gameMain.IsNull())
                    throw new InvalidOperationException("GameMain.Instance 为空。");

                // 只有从非模拟状态进入区域时，才记录真实地图和位置
                if (recordInitialState)
                {
                    RecordInitialState(gameMain, worldVersion);
                }

                var territory = GameDataService.Instance.GetSheet<TerritoryType>().GetRowOrDefault((uint)territoryId);

                lock (_stateGate)
                {
                    EnsureWorld(worldVersion);
                    _isInSimulatedTerritory = false;
                }
                LoadTerritoryInternal(territoryId, territory, worldVersion, storyProgress);

                lock (_stateGate)
                {
                    EnsureWorld(worldVersion);
                    _isInSimulatedTerritory = true;
                }

                var finalTerritoryId = territory.HasValue ? (int)territory.Value.RowId : territoryId;
                var placeName = territory?.PlaceName.ValueNullable?.Name.ExtractText() ?? "未知区域";
                _host.LogSim($"已进入区域：{placeName} ({finalTerritoryId})");

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                _host.LogError("进入区域失败：" + ex.ToString());
                return false;
            }
        }

        public async Task<bool> EnterPhase(PhaseData phaseData, bool waitForZoneInit)
        {
            try
            {
                if (!IsInSimulatedTerritory)
                    throw new InvalidOperationException("当前不在模拟区域中，无法进入阶段。");

                return await ApplyPhaseData(phaseData, waitForZoneInit);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                _host.LogError("进入阶段失败：" + ex.ToString());
                return false;
            }
        }

        public bool ExitToInitialTerritory()
        {
            CancelPendingPhase();
            try
            {
                int worldVersion;
                uint? initialTerritoryId;
                Vector3? initialPosition;
                lock (_stateGate)
                {
                    worldVersion = _worldVersion;
                    initialTerritoryId = _initialTerritoryId;
                    initialPosition = _initialPosition;
                }
                if (!initialTerritoryId.HasValue)
                {
                    _host.LogWarning("当前不在模拟区域中，跳过退出。");
                    return false;
                }

                if (!initialTerritoryId.HasValue || initialTerritoryId.Value == 0)
                    throw new InvalidOperationException("尚未记录真实地图，无法退出。");

                var territoryId = (int)initialTerritoryId.Value;

                var territory = GameDataService.Instance.GetSheet<TerritoryType>().GetRowOrDefault((uint)territoryId);

                lock (_stateGate)
                {
                    EnsureWorld(worldVersion);
                    _isInSimulatedTerritory = false;
                }
                LoadTerritoryInternal(territoryId, territory, worldVersion);

                RestoreInitialPosition(initialPosition, worldVersion);

                EnsureWorld(worldVersion);
                _host.EnvironmentService.SetBgm(0);
                lock (_stateGate)
                {
                    EnsureWorld(worldVersion);
                    _isInSimulatedTerritory = false;
                    _initialTerritoryId = null;
                    _initialPosition = null;
                }

                var finalTerritoryId = territory.HasValue ? (int)territory.Value.RowId : territoryId;
                var placeName = territory?.PlaceName.ValueNullable?.Name.ExtractText() ?? "未知区域";
                _host.LogSim($"已返回真实区域：{placeName} ({finalTerritoryId})");

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                _host.LogError("返回真实区域失败：" + ex.Message);
                return false;
            }
        }

        private void RecordInitialState(GameMain gameMain, int worldVersion)
        {
            EnsureWorld(worldVersion);
            var currentTerritoryId = gameMain.CurrentTerritoryTypeId.Get(); // CurrentTerritoryTypeId 切地图后更新有一定延迟，考虑换为 EventFramework 或 LayoutWorld.Active 中的来源
            if (currentTerritoryId == 0)
                throw new InvalidOperationException("当前真实地图尚未就绪，不能进入模拟区域。");

            var me = _host.EntityProvider.GetMyself();
            var initialPosition = me?.Pos3D;
            lock (_stateGate)
            {
                EnsureWorld(worldVersion);
                _initialTerritoryId = currentTerritoryId;
                _initialPosition = initialPosition;
            }
            _host.LogRuntime($"已记录真实地图：{currentTerritoryId}");
            if (initialPosition.HasValue)
                _host.LogRuntime($"已记录真实位置：{initialPosition.Value.X}, {initialPosition.Value.Y}, {initialPosition.Value.Z}");
        }

        private void RestoreInitialPosition(Vector3? initialPosition, int worldVersion)
        {
            if (!initialPosition.HasValue)
                return;

            EnsureWorld(worldVersion);
            var me = _host.EntityProvider.GetMyself()
                ?? throw new InvalidOperationException("无法获取玩家实体，不能恢复真实位置。");

            EnsureWorld(worldVersion);
            me.Pos3D = initialPosition.Value;
            _host.LogRuntime($"已恢复真实位置：{initialPosition.Value.X}, {initialPosition.Value.Y}, {initialPosition.Value.Z}");
        }

        private Task<bool> ApplyPhaseData(PhaseData phaseData, bool waitForZoneInit)
        {
            int worldVersion;
            lock (_stateGate)
            {
                if (!_isInSimulatedTerritory) throw new OperationCanceledException();
                worldVersion = _worldVersion;
            }
            _host.LogSim($"进入阶段 {phaseData.Name}：天气 {phaseData.Weather}，BGM {phaseData.BGM}，坐标 {phaseData.Spawn?.ToString() ?? "null"}");

            void CheckCurrent(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                EnsureWorld(worldVersion);
            }

            void Prepare(CancellationToken token)
            {
                CheckCurrent(token);
                if (!phaseData.Spawn.HasValue) return;
                var me = _host.EntityProvider.GetMyself() ?? throw new InvalidOperationException("无法获取玩家实体，不能设置出生点。");
                CheckCurrent(token);
                me.Pos3D = phaseData.Spawn.Value;
            }

            void Apply(CancellationToken token) // 这些在区域没完全加载时设置无效
            {
                CheckCurrent(token);
                _host.EnvironmentService.SetWeather(phaseData.Weather);
                CheckCurrent(token);
                _host.EnvironmentService.SetBgm(phaseData.BGM);
                for (uint slot = 0; slot < phaseData.MapEffectFlags.Count; slot++)
                {
                    CheckCurrent(token);
                    var flag = phaseData.MapEffectFlags[(int)slot];
                    if (flag.HasValue)
                        _host.EnvironmentService.PlayMapEffect(slot, flag.Value);
                }
            }

            return _phaseApplication.ApplyAsync(Prepare, Apply, waitForZoneInit);
        }

        private void LoadTerritoryInternal(int territoryId, TerritoryType? territory, int worldVersion, int storyProgress = 0)
        {
            EnsureWorld(worldVersion);
            _host.PrepareEntityWorldChange((uint)territoryId);
            ushort? loadedInstanceContentId;
            lock (_stateGate)
            {
                EnsureWorld(worldVersion);
                loadedInstanceContentId = _loadedInstanceContentId;
            }
            if (loadedInstanceContentId.HasValue && loadedInstanceContentId.Value != 0)
            {
                _host.LogVerbose($"FinalizeInstanceContent: {loadedInstanceContentId.Value}");
                EnsureWorld(worldVersion);
                FinalizeInstanceContent(loadedInstanceContentId.Value);
                lock (_stateGate)
                {
                    EnsureWorld(worldVersion);
                    _loadedInstanceContentId = null;
                }
            }

            EnsureWorld(worldVersion);
            DisableCurrentObjects(worldVersion);

            var nextContentId = ResolveInstanceContentId(territory);
            if (nextContentId != 0)
            {
                _host.LogVerbose($"SetupInstanceContent: {nextContentId}");
                EnsureWorld(worldVersion);
                SetupInstanceContent(nextContentId);
                lock (_stateGate)
                {
                    EnsureWorld(worldVersion);
                    _loadedInstanceContentId = nextContentId;
                }
            }

            _host.LogVerbose($"LoadZone: {territoryId}");
            EnsureWorld(worldVersion);
            GameMain.Instance.ThrowIfNull().LoadZone((uint)territoryId, storyProgress);

            _host.LogVerbose($"SetTerritoryTypeId: {territoryId}");
            EnsureWorld(worldVersion);
            EventFramework.Instance.ThrowIfNull().SetTerritoryTypeId((ushort)territoryId);
            EnsureWorld(worldVersion);
            _host.CompleteEntityWorldChange();
        }

        // 需要扫描全部实体（含本地实体），似乎有实体模型的时候游戏有概率会炸
        // to-do: EntityProvider
        private void DisableCurrentObjects(int worldVersion)
        {
            var count = 0;
            foreach (var entity in GameObjectManager.Instance().Objects.IndexSorted.GameObjects)
            {
                EnsureWorld(worldVersion);
                if (entity.IsNull()) continue;

                try
                {
                    entity.DisableDraw();
                }
                catch (Exception ex)
                {
                    _host.LogWarning("DisableDraw 失败：" + ex.Message);
                }
                count++;
            }
            _host.LogVerbose($"DisableDraw 时实体总数：{count}");
        }

        public void SetupInstanceContent(ushort contentId)
        {
            EventFramework.Instance.SetupInstanceContent(0x80030000u + contentId, contentId);
        }

        public void FinalizeInstanceContent(ushort contentId)
        {
            EventFramework.Instance.FinalizeInstanceContent(0x80030000u + contentId);
        }

        private static ushort ResolveInstanceContentId(TerritoryType? territory)
        {
            return (ushort)(territory?.ContentFinderCondition.ValueNullable?.Content.RowId ?? 0);
        }
    }
}
