using Advanced_Combat_Tracker;
using Simulant.ACT;
using Simulant.Core.Entity;
using Simulant.Core.Zone;
using Simulant.Game;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.Game;
using TerritoryType = Lumina.Excel.Sheets.TerritoryType;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using Simulant.UI;
using SimuMagic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Simulant.Core
{
    public sealed class PluginHost
    {
        public static PluginHost Instance
        {
            get;
            set;
        } // 仅为了调试方便

        public ExternalProcessMemory Memory => GameInterop.Connection.Memory;

        private readonly Label _statusLabel;
        private readonly SimulantUI _ui;

        private readonly object _initLock = new object();
        private readonly object _subscriptionGate = new object();
        private readonly object _disposeGate = new object();
        private readonly SemaphoreSlim _processRefresh = new SemaphoreSlim(0, 1);
        private bool _retryConnection;
        private bool _disconnectPending;
        private FFXIV_ACT_Plugin.FFXIV_ACT_Plugin _subscribedXivPlugin;
        private bool _subscriptionsStopped;
        private bool _disposing;
        private volatile bool _disposeCompleted;

        internal PluginLog PluginLog = new PluginLog();
        internal Configuration Configuration { get; private set; } = new Configuration();
        internal PluginUpdater Updater { get; }
        internal string ConfigurationPath { get; } = Path.Combine(Application.StartupPath, "Config", "Simulant.config.xml");

        internal bool GameDataReady { get; private set; }
        internal bool GameDataLoading { get; private set; }

        private GameDataService _gameData;
        private GameImageFile? _gameDataFile;
        private GameImageScan _gameImage;
        internal bool PluginReady { get; private set; }
        internal bool IsInitializing { get; private set; }
        internal bool FirewallEnabled => FirewallService != null && FirewallService.IsEnabled;
        internal bool IsSwitchingTerritory => _ui?.IsSwitchingTerritory == true;
        internal bool IsGameForeground => Utils.WindowFocus.IsProcessForeground(_entityProcess?.Id ?? 0);
        internal bool CanRunDebug => PluginReady && !IsInitializing && !Disposed && !IsSwitchingTerritory
            && (FirewallEnabled || Configuration.DeveloperMode);

        internal void EnsureDebugAllowed()
        {
            if (!CanRunDebug)
                throw new InvalidOperationException("请等待插件连接游戏并完成区域切换；未开启开发者模式时，调试操作必须启用防火墙。");
        }

        internal void EnsureFirewallAllowed()
        {
            if (!PluginReady || IsInitializing || Disposed || IsSwitchingTerritory)
                throw new InvalidOperationException("请等待插件连接游戏并完成区域切换，再启用防火墙。");

            if (Configuration.DeveloperMode)
                return;

            var game = GameMain.Instance.ThrowIfNull();
            if (game.TerritoryLoadState.Get() != 2)
                throw new InvalidOperationException("请等待地图加载完成后再启用防火墙。");

            var territory = GameDataService.Instance.GetSheet<TerritoryType>().GetRowOrDefault(game.CurrentTerritoryTypeId.Get());
            if (territory?.TerritoryIntendedUse.RowId != (uint)TerritoryIntendedUseEnum.Inn)
                throw new InvalidOperationException("未开启开发者模式时，仅允许在旅馆地图内启用防火墙。");
        }

        internal Firewall.FirewallService FirewallService;
        internal ZoneService ZoneService;
        internal Environment.EnvironmentService EnvironmentService;
        internal EntityProvider EntityProvider;
        internal EntitySpawner EntitySpawner;
        internal Party.SimPartyService PartyService;
        internal EntityRegistry EntityRegistry;
        internal Test DebugTest { get; }
        internal SimCommandListener Commands { get; }
        internal SimActionService PlayerActions { get; }

        private readonly object _debugGate = new object();
        private SimScheduler _debugScheduler;
        private SimEntityManager _debugEntities;
        private bool _debugStopping;
        private int _debugStoppingThreadId;
        private SimScheduler _stoppingDebugScheduler;
        private (int Id, DateTime Started)? _entityProcess;
        private uint? _entityZoneId;
        private int _worldGeneration;
        internal int WorldGeneration => Volatile.Read(ref _worldGeneration);

        internal bool HasPendingDebugEntityCleanup
        {
            get
            {
                bool pending;

                lock (_debugGate)
                    pending = _debugStopping || (_debugEntities?.HasPendingCleanup ?? false);

                return pending || (DebugTest?.HasPendingEntityTestCleanup ?? false);
            }
        }

        // 调试入口也有明确的行为和资源归属，不再把调度器藏在实体/生成器中。
        internal SimEntityManager DebugEntities
        {
            get
            {
                EnsureDebugAllowed();
                lock (_debugGate)
                {
                    if (Disposed)
                        throw new ObjectDisposedException(nameof(PluginHost));

                    if (_debugStopping)
                        throw new InvalidOperationException("正在清理调试实体，请等待清理结束。");

                    if (_debugScheduler == null)
                    {
                        if (_debugEntities?.HasPendingCleanup ?? false)
                            throw new InvalidOperationException("上次调试实体尚未清理完成，请再次清除后重试。");

                        SimScheduler debugScheduler = null;

                        debugScheduler = new SimScheduler((ex, node) =>
                        {
                            try
                            {
                                LogError($"调试节点「{node}」失败：{ex}");
                            }
                            finally
                            {
                                StopDebugEntities(debugScheduler);
                            }
                        });

                        SimEntityManager debugEntities = null;

                        try
                        {
                            debugEntities = new SimEntityManager(EntitySpawner, ex => LogError("调试实体清理失败：" + ex))
                            {
                                PlayerBehavior = Configuration.SimulatedParty.Copy()
                            };
                            debugEntities.Scheduler = debugScheduler;
                            debugScheduler.Start();
                        }
                        catch
                        {
                            try
                            {
                                debugScheduler.Dispose();
                            }
                            finally
                            {
                                debugEntities?.Clear();
                            }

                            throw;
                        }

                        _debugScheduler = debugScheduler;
                        _debugEntities = debugEntities;
                    }

                    return _debugEntities;
                }
            }
        }

        internal void StopDebugEntities()
        {
            try
            {
                DebugTest?.StopEntityBehaviorTest();
            }
            finally
            {
                StopDebugEntities(null);
            }
        }

        private void StopDebugEntities(SimScheduler expectedScheduler)
        {
            SimScheduler scheduler;
            SimEntityManager entities;
            var waitedForStop = false;

            lock (_debugGate)
            {
                if (expectedScheduler != null && !ReferenceEquals(_debugScheduler, expectedScheduler))
                    return;

                if (_debugStopping)
                {
                    // 外部停止可能正在等待调度线程；调度线程和同线程重入不能反向等待。
                    if ((_stoppingDebugScheduler?.IsSchedulerThread ?? false) ||
                        _debugStoppingThreadId == Thread.CurrentThread.ManagedThreadId)
                        return;

                    scheduler = _stoppingDebugScheduler;
                    entities = null;

                    while (_debugStopping)
                        Monitor.Wait(_debugGate);

                    waitedForStop = true;
                }
                else
                {
                    scheduler = _debugScheduler;
                    entities = _debugEntities;
                    _debugScheduler = null;
                    _debugStopping = true;
                    _debugStoppingThreadId = Thread.CurrentThread.ManagedThreadId;
                    _stoppingDebugScheduler = scheduler;
                }
            }

            if (waitedForStop)
            {
                // 若停止由调度线程发起，清理完成时该回调仍可能尚未返回。
                scheduler?.Dispose();
                return;
            }

            try
            {
                try
                {
                    scheduler?.Dispose();
                }
                finally
                {
                    entities?.Clear();
                }
            }
            finally
            {
                lock (_debugGate)
                {
                    // 失败记录继续留在原 manager，下一次手动清理只重试这一批资源。
                    if (ReferenceEquals(_debugEntities, entities) && !(entities?.HasPendingCleanup ?? false))
                        _debugEntities = null;

                    _debugStopping = false;
                    _debugStoppingThreadId = 0;
                    _stoppingDebugScheduler = null;
                    Monitor.PulseAll(_debugGate);
                }
            }
        }

        internal void PrepareEntityWorldChange(uint? nextZoneId = null, bool exitingSimulation = false)
        {
            void Cleanup(Action action)
            {
                try
                {
                    action();
                }
                catch (Exception error) when (exitingSimulation)
                {
                    LogWarning("退出模拟时清理失败：" + error.Message + "；继续返回真实区域。");
                }
            }

            Cleanup(() => _ui?.StopCurrentSession());
            Cleanup(StopDebugEntities);
            Cleanup(VfxManager.Clear);
            Cleanup(() => PlayerActions?.Reset());
            Cleanup(() => PartyService?.RestoreAll());
            Cleanup(() => EntitySpawner?.Clear());
            EntitySpawner?.ResetWorld();
            VfxManager.InvalidateWorld();
            Interlocked.Increment(ref _worldGeneration);
            // 本地切图已经在这里清理；稍后到达的相同区域通知不应停止新会话。
            _entityZoneId = nextZoneId ?? XivPluginInterop.plugin?.DataRepository.GetCurrentTerritoryID();
        }

        internal void CompleteEntityWorldChange()
        {
            lock (_initLock)
            {
                if (PluginReady && !IsInitializing && !Disposed)
                    EntitySpawner.ResumeWorld();
            }
        }

        public PluginHost(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            _statusLabel = pluginStatusText;
            pluginScreenSpace.Text = "仿生石";

            try
            {
                Configuration = Simulant.Configuration.Load(ConfigurationPath);

                if (!File.Exists(ConfigurationPath))
                    Configuration.Save(ConfigurationPath);
            }
            catch (Exception error)
            {
                LogError("读取或创建全局配置失败，本次使用默认设置，已有文件保持不变：" + error);
            }

            Updater = new PluginUpdater(
                () => ActGlobals.oFormActMain?.ActPlugins.FirstOrDefault(x => x.pluginObj?.GetType() == typeof(PluginMain))?.pluginFile?.FullName,
                typeof(PluginMain).Assembly.GetName().Version, () => Configuration.AutoInstallUpdates, LogWarning, LogRuntime);
            _ui = new SimulantUI(this)
            {
                Dock = DockStyle.Fill
            };
            pluginScreenSpace.Controls.Add(_ui);

            FirewallService = new Firewall.FirewallService(this);
            ZoneService = new ZoneService(this);
            EnvironmentService = new Environment.EnvironmentService(this);
            EntityRegistry = new EntityRegistry();
            EntityProvider = new EntityProvider(EntityRegistry,
                () => XivPluginInterop.plugin?.DataRepository.GetCurrentPlayerID() ?? 0);
            EntitySpawner = new EntitySpawner(EntityRegistry, EntityProvider, LogError, () => CanRunDebug);
            PartyService = new Party.SimPartyService(this);
            DebugTest = new Test(this);
            Commands = new SimCommandListener(LogError);
            PlayerActions = new SimActionService(this);
            Commands.Received += PlayerActions.Receive;
            Commands.Received += _ui.ReceiveSimulationCommand;

            _statusLabel.Text = "仿生石已启动，等待 FFXIV 解析插件绑定游戏。";

            _ = WatchGameProcessAsync();
            _ = Updater.CheckAsync();
        }

        private async Task<GameImageScan> PrepareGameAsync(string path, Func<bool> isCurrent)
        {
            var file = new GameImageFile(path);
            var scan = PrepareGameImageAsync(file);
            await Task.WhenAll(scan, LoadGameDataAsync(file, isCurrent));
            return scan.Result;
        }

        private async Task<GameImageScan> PrepareGameImageAsync(GameImageFile file)
        {
            if (_gameImage?.File.Matches(file) == true)
                return _gameImage;

            LogRuntime("正在扫描游戏 EXE：" + file.Path);
            var image = await Task.Run(() => GameImageScanner.Scan(file));
            if (Disposed)
                return image;

            _gameImage = image;
            foreach (var address in image.Addresses.Where(address => !address.Value.HasValue))
                LogError("地址扫描失败：" + SigAddressScanner.GetPropKey(address.Key));

            LogRuntime($"EXE 扫描完成，定位地址 {image.Addresses.Count(address => address.Value.HasValue)} / {image.Addresses.Count}。");
            return image;
        }

        private async Task LoadGameDataAsync(GameImageFile file, Func<bool> isCurrent)
        {
            GameDataService candidate = null;

            try
            {
                if (_gameDataFile?.Matches(file) == true)
                {
                    if (isCurrent())
                        GameDataReady = true;
                    return;
                }

                GameDataLoading = true;
                _ui?.UpdateControlStates();
                LogRuntime("正在读取游戏客户端数据……");
                candidate = await Task.Run(() => new GameDataService(file.Path));

                lock (_initLock)
                {
                    if (!isCurrent())
                        return;

                    candidate.Activate();
                    _gameData = candidate;
                    _gameDataFile = file;
                    candidate = null;
                    GameDataReady = true;
                }

                LogRuntime("游戏数据加载完成（Lumina.ACT）。");
            }
            catch (Exception error)
            {
                if (isCurrent())
                {
                    GameDataReady = false;
                    LogError("游戏数据加载失败：" + error.Message);
                }
            }
            finally
            {
                candidate?.Dispose();
                GameDataLoading = false;
                if (!Disposed)
                    _ui?.UpdateControlStates();
            }
        }

        internal void SaveConfiguration(Configuration configuration)
        {
            if (Disposed)
                throw new InvalidOperationException("插件已卸载，不能保存配置。");

            var stopDebug = Configuration.DeveloperMode && !configuration.DeveloperMode && !FirewallEnabled;
            lock (_initLock)
            {
                var snapshot = configuration.Copy();
                // 全局设置窗口可能打开较久，保留期间自动保存的预设选项和悬浮窗位置。
                snapshot.Presets = Configuration.Copy().Presets;
                snapshot.QuickControlsLocation = Configuration.QuickControlsLocation;
                snapshot.Save(ConfigurationPath);

                Configuration = snapshot;
            }

            if (stopDebug)
            {
                try
                {
                    StopDebugEntities();
                }
                catch (Exception error)
                {
                    LogWarning("开发者模式已关闭，停止调试时发生错误：" + error.Message);
                }
            }

            _ui?.UpdateControlStates();
            SignalProcessRefresh();
            LogRuntime("全局配置已保存。未连接时使用配置的游戏路径，模拟队员设置从下一次模拟开始生效。");
        }

        internal void SaveQuickControlsLocation(System.Drawing.Point location)
        {
            try
            {
                lock (_initLock)
                {
                    if (Disposed || Configuration.QuickControlsLocation == location)
                        return;
                    var snapshot = Configuration.Copy();
                    snapshot.QuickControlsLocation = location;
                    snapshot.Save(ConfigurationPath);
                    Configuration = snapshot;
                }
            }
            catch (Exception error)
            {
                LogWarning("保存快捷控制悬浮窗位置失败：" + error.Message);
            }
        }

        internal void SavePresetOptions(Simulation.SimPresetBase preset)
        {
            lock (_initLock)
            {
                if (Disposed)
                    return;

                var snapshot = Configuration.Copy();
                var values = preset.Options.Select(option =>
                    new KeyValuePair<string, string>(option.PropertyName, option.SerializeValue()));
                if (!snapshot.UpdatePreset(preset.GetType().FullName, values))
                    return;

                snapshot.Save(ConfigurationPath);
                Configuration = snapshot;
            }
        }

        internal void RetryGameConnection()
        {
            _retryConnection = true;
            SignalProcessRefresh();
        }

        /// <summary>用解析插件当前绑定的进程校验预扫结果，并在连接就绪后开放操作。</summary>
        private async Task<bool> ConnectGameProcessAsync(Process process, (int Id, DateTime Started) identity)
        {
            bool IsCurrent()
            {
                if (Disposed)
                    return false;

                RefreshXivPlugin();
                return GetProcessIdentity(ReadGameProcess()) == identity;
            }

            IsInitializing = true;
            _ui?.UpdateControlStates();

            try
            {
                if (!System.Environment.Is64BitProcess)
                    throw new InvalidOperationException("模拟器需要 64 位 ACT。");

                var module = process.MainModule;
                var path = module.FileName;
                var image = await PrepareGameAsync(path, IsCurrent);

                lock (_initLock)
                {
                    if (!IsCurrent())
                        return false;

                    if (!GameDataReady)
                        return true;

                    if (!image.File.Matches(new GameImageFile(path)))
                        return false;

                    PluginReady = false;
                    lock (_subscriptionGate)
                        Commands.Stop();

                    ZoneService.CancelPendingPhase();
                    if (GameInterop.IsConnectedTo(process))
                        PrepareEntityWorldChange();

                    GameInterop.Disconnect();

                    // EXE 扫描和旧会话清理期间解析插件都可能切换进程。
                    if (!IsCurrent())
                        return false;

                    try
                    {
                        GameInterop.Connect(process, module.BaseAddress, image, LogWarning);
                        SigAddressScanner.Bind(image, module.BaseAddress);

                        if (!IsCurrent())
                        {
                            GameInterop.Disconnect();
                            return false;
                        }

                        EntitySpawner.ResumeWorld();
                        lock (_subscriptionGate)
                            Commands.Start(ActGlobals.oFormActMain);
                        PluginReady = true;
                    }
                    catch
                    {
                        GameInterop.Disconnect();
                        throw;
                    }

                    RememberGamePath(path);
                }

                LogRuntime($"已连接 FFXIV 进程（PID {identity.Id}），开始监听 sim 默语命令。");
                _statusLabel.Text = "仿生石已连接游戏。";
                return true;
            }
            catch (Exception error)
            {
                if (!IsCurrent())
                    return false;

                _statusLabel.Text = PluginReady ? "重试失败，仍连接到游戏。" : "连接游戏失败，可重试连接。";
                LogError("准备或连接游戏失败：" + error);
                return true;
            }
            finally
            {
                IsInitializing = false;
                if (!Disposed)
                    _ui?.UpdateControlStates();
            }
        }

        private void RememberGamePath(string path)
        {
            if (!string.IsNullOrWhiteSpace(Configuration.FfxivPath))
                return;

            try
            {
                var snapshot = Configuration.Copy();
                snapshot.FfxivPath = path;
                snapshot.Save(ConfigurationPath);
                Configuration = snapshot;
                LogRuntime("已保存解析插件绑定的游戏路径：" + path);
            }
            catch (Exception error)
            {
                LogWarning("保存自动检测的游戏路径失败：" + error.Message);
            }
        }

        public bool Disposed { get; private set; }

        public void Dispose()
        {
            lock (_disposeGate)
            {
                if (_disposeCompleted)
                    return;

                if (_disposing)
                    throw new InvalidOperationException("插件正在卸载，请等待本轮清理结束后重试。");

                _disposing = true;

                try
                {
                    lock (_initLock)
                    {
                        Disposed = true;
                        PluginReady = false;
                    }

                    if (_ui != null && !_ui.IsDisposed)
                        _ui.Enabled = false;

                    Updater?.Dispose();

                    List<Exception> failures = null;

                    void TryCleanup(string message, Action cleanup)
                    {
                        try
                        {
                            cleanup();
                        }
                        catch (Exception error)
                        {
                            (failures ?? (failures = new List<Exception>())).Add(
                                new InvalidOperationException(message, error));

                            try
                            {
                                LogError(message + "：" + error);
                            }
                            catch
                            { /* 日志失败不能遮蔽清理错误或中断其余资源回收。 */
                            }
                        }
                    }

                    void ThrowIfFailed()
                    {
                        if (failures != null)
                            throw new AggregateException("插件卸载未完成，清理记录已保留；请再次卸载或重新启用插件以重试。", failures);
                    }

                    TryCleanup("卸载时取消命令日志订阅失败", () =>
                    {
                        lock (_subscriptionGate)
                            Commands?.Stop();
                    });

                    // 资源清理失败可留待重试，但不能阻止返回真实环境并恢复通信。
                    TryCleanup("卸载时取消阶段等待失败", () => ZoneService?.CancelPendingPhase());

                    TryCleanup("卸载时停止模拟失败", () => _ui?.StopCurrentSession());
                    TryCleanup("卸载时停止调试失败", StopDebugEntities);
                    TryCleanup("卸载时清理模拟技能失败", () => PlayerActions?.Reset());
                    TryCleanup("卸载时恢复小队失败", () => PartyService?.RestoreAll());
                    TryCleanup("卸载时清理特效失败", VfxManager.Clear);
                    TryCleanup("卸载时释放实体资源失败", () => EntitySpawner?.Dispose());

                    var returned = false;
                    TryCleanup("卸载时返回真实区域失败", () =>
                    {
                        if (ZoneService?.HasPendingReturn == true && !ZoneService.ExitToInitialTerritory())
                            throw new InvalidOperationException("尚未返回真实区域，防火墙保持原状。");

                        returned = true;
                    });

                    if (!returned)
                        ThrowIfFailed();

                    TryCleanup("卸载时关闭防火墙失败", () =>
                    {
                        if (FirewallService?.HasPendingCleanup == true)
                            FirewallService.Disable();
                    });

                    ThrowIfFailed();

                    lock (_subscriptionGate)
                    {
                        // 失败期间继续接收世界失效；全部原生资源清理完成后才取消订阅。
                        _subscriptionsStopped = true;
                        var subscribedPlugin = _subscribedXivPlugin;

                        if (subscribedPlugin != null)
                        {
                            TryCleanup("卸载时取消进程订阅失败", () =>
                                subscribedPlugin.DataSubscription.ProcessChanged -= OnFFXIVProcessChanged);
                            TryCleanup("卸载时取消区域订阅失败", () =>
                                subscribedPlugin.DataSubscription.ZoneChanged -= OnFFXIVZoneChanged);

                            if (failures == null)
                            {
                                _subscribedXivPlugin = null;
                                XivPluginInterop.plugin = null;
                            }
                        }
                    }

                    ThrowIfFailed();

                    GameInterop.Disconnect();
                    _gameData?.Dispose();
                    _gameData = null;
                    _gameDataFile = null;
                    _gameImage = null;
                    GameDataReady = false;
                    _ui?.Dispose();

                    if (!_statusLabel.IsDisposed)
                        _statusLabel.Text = "插件已卸载";

                    _disposeCompleted = true;
                }
                catch
                {
                    try
                    {
                        if (!_statusLabel.IsDisposed)
                            _statusLabel.Text = "插件卸载未完成，请再次卸载或重新启用以重试。";
                    }
                    catch
                    { /* 状态栏失效时仍须向调用方报告原始清理失败。 */
                    }

                    throw;
                }
                finally
                {
                    _disposing = false;
                }
            }
        }

        /// <summary>在 ACT 界面线程串行处理启动预扫、解析插件通知、定期检查和手动重试。</summary>
        private async Task WatchGameProcessAsync()
        {
            // 等待 PluginMain 登记当前宿主后，再开始自动初始化。
            await Task.Yield();
            string attemptedPath = null;
            string observationError = null;
            var connectPending = false;

            while (!_disposeCompleted)
            {
                try
                {
                    RefreshXivPlugin();
                    var process = ReadGameProcess();
                    var identity = GetProcessIdentity(process);
                    if (UpdateGameProcess(identity))
                    {
                        connectPending = identity.HasValue;
                        attemptedPath = null;
                    }

                    var retry = _retryConnection;
                    _retryConnection = false;
                    var oldConnectionClosed = !Disposed && FinishProcessDisconnect();
                    if (!Disposed && identity.HasValue)
                    {
                        connectPending |= retry;
                        if (connectPending && oldConnectionClosed && !FirewallService.HasPendingCleanup)
                            connectPending = !await ConnectGameProcessAsync(process, identity.Value);
                    }
                    else if (!Disposed)
                    {
                        if (retry)
                            LogRuntime("正在等待 FFXIV 解析插件绑定游戏进程。");

                        var configuredPath = Configuration.FfxivPath;
                        if (retry || !string.Equals(attemptedPath, configuredPath, StringComparison.OrdinalIgnoreCase))
                        {
                            attemptedPath = configuredPath;
                            var path = FfxivInstallation.Resolve(configuredPath);
                            if (path != null)
                                await PrepareGameAsync(path, () => !Disposed &&
                                    string.Equals(Configuration.FfxivPath, configuredPath, StringComparison.OrdinalIgnoreCase) &&
                                    !GetProcessIdentity(ReadGameProcess()).HasValue);
                        }
                    }

                    observationError = null;
                }
                catch (Exception error)
                {
                    if (!Disposed && observationError != error.Message)
                        LogError("准备客户端或读取 FFXIV 绑定状态失败：" + error.Message);
                    observationError = error.Message;
                }

                // 部分版本的解析插件偶尔漏报 ProcessChanged，定期以 DataRepository 为准。
                if (!_disposeCompleted)
                    await _processRefresh.WaitAsync(1000);
            }
        }

        private void RefreshXivPlugin()
        {
            lock (_subscriptionGate)
            {
                if (Disposed || _subscriptionsStopped)
                    return;

                var parser = ActGlobals.oFormActMain?.ActPlugins
                    .Where(entry => entry.cbEnabled?.Checked != false)
                    .Select(entry => entry.pluginObj as FFXIV_ACT_Plugin.FFXIV_ACT_Plugin)
                    .FirstOrDefault(plugin => plugin?.PluginStarted == true &&
                        plugin.DataRepository != null && plugin.DataSubscription != null);
                var previous = _subscribedXivPlugin;
                if (ReferenceEquals(previous, parser))
                    return;

                if (previous != null)
                {
                    previous.DataSubscription.ProcessChanged -= OnFFXIVProcessChanged;
                    previous.DataSubscription.ZoneChanged -= OnFFXIVZoneChanged;
                }

                _subscribedXivPlugin = parser;
                XivPluginInterop.plugin = parser;

                if (parser != null)
                {
                    parser.DataSubscription.ProcessChanged += OnFFXIVProcessChanged;
                    parser.DataSubscription.ZoneChanged += OnFFXIVZoneChanged;
                }
            }
        }

        private Process ReadGameProcess() => _subscribedXivPlugin?.DataRepository?.GetCurrentFFXIVProcess();

        private static (int Id, DateTime Started)? GetProcessIdentity(Process process)
        {
            try
            {
                return process == null || process.HasExited ? ((int, DateTime)?)null : (process.Id, process.StartTime);
            }
            catch (InvalidOperationException)
            {
                // 读取 PID 和启动时间之间进程可能已经退出。
                return null;
            }
        }

        private void OnFFXIVProcessChanged(Process process) => SignalProcessRefresh();

        private void SignalProcessRefresh()
        {
            lock (_processRefresh)
            {
                if (_processRefresh.CurrentCount == 0)
                    _processRefresh.Release();
            }
        }

        /// <summary>切换目标时使旧世界和地址失效，并尝试恢复旧进程中仍存在的防火墙。</summary>
        private bool UpdateGameProcess((int Id, DateTime Started)? identity)
        {
            Exception cleanupError = null;

            lock (_initLock)
            {
                if (_disposeCompleted || _entityProcess == identity)
                    return false;

                _entityProcess = identity;
                PluginReady = false;
                lock (_subscriptionGate)
                    Commands?.Stop();

                Interlocked.Increment(ref _worldGeneration);
                VfxManager.InvalidateWorld();
                _entityZoneId = _subscribedXivPlugin?.DataRepository?.GetCurrentTerritoryID();
                ZoneService?.InvalidateWorld();
                PartyService?.InvalidateWorld();
                EntitySpawner?.ResetWorld();

                _disconnectPending = true;
                try
                {
                    FirewallService?.ResetProcess();
                    GameInterop.Disconnect();
                    _disconnectPending = false;
                }
                catch (Exception error)
                {
                    // 在旧进程的防火墙恢复完成前，保留它的连接供清理使用。
                    cleanupError = error;
                }
            }

            if (!Disposed)
            {
                _statusLabel.Text = identity.HasValue ? "正在准备连接 FFXIV……" : "等待 FFXIV 解析插件绑定游戏。";
                _ui?.OnEntityWorldChanged();
            }

            if (cleanupError != null)
                LogError("切换进程时清理旧连接失败，请重试关闭防火墙：" + cleanupError);

            return true;
        }

        private bool FinishProcessDisconnect()
        {
            lock (_initLock)
            {
                if (!_disconnectPending)
                    return true;

                if (FirewallService.HasPendingCleanup && !FirewallService.TryResetExitedProcess())
                    return false;

                GameInterop.Disconnect();
                _disconnectPending = false;
            }

            _ui?.UpdateControlStates();
            return true;
        }

        private void OnFFXIVZoneChanged(uint zoneId, string zoneName)
        {
            lock (_initLock)
            {
                if (_disposeCompleted || _entityZoneId == zoneId)
                    return;

                _entityZoneId = zoneId;
                Interlocked.Increment(ref _worldGeneration);
                VfxManager.InvalidateWorld();
                ZoneService?.InvalidateWorld();
                PartyService?.InvalidateWorld();
                // 外部换图发生后只能失效旧引用，不能再尝试释放上一场景的内存。
                EntitySpawner?.ResetWorld();
                PlayerActions?.Reset();
            }

            _ui?.OnEntityWorldChanged();
        }

        internal void Log(LogType type, string message)
        {
            PluginLog.Add(type, message);
            _ui?.RefreshLogView();
        }

        internal void LogError(string message) => Log(LogType.Error, message);
        internal void LogWarning(string message) => Log(LogType.Warning, message);
        internal void LogRuntime(string message) => Log(LogType.Runtime, message);
        internal void LogSim(string message) => Log(LogType.Sim, message);
        internal void LogVerbose(string message) => Log(LogType.Verbose, message);
    }
}
