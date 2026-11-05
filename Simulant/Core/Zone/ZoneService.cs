using Simulant.Core.Environment;
using Simulant.Game;
using Simulant.Game.ExtractedCsv;
using Simulant.Game.ExtractedCsv.Rows;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Game.FFCS.Client.Game.Event;
using Simulant.Game.FFCS.Client.Game.Object;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Simulant.Core.Zone
{
    internal sealed class ZoneService
    {
        private readonly PluginHost _host;
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
        public bool IsInSimulatedTerritory => _isInSimulatedTerritory;

        public bool TryEnterTerritory(int territoryId, int storyProgress = 0)
        {
            try
            {
                var gameMain = GameMain.Instance;
                if (gameMain.IsNull())
                    throw new InvalidOperationException("GameMain.Instance 为空。");

                // 只有从非模拟状态进入区域时，才记录真实地图和位置
                if (!_isInSimulatedTerritory)
                {
                    RecordInitialState(gameMain);
                }

                _ = CsvManager.Instance.Get<TerritoryType>().TryGetValue(territoryId, out TerritoryType territory);

                LoadTerritoryInternal(territoryId, territory, storyProgress);

                _isInSimulatedTerritory = true;

                var finalTerritoryId = territory != null ? (int)territory.Index : territoryId;
                var placeName = territory != null ? territory.PlaceName.Name : "未知区域";
                _host.LogSim($"已进入区域：{placeName} ({finalTerritoryId})");

                return true;
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
                if (!_isInSimulatedTerritory)
                    throw new InvalidOperationException("当前不在模拟区域中，无法进入阶段。");

                await ApplyPhaseData(phaseData, waitForZoneInit);
                return true;
            }
            catch (Exception ex)
            {
                _host.LogError("进入阶段失败：" + ex.ToString());
                return false;
            }
        }

        public bool ExitToInitialTerritory()
        {
            try
            {
                if (!_isInSimulatedTerritory)
                {
                    _host.LogWarning("当前不在模拟区域中，跳过退出。");
                    return false;
                }

                if (!_initialTerritoryId.HasValue || _initialTerritoryId.Value == 0)
                    throw new InvalidOperationException("尚未记录真实地图，无法退出。");

                var territoryId = (int)_initialTerritoryId.Value;

                _ = CsvManager.Instance.Get<TerritoryType>().TryGetValue(territoryId, out TerritoryType territory);

                LoadTerritoryInternal(territoryId, territory);

                RestoreInitialPosition();

                _isInSimulatedTerritory = false;
                _initialTerritoryId = null;
                _initialPosition = null;

                _host.EnvironmentService.SetBgm(0);

                var finalTerritoryId = territory != null ? (int)territory.Index : territoryId;
                var placeName = territory != null ? territory.PlaceName.Name : "未知区域";
                _host.LogSim($"已返回真实区域：{placeName} ({finalTerritoryId})");

                return true;
            }
            catch (Exception ex)
            {
                _host.LogError("返回真实区域失败：" + ex.Message);
                return false;
            }
        }

        private void RecordInitialState(GameMain gameMain)
        {
            var currentTerritoryId = gameMain.CurrentTerritoryTypeId.Get(); // CurrentTerritoryTypeId 切地图后更新有一定延迟，考虑换为 EventFramework 或 LayoutWorld.Active 中的来源
            if (currentTerritoryId != 0)
            {
                _initialTerritoryId = currentTerritoryId;
                _host.LogRuntime($"已记录真实地图：{currentTerritoryId}");
            }

            var me = _host.EntityProvider.GetMyself();
            if (me != null)
            {
                _initialPosition = me.Pos3D;
                _host.LogRuntime($"已记录真实位置：{_initialPosition.Value.X}, {_initialPosition.Value.Y}, {_initialPosition.Value.Z}");
            }
        }

        private void RestoreInitialPosition()
        {
            if (!_initialPosition.HasValue)
                return;

            var me = _host.EntityProvider.GetMyself()
                ?? throw new InvalidOperationException("无法获取玩家实体，不能恢复真实位置。");

            me.Pos3D = _initialPosition.Value;
            _host.LogRuntime($"已恢复真实位置：{_initialPosition.Value.X}, {_initialPosition.Value.Y}, {_initialPosition.Value.Z}");
        }

        public async Task ApplyPhaseData(PhaseData phaseData, bool waitForZoneInit)
        {
            _host.LogSim($"进入阶段 {phaseData.Name}：天气 {phaseData.Weather}，BGM {phaseData.BGM}，坐标 {phaseData.Spawn?.ToString() ?? "null"}");
            
            var me = _host.EntityProvider.GetMyself() ?? throw new InvalidOperationException("无法获取玩家实体，不能设置出生点。");
            if (phaseData.Spawn.HasValue)
            {
                me.Pos3D = phaseData.Spawn.Value;
            }
            
            void Apply() // 这些在区域没完全加载时设置无效
            {
                _host.EnvironmentService.SetWeather(phaseData.Weather);
                _host.EnvironmentService.SetBgm(phaseData.BGM);
                for (uint slot = 0; slot < phaseData.MapEffectFlags.Count; slot++)
                {
                    var flag = phaseData.MapEffectFlags[(int)slot];
                    _host.EnvironmentService.PlayMapEffect(slot, flag);
                }
            }

            if (waitForZoneInit)
            {
                await Task.Delay(3000);
                Apply();
                await Task.Delay(2000);
                Apply();
            }
            else
            {
                Apply();
            }
        }

        private void LoadTerritoryInternal(int territoryId, TerritoryType territory, int storyProgress = 0)
        {
            _host.PrepareEntityWorldChange((uint)territoryId);
            if (_loadedInstanceContentId.HasValue && _loadedInstanceContentId.Value != 0)
            {
                _host.LogVerbose($"FinalizeInstanceContent: {_loadedInstanceContentId.Value}");
                FinalizeInstanceContent(_loadedInstanceContentId.Value);
                _loadedInstanceContentId = null;
            }

            DisableCurrentObjects();

            var nextContentId = ResolveInstanceContentId(territory);
            if (nextContentId != 0)
            {
                _host.LogVerbose($"SetupInstanceContent: {nextContentId}");
                SetupInstanceContent(nextContentId);
                _loadedInstanceContentId = nextContentId;
            }

            _host.LogVerbose($"LoadZone: {territoryId}");
            GameMain.Instance.ThrowIfNull().LoadZone((uint)territoryId, storyProgress);

            _host.LogVerbose($"SetTerritoryTypeId: {territoryId}");
            EventFramework.Instance.ThrowIfNull().SetTerritoryTypeId((ushort)territoryId);
            _host.CompleteEntityWorldChange();
        }

        // 需要扫描全部实体（含本地实体），似乎有实体模型的时候游戏有概率会炸
        // to-do: EntityProvider
        private void DisableCurrentObjects() 
        {
            var count = 0;
            foreach (var entity in GameObjectManager.Instance().Objects.IndexSorted.GameObjects)
            {
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

        private static ushort ResolveInstanceContentId(TerritoryType territory)
        {
            return (ushort)(territory?.ContentFinderCondition?.ContentId ?? 0);
        }
    }
}