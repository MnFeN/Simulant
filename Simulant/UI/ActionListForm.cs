using Simulant.Core;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.Game.Control;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Simulant.UI
{
    public partial class ActionListForm : FormEx
    {
        private readonly PluginHost _host;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly CancellationToken _lifetimeToken;
        private CancellationTokenSource _search;
        private ActionResource[] _all = Array.Empty<ActionResource>();
        private ActionResource[] _view = Array.Empty<ActionResource>();
        private bool _updatingFilters;
        private bool _loaded;
        private bool _workDisposed;
        private int _sortColumn;
        private bool _sortAscending = true;

        public ActionListForm()
        {
            _lifetimeToken = _lifetime.Token;
            InitializeComponent();
            Icon = PluginWindowIcon;
            chkKeepOnTop.CheckedChanged += (_, __) => TopMost = chkKeepOnTop.Checked;
            ScrollBarTheme.Attach(panelShapes);
        }

        internal ActionListForm(PluginHost host) : this()
        {
            _host = host;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (DesignMode)
                return;

            Padding = new Padding(Font.Height / 2);
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            ClientSize = new System.Drawing.Size(area.Width * 3 / 4, area.Height * 3 / 4);
            MinimumSize = SizeFromClientSize(new System.Drawing.Size(
                Math.Max(panelTop.PreferredSize.Width, panelFilters.PreferredSize.Width) + Padding.Horizontal,
                panelTop.PreferredSize.Height + panelFilters.PreferredSize.Height + panelShapes.PreferredSize.Height / 2
                + chkPreviewOnly.PreferredSize.Height + chkPreviewOnly.Margin.Vertical
                + chkPlayerOnly.PreferredSize.Height + chkPlayerOnly.Margin.Vertical
                + Padding.Vertical + panelTop.Margin.Vertical + panelFilters.Margin.Vertical));


            // 等自动缩放和布局完成后，再按最终尺寸居中。
            if (StartPosition == FormStartPosition.CenterScreen)
                CenterToScreen();
        }

        private CheckBox[] ShapeChecks => panelShapes.Controls.OfType<CheckBox>().Where(control => control != chkShapeAll).ToArray();

        private void panelShapes_Layout(object sender, LayoutEventArgs e)
        {
            var checks = panelShapes.Controls.Cast<Control>().ToArray();
            var height = checks.Sum(check => check.PreferredSize.Height + check.Margin.Vertical);
            // 仅在需要纵向滚动时留出滚动条宽度，避免选项被挤出横向滚动条。
            var width = height > panelShapes.Height
                ? checks.Max(check => check.PreferredSize.Width + check.Margin.Horizontal) + SystemInformation.VerticalScrollBarWidth
                : 0;
            var minimum = new System.Drawing.Size(width, 0);

            if (panelShapes.MinimumSize == minimum)
                return;

            panelShapes.MinimumSize = minimum;

            if (IsHandleCreated)
                BeginInvoke((MethodInvoker)(() =>
                {
                    if (!IsDisposed)
                        layoutMain.PerformLayout();
                }));
        }

        private async void ActionListForm_Shown(object sender, EventArgs e)
        {
            if (DesignMode)
                return;

            try
            {
                var data = GameDataService.Instance;
                var path = data.SqPackPath;
                var language = data.Language;
                lblResult.Text = "正在读取技能与时间轴资源……";
                var rows = await Task.Run(() => ActionResource.Load(path, language, _lifetimeToken,
                    message => _host?.LogWarning(message)), _lifetimeToken);

                if (_lifetimeToken.IsCancellationRequested)
                    return;

                _all = rows;
                _loaded = true;
                btnSearch.Enabled = btnReset.Enabled = true;
                await SearchAsync();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                ShowError("读取技能资源失败", error);
            }
        }

        private async void btnSearch_Click(object sender, EventArgs e) => await SearchAsync();

        private async Task SearchAsync()
        {
            if (!_loaded || _lifetimeToken.IsCancellationRequested)
                return;

            var filter = new ActionResourceFilter
            {
                NewOnly = chkNewOnly.Checked,
                PreviewOnly = chkPreviewOnly.Checked,
                PlayerOnly = chkPlayerOnly.Checked,
                Shapes = ShapeChecks.Select(check => check.Checked).ToArray(),
                HexId = txtHexId.Text,
                Name = txtName.Text,
                Expression = txtExpression.Text,
                Omen = txtOmen.Text,
                AnimationEnd = txtAnimationEnd.Text,
                SortColumn = _sortColumn,
                SortAscending = _sortAscending
            };
            _search?.Cancel();
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeToken);
            _search = cancellation;
            lblResult.Text = "正在筛选……";

            try
            {
                var result = await Task.Run(() => filter.Apply(_all, cancellation.Token), cancellation.Token);

                if (cancellation.IsCancellationRequested)
                    return;

                // 先清空虚拟行，避免对数万条旧行逐条调整大小；单元格只显示已读取的快照。
                if (gridActions.RowCount != 0)
                    gridActions.Rows.Clear();

                _view = result;

                if (result.Length != 0)
                    gridActions.RowCount = result.Length;

                gridActions.ClearSelection();

                foreach (DataGridViewColumn column in gridActions.Columns)
                    column.HeaderCell.SortGlyphDirection = column.Index != _sortColumn ? SortOrder.None :
                        _sortAscending ? SortOrder.Ascending : SortOrder.Descending;

                lblResult.Text = $"当前 {result.Length} 条结果";
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                if (!cancellation.IsCancellationRequested)
                    ShowError("搜索失败，请检查正则或过滤表达式", error);
            }
            finally
            {
                if (ReferenceEquals(_search, cancellation))
                    _search = null;

                cancellation.Dispose();
            }
        }

        private async void btnReset_Click(object sender, EventArgs e)
        {
            _updatingFilters = true;
            chkNewOnly.Checked = chkShapeAll.Checked = false;
            chkPreviewOnly.Checked = chkPlayerOnly.Checked = false;

            foreach (var check in ShapeChecks)
                check.Checked = false;

            _updatingFilters = false;
            txtHexId.Clear();
            txtName.Clear();
            txtExpression.Clear();
            txtOmen.Clear();
            txtAnimationEnd.Clear();
            _sortColumn = 0;
            _sortAscending = true;
            await SearchAsync();
        }

        private void chkShapeAll_CheckedChanged(object sender, EventArgs e)
        {
            if (_updatingFilters)
                return;

            _updatingFilters = true;

            foreach (var check in ShapeChecks)
                check.Checked = chkShapeAll.Checked;

            _updatingFilters = false;
        }

        private void Shape_CheckedChanged(object sender, EventArgs e)
        {
            if (_updatingFilters)
                return;

            _updatingFilters = true;
            chkShapeAll.Checked = ShapeChecks.All(check => check.Checked);
            _updatingFilters = false;
        }

        private async void filterSwitch_CheckedChanged(object sender, EventArgs e)
        {
            if (!_updatingFilters)
                await SearchAsync();
        }

        private void gridActions_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < _view.Length)
                e.Value = _view[e.RowIndex].CellValue(e.ColumnIndex);
        }

        private void gridActions_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex != col11.Index || e.RowIndex < 0 || e.RowIndex >= _view.Length)
                return;

            var row = _view[e.RowIndex];
            if (row.HasModelTimeline || row.CanPlayOnSelf || row.CanPlayOnNpc && row.VfxPaths.Length == 0)
                e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor = WorkbenchTheme.Yellow;
            else if (row.VfxPaths.Length != 0)
                e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor = WorkbenchTheme.Blue;
        }

        private async void gridActions_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.Button != MouseButtons.Left)
                return;

            _sortAscending = _sortColumn != e.ColumnIndex || !_sortAscending;
            _sortColumn = e.ColumnIndex;
            await SearchAsync();
        }

        private void gridActions_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var row = gridActions.Rows[e.RowIndex];

            if (!row.Selected)
            {
                gridActions.ClearSelection();
                gridActions.CurrentCell = row.Cells[e.ColumnIndex];
                row.Selected = true;
            }
            else
            {
                // 移动当前单元格会清除多选；恢复原名单，确保右键批量复制不会丢行。
                var selected = gridActions.SelectedRows.Cast<DataGridViewRow>().Select(item => item.Index).ToArray();
                gridActions.CurrentCell = row.Cells[e.ColumnIndex];

                foreach (var index in selected)
                    gridActions.Rows[index].Selected = true;
            }
        }

        private ActionResource CurrentRow => gridActions.CurrentCell != null && gridActions.CurrentCell.RowIndex < _view.Length
            ? _view[gridActions.CurrentCell.RowIndex] : null;

        private void contextActions_Opening(object sender, CancelEventArgs e)
        {
            var row = CurrentRow;
            miCopyCell.Enabled = row != null;
            miCopyTemplate.Enabled = gridActions.SelectedRows.Count != 0;
            miOmen.Visible = row != null && !string.IsNullOrWhiteSpace(row.Omen);
            miOmen.Enabled = _host?.CanRunDebug == true;
            ConfigureTimelineMenu(row, miOmen.Enabled && !_loadingTimelinePreview);
        }

        private void miCopyCell_Click(object sender, EventArgs e)
        {
            if (CurrentRow == null)
                return;

            CopyText(Convert.ToString(CurrentRow.CellValue(gridActions.CurrentCell.ColumnIndex, true), CultureInfo.InvariantCulture), "已复制当前单元格");
        }

        private async void btnExport_Click(object sender, EventArgs e) => await CopyRowsAsync(false);
        private async void miCopyTemplate_Click(object sender, EventArgs e) => await CopyRowsAsync(true);

        private async Task CopyRowsAsync(bool template)
        {
            var selected = gridActions.SelectedRows.Cast<DataGridViewRow>().OrderBy(row => row.Index).Select(row => _view[row.Index]).ToArray();

            if (selected.Length == 0)
            {
                lblResult.Text = "请先选中要复制的行。";
                return;
            }

            var headers = gridActions.Columns.Cast<DataGridViewColumn>().Select(column => column.HeaderText).ToArray();

            try
            {
                var token = _lifetimeToken;
                var text = await Task.Run(() =>
                {
                    if (template)
                        return ActionResource.ExportActions(selected.TakeWhile(_ =>
                        {
                            token.ThrowIfCancellationRequested();
                            return true;
                        }));

                    var output = new StringBuilder(string.Join("\t", headers) + "\r\n");

                    foreach (var row in selected)
                    {
                        token.ThrowIfCancellationRequested();
                        output.AppendLine(string.Join("\t", Enumerable.Range(0, headers.Length).Select(column =>
                            Convert.ToString(row.CellValue(column), CultureInfo.InvariantCulture).Replace("\t", "    ").Replace("\r", " ").Replace("\n", " "))));
                    }

                    return output.ToString();
                }, token);

                if (!token.IsCancellationRequested)
                    CopyText(text, $"已复制 {selected.Length} {(template ? "条动作" : "行")}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                ShowError("复制失败", error);
            }
        }

        private void CopyText(string text, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                    Clipboard.Clear();
                else
                    Clipboard.SetText(text);

                lblResult.Text = message;
            }
            catch (Exception error)
            {
                ShowError("无法写入剪贴板", error);
            }
        }

        private async void miOmen_Click(object sender, EventArgs e)
        {
            var row = CurrentRow;

            if (row == null || string.IsNullOrEmpty(row.Omen))
                return;

            try
            {
                var command = await Task.Run(() =>
                {
                    if (_host == null)
                        throw new InvalidOperationException("请先初始化插件并进入游戏。");

                    _host.EnsureDebugAllowed();
                    _lifetimeToken.ThrowIfCancellationRequested();
                    var me = _host.EntityProvider.GetMyself() ?? throw new InvalidOperationException("没有找到当前玩家。");
                    var pos = me.Pos3D;
                    row.GetScales(out var x, out var y);
                    var heading = me.Heading;
                    var scale = new Vector3(x, y, 1);
                    VfxManager.ShowOmen(row.Omen, pos, heading, scale, 10, this, cancellation: _lifetimeToken);

                    if (row.Shape == 11)
                        VfxManager.ShowOmen(row.Omen, pos, heading + (float)(Math.PI / 2), scale, 10, this, cancellation: _lifetimeToken);

                    return string.Format(CultureInfo.InvariantCulture,
                        "Omen: {0}\nt: 10\nO: {1}, {2}, {3}\nθ: {4}\nScale: {5}, {6}, 1\nAngle: -π",
                        row.Omen, pos.X, pos.Y, pos.Z, heading, x, y);
                }, _lifetimeToken);

                if (!_lifetimeToken.IsCancellationRequested)
                    CopyText(command, "已显示预兆并复制命令");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                ShowError("显示预兆失败", error);
            }
        }

        private async Task PreviewTimelineVfxAsync(string[] paths)
        {
            try
            {
                await Task.Run(() =>
                {
                    if (_host == null)
                        throw new InvalidOperationException("请先初始化插件并进入游戏。");

                    _host.EnsureDebugAllowed();
                    _lifetimeToken.ThrowIfCancellationRequested();
                    var me = _host.EntityProvider.GetMyself() ?? throw new InvalidOperationException("没有找到当前玩家。");
                    var address = TargetSystem.InstancePtr == IntPtr.Zero ? IntPtr.Zero : (IntPtr)TargetSystem.Instance().Target;
                    var target = _host.EntityProvider.GetEntities().FirstOrDefault(entity => entity.Address == address) ?? me;

                    foreach (var path in paths)
                    {
                        _lifetimeToken.ThrowIfCancellationRequested();
                        VfxManager.ShowActor(path, target.Address, target.Address);
                    }
                }, _lifetimeToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                ShowError("显示特效失败", error);
            }
        }

        private void ShowError(string message, Exception error)
        {
            if (IsDisposed || Disposing || _lifetimeToken.IsCancellationRequested)
                return;

            lblResult.Text = message + "：" + error.Message;
            toolTip.SetToolTip(lblResult, lblResult.Text);
            _host?.LogError(message + "：" + error.Message);
        }

        private void CancelPendingWork()
        {
            if (_workDisposed)
                return;

            _workDisposed = true;
            _lifetime.Cancel();
            _search?.Cancel();
            _lifetime.Dispose();
            TryClearTimelinePreview();
            try
            {
                VfxManager.Remove(this);
            }
            catch (Exception error)
            {
                _host?.LogWarning("清理技能预览失败：" + error.Message);
            }
        }
    }
}
