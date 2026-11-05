using Simulant.Core;
using Simulant.Simulation;
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

        public PresetControl()
        {
            InitializeComponent();
            _sessionStateTimer = new Timer(components) { Interval = 500 };
            _sessionStateTimer.Tick += (sender, e) =>
            {
                if ((_session?.IsRunning == true) != _displayedRunning)
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

            StopCurrentSession();
            try
            {
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
                _session = null;
                if (!IsDisposed && !Disposing)
                    UpdateButtons();
            }
        }

        private void UpdateButtons()
        {
            var hasHost = _host != null;
            var hasPreset = _preset != null;
            var isRunning = _session?.IsRunning == true;
            _displayedRunning = isRunning;

            _host?.LogVerbose($"UpdateButtons: hasHost = {hasHost}, hasPreset = {hasPreset}, isRunning = {isRunning}");
            btnStart.Enabled = hasHost && hasPreset && !isRunning;
            btnEnd.Enabled = hasPreset && isRunning;
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
