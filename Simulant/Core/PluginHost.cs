using Advanced_Combat_Tracker;
using Simulant.ACT;
using Simulant.Core.Entity;
using Simulant.Core.Zone;
using Simulant.Game;
using Simulant.Game.ExtractedCsv;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using Simulant.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Simulant.Core
{
    public sealed class PluginHost
    {
        public static PluginHost Instance { get; set; } // 仅为了调试方便

        public GreyMagicExternalProcessMemory Memory => NamazuInterop.Plugin.Memory;

        private readonly Label _statusLabel;
        private readonly SimulantUI _ui;

        private readonly object _initLock = new object();
        private readonly object _subscriptionGate = new object();
        private readonly object _disposeGate = new object();
        private FFXIV_ACT_Plugin.FFXIV_ACT_Plugin _subscribedXivPlugin;
        private bool _subscriptionsStopped;
        private bool _disposing;
        private volatile bool _disposeCompleted;

        internal PluginLog PluginLog = new PluginLog();

        internal bool CsvReady { get; private set; }
        internal bool CsvLoading { get; private set; }
        internal bool PluginReady { get; private set; }
        internal bool IsInitializing { get; private set; }
        internal bool FirewallEnabled => FirewallService != null && FirewallService.IsEnabled;
        internal bool IsSwitchingTerritory => _ui?.IsSwitchingTerritory == true;

        internal Firewall.FirewallService FirewallService;
        internal ZoneService ZoneService;
        internal Environment.EnvironmentService EnvironmentService;
        internal EntityProvider EntityProvider;
        internal EntitySpawner EntitySpawner;
        internal EntityRegistry EntityRegistry;
        internal Test DebugTest { get; }
        private readonly object _debugGate = new object();
        private SimScheduler _debugScheduler;
        private SimEntityManager _debugEntities;
        private bool _debugStopping;
        private int _debugStoppingThreadId;
        private SimScheduler _stoppingDebugScheduler;
        private int? _entityProcessId;
        private int _entityProcessGeneration;
        private uint? _entityZoneId;

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
                lock (_debugGate)
                {
                    if (Disposed) throw new ObjectDisposedException(nameof(PluginHost));
                    if (_debugStopping)
                        throw new InvalidOperationException("正在清理调试实体，请等待清理结束。");
                    if (_debugScheduler == null)
                    {
                        if (_debugEntities?.HasPendingCleanup ?? false)
                            throw new InvalidOperationException("上次调试实体尚未清理完成，请再次清除后重试。");
                        SimScheduler debugScheduler = null;
                        debugScheduler = new SimScheduler((ex, node) =>
                        {
                            try { LogError($"调试节点「{node}」失败：{ex}"); }
                            finally { StopDebugEntities(debugScheduler); }
                        });
                        SimEntityManager debugEntities = null;
                        try
                        {
                            debugEntities = new SimEntityManager(EntitySpawner, ex => LogError("调试实体清理失败：" + ex));
                            debugEntities.Scheduler = debugScheduler;
                            debugScheduler.Start();
                        }
                        catch
                        {
                            try { debugScheduler.Dispose(); }
                            finally { debugEntities?.Clear(); }
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
            try { DebugTest?.StopEntityBehaviorTest(); }
            finally { StopDebugEntities(null); }
        }

        private void StopDebugEntities(SimScheduler expectedScheduler)
        {
            SimScheduler scheduler;
            SimEntityManager entities;
            var waitedForStop = false;
            lock (_debugGate)
            {
                if (expectedScheduler != null && !ReferenceEquals(_debugScheduler, expectedScheduler)) return;
                if (_debugStopping)
                {
                    // 外部停止可能正在等待调度线程；调度线程和同线程重入不能反向等待。
                    if ((_stoppingDebugScheduler?.IsSchedulerThread ?? false) ||
                        _debugStoppingThreadId == Thread.CurrentThread.ManagedThreadId)
                        return;
                    scheduler = _stoppingDebugScheduler;
                    entities = null;
                    while (_debugStopping) Monitor.Wait(_debugGate);
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
                try { scheduler?.Dispose(); }
                finally { entities?.Clear(); }
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

        internal void PrepareEntityWorldChange(uint? nextZoneId = null)
        {
            _ui?.StopCurrentSession();
            StopDebugEntities();
            EntitySpawner?.Clear();
            EntitySpawner?.ResetWorld();
            // 本地切图已经在这里清理；稍后到达的相同区域通知不应停止新会话。
            _entityZoneId = nextZoneId ?? XivPluginInterop.plugin?.DataRepository.GetCurrentTerritoryID();
        }

        internal void CompleteEntityWorldChange()
        {
            lock (_initLock)
            {
                if (PluginReady && !IsInitializing && !Disposed) EntitySpawner.ResumeWorld();
            }
        }

        public PluginHost(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            _statusLabel = pluginStatusText;
            pluginScreenSpace.Text = "仿生石";

            _ui = new SimulantUI(this) { Dock = DockStyle.Fill };
            pluginScreenSpace.Controls.Add(_ui);

            FirewallService = new Firewall.FirewallService(this);
            ZoneService = new ZoneService(this);
            EnvironmentService = new Environment.EnvironmentService(this);
            EntityRegistry = new EntityRegistry();
            EntityProvider = new EntityProvider(EntityRegistry,
                () => XivPluginInterop.plugin?.DataRepository.GetCurrentPlayerID() ?? 0);
            EntitySpawner = new EntitySpawner(EntityRegistry, EntityProvider, LogError,
                () => FirewallEnabled && PluginReady && !IsInitializing && !Disposed);
            DebugTest = new Test(this);

            Attach();

            _statusLabel.Text = "仿生石已启动。";

            _ = LoadCsvAsync();
        }

        internal async Task LoadCsvAsync()
        {
            if (CsvReady || CsvLoading)
                return;

            CsvLoading = true;
            _ui?.UpdateControlStates();

            try
            {
                LogRuntime("正在加载 CSV 数据……");

                await Task.Run(() =>
                {
                    CsvManager.Instance.LoadAllTables();
                });

                CsvReady = true;
                LogRuntime("CSV 数据加载完成。");
                LogRuntime("如需启用模拟，请先在左侧初始化插件，然后启用防火墙。");
            }
            catch (Exception ex)
            {
                CsvReady = false;
                LogError("CSV 数据加载失败：" + ex);
            }
            finally
            {
                CsvLoading = false;
                _ui?.UpdateControlStates();
            }
        }

        internal async Task InitAsync()
        {
            int processGeneration;
            lock (_initLock)
            {
                if (Disposed)
                {
                    LogWarning("插件正在卸载，不能重新初始化；请先重试完成卸载。");
                    return;
                }
                if (IsInitializing)
                {
                    LogWarning("插件正在初始化，跳过重复请求。");
                    return;
                }
                if (FirewallService.HasPendingCleanup)
                {
                    LogWarning("防火墙尚未完全关闭，请先关闭防火墙并完成清理，再重新初始化插件。");
                    return;
                }

                IsInitializing = true;
                PluginReady = false;
                processGeneration = _entityProcessGeneration;
            }

            _ui?.UpdateControlStates();

            try
            {
                ZoneService.CancelPendingPhase();
                PrepareEntityWorldChange();
                LogRuntime("正在绑定鲶鱼精邮差……");
                await Task.Run(() => NamazuInterop.Init());

                LogRuntime("正在绑定 Triggernometry……");
                await Task.Run(() => TriggernometryInterop.Init());

                LogRuntime("正在扫描地址……");
                var scanResult = await Task.Run(() => SigAddressScanner.Scan());

                var errorLines = scanResult
                    .Where(x => !string.IsNullOrEmpty(x.Value))
                    .Select(x => $"{x.Key}: {x.Value}")
                    .ToList();

                foreach (var line in errorLines)
                {
                    LogError("地址扫描失败：" + line);
                }

                LogRuntime($"地址扫描完成，成功：{scanResult.Count - errorLines.Count} / {scanResult.Count}");

                lock (_initLock)
                {
                    if (Disposed || processGeneration != _entityProcessGeneration)
                        throw new InvalidOperationException("初始化期间游戏进程发生变化，请重新初始化插件。");
                    EntitySpawner.ResumeWorld();
                    PluginReady = true;
                }
                LogRuntime("插件初始化完成。");
            }
            catch (Exception ex)
            {
                PluginReady = false;
                LogError("初始化插件失败：" + ex);
            }
            finally
            {
                lock (_initLock)
                {
                    IsInitializing = false;
                }

                _ui?.UpdateControlStates();
            }
        }

        public bool Disposed { get; private set; }

        public void Dispose()
        {
            lock (_disposeGate)
            {
                if (_disposeCompleted) return;
                if (_disposing)
                    throw new InvalidOperationException("插件正在卸载，请等待本轮清理结束后重试。");
                _disposing = true;
                try
                {
                    bool initializationPending;
                    lock (_initLock)
                    {
                        Disposed = true;
                        PluginReady = false;
                        initializationPending = IsInitializing;
                    }
                    if (_ui != null && !_ui.IsDisposed) _ui.Enabled = false;

                    List<Exception> failures = null;
                    void TryCleanup(string message, Action cleanup)
                    {
                        try { cleanup(); }
                        catch (Exception error)
                        {
                            (failures ?? (failures = new List<Exception>())).Add(
                                new InvalidOperationException(message, error));
                            try { LogError(message + "：" + error); }
                            catch { /* 日志失败不能遮蔽清理错误或中断其余资源回收。 */ }
                        }
                    }
                    void ThrowIfFailed()
                    {
                        if (failures != null)
                            throw new AggregateException("插件卸载未完成，清理记录已保留；请再次卸载或重新启用插件以重试。", failures);
                    }

                    // 保留真实地图记录，只有资源回收完成后才返回并恢复通信。
                    TryCleanup("卸载时取消阶段等待失败", () => ZoneService?.CancelPendingPhase());
                    if (initializationPending)
                        throw new InvalidOperationException("初始化尚未结束，旧宿主已停止接受操作；请稍后重试卸载或重新启用。");
                    TryCleanup("卸载时停止模拟失败", () => _ui?.StopCurrentSession());
                    TryCleanup("卸载时停止调试失败", StopDebugEntities);
                    TryCleanup("卸载时释放实体资源失败", () => EntitySpawner?.Dispose());
                    ThrowIfFailed();

                    TryCleanup("卸载时返回真实区域失败", () =>
                    {
                        if (ZoneService?.HasPendingReturn == true && !ZoneService.ExitToInitialTerritory())
                            throw new InvalidOperationException("尚未返回真实区域，防火墙保持原状。");
                    });
                    ThrowIfFailed();

                    TryCleanup("卸载时关闭防火墙失败", () =>
                    {
                        if (FirewallService?.HasPendingCleanup == true) FirewallService.Disable();
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
                            if (failures == null) _subscribedXivPlugin = null;
                        }
                    }
                    ThrowIfFailed();

                    _ui?.Dispose();
                    if (!_statusLabel.IsDisposed) _statusLabel.Text = "插件已卸载";
                    _disposeCompleted = true;
                }
                catch
                {
                    try
                    {
                        if (!_statusLabel.IsDisposed) _statusLabel.Text = "插件卸载未完成，请再次卸载或重新启用以重试。";
                    }
                    catch { /* 状态栏失效时仍须向调用方报告原始清理失败。 */ }
                    throw;
                }
                finally { _disposing = false; }
            }
        }

        private void Attach()
        {
            if (ActGlobals.oFormActMain == null)
            {
                XivPluginInterop.plugin = null;
                return;
            }

            var ffxivPluginData = ActGlobals.oFormActMain.ActPlugins
                .FirstOrDefault(x => x.pluginObj != null
                    && x.pluginObj.GetType().ToString() == "FFXIV_ACT_Plugin.FFXIV_ACT_Plugin");

            var plugin = (FFXIV_ACT_Plugin.FFXIV_ACT_Plugin)ffxivPluginData?.pluginObj;
            XivPluginInterop.plugin = plugin;

            if (plugin == null)
                return;

            async Task WaitForPluginAsync()
            {
                try
                {
                    while (!Disposed)
                    {
                        if (ffxivPluginData.lblPluginStatus.IsDisposed)
                            return;

                        if (ffxivPluginData.lblPluginStatus.Text.IndexOf("STARTED", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            lock (_subscriptionGate)
                            {
                                if (Disposed || _subscriptionsStopped || _subscribedXivPlugin != null)
                                    return;

                                // 缓存实例不等于本宿主已订阅；重新启用插件时也必须走这里。
                                _entityZoneId = plugin.DataRepository.GetCurrentTerritoryID();
                                _subscribedXivPlugin = plugin;
                                plugin.DataSubscription.ProcessChanged += OnFFXIVProcessChanged;
                                plugin.DataSubscription.ZoneChanged += OnFFXIVZoneChanged;
                            }
                            OnFFXIVProcessChanged(plugin.DataRepository.GetCurrentFFXIVProcess());
                            return;
                        }

                        await Task.Delay(3000);
                    }
                }
                catch (Exception ex)
                {
                    if (!Disposed)
                        LogError("等待 FFXIV 插件启动失败：" + ex.Message);
                }
            }

            _ = WaitForPluginAsync();
        }

        private void OnFFXIVProcessChanged(Process process)
        {
            if (_disposeCompleted) return;
            var processId = process?.Id;
            Exception firewallCleanupError = null;
            lock (_initLock)
            {
                if (_disposeCompleted || _entityProcessId == processId) return;
                _entityProcessId = processId;
                _entityProcessGeneration++;
                _entityZoneId = XivPluginInterop.plugin?.DataRepository.GetCurrentTerritoryID();
                // 旧进程可能已经退出；先使引用失效，绝不能尝试在新进程中删除旧地址。
                PluginReady = false;
                ZoneService?.InvalidateWorld();
                EntitySpawner?.ResetWorld();
                // 旧进程的补丁恢复失败也不能阻止新世界使实体引用失效。
                try { FirewallService?.ResetProcess(); }
                catch (Exception error) { firewallCleanupError = error; }
            }
            try
            {
                if (firewallCleanupError != null)
                    LogError("切换进程时恢复旧防火墙失败，记录已保留，请重试关闭：" + firewallCleanupError);
            }
            finally { _ui?.OnEntityWorldChanged(); }
        }

        private void OnFFXIVZoneChanged(uint zoneId, string zoneName)
        {
            lock (_initLock)
            {
                if (_disposeCompleted || _entityZoneId == zoneId) return;
                _entityZoneId = zoneId;
                ZoneService?.InvalidateWorld();
                // 外部换图发生后只能失效旧引用，不能再尝试释放上一场景的内存。
                EntitySpawner?.ResetWorld();
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
