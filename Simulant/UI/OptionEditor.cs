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

        internal void Build(IEnumerable<SimOptionBase> options)
        {
            Table.SuspendLayout();
            try
            {
                Clear();
                foreach (var option in options) option?.CreateUI(this, _toolTip);
            }
            finally { Table.ResumeLayout(true); }
        }

        internal void Clear()
        {
            _toolTip?.RemoveAll();
            var controls = new Control[Table.Controls.Count];
            Table.Controls.CopyTo(controls, 0);
            Table.Controls.Clear();
            foreach (var control in controls) control.Dispose();
            Table.RowStyles.Clear();
            Table.RowCount = 1;
            Table.RowStyles.Add(new RowStyle(SizeType.Absolute, 5));
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
