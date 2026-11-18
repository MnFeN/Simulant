using Simulant.Core.Environment;
using Simulant.Game;
using Simulant.Game.Data;
using Lumina.Excel.Sheets;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Game.FFCS.Client.Game.Event;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Game.FFCS.Client.LayoutEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly PhaseApplication _entranceApplication = new PhaseApplication();
        private Task _openingEntrance = Task.CompletedTask;
        private Vector3? _entrancePosition;
        private uint _simulatedTerritoryId;
        private int _worldVersion;
        public ZoneService(PluginHost host)
        {
            _host = host;
        }

        // 用于下次切换区域时清理旧 instance
        private ushort? _loadedInstanceContentId;

        internal uint InstanceDirectorId
        {
            get
            {
                lock (_stateGate)
                    return _loadedInstanceContentId is ushort id && id != 0 ? 0x80030000u + id : 0;
            }
        }

        // 进入模拟区域前记录真实地图和位置，退出时返回
        private uint? _initialTerritoryId;
        private Vector3? _initialPosition;
        private bool _returnLoadRequested;

        private bool _isInSimulatedTerritory;
        public bool IsInSimulatedTerritory
        {
            get
            {
                lock (_stateGate)
                    return _isInSimulatedTerritory;
            }
        }

        /// <summary>包括进入中途失败的情况；恢复通信前仍需返回已记录的真实地图。</summary>
        internal bool HasPendingReturn
        {
            get
            {
                lock (_stateGate)
                    return _initialTerritoryId.HasValue;
            }
        }

        internal void CancelPendingPhase()
        {
            _phaseApplication.Cancel();
            _entranceApplication.Cancel();
        }

        // 外部换图/进程退出时只丢弃归属，不在新世界中清理旧副本实例。
        internal void InvalidateWorld()
        {
            lock (_stateGate)
            {
                _worldVersion++;
                _isInSimulatedTerritory = false;
                _simulatedTerritoryId = 0;
                _initialTerritoryId = null;
                _initialPosition = null;
                _returnLoadRequested = false;
                _loadedInstanceContentId = null;
                _entrancePosition = null;
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
                    if (_returnLoadRequested)
                        throw new InvalidOperationException("尚未确认返回原区域，请先重试退出模拟。");

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
                    _simulatedTerritoryId = (uint)territoryId;
                }

                var finalTerritoryId = territory.HasValue ? (int)territory.Value.RowId : territoryId;
                _openingEntrance = OpenEntranceAsync(territory, worldVersion);
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

        public async Task<bool> EnterPhase(PhaseData phaseData)
        {
            try
            {
                if (!IsInSimulatedTerritory)
                    throw new InvalidOperationException("当前不在模拟区域中，无法进入阶段。");

                var openingEntrance = _openingEntrance;
                var applied = await ApplyPhaseData(phaseData);
                await openingEntrance;
                return applied;
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
                bool returnLoadRequested;

                lock (_stateGate)
                {
                    worldVersion = _worldVersion;
                    initialTerritoryId = _initialTerritoryId;
                    initialPosition = _initialPosition;
                    returnLoadRequested = _returnLoadRequested;
                }

                if (!initialTerritoryId.HasValue)
                {
                    _host.LogWarning("当前不在模拟区域中，跳过退出。");
                    return false;
                }

                // 已回到大厅时不再加载掉线前的地图，直接解除旧模拟区域的返回责任。
                var player = _host.EntityProvider.GetMyself();

                if (player == null || !player.IsAlive)
                {
                    InvalidateWorld();
                    return true;
                }

                if (initialTerritoryId.Value == 0 || !initialPosition.HasValue)
                    throw new InvalidOperationException("原区域或原位置记录不完整，无法验证返回状态。");

                var territoryId = (int)initialTerritoryId.Value;

                var territory = GameDataService.Instance.GetSheet<TerritoryType>().GetRowOrDefault((uint)territoryId);

                lock (_stateGate)
                {
                    EnsureWorld(worldVersion);
                    _isInSimulatedTerritory = false;
                }

                if (!returnLoadRequested)
                {
                    LoadTerritoryInternal(territoryId, territory, worldVersion, exitingSimulation: true);

                    lock (_stateGate)
                    {
                        EnsureWorld(worldVersion);
                        _returnLoadRequested = true;
                    }
                }

                WaitForInitialTerritory(initialTerritoryId.Value, worldVersion);
                RestoreInitialPosition(initialPosition.Value, worldVersion);

                EnsureWorld(worldVersion);
                _host.EnvironmentService.SetBgm(0);

                lock (_stateGate)
                {
                    EnsureWorld(worldVersion);
                    if (!IsMapReady(initialTerritoryId.Value, out var state))
                        throw new InvalidOperationException("恢复位置后，原区域尚未就绪：" + state);

                    _isInSimulatedTerritory = false;
                    _initialTerritoryId = null;
                    _initialPosition = null;
                    _returnLoadRequested = false;
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

            var me = _host.EntityProvider.GetMyself()
                ?? throw new InvalidOperationException("无法记录返回位置，不能进入模拟区域。");
            var initialPosition = me.Pos3D;

            lock (_stateGate)
            {
                EnsureWorld(worldVersion);
                _initialTerritoryId = currentTerritoryId;
                _initialPosition = initialPosition;
            }

            _host.LogRuntime($"已记录真实地图：{currentTerritoryId}");

            _host.LogRuntime(FormattableString.Invariant($"已记录真实位置：{initialPosition.X}, {initialPosition.Y}, {initialPosition.Z}"));
        }

        private void WaitForInitialTerritory(uint territoryId, int worldVersion)
        {
            // ACT 的卸载入口是同步的。限时等待游戏进程完成换图，超时后保留记录供重试。
            var clock = Stopwatch.StartNew();

            while (true)
            {
                EnsureWorld(worldVersion);
                if (IsMapReady(territoryId, out var state))
                    return;

                if (clock.Elapsed.TotalSeconds >= 5)
                    throw new TimeoutException($"等待原区域 {territoryId} 就绪超过 5 秒：{state}");

                Thread.Sleep(50);
            }
        }

        private void RestoreInitialPosition(Vector3 initialPosition, int worldVersion)
        {
            EnsureWorld(worldVersion);
            var me = _host.EntityProvider.GetMyself()
                ?? throw new InvalidOperationException("无法获取玩家实体，不能恢复真实位置。");

            EnsureWorld(worldVersion);
            me.Pos3D = initialPosition;
            EnsureWorld(worldVersion);
            var actual = me.Pos3D;
            if (!(Vector3.Distance(actual, initialPosition) <= 0.1f))
                throw new InvalidOperationException(FormattableString.Invariant(
                    $"真实位置验证失败：期望 {initialPosition}，实际 {actual}。"));

            _host.LogRuntime(FormattableString.Invariant($"已恢复真实位置：{actual.X}, {actual.Y}, {actual.Z}"));
        }

        private static bool IsMapReady(uint territoryId, out string state)
        {
            var game = GameMain.Instance.ThrowIfNull();
            var world = LayoutWorld.Instance();
            var layout = world.IsNull() ? default(LayoutManager) : world.ActiveLayout;
            var loadState = game.TerritoryLoadState.Get();
            var currentTerritory = game.CurrentTerritoryTypeId.Get();
            var layoutTerritory = layout.IsNull() ? 0 : layout.TerritoryTypeId.Get();
            var initState = layout.IsNull() ? -1 : layout.InitState.Get();
            state = $"地图加载状态={loadState}，当前区域={currentTerritory}；"
                + (layout.IsNull() ? "场地尚不存在" : $"场地区域={layoutTerritory}，初始化状态={initState}");
            return territoryId != 0 && loadState == 2 && currentTerritory == territoryId
                && layoutTerritory == territoryId && initState == 7;
        }

        private Task<bool> ApplyPhaseData(PhaseData phaseData)
        {
            int worldVersion;
            uint territoryId;
            Vector3? spawn;

            lock (_stateGate)
            {
                if (!_isInSimulatedTerritory)
                    throw new OperationCanceledException();

                worldVersion = _worldVersion;
                territoryId = _simulatedTerritoryId;
                spawn = phaseData.Spawn ?? _entrancePosition;
            }

            var clock = Stopwatch.StartNew();
            _host.LogSim($"进入阶段 {phaseData.Name}：天气 {phaseData.Weather}，BGM {phaseData.BGM}，坐标 {spawn?.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? "null"}");

            void CheckCurrent(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                EnsureWorld(worldVersion);
            }

            void Prepare(CancellationToken token)
            {
                CheckCurrent(token);

                if (!spawn.HasValue)
                    return;

                var me = _host.EntityProvider.GetMyself() ?? throw new InvalidOperationException("无法获取玩家实体，不能设置出生点。");
                CheckCurrent(token);
                me.Pos3D = spawn.Value;
            }

            var needsDirector = false;

            foreach (var flag in phaseData.MapEffectFlags)
                needsDirector |= flag.HasValue;

            string lastState = null;

            bool Ready(CancellationToken token)
            {
                CheckCurrent(token);
                var ready = IsMapReady(territoryId, out var state);

                if (needsDirector)
                {
                    var framework = EventFramework.Instance;
                    var directorReady = !framework.IsNull() && !framework.ContentDirector.IsNull();
                    ready &= directorReady;
                    state += $"；场地效果控制器已创建={directorReady}";
                }

                if (state != lastState)
                {
                    lastState = state;
                    _host.LogVerbose(FormattableString.Invariant($"[场地初始化] 区域 {territoryId}，{clock.Elapsed.TotalSeconds:F3} 秒：{state}"));
                }

                return ready;
            }

            void Apply(CancellationToken token)
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

                _host.LogVerbose(FormattableString.Invariant($"[场地初始化] 区域 {territoryId}，{clock.Elapsed.TotalSeconds:F3} 秒：地图已就绪，已提交阶段「{phaseData.Name}」的天气、BGM 和场地效果。"));
            }

            return _phaseApplication.ApplyAsync(Prepare, Apply, Ready);
        }

        private void LoadTerritoryInternal(int territoryId, TerritoryType? territory, int worldVersion, int storyProgress = 0,
            bool exitingSimulation = false)
        {
            EnsureWorld(worldVersion);
            _entranceApplication.Cancel();

            lock (_stateGate)
                _entrancePosition = null;

            _host.PrepareEntityWorldChange((uint)territoryId, exitingSimulation);
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

        private async Task OpenEntranceAsync(TerritoryType? territory, int worldVersion)
        {
            if (!territory.HasValue || ResolveInstanceContentId(territory) == 0)
                return;

            var clock = Stopwatch.StartNew();

            try
            {
                var specs = EntranceBarrier.Load(GameDataService.Instance, territory.Value);

                if (specs.Length == 0)
                    return;

                lock (_stateGate)
                {
                    EnsureWorld(worldVersion);
                    // 与出生圈实体使用同一份位置，首次异步等待前即可设置玩家出生点。
                    _entrancePosition = specs[0].Pos;
                }

                var entrances = new List<Entity.EventObject>();
                var spawned = false;
                var mapReady = false;
                var groupsReady = false;

                void LogTiming(string message)
                    => _host.LogVerbose(FormattableString.Invariant($"[出生圈计时] 区域 {territory.Value.RowId}，{clock.Elapsed.TotalSeconds:F3} 秒：{message}"));

                string lastState = null;

                void Observe()
                {
                    mapReady = IsMapReady(territory.Value.RowId, out var mapState);
                    groupsReady = spawned;
                    var states = new List<string>
                    {
                        mapState
                    };

                    if (!spawned)
                        states.Add("EObj 尚未生成");

                    foreach (var entrance in entrances)
                    {
                        var native = entrance.Native.Ptr.As<GameObject>();
                        var loaded = native.IsSharedGroupLoaded();
                        groupsReady &= loaded;
                        states.Add($"EObj {entrance.Id:X8}：场地组已加载={loaded}，事件状态={native.EventState.Get()}");
                    }

                    var state = string.Join("；", states);

                    if (state == lastState)
                        return;

                    lastState = state;
                    LogTiming(state);
                }

                // EObj 保留至离开地图；停止机制、切换阶段不重新关圈。
                await _entranceApplication.ApplyAsync(async token =>
                {
                    Task WaitUntil(Func<bool> ready, string stage)
                        => _entranceApplication.WaitUntilAsync(current =>
                    {
                        EnsureWorld(worldVersion);
                        Observe();
                        return ready();
                    }, token, stage);

                    LogTiming("从本地进图返回后计时；地图就绪后生成 EObj，场地组就绪后立即开圈。");
                    await WaitUntil(() => mapReady, "地图就绪");

                    foreach (var spec in specs)
                    {
                        token.ThrowIfCancellationRequested();
                        EnsureWorld(worldVersion);
                        entrances.Add(_host.EntitySpawner.SpawnEObj(spec, true));
                    }

                    spawned = true;
                    LogTiming($"已生成 {entrances.Count} 个出生圈 EObj。");

                    await WaitUntil(() => mapReady && groupsReady, "出生圈场地组就绪");

                    foreach (var entrance in entrances)
                    {
                        token.ThrowIfCancellationRequested();
                        EnsureWorld(worldVersion);
                        entrance.ActorControl(ActorControlCategory.DirectorEObjMod, 7);
                    }

                    LogTiming("已发送开圈状态 7；请实机确认出生圈碰撞已消失。");
                    _host.LogVerbose($"已解除出生圈：区域 {territory.Value.RowId}，共 {entrances.Count} 个。");
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

        // 需要扫描全部实体（含本地实体），似乎有实体模型的时候游戏有概率会炸
        // to-do: EntityProvider
        private void DisableCurrentObjects(int worldVersion)
        {
            var count = 0;

            foreach (var entity in GameObjectManager.Instance().Objects.IndexSorted.GameObjects)
            {
                EnsureWorld(worldVersion);

                if (entity.IsNull())
                    continue;

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
