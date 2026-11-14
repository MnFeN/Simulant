using Simulant.UI;
using System.IO;
using System.Windows.Forms;

namespace Simulant.Simulation.Options
{
    /// <summary>可手动输入、清空或通过文件选择窗口设置路径的选项。</summary>
    public sealed class FilePathOption : SimOption<string>
    {
        private readonly string _filter;
        private TextBox _textBox;
        private Button _browse;

        public FilePathOption(string propName, string labelText, string defaultValue, string filter, string hint = null)
        {
            PropertyName = propName;
            _labelText = labelText;
            _default = defaultValue ?? string.Empty;
            _filter = filter;
            _hint = hint;
        }

        protected override Control CreateControl(OptionEditor editor)
        {
            var panel = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 1 };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _textBox = new TextBox { Text = _default, Dock = DockStyle.Fill, Margin = Padding.Empty };
            _browse = new Button { Text = "浏览…", AutoSize = true, Margin = new Padding(5, 0, 0, 0) };
            _browse.Click += (sender, e) =>
            {
                using (var dialog = new OpenFileDialog { Title = _labelText, Filter = _filter,
                    CheckFileExists = true, Multiselect = false, RestoreDirectory = true })
                {
                    if (File.Exists(_textBox.Text)) dialog.FileName = _textBox.Text;
                    if (dialog.ShowDialog(panel.FindForm()) == DialogResult.OK) _textBox.Text = dialog.FileName;
                }
            };
            panel.Controls.Add(_textBox, 0, 0);
            panel.Controls.Add(_browse, 1, 0);
            editor.ApplyStyle(panel);
            return panel;
        }

        internal override void CreateUI(OptionEditor editor, ToolTip toolTip)
        {
            base.CreateUI(editor, toolTip);
            toolTip?.SetToolTip(_textBox, _hint);
            toolTip?.SetToolTip(_browse, _hint);
        }

        internal override string GetValue() => (_textBox?.Text ?? _default).Trim();
    }
}
