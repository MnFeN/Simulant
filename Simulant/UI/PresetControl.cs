using Simulant.Core;
using Simulant.Simulation;
using Simulant.Game;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Simulation.Runtime;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Simulant.UI
{
    public partial class PresetControl : UserControlEx
    {
        private PluginHost _host;
        private SimPresetBase _preset;
        private SimSession _session;
        private (int World, int Seed)? _lastAttempt;
        private long _restartNotBefore;
        private volatile bool _preparingStart;
        private int _stopVersion;
        private readonly System.Windows.Forms.Timer _sessionStateTimer;
        private readonly OptionEditor _optionEditor;
        private FormEx.StaWindow<QuickControlForm> _quickControls;
        private bool _displayedRunning;
        private bool _displayedPendingCleanup;
        private bool _displayedReadyToStart;
        private bool _displayedReadyToRetry;

        public PresetControl()
        {
            InitializeComponent();
            toolTip.SetToolTip(btnRetry, "清理当前模拟，恢复本阶段场地和出生点，使用上一场的种子再次开始。");
            _optionEditor = new OptionEditor(tableOptions, toolTip);
            optionsScroll.SetContent(optionsContent);
            _optionEditor.ValueChanged += SavePresetOptions;
            _sessionStateTimer = new System.Windows.Forms.Timer(components)
            {
                Interval = 500
            };

            _sessionStateTimer.Tick += (sender, e) =>
            {
                if ((_session?.IsRunning == true) != _displayedRunning ||
                    (_session?.HasPendingCleanup == true) != _displayedPendingCleanup ||
                    (GetStartBlockReason() == null) != _displayedReadyToStart ||
                    CanRetry != _displayedReadyToRetry)
                    UpdateButtons();

                UpdateQuickControls();
            };

            Disposed += (sender, e) =>
            {
                CloseQuickControls();
                StopCurrentSession();
            };
            ClearPreset();
        }

        internal void Bind(PluginHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _sessionStateTimer.Start();
            UpdateButtons();
        }

        internal void LoadPreset(SimPresetBase preset)
        {
            StopCurrentSession();

            if (!ReferenceEquals(_preset, preset))
                _lastAttempt = null;

            _preset = preset;

            if (preset == null)
            {
                ClearPreset();
                return;
            }

            lblPreset.Text = preset.Name;
            toolTip.SetToolTip(lblPreset, preset.Name);
            lblAuthor.Text = "作者 " + preset.Author;
            lblLastChanged.Text = preset.LastUpdated.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            rtbInfo.Text = preset.Description?.Trim() ?? string.Empty;
            if (!preset.IsImplemented)
                rtbInfo.AppendText(System.Environment.NewLine + "该预设尚未实现，暂不能开始模拟。");
            rtbInfo.Visible = !string.IsNullOrWhiteSpace(rtbInfo.Text);

            RebuildOptions(preset);
            UpdateButtons();
        }

        internal void ClearPreset()
        {
            StopCurrentSession();

            _preset = null;
            _lastAttempt = null;

            lblPreset.Text = "选择模拟";
            toolTip.SetToolTip(lblPreset, null);
            lblAuthor.Text = string.Empty;
            lblLastChanged.Text = string.Empty;
            rtbInfo.Clear();
            rtbInfo.Visible = false;

            ClearOptions();
            lblNoOptions.Text = "选择模拟后可调整参数";
            lblNoOptions.Visible = true;
            optionsScroll.ResetScroll();
            UpdateButtons();
        }

        private void RebuildOptions(SimPresetBase preset)
        {
            var saved = _host?.Configuration.Presets.Find(item => item.Id == preset.GetType().FullName);
            _optionEditor.Build(preset.Options, option =>
            {
                var value = saved?.Options.Find(item => item.Name == option.PropertyName);
                if (value == null)
                    return;

                try
                {
                    option.RestoreValue(value.Value ?? string.Empty);
                }
                catch (Exception error)
                {
                    _host.LogWarning($"预设「{preset.Name}」的选项「{option.PropertyName}」无法恢复，使用默认值：{error.Message}");
                }
            });
            lblNoOptions.Text = "此预设没有可调参数";
            lblNoOptions.Visible = tableOptions.Controls.Count == 0;
            optionsScroll.ResetScroll();
        }

        private void SavePresetOptions()
        {
            if (_host == null || _preset == null)
                return;

            try
            {
                _host.SavePresetOptions(_preset);
            }
            catch (Exception error)
            {
                _host.LogError($"保存预设「{_preset.Name}」的选项失败：{error}");
            }
        }

        private void ClearOptions()
            => _optionEditor.Clear();

        private void btnStart_Click(object sender, EventArgs e)
            => StartCurrentSession();

        private void btnRetry_Click(object sender, EventArgs e)
            => RetryCurrentSession();

        internal async void StartCurrentSession()
            => await StartCurrentSessionAsync(retry: false);

        internal async void RetryCurrentSession()
            => await StartCurrentSessionAsync(retry: true);

        private bool CanRetry => _lastAttempt?.World == _host?.WorldGeneration &&
            _host?.ZoneService?.IsInSimulatedTerritory == true;

        private async Task StartCurrentSessionAsync(bool retry)
        {
            var preset = _preset;
            if (_host == null || preset?.IsImplemented != true)
                return;

            try
            {
                var blocked = GetStartBlockReason();

                if (blocked != null)
                {
                    _host.LogWarning(blocked);
                    return;
                }

                var world = _host.WorldGeneration;
                int? seed = null;
                if (retry)
                {
                    var attempt = _lastAttempt;
                    if (attempt?.World != world || _host.ZoneService?.IsInSimulatedTerritory != true)
                    {
                        _host.LogWarning("请先在当前区域开始一次该预设，再重复上次。");
                        return;
                    }

                    seed = attempt.Value.Seed;
                }

                // 只在实际启动时读取原生区域编号，状态轮询不访问游戏内存。
                var currentTerritoryId = GameMain.Instance.ThrowIfNull().CurrentTerritoryTypeId.Get();

                if (currentTerritoryId != preset.TerritoryId)
                {
                    _host.LogWarning($"当前区域为 {currentTerritoryId}，预设需要区域 {preset.TerritoryId}，请先加载对应区域。");
                    return;
                }

                StopCurrentSession();
                if (_session != null || !await PrepareNextStart(preset, retry))
                    return;

                if (IsDisposed || Disposing || !ReferenceEquals(_preset, preset) ||
                    _host.WorldGeneration != world || GetStartBlockReason() != null)
                    return;

                _session = preset.CreateSimSession(_host, seed);
                _session.Start();
                _lastAttempt = (world, _session.RandomSeed);
                _host.LogSim(FormattableString.Invariant($"本场模拟种子：{_session.RandomSeed}"));
            }
            catch (Exception ex)
            {
                StopCurrentSession();
                _host.LogError("模拟启动失败：" + ex);
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                    UpdateButtons();
            }
        }

        private async Task<bool> PrepareNextStart(SimPresetBase preset, bool retry)
        {
            var restartNotBefore = _restartNotBefore;
            if (!retry && SimSession.DelayMillisecondsUntil(restartNotBefore) == 0)
                return true;

            var version = Volatile.Read(ref _stopVersion);
            _preparingStart = true;
            try
            {
                UpdateButtons();
                if (retry && !await _host.ZoneService.EnterPhase(preset.Phase))
                    return false;

                int delay;
                while ((delay = SimSession.DelayMillisecondsUntil(restartNotBefore)) > 0)
                    await Task.Delay(delay);

                // 停止或离开区域后，取消仍在等待启动的请求。
                return Volatile.Read(ref _stopVersion) == version;
            }
            finally
            {
                _preparingStart = false;
            }
        }

        private void btnEnd_Click(object sender, EventArgs e)
        {
            _host?.LogVerbose("点击 btnEnd");
            StopCurrentSession();
            UpdateButtons();
        }

        internal void StopCurrentSession()
        {
            var session = _session;
            try
            {
                Interlocked.Increment(ref _stopVersion);
                if (_preparingStart)
                    _host.ZoneService.CancelPendingPhase();

                session?.Dispose();
            }
            finally
            {
                if (session != null)
                    _restartNotBefore = Math.Max(_restartNotBefore, session.RestartNotBefore);

                if (_session?.HasPendingCleanup == true)
                    _host?.LogError("模拟实体或场地标点尚未完全清理，已保留会话。请再次点击“停止”重试；清理完成前不能开始新模拟。");
                else
                    _session = null;

                if (!IsDisposed && !Disposing)
                    UpdateButtons();
            }
        }

        private string GetStartBlockReason()
        {
            if (_host == null || !_host.PluginReady || _host.IsInitializing || _host.Disposed)
                return "插件尚未就绪，请先完成初始化。";

            if (!_host.GameDataReady || _host.GameDataLoading)
                return "数据尚未加载完成，请稍后开始模拟。";

            if (_host.IsSwitchingTerritory || _preparingStart)
                return "区域或模拟正在准备，请等待就绪后再开始。";

            if (!_host.FirewallEnabled)
                return "预设使用网络模拟实体，请先开启防火墙。";

            return null;
        }

        private void UpdateButtons()
        {
            var hasHost = _host != null;
            var hasPreset = _preset != null;
            var isRunning = _session?.IsRunning == true;
            var hasPendingCleanup = _session?.HasPendingCleanup == true;
            _displayedRunning = isRunning;
            _displayedPendingCleanup = hasPendingCleanup;
            _displayedReadyToStart = GetStartBlockReason() == null;
            _displayedReadyToRetry = CanRetry;

            _host?.LogVerbose($"UpdateButtons: hasHost = {hasHost}, hasPreset = {hasPreset}, isRunning = {isRunning}");
            btnStart.Enabled = hasHost && hasPreset && _preset.IsImplemented && _displayedReadyToStart;
            btnRetry.Enabled = btnStart.Enabled && _displayedReadyToRetry;
            btnEnd.Enabled = hasHost && (isRunning || hasPendingCleanup || _preparingStart);
            lblRunState.Text = !hasPreset ? string.Empty : _preparingStart ? "准备下一轮" :
                isRunning ? "模拟中" : hasPendingCleanup ? "等待清理" : btnStart.Enabled ? "可开始" : "等待就绪";
            lblRunState.ForeColor = _preparingStart ? WorkbenchTheme.Muted :
                hasPendingCleanup ? WorkbenchTheme.Yellow :
                isRunning || btnStart.Enabled ? WorkbenchTheme.Accent : WorkbenchTheme.Muted;
            UpdateQuickControls();
        }

        internal void UpdateQuickControls()
        {
            var show = !IsDisposed && !Disposing && _host != null && !_host.Disposed &&
                _host.PluginReady && _host.FirewallEnabled && _host.Configuration.QuickControlsEnabled &&
                _host.ZoneService?.IsInSimulatedTerritory == true && _preset?.IsImplemented == true;

            if (!show)
            {
                CloseQuickControls();
                return;
            }

            var canStart = btnStart.Enabled;
            var canRetry = btnRetry.Enabled;
            var canStop = btnEnd.Enabled;
            Action<QuickControlForm> update = form => form.UpdateState(canStart, canRetry, canStop);
            if (_quickControls?.TryPost(update) == true)
                return;

            var host = _host;
            var location = host.Configuration.QuickControlsLocation;
            _quickControls = FormEx.ShowOnSta(() =>
            {
                var form = new QuickControlForm(
                    () => PostQuickControlAction(StartCurrentSession),
                    () => PostQuickControlAction(RetryCurrentSession),
                    () => PostQuickControlAction(StopCurrentSession))
                {
                    IsGameForeground = () => host.IsGameForeground
                };
                if (location.HasValue)
                    form.RestoreLocation(location.Value);
                form.LocationCommitted += host.SaveQuickControlsLocation;
                return form;
            }, error => host.LogError("快捷控制窗口出错：" + error.Message), update);
        }

        internal void CloseQuickControls()
        {
            _quickControls?.Dispose();
            _quickControls = null;
        }

        private void PostQuickControlAction(Action action)
        {
            if (IsDisposed || Disposing || _host.Disposed || !IsHandleCreated)
                return;

            BeginInvoke((Action)(() =>
            {
                if (IsDisposed || Disposing || _host.Disposed)
                    return;
                try { action(); }
                catch (Exception error) { _host.LogWarning("执行快捷控制失败：" + error.Message); }
            }));
        }
    }
}
