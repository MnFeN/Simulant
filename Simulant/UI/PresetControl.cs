using Simulant.Core;
using Simulant.Simulation;
using Simulant.Game;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Simulation.Runtime;
using System;
using System.Windows.Forms;

namespace Simulant.UI
{
    public partial class PresetControl : UserControl
    {
        private PluginHost _host;
        private SimPresetBase _preset;
        private SimSession _session;
        private readonly Timer _sessionStateTimer;
        private bool _displayedRunning;
        private bool _displayedPendingCleanup;
        private bool _displayedReadyToStart;

        public PresetControl()
        {
            InitializeComponent();
            _sessionStateTimer = new Timer(components) { Interval = 500 };
            _sessionStateTimer.Tick += (sender, e) =>
            {
                if ((_session?.IsRunning == true) != _displayedRunning ||
                    (_session?.HasPendingCleanup == true) != _displayedPendingCleanup ||
                    (GetStartBlockReason() == null) != _displayedReadyToStart)
                    UpdateButtons();
            };
            _sessionStateTimer.Start();
            Disposed += (sender, e) => StopCurrentSession();
            ClearPreset();
        }

        internal void Bind(PluginHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            UpdateButtons();
        }

        internal void LoadPreset(SimPresetBase preset)
        {
            StopCurrentSession();
            _preset = preset;
            if (preset == null)
            {
                ClearPreset();
                return;
            }

            lblPreset.Text = "预设：" + preset.Name;
            lblAuthor.Text = "作者：" + preset.Author;
            lblLastChanged.Text = "更新时间：" + preset.LastUpdated.ToString("yyyy-MM-dd");
            rtbInfo.Text = preset.Description ?? "（无）";
            if (!preset.IsImplemented)
                rtbInfo.AppendText(System.Environment.NewLine + "该预设尚未实现，暂不能开始模拟。");

            RebuildOptions(preset);
            UpdateButtons();
        }

        internal void ClearPreset()
        {
            StopCurrentSession();

            _preset = null;

            lblPreset.Text = "预设：";
            lblAuthor.Text = "作者：";
            lblLastChanged.Text = "更新时间：";
            rtbInfo.Text = "请先在左侧选择预设。";

            ClearOptions();
            UpdateButtons();
        }

        private void RebuildOptions(SimPresetBase preset)
        {
            ClearOptions();

            foreach (var option in preset.Options)
            {
                option?.CreateUI(this, toolTip);
            }

            AddBottomOptionFillerRow();
            tableOptions.PerformLayout();
        }

        private void ClearOptions()
        {
            tableOptions.SuspendLayout();
            try
            {
                var controls = new Control[tableOptions.Controls.Count];
                tableOptions.Controls.CopyTo(controls, 0);
                tableOptions.Controls.Clear();

                foreach (var control in controls)
                    control.Dispose();

                tableOptions.RowStyles.Clear();
                tableOptions.RowCount = 0;
                AddTopOptionFillerRow();
            }
            finally
            {
                tableOptions.ResumeLayout();
            }
        }

        /// <summary>在选项列表顶部添加一个占位行补充 Margin。 </summary>
        private void AddTopOptionFillerRow()
        {
            tableOptions.RowCount += 1;
            tableOptions.RowStyles.Add(new RowStyle(SizeType.Absolute, 5F));
        }

        /// <summary>在选项列表末尾添加一个占位行，占满剩余空间。 </summary>
        private void AddBottomOptionFillerRow()
        {
            tableOptions.RowCount += 1;
            tableOptions.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (_host == null)
                throw new InvalidOperationException("PresetControl 尚未初始化 PluginHost。");

            _host?.LogVerbose("点击 btnStart");

            if (_preset == null) return;

            try
            {
                var blocked = GetStartBlockReason();
                if (blocked != null)
                {
                    _host.LogWarning(blocked);
                    return;
                }
                // 只在点击开始时读取原生区域编号，状态轮询不访问游戏内存。
                var currentTerritoryId = GameMain.Instance.ThrowIfNull().CurrentTerritoryTypeId.Get();
                if (currentTerritoryId != _preset.TerritoryId)
                {
                    _host.LogWarning($"当前区域为 {currentTerritoryId}，预设需要区域 {_preset.TerritoryId}，请先加载对应区域。");
                    return;
                }
                StopCurrentSession();
                if (_session != null) return;
                _session = _preset.CreateSimSession(_host);
                _session.Start();
            }
            catch (Exception ex)
            {
                StopCurrentSession();
                _host.LogError("模拟启动失败：" + ex);
            }
            finally
            {
                UpdateButtons();
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
            try
            {
                _session?.Dispose();
            }
            finally
            {
                if (_session?.HasPendingCleanup == true)
                    _host?.LogError("模拟实体尚未完全回收，已保留会话。请再次点击“停止”重试；清理完成前不能开始新模拟。");
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
            if (!_host.CsvReady || _host.CsvLoading)
                return "数据尚未加载完成，请稍后开始模拟。";
            if (_host.IsSwitchingTerritory)
                return "区域与阶段正在加载，请等待加载完成后再开始模拟。";
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

            _host?.LogVerbose($"UpdateButtons: hasHost = {hasHost}, hasPreset = {hasPreset}, isRunning = {isRunning}");
            btnStart.Enabled = hasHost && hasPreset && _preset.IsImplemented && _displayedReadyToStart && !isRunning && !hasPendingCleanup;
            btnEnd.Enabled = hasHost && (isRunning || hasPendingCleanup);
        }

        #region Copy Style from dummy controls

        internal void ApplyLabelStyle(Label label)
        {
            CopyCommonStyle(dummyLblTxt, label);
        }

        internal void ApplyCheckBoxStyle(CheckBox checkBox)
        {
            CopyCommonStyle(dummyChk, checkBox);

            checkBox.AutoSize = dummyChk.AutoSize;
            checkBox.TextAlign = dummyChk.TextAlign;
            checkBox.UseVisualStyleBackColor = dummyChk.UseVisualStyleBackColor;
        }

        internal void ApplyTextBoxStyle(TextBox textBox)
        {
            CopyCommonStyle(dummyTxt, textBox);
        }

        internal void ApplyComboBoxStyle(ComboBox comboBox)
        {
            CopyCommonStyle(dummyCbx, comboBox);

            comboBox.DropDownHeight = dummyCbx.DropDownHeight;
            comboBox.DropDownStyle = dummyCbx.DropDownStyle;
            comboBox.FormattingEnabled = dummyCbx.FormattingEnabled;
            comboBox.IntegralHeight = dummyCbx.IntegralHeight;
            comboBox.TabStop = dummyCbx.TabStop;
        }

        internal void ApplyNumericUpDownStyle(NumericUpDown num)
        {
            CopyCommonStyle(dummyNud, num);
        }

        private static void CopyCommonStyle(Control source, Control target)
        {
            if (source == null || target == null)
                return;

            target.Anchor = source.Anchor;
            target.AutoSize = source.AutoSize;
            target.Dock = source.Dock;
            target.Margin = source.Margin;
            target.Padding = source.Padding;
            target.ResetFont();
            target.ResetForeColor();
            target.ResetBackColor();
        }

        #endregion Copy Style from dummy controls
    }
}
