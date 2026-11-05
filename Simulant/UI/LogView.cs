#pragma warning disable IDE1006
using Simulant.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Simulant.UI
{
    public partial class LogView : UserControl
    {
        private PluginHost _host;
        private readonly System.Windows.Forms.Timer _refreshTimer;
        private int _refreshRequested;
        private string _regexPattern;
        private Regex _regex;
        private bool _invalidRegex;

        public LogView()
        {
            InitializeComponent();
            // The component container stops and disposes the timer with this control.
            _refreshTimer = new System.Windows.Forms.Timer(components) { Interval = 100 };
            _refreshTimer.Tick += (sender, e) => RefreshPendingLogs();
            _refreshTimer.Start();
        }

        internal void Bind(PluginHost host)
        {
            _host = host;
            RefreshLogView();
        }

        private Dictionary<LogType, List<LogItem>> _logSnapshot = new Dictionary<LogType, List<LogItem>>();
        private List<LogItem> _virtualData = new List<LogItem>();

        private static readonly Color LogBgRed = Color.FromArgb(255, 225, 225);
        private static readonly Color LogBgYellow = Color.FromArgb(255, 245, 215);
        private static readonly Color LogBgGreen = Color.FromArgb(215, 255, 225);
        private static readonly Color LogBgBlue = Color.FromArgb(215, 235, 255);
        private static readonly Color LogBgGray = Color.FromArgb(250, 250, 250);

        internal void RefreshLogView()
        {
            // Logging can originate on the simulation thread. Coalesce all requests
            // without posting one Windows message and rebuilding the list per line.
            Interlocked.Exchange(ref _refreshRequested, 1);
        }

        private void RefreshPendingLogs()
        {
            if (IsDisposed || Disposing || _host == null ||
                Interlocked.Exchange(ref _refreshRequested, 0) == 0)
                return;

            _logSnapshot = _host.PluginLog.Snapshot();
            ApplyLogFilter();
        }

        private bool IsLogTypeEnabled(LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                    return chkLogFilterError.Checked;
                case LogType.Warning:
                    return chkLogWarning.Checked;
                case LogType.Runtime:
                    return chkLogFilterRuntime.Checked;
                case LogType.Sim:
                    return chkLogFilterSim.Checked;
                case LogType.Verbose:
                    return chkLogFilterVerbose.Checked;
                default:
                    return false;
            }
        }

        private void ApplyLogFilter()
        {
            var pattern = txtLogRegex.Text ?? string.Empty;
            if (!string.Equals(pattern, _regexPattern, StringComparison.Ordinal))
            {
                _regexPattern = pattern;
                _regex = null;
                _invalidRegex = false;
                try
                {
                    if (pattern.Length > 0)
                        _regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                }
                catch (ArgumentException)
                {
                    _invalidRegex = true;
                }
            }

            txtLogRegex.BackColor = _invalidRegex ? Color.MistyRose : SystemColors.Window;
            if (_invalidRegex)
                return;

            _virtualData = _logSnapshot
                .Where(pair => IsLogTypeEnabled(pair.Key))
                .SelectMany(pair => pair.Value)
                .Where(log => _regex == null || _regex.IsMatch(log.Message ?? string.Empty))
                .OrderBy(log => log.Timestamp)
                .ToList();

            dgvLog.RowCount = _virtualData.Count;
            dgvLog.Invalidate();

            if (dgvLog.RowCount > 0)
            {
                dgvLog.ClearSelection();
                // scroll to bottom
                try
                {
                    if (dgvLog.Visible && dgvLog.DisplayedRowCount(false) > 0)
                        dgvLog.FirstDisplayedScrollingRowIndex = dgvLog.RowCount - 1;
                }
                catch (InvalidOperationException)
                {
                    // in case:
                    // System.InvalidOperationException: 没有可供显示行的空间。
                    // 在 System.Windows.Forms.DataGridView.set_FirstDisplayedScrollingRowIndex(Int32 value)
                }
            }
        }

        private void txtLogRegex_TextChanged(object sender, EventArgs e)
        {
        }

        private void txtLogRegex_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            ApplyLogFilter();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void btnLogSearch_Click(object sender, EventArgs e)
        {
            ApplyLogFilter();
        }

        private void dgvLog_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _virtualData.Count)
                return;

            var item = _virtualData[e.RowIndex];

            switch (item.Type)
            {
                case LogType.Error:
                    e.CellStyle.BackColor = LogBgRed;
                    break;
                case LogType.Warning:
                    e.CellStyle.BackColor = LogBgYellow;
                    break;
                case LogType.Runtime:
                    e.CellStyle.BackColor = LogBgGreen;
                    break;
                case LogType.Sim:
                    e.CellStyle.BackColor = LogBgBlue;
                    break;
                case LogType.Verbose:
                    e.CellStyle.BackColor = LogBgGray;
                    break;
                default:
                    e.CellStyle.BackColor = Color.White;
                    break;
            }
            e.CellStyle.ForeColor = Color.Black;
            e.FormattingApplied = true;
        }

        private void dgvLog_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _virtualData.Count)
                return;

            var item = _virtualData[e.RowIndex];

            switch (e.ColumnIndex)
            {
                case 0:
                    e.Value = item.Timestamp.ToString("HH:mm:ss.f");
                    break;
                case 1:
                    e.Value = item.TypeDescription;
                    break;
                case 2:
                    e.Value = item.Message;
                    break;
            }
        }

        private IEnumerable<CheckBox> GetLogFilterItems()
        {
            yield return chkLogFilterError;
            yield return chkLogWarning;
            yield return chkLogFilterRuntime;
            yield return chkLogFilterSim;
            yield return chkLogFilterVerbose;
        }

        // Only for LeftClick, RightClick would not trigger MouseClick on chks
        private void chkLogFilterAll_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                chkLogFilterAll_Click();
        }

        // Only for RightClick
        private void chkLogFilterAll_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
                chkLogFilterAll_Click();
        }

        private void chkLogFilterAll_Click()
        {
            var newChecked = chkLogFilterAll.CheckState != CheckState.Checked;

            chkLogFilterAll.CheckState = newChecked
                ? CheckState.Checked
                : CheckState.Unchecked;

            foreach (var item in GetLogFilterItems())
            {
                item.Checked = newChecked;
            }

            ApplyLogFilter();
        }

        // Only for LeftClick, RightClick would not trigger MouseClick on chks
        private void chkLogFilters_MouseClick(object sender, MouseEventArgs e)
        {
            if (!(sender is CheckBox current))
                return;

            if (e.Button == MouseButtons.Left)
            {
                current.Checked = !current.Checked;
                SyncLogFilterAllState();
                ApplyLogFilter();
            }
        }

        // Only for RightClick
        private void chkLogFilters_MouseDown(object sender, MouseEventArgs e)
        {
            if (!(sender is CheckBox current))
                return;

            if (e.Button == MouseButtons.Right)
            {
                foreach (var item in GetLogFilterItems())
                {
                    item.Checked = item == current;
                }

                SyncLogFilterAllState();
                ApplyLogFilter();
                return;
            }
        }

        private void SyncLogFilterAllState()
        {
            var totalCount = GetLogFilterItems().Count();
            var checkedCount = GetLogFilterItems().Count(x => x.Checked);

            if (checkedCount == 0)
            {
                chkLogFilterAll.CheckState = CheckState.Unchecked;
            }
            else if (checkedCount == totalCount)
            {
                chkLogFilterAll.CheckState = CheckState.Checked;
            }
            else
            {
                chkLogFilterAll.CheckState = CheckState.Indeterminate;
            }
        }

        /// <summary>
        /// 右键选中行
        /// </summary>
        private void dgvLog_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            if (e.RowIndex < 0)
                return;

            dgvLog.ClearSelection();
            dgvLog.Rows[e.RowIndex].Selected = true;

            if (e.ColumnIndex >= 0)
                dgvLog.CurrentCell = dgvLog.Rows[e.RowIndex].Cells[e.ColumnIndex];
        }

        /// <summary>
        /// 菜单项：复制选中行文本（格式：时间 类型 消息）
        /// </summary>
        private void mnuLogCopySelected_Click(object sender, EventArgs e)
        {
            if (dgvLog.SelectedRows.Count == 0)
                return;

            var lines = dgvLog.SelectedRows
                .Cast<DataGridViewRow>()
                .OrderBy(r => r.Index)
                .Where(r => r.Index >= 0 && r.Index < _virtualData.Count)
                .Select(r =>
                {
                    var item = _virtualData[r.Index];
                    return $"{item.Timestamp:HH:mm:ss.fff} {item.TypeDescription} {item.Message}";
                });

            var text = string.Join(Environment.NewLine, lines);

            if (!string.IsNullOrEmpty(text))
                Clipboard.SetText(text);
        }

        /// <summary>
        /// 删除选中行日志
        /// </summary>
        private void mnuLogDeleteSelected_Click(object sender, EventArgs e)
        {
            if (_host == null)
                return;

            var selectedItems = dgvLog.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(r => r.Index)
                .Where(i => i >= 0 && i < _virtualData.Count)
                .Select(i => _virtualData[i])
                .ToList();

            if (selectedItems.Count == 0)
                return;

            _host.PluginLog.Remove(selectedItems);

            RefreshLogView();
        }

        /// <summary>
        /// 清空
        /// </summary>
        private void mnuLogClearAll_Click(object sender, EventArgs e)
        {
            _host?.PluginLog?.Clear();
            _logSnapshot.Clear();
            _virtualData.Clear();

            dgvLog.RowCount = 0;
            dgvLog.ClearSelection();
            dgvLog.Invalidate();
        }
    }
}
#pragma warning restore IDE1006
