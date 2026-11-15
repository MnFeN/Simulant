using Simulant.Simulation;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Simulant.UI
{
    /// <summary>为预设和全局配置共用的 Option 列表生成、排列与清理控件。</summary>
    public sealed class OptionEditor
    {
        internal TableLayoutPanel Table { get; }

        private readonly ToolTip _toolTip;
        private bool _updating;
        internal event Action ValueChanged;

        public OptionEditor(TableLayoutPanel table, ToolTip toolTip)
        {
            Table = table ?? throw new ArgumentNullException(nameof(table));
            _toolTip = toolTip;
            Table.Dock = DockStyle.Top;
            Table.AutoSize = true;
            Table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Table.ColumnCount = 2;
            Table.ColumnStyles.Clear();
            Table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        }

        internal void Build(IEnumerable<SimOptionBase> options, Action<SimOptionBase> restore = null)
        {
            _updating = true;
            Table.SuspendLayout();

            try
            {
                Clear();

                foreach (var option in options)
                {
                    if (option == null)
                        continue;

                    option.CreateUI(this, _toolTip);
                    restore?.Invoke(option);
                    ObserveChanges(option.Control);
                }
            }
            finally
            {
                Table.ResumeLayout(true);
                _updating = false;
            }
        }

        internal void Clear()
        {
            var wasUpdating = _updating;
            _updating = true;
            try
            {
                var controls = new Control[Table.Controls.Count];
                Table.Controls.CopyTo(controls, 0);
                Table.Controls.Clear();

                foreach (var control in controls)
                {
                    // 多个选项卡可能共用提示组件，只移除当前编辑器的提示。
                    _toolTip?.SetToolTip(control, null);
                    control.Dispose();
                }

                Table.RowStyles.Clear();
                Table.RowCount = 1;
                Table.RowStyles.Add(new RowStyle(SizeType.Absolute, 5));
            }
            finally
            {
                _updating = wasUpdating;
            }
        }

        private void ObserveChanges(Control control)
        {
            if (control == null)
                return;

            if (control is ComboBox combo)
                combo.SelectedIndexChanged += OnValueChanged;
            else if (control is CheckBox check)
                check.CheckedChanged += OnValueChanged;
            else if (control is NumericUpDown number)
                number.ValueChanged += OnValueChanged;
            else if (control is TextBox text)
                text.TextChanged += OnValueChanged;
            else
                foreach (Control child in control.Controls)
                    ObserveChanges(child);
        }

        private void OnValueChanged(object sender, EventArgs e)
        {
            if (!_updating)
                ValueChanged?.Invoke();
        }

        /// <summary>沿用预设选项的标签、间距和输入控件样式。</summary>
        internal void ApplyStyle(Control control)
        {
            control.ResetFont();
            control.ResetForeColor();
            control.ResetBackColor();

            if (control is Label)
            {
                control.Anchor = AnchorStyles.Left;
                control.AutoSize = true;
                control.Margin = Padding.Empty;
                control.Padding = new Padding(15, 10, 5, 10);
                return;
            }

            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(30, 10, 20, 10);

            if (control is CheckBox checkBox)
            {
                checkBox.AutoSize = true;
                checkBox.Margin = Padding.Empty;
                checkBox.Padding = new Padding(30, 10, 5, 10);
                checkBox.TextAlign = ContentAlignment.MiddleRight;
                checkBox.UseVisualStyleBackColor = true;
            }

            if (control is ComboBox comboBox)
            {
                comboBox.DropDownHeight = 200;
                comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
                comboBox.FormattingEnabled = true;
                comboBox.IntegralHeight = false;
            }
        }
    }
}
