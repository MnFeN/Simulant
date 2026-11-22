using Simulant.Core;
using Simulant.Core.Environment;
using Simulant.Game.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Simulant.UI
{
    internal sealed partial class EnvironmentEditorForm : FormEx
    {
        private readonly PluginHost _host;
        private readonly Timer _poll = new Timer { Interval = 500 };
        private readonly LabelEx _region = MakeLabel("等待游戏区域", LabelTone.Heading);
        private readonly LabelEx _feedback = MakeLabel(string.Empty, LabelTone.Muted);
        private readonly LabelEx _weatherNow = MakeLabel(string.Empty, LabelTone.Muted);
        private readonly DataGridViewEx _weather = ConfigureGrid(new DataGridViewEx(), 40);
        private readonly TabControlEx _tabs = new TabControlEx
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, SizeMode = TabSizeMode.Fixed, ItemSize = new Size(118, 31)
        };
        private readonly CheckBoxEx _keepOnTop = new CheckBoxEx
        {
            Text = "窗口置顶", Style = CheckBoxStyle.Switch, Dock = DockStyle.Right, Width = 130, Margin = Padding.Empty
        };
        private readonly ButtonEx _refresh = new ButtonEx { Text = "刷新", Dock = DockStyle.Right, Width = 76, Margin = Padding.Empty };

        private LiveEnvironment _shown;
        private GameDataService _gameData;
        private EditorResources _resources;
        private bool _hasSnapshot;
        private bool _loading;
        private bool _reloadRequested;
        private int _weatherId = -1;

        internal EnvironmentEditorForm(PluginHost host)
        {
            SuspendLayout();
            _host = host;
            _collisionInspector = new CollisionInspector(host);
            Text = "场景调整";
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            Icon = PluginWindowIcon;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1100, 800);
            MinimumSize = new Size(300, 200);
            Padding = new Padding(16);

            _keepOnTop.CheckedChanged += (_, __) => TopMost = _keepOnTop.Checked;
            _keepOnTop.Checked = true;
            _refresh.Click += (_, __) => UpdateView(true);
            var header = new PanelEx { Dock = DockStyle.Fill, Padding = new Padding(14, 10, 10, 10), Margin = new Padding(0, 0, 0, 10) };
            header.Controls.Add(_region);
            header.Controls.Add(_keepOnTop);
            header.Controls.Add(_refresh);

            _tabs.TabPages.Add(BuildWeatherTab());
            _tabs.TabPages.Add(BuildMapTab());
            _studioTab = BuildStudioTab();
            _tabs.TabPages.Add(_studioTab);
            _collisionTab = BuildCollisionTab();
            RefreshCollisionResults();
            _tabs.SelectedIndexChanged += (_, __) =>
            {
                RefreshMapEffects();
                RefreshStudioState();
            };

            var root = MakeRows();
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.Controls.Add(header, 0, 0);
            root.Controls.Add(_tabs, 0, 1);
            root.Controls.Add(_feedback, 0, 2);
            Controls.Add(root);

            _poll.Tick += (_, __) =>
            {
                RefreshCollisionResults();
                RefreshStudioState();
                UpdateView();
            };
            RefreshEffectInput();
            ResumeLayout(true);
        }

        private TabPageEx BuildWeatherTab()
        {
            _weather.Columns.Add(new DataGridViewImageColumn
            {
                HeaderText = "",
                Width = 56,
                ImageLayout = DataGridViewImageCellLayout.Zoom,
                DefaultCellStyle = new DataGridViewCellStyle { NullValue = null, Padding = new Padding(8, 4, 8, 4) }
            });
            _weather.Columns.Add(MakeColumn("天气", 0));
            _weather.Columns.Add(MakeContentColumn("ID"));
            _weather.Columns.Add(MakeContentColumn("状态"));
            _weather.CellMouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left && e.RowIndex >= 0)
                    ApplyWeather(_weather.Rows[e.RowIndex]);
            };
            _weather.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    ApplyWeather(_weather.CurrentRow);
                }
            };

            var tab = new TabPageEx { Text = "天气", Padding = new Padding(14) };
            var layout = MakeRows();
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(_weatherNow, 0, 0);
            layout.Controls.Add(_weather, 0, 1);
            tab.Controls.Add(layout);
            return tab;
        }

        private static TableLayoutPanel MakeRows()
        {
            var layout = new TableLayoutPanel { ColumnCount = 1, Dock = DockStyle.Fill, Margin = Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return layout;
        }

        private static TableLayoutPanel MakeBar(LabelEx status, Control action)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, ColumnCount = 2, RowCount = 1 };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(status, 0, 0);
            action.Anchor = AnchorStyles.Right;
            panel.Controls.Add(action, 1, 0);
            return panel;
        }

        private static LabelEx MakeLabel(string text, LabelTone tone) => new LabelEx
        {
            Text = text,
            Tone = tone,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };

        private static T ConfigureGrid<T>(T grid, int rowHeight) where T : DataGridViewEx
        {
            grid.Dock = DockStyle.Fill;
            grid.Margin = Padding.Empty;
            grid.ReadOnly = true;
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.AllowUserToResizeColumns = false;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.ScrollBars = ScrollBars.Vertical;
            grid.ColumnHeadersHeight = 29;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.RowTemplate.Height = rowHeight;
            return grid;
        }

        private static DataGridViewTextBoxColumn MakeColumn(string title, int width)
        {
            var column = new DataGridViewTextBoxColumn { HeaderText = title, SortMode = DataGridViewColumnSortMode.NotSortable, ReadOnly = true };
            if (width > 0)
                column.Width = width;
            else
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            return column;
        }

        protected override void OnLoad(EventArgs e)
        {
            var area = Screen.FromControl(this).WorkingArea;
            var width = Math.Min(Width, area.Width);
            var height = Math.Min(Height, area.Height);
            if (width != Width || height != Height)
            {
                Bounds = new Rectangle(area.Left + (area.Width - width) / 2, area.Top + (area.Height - height) / 2, width, height);
            }
            base.OnLoad(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UpdateView();
            _poll.Start();
        }

        private static int GetGridContentWidth(DataGridView grid, int height)
        {
            return grid.Columns.Cast<DataGridViewColumn>().Where(column => column.Visible)
                .Sum(column => column.AutoSizeMode == DataGridViewAutoSizeColumnMode.None ? column.Width :
                    Math.Max(column.MinimumWidth, column.GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCells, true)))
                + GetGridScrollbarWidth(grid, height);
        }

        private static int GetGridScrollbarWidth(DataGridView grid, int height)
            => grid.Rows.GetRowsHeight(DataGridViewElementStates.Visible) + grid.ColumnHeadersHeight > height
                ? SystemInformation.VerticalScrollBarWidth : 0;

        /// <summary>区域或副本槽列表变化时重读资源。常规刷新读取生效的天气及 MapEffect 页中的槽位。</summary>
        private async void UpdateView(bool reload = false)
        {
            if (_loading)
            {
                _reloadRequested |= reload;
                return;
            }

            var environment = _host.EnvironmentService;
            _loading = true;
            EditorResources pendingResources = null;
            try
            {
                var (available, current) = await Task.Run(() =>
                {
                    LiveEnvironment region = default;
                    return (environment != null && environment.TryReadCurrent(out region), region);
                });
                if (IsDisposed || Disposing)
                {
                    return;
                }

                if (!available)
                {
                    if (_hasSnapshot)
                        ResetView();
                    _region.Text = _host.PluginReady ? "等待区域加载" : "等待连接游戏";
                    return;
                }

                var game = GameDataService.Instance;
                if (_hasSnapshot && _shown.IsSameLayout(current) && ReferenceEquals(game, _gameData) && !reload)
                {
                    _shown = current;
                    RefreshCurrent();
                    return;
                }

                if (_hasSnapshot)
                    ResetView();

                _hasSnapshot = true;
                _shown = current;
                _gameData = game;
                _region.Text = $"区域 {current.TerritoryId} · 正在读取资源";
                var loaded = await Task.Run(() =>
                {
                    var slots = ReadSlots(current);
                    var resources = EditorResources.Load(game, current.TerritoryId, slots);
                    try
                    {
                        var matches = environment.TryReadCurrent(out var live) && current.IsSameLayout(live);
                        return (Slots: slots, Resources: resources, Matches: matches, Live: live);
                    }
                    catch
                    {
                        resources.Dispose();
                        throw;
                    }
                });
                pendingResources = loaded.Resources;
                if (IsDisposed || Disposing)
                {
                    return;
                }

                if (!_hasSnapshot || !_shown.IsSameLayout(current) || !loaded.Matches ||
                    !ReferenceEquals(game, GameDataService.Instance))
                {
                    ResetView();
                    return;
                }

                _resources = pendingResources;
                pendingResources = null;
                _shown = loaded.Live;
                ShowResources(loaded.Slots);
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    _region.Text = _hasSnapshot ? $"区域 {_shown.TerritoryId}" : "等待游戏区域";
                    SetFeedback("无法读取场景数据。", ex);
                }
            }
            finally
            {
                pendingResources?.Dispose();
                _loading = false;
                if (_reloadRequested && !IsDisposed && !Disposing)
                {
                    _reloadRequested = false;
                    UpdateView(true);
                }
            }
        }

        private static (int Index, uint LayoutId)[] ReadSlots(LiveEnvironment current)
        {
            var slots = new List<(int Index, uint LayoutId)>();
            for (var index = 0; index < current.SlotCount; index++)
            {
                var id = current.MapEffects.GetItem(index).LayoutId.Get();
                if (id != 0)
                    slots.Add((index, id));
            }
            return slots.ToArray();
        }

        private void ShowResources((int Index, uint LayoutId)[] slots)
        {
            var resources = _resources;
            _region.Text = $"{resources.Data.Name}  ·  区域 {_shown.TerritoryId}";
            foreach (var item in resources.Data.Weather)
            {
                resources.Icons.TryGetValue(item.Icon, out var icon);
                _weather.Rows[_weather.Rows.Add(icon, item.Name, item.Id)].Tag = item;
            }
            ShowMapSlots(slots);

            if (_weather.Rows.Count > 0)
            {
                var active = _weather.Rows.Cast<DataGridViewRow>().FirstOrDefault(row => ((WeatherChoice)row.Tag).Id == _shown.WeatherId);
                _weather.CurrentCell = (active ?? _weather.Rows[0]).Cells[1];
            }

            RefreshCurrent();
            RefreshEffectInput();
        }

        private void RefreshCurrent()
        {
            if (_weatherId != _shown.WeatherId)
            {
                _weatherId = _shown.WeatherId;
                string active = null;
                foreach (DataGridViewRow row in _weather.Rows)
                {
                    var choice = (WeatherChoice)row.Tag;
                    var selected = choice.Id == _weatherId;
                    row.Cells[3].Value = selected ? "生效中" : null;
                    if (selected)
                    {
                        active = choice.Name;
                        row.Cells[3].Style.ForeColor = WorkbenchTheme.Accent;
                    }
                }

                _weatherNow.Text = active != null ? $"当前天气  {active}" : $"当前天气 ID  {_weatherId}";
                if (_resources?.Data.Weather.Length == 0)
                    _weatherNow.Text += "   ·   当前区域没有可选天气";
            }
            RefreshMapEffects();
        }

        private void RefreshEffectInput()
            => _mask.Enabled = _resources != null && _inspectedSlot != null && !_applyingEffects;

        private void ApplyWeather(DataGridViewRow row)
        {
            if (_resources == null || !_hasSnapshot || !(row?.Tag is WeatherChoice choice))
                return;

            try
            {
                if (_host.EnvironmentService.TrySetCurrentWeather(_shown, choice.Id))
                    SetFeedback($"已应用天气  {choice.Name}");
                else
                    SetFeedback("区域已变化，请刷新后重试。", failed: true);
            }
            catch (Exception ex)
            {
                SetFeedback("更改天气失败。", ex);
            }
        }

        private void ResetView()
        {
            _hasSnapshot = false;
            _gameData = null;
            _weather.Rows.Clear();
            _inspectedSlot = null;
            _slots.Rows.Clear();
            ShowSlotDetails(null);
            _resources?.Dispose();
            _resources = null;
            _weatherId = -1;
            _weatherNow.Text = string.Empty;
            _effectNow.Text = string.Empty;
            SetFeedback(string.Empty);
            RefreshEffectInput();
        }

        private void SetFeedback(string text, Exception error = null, bool failed = false)
        {
            _feedback.ForeColor = failed || error != null ? WorkbenchTheme.PaleRed : WorkbenchTheme.Muted;
            _feedback.Text = error == null ? text : $"{text} {error.Message}";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            (ActiveControl as NumericTextBoxEx)?.CommitEdit();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeCollisionTools();
                FlushStudioSettings();
                _studioSaveTimer.Dispose();
                _studioHints.Dispose();
                _poll.Dispose();
                _resources?.Dispose();
            }
            base.Dispose(disposing);
        }

        private sealed class EditorResources : IDisposable
        {
            internal TerritoryEnvironmentData Data { get; }
            internal Dictionary<int, Bitmap> Icons { get; } = new Dictionary<int, Bitmap>();

            private EditorResources(TerritoryEnvironmentData data) => Data = data;

            internal static EditorResources Load(GameDataService game, uint territory, (int Index, uint LayoutId)[] slots)
            {
                var resources = new EditorResources(TerritoryEnvironmentData.Load(game, territory, slots));
                try
                {
                    foreach (var iconId in resources.Data.Weather.Select(weather => weather.Icon).Where(id => id > 0).Distinct())
                        resources.Icons.Add(iconId, GameIcon.Load(game, iconId));
                    return resources;
                }
                catch
                {
                    resources.Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                foreach (var icon in Icons.Values)
                    icon?.Dispose();
            }
        }
    }
}
