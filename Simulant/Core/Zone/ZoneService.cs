using Simulant.Game;
using Simulant.Game.Data;
using Lumina.Excel.Sheets;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Event;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Game.FFCS.Client.LayoutEngine;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Simulant.Core.Zone
{
    internal sealed partial class ZoneService
    {
        private readonly PluginHost _host;
        private readonly object _stateGate = new object();
        private readonly PhaseApplication _phaseApplication = new PhaseApplication();
        private readonly PhaseApplication _entranceApplication = new PhaseApplication();
        private Task _openingEntrance = Task.CompletedTask;
        private TerritorySpawnData _spawnData;
        private uint _simulatedTerritoryId;
        private int _worldVersion;
        private GreenScreenStage _greenScreen;
        public ZoneService(PluginHost host)
        {
            _host = host;
        }

        internal GreenScreenStage ActiveGreenScreen
        {
            get
            {
                GreenScreenStage stage;
                lock (_stateGate)
                {
                    stage = _isInSimulatedTerritory && _simulatedTerritoryId == GreenScreenStage.TerritoryId ? _greenScreen : null;
                }

                return stage?.IsReady == true ? stage : null;
            }
        }

        private const uint InstanceContentDirectorPrefix = 0x80030000;
        private const uint PublicContentDirectorPrefix = 0x80040000;

        // 保留完整编号，切换区域时用同一个编号清理旧 Director。
        private uint _loadedContentDirectorId;

        internal uint ContentDirectorId
        {
            get
            {
                lock (_stateGate)
                    return _loadedContentDirectorId;
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
            GreenScreenStage greenScreen;
            lock (_stateGate)
            {
                _worldVersion++;
                _isInSimulatedTerritory = false;
                _simulatedTerritoryId = 0;
                _initialTerritoryId = null;
                _initialPosition = null;
                _returnLoadRequested = false;
                _loadedContentDirectorId = 0;
                _spawnData = null;
                greenScreen = _greenScreen;
                _greenScreen = null;
            }

            CancelPendingPhase();
            ReleaseGreenScreen(greenScreen);
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

            GreenScreenStage newStage = null;
            try
            {
                int worldVersion;
                bool recordInitialState;
                GreenScreenStage greenScreen = null;

                lock (_stateGate)
                {
                    if (_returnLoadRequested)
                        throw new InvalidOperationException("尚未确认返回原区域，请先重试退出模拟。");

                    worldVersion = _worldVersion;
                    recordInitialState = !_initialTerritoryId.HasValue;
                    if (territoryId == GreenScreenStage.TerritoryId)
                        greenScreen = _greenScreen;
                }

                var gameMain = GameMain.Instance;

                if (gameMain.IsNull())
                    throw new InvalidOperationException("GameMain.Instance 为空。");

                if (territoryId == GreenScreenStage.TerritoryId && greenScreen == null)
                    greenScreen = newStage = GreenScreenStage.Load();

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
                    _greenScreen?.BeginTransition();
                    if (newStage != null)
                    {
                        _greenScreen = newStage;
                        newStage = null;
                    }
                }

                LoadTerritoryInternal(territoryId, territory, worldVersion, storyProgress);

                lock (_stateGate)
                {
                    EnsureWorld(worldVersion);
                    _isInSimulatedTerritory = true;
                    _simulatedTerritoryId = (uint)territoryId;
                }

                _openingEntrance = PrepareSpawns(territory, worldVersion);
                var placeName = greenScreen != null ? GreenScreenStage.Name
                    : territory?.PlaceName.ValueNullable?.Name.ExtractText() ?? "未知区域";
                _host.LogSim($"已进入区域：{placeName} ({territoryId})");

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
            finally
            {
                ReleaseGreenScreen(newStage);
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
                    _greenScreen?.BeginTransition();
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

                GreenScreenStage greenScreen;
                lock (_stateGate)
                {
                    EnsureWorld(worldVersion);
                    if (!IsMapReady(initialTerritoryId.Value, out var state))
                        throw new InvalidOperationException("恢复位置后，原区域尚未就绪：" + state);

                    _isInSimulatedTerritory = false;
                    _initialTerritoryId = null;
                    _initialPosition = null;
                    _returnLoadRequested = false;
                    greenScreen = _greenScreen;
                    _greenScreen = null;
                }
                ReleaseGreenScreen(greenScreen);

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

        private void ReleaseGreenScreen(GreenScreenStage stage)
        {
            try
            {
                stage?.Dispose();
            }
            catch (Exception ex)
            {
                _host.LogWarning("未能释放绿幕场景资源：" + ex.Message);
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
            // 这里直接设置 Pos 可能卡在半空中，实测 ActorSetPos 可以防止角色卡住
            me.ActorSetPos(initialPosition, me.Heading);
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

        private void LoadTerritoryInternal(int territoryId, TerritoryType? territory, int worldVersion, int storyProgress = 0,
            bool exitingSimulation = false)
        {
            EnsureWorld(worldVersion);
            _entranceApplication.Cancel();

            lock (_stateGate)
                _spawnData = null;

            _host.PrepareEntityWorldChange((uint)territoryId, exitingSimulation);
            GameInterop.Connection.RunInFrame(() => SwitchTerritoryInFrame(territoryId, territory, worldVersion, storyProgress));
            EnsureWorld(worldVersion);
            _host.CompleteEntityWorldChange();
            _host.PlayerActions.ResetCooldowns();
        }

        private void SwitchTerritoryInFrame(int territoryId, TerritoryType? territory, int worldVersion, int storyProgress)
        {
            EnsureWorld(worldVersion);
            var player = _host.EntityProvider.GetMyself()
                ?? throw new InvalidOperationException("无法确认本机角色，不能清理旧区域。");
            uint loadedDirectorId;

            lock (_stateGate)
            {
                EnsureWorld(worldVersion);
                loadedDirectorId = _loadedContentDirectorId;
            }

            if (loadedDirectorId != 0)
            {
                _host.LogVerbose($"FinalizeContentDirector: {loadedDirectorId:X8}");
                EnsureWorld(worldVersion);
                EventFramework.Instance.FinalizeInstanceContent(loadedDirectorId);

                lock (_stateGate)
                {
                    EnsureWorld(worldVersion);
                    _loadedContentDirectorId = 0;
                }
            }

            // 同一帧内按原生入口回收旧区域对象，再卸载布局。客户端的整区清理会同时销毁本机角色。
            _host.LogVerbose("回收旧区域对象");
            EnsureWorld(worldVersion);
            ClearOtherCharacters(player.Address);
            ClientObjectManager.Instance.ThrowIfNull().DeleteAllObjects();
            EventObjectManager.Instance.ThrowIfNull().Clear();
            StandObjectManager.Instance().ThrowIfNull().Clear();
            ReactionEventObjectManager.Instance().ThrowIfNull().DeleteAllObjects();

            EnsureWorld(worldVersion);
            DisableCurrentObjects(worldVersion);

            var nextDirectorId = ResolveContentDirectorId(territory);

            if (nextDirectorId != 0)
            {
                _host.LogVerbose($"SetupContentDirector: {nextDirectorId:X8}");
                EnsureWorld(worldVersion);
                EventFramework.Instance.SetupInstanceContent(nextDirectorId, nextDirectorId & 0xFFFF);

                lock (_stateGate)
                {
                    EnsureWorld(worldVersion);
                    _loadedContentDirectorId = nextDirectorId;
                }
            }

            _host.LogVerbose($"LoadZone: {territoryId}");
            EnsureWorld(worldVersion);
            GameMain.Instance.ThrowIfNull().LoadZone((uint)territoryId, storyProgress);

            _host.LogVerbose($"SetTerritoryTypeId: {territoryId}");
            EnsureWorld(worldVersion);
            EventFramework.Instance.ThrowIfNull().SetTerritoryTypeId((ushort)territoryId);
        }

        private static void ClearOtherCharacters(IntPtr player)
        {
            var manager = CharacterManager.Instance().ThrowIfNull();
            var characters = manager.BattleCharas.Get();
            if (player == IntPtr.Zero || Array.IndexOf(characters, player) < 0)
                throw new InvalidOperationException("原生角色表中未找到本机角色，不能清理旧区域。");

            for (var index = 0; index < characters.Length; index++)
            {
                var character = characters[index];
                if (character != IntPtr.Zero && character != player)
                    manager.DeleteCharacterAtIndex(index);
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

        private static uint ResolveContentDirectorId(TerritoryType? territory)
        {
            var content = territory?.ContentFinderCondition.ValueNullable?.Content;
            if (!content.HasValue || content.Value.RowId == 0)
                return 0;

            var reference = content.Value;
            if (reference.Is<InstanceContent>())
                return InstanceContentDirectorPrefix | reference.RowId;
            if (reference.Is<PublicContent>())
                return PublicContentDirectorPrefix | reference.RowId;
            return 0;
        }
    }
}
