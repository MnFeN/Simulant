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
using System.Diagnostics;
using System.Linq;
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

        internal PluginLog PluginLog = new PluginLog();

        internal bool CsvReady { get; private set; }
        internal bool CsvLoading { get; private set; }
        internal bool PluginReady { get; private set; }
        internal bool IsInitializing { get; private set; }
        internal bool FirewallEnabled => FirewallService != null && FirewallService.IsEnabled;

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
        private int? _entityProcessId;
        private int _entityProcessGeneration;
        private uint? _entityZoneId;

        // 调试入口也有明确的行为和资源归属，不再把调度器藏在实体/生成器中。
        internal SimEntityManager DebugEntities
        {
            get
            {
                lock (_debugGate)
                {
                    if (Disposed) throw new ObjectDisposedException(nameof(PluginHost));
                    if (_debugEntities == null)
                    {
                        SimScheduler debugScheduler = null;
                        debugScheduler = new SimScheduler((ex, node) =>
                        {
                            try { LogError($"调试节点「{node}」失败：{ex}"); }
                            finally { StopDebugEntities(debugScheduler); }
                        });
                        _debugScheduler = debugScheduler;
                        _debugEntities = new SimEntityManager(EntitySpawner, ex => LogError("调试实体清理失败：" + ex))
                        { Scheduler = _debugScheduler };
                        _debugScheduler.Start();
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
            lock (_debugGate)
            {
                if (expectedScheduler != null && !ReferenceEquals(_debugScheduler, expectedScheduler)) return;
                scheduler = _debugScheduler;
                entities = _debugEntities;
                _debugScheduler = null;
                _debugEntities = null;
            }
            try { scheduler?.Dispose(); }
            finally { entities?.Clear(); }
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
            EntitySpawner = new EntitySpawner(EntityRegistry, EntityProvider, LogError);
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
                if (IsInitializing)
                {
                    LogWarning("插件正在初始化，跳过重复请求。");
                    return;
                }

                IsInitializing = true;
                PluginReady = false;
                processGeneration = _entityProcessGeneration;
            }

            _ui?.UpdateControlStates();

            try
            {
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
            if (Disposed)
                return;

            Disposed = true;
            try
            {
                _ui?.StopCurrentSession();
            }
            catch (Exception ex)
            {
                LogError("卸载时停止模拟失败：" + ex.Message);
            }

            try
            {
                try { StopDebugEntities(); }
                finally { EntitySpawner?.Dispose(); }
            }
            catch (Exception ex)
            {
                LogError("卸载时释放实体资源失败：" + ex.Message);
            }

            try
            {
                if (FirewallService != null && FirewallService.IsEnabled)
                {
                    FirewallService.Disable();
                }
            }
            catch (Exception ex)
            {
                LogError("卸载时关闭防火墙失败：" + ex.Message);
            }

            if (XivPluginInterop.plugin != null)
            {
                XivPluginInterop.plugin.DataSubscription.ProcessChanged -= OnFFXIVProcessChanged;
                XivPluginInterop.plugin.DataSubscription.ZoneChanged -= OnFFXIVZoneChanged;
            }

            _ui?.Dispose();
            _statusLabel.Text = "插件已卸载";
        }

        private void Attach()
        {
            lock (this)
            {
                if (ActGlobals.oFormActMain == null)
                {
                    XivPluginInterop.plugin = null;
                    return;
                }

                if (XivPluginInterop.plugin != null)
                    return;

                var ffxivPluginData = ActGlobals.oFormActMain.ActPlugins
                    .FirstOrDefault(x => x.pluginObj != null
                        && x.pluginObj.GetType().ToString() == "FFXIV_ACT_Plugin.FFXIV_ACT_Plugin");

                XivPluginInterop.plugin =
                    (FFXIV_ACT_Plugin.FFXIV_ACT_Plugin)ffxivPluginData?.pluginObj;

                if (XivPluginInterop.plugin == null)
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
                                var plugin = XivPluginInterop.plugin;
                                if (plugin == null)
                                    return;

                                plugin.DataSubscription.ProcessChanged += OnFFXIVProcessChanged;
                                _entityZoneId = plugin.DataRepository.GetCurrentTerritoryID();
                                plugin.DataSubscription.ZoneChanged += OnFFXIVZoneChanged;
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
        }

        private void OnFFXIVProcessChanged(Process process)
        {
            if (Disposed) return;
            var processId = process?.Id;
            lock (_initLock)
            {
                if (_entityProcessId == processId) return;
                _entityProcessId = processId;
                _entityProcessGeneration++;
                _entityZoneId = XivPluginInterop.plugin?.DataRepository.GetCurrentTerritoryID();
                // 旧进程可能已经退出；先使引用失效，绝不能尝试在新进程中删除旧地址。
                PluginReady = false;
                EntitySpawner?.ResetWorld();
            }
            _ui?.OnEntityWorldChanged();
        }

        private void OnFFXIVZoneChanged(uint zoneId, string zoneName)
        {
            lock (_initLock)
            {
                if (Disposed || _entityZoneId == zoneId) return;
                _entityZoneId = zoneId;
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
