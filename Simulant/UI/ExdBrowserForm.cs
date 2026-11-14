using Simulant.Game.Data;
using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Simulant.UI
{
    public partial class ExdBrowserForm : Form
    {
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly CancellationToken _token;
        private CancellationTokenSource _request;
        private string[] _sheets = Array.Empty<string>();
        private string _path;
        private Lumina.Data.Language _language;
        private ExdResource _view;
        private string _query = "";
        private bool _updatingSheets;
        private bool _closed;

        public ExdBrowserForm()
        {
            _token = _lifetime.Token;
            InitializeComponent();
            Padding = new Padding(Font.Height / 2);
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            ClientSize = new Size(area.Width * 3 / 4, area.Height * 3 / 4);
            MinimumSize = SizeFromClientSize(new Size(panelSearch.PreferredSize.Width * 4 / 3 + Padding.Horizontal,
                panelSearch.PreferredSize.Height + panelNavigation.PreferredSize.Height + Font.Height * 12 + Padding.Vertical));
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (StartPosition == FormStartPosition.CenterScreen) CenterToScreen();
        }

        private async void ExdBrowserForm_Shown(object sender, EventArgs e)
        {
            if (DesignMode) return;
            try
            {
                var service = GameDataService.Instance;
                _path = service.SqPackPath;
                _language = service.Language;
                lblStatus.Text = "正在读取表目录……";
                _sheets = await Task.Run(() => ExdResource.ListSheets(_path, _language, _token), _token);
                if (_token.IsCancellationRequested) return;
                txtSheets.Enabled = btnSearch.Enabled = true;
                FilterSheets();
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { ShowError(error); }
        }

        private void txtSheets_TextChanged(object sender, EventArgs e) => FilterSheets();

        private void FilterSheets()
        {
            var selected = listSheets.SelectedItem as string;
            var names = _sheets.Where(name => name.IndexOf(txtSheets.Text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            _updatingSheets = true;
            listSheets.BeginUpdate();
            listSheets.Items.Clear();
            listSheets.Items.AddRange(names);
            listSheets.SelectedItem = names.Contains(selected) ? selected : names.Contains("Action") ? "Action" : names.FirstOrDefault();
            listSheets.EndUpdate();
            _updatingSheets = false;
            if (selected != listSheets.SelectedItem as string) listSheets_SelectedIndexChanged(this, EventArgs.Empty);
        }

        private async void listSheets_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_updatingSheets) return;
            _query = "";
            txtSearch.Clear();
            await LoadPageAsync(0);
        }

        private async void btnSearch_Click(object sender, EventArgs e)
        {
            _query = txtSearch.Text.Trim();
            await LoadPageAsync(0);
        }

        private async void btnPrevious_Click(object sender, EventArgs e) => await LoadPageAsync((_view?.Page ?? 0) - 1);
        private async void btnNext_Click(object sender, EventArgs e) => await LoadPageAsync((_view?.Page ?? 0) + 1);

        private async Task LoadPageAsync(int page)
        {
            _request?.Cancel();
            var name = listSheets.SelectedItem as string;
            _view = null;
            grid.Rows.Clear();
            grid.Columns.Clear();
            btnPrevious.Enabled = btnNext.Enabled = false;
            if (name == null) { lblStatus.Text = "没有匹配的表。"; return; }
            var request = CancellationTokenSource.CreateLinkedTokenSource(_token);
            _request = request;
            var query = _query;
            lblStatus.Text = $"正在读取 {name}……";
            try
            {
                var view = await Task.Run(() => ExdResource.Read(_path, _language, name, query, page, request.Token), request.Token);
                if (request.IsCancellationRequested) return;
                _view = view;
                foreach (var column in view.Columns)
                    grid.Columns.Add(new DataGridViewTextBoxColumn { Name = column, HeaderText = column, SortMode = DataGridViewColumnSortMode.NotSortable });
                grid.RowCount = view.Rows.Length;
                grid.ClearSelection();
                var pages = Math.Max(1, (view.Matches + ExdResource.PageSize - 1) / ExdResource.PageSize);
                lblStatus.Text = $"{name}：共 {view.Total} 行，匹配 {view.Matches} 行，第 {view.Page + 1}/{pages} 页。{view.Notice}";
                btnPrevious.Enabled = view.Page > 0;
                btnNext.Enabled = view.Page + 1 < pages;
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (!request.IsCancellationRequested) ShowError(error); }
            finally
            {
                if (ReferenceEquals(_request, request)) _request = null;
                request.Dispose();
            }
        }

        private void grid_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (_view != null && e.RowIndex < _view.Rows.Length && e.ColumnIndex < _view.Columns.Length)
                e.Value = _view.Rows[e.RowIndex][e.ColumnIndex];
        }

        private void ShowError(Exception error)
        {
            if (!_token.IsCancellationRequested) lblStatus.Text = "读取游戏表失败：" + error.Message;
        }

        private void CancelPendingWork()
        {
            if (_closed) return;
            _closed = true;
            _lifetime.Cancel();
            _request?.Cancel();
            _lifetime.Dispose();
        }
    }
}
