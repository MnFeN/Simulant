using Simulant.Game.Data;
using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Simulant.UI
{
    internal sealed partial class EnvironmentEditorForm
    {
        private const int FirstStateColumn = 2;
        private const int StateCount = 16;
        private const string LogFieldSeparator = "????:";
        private const int LogFieldLength = 11;
        private const string ActiveStateMarker = "●";
        private const string AvailableStateMarker = "·";
        private readonly LabelEx _effectNow = MakeLabel(string.Empty, LabelTone.Muted);
        private readonly MapEffectSlotGrid _slots = ConfigureGrid(new MapEffectSlotGrid(), 32);
        private readonly MapEffectDetailGrid _bits = ConfigureGrid(new MapEffectDetailGrid(), 32);
        private readonly CheckBoxEx _expandSidebar = new CheckBoxEx
        {
            Text = "展开侧栏", Style = CheckBoxStyle.Switch, Checked = true, Size = new Size(130, 30),
            Margin = new Padding(0, 5, 12, 0)
        };
        private readonly TextBoxEx _mask = new TextBoxEx { Width = 124, MaxLength = LogFieldLength, TextAlign = HorizontalAlignment.Center };
        private readonly TableLayoutPanel _mapSplit = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty
        };

        private MapSlot _inspectedSlot;
        private bool _readingEffects;
        private bool _applyingEffects;
        private bool _syncingBits;
        private int _effectRevision;

        private TabPageEx BuildMapTab()
        {
            _slots.MultiSelect = true;
            _slots.ScrollBars = ScrollBars.Both;
            _slots.Columns.Add(MakeContentColumn("槽位"));
            _slots.Columns.Add(MakeContentColumn("场景资源"));
            for (var bit = 0; bit < StateCount; bit++)
            {
                var column = MakeColumn(bit.ToString("X", CultureInfo.InvariantCulture), 32);
                column.HeaderCell.Style = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter, Padding = new Padding(4, 0, 4, 0)
                };
                column.DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter, Padding = new Padding(3, 0, 3, 0)
                };
                _slots.Columns.Add(column);
            }

            _slots.CurrentCellChanged += (_, __) =>
            {
                if (_slots.CurrentCell?.ColumnIndex < FirstStateColumn)
                    ShowSlotDetails(_slots.CurrentRow?.Tag as MapSlot);
            };
            _slots.QuickApplyRequested += QuickApplyMapEffect;

            _bits.ScrollBars = ScrollBars.Both;
            _bits.Margin = new Padding(12, 0, 0, 0);
            _expandSidebar.CheckedChanged += (_, __) =>
            {
                _bits.Visible = _expandSidebar.Checked;
                SizeMapGrid();
            };
            _bits.Columns.Add(MakeContentColumn("位"));
            _bits.Columns.Add(MakeContentColumn("值"));
            _bits.Columns.Add(MakeColumn("名称", 0));
            _bits.CurrentCellChanged += (_, __) =>
            {
                if (!_syncingBits && _inspectedSlot != null && _bits.CurrentRow?.Tag is ushort state)
                    _mask.Text = FormatActLogField(state, _inspectedSlot.Index);
            };

            _bits.CellMouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left && e.RowIndex >= 0)
                    ApplyEffectRow(_bits.Rows[e.RowIndex]);
            };
            _bits.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    ApplyEffectRow(_bits.CurrentRow);
                }
            };

            _mask.TextChanged += (_, __) => UpdateMask();
            _mask.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    if (_inspectedSlot != null && TryReadMask(out var state))
                        ApplyMapEffects(new[] { _inspectedSlot }, state);
                }
            };

            _mapSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
            _mapSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _mapSplit.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _mapSplit.Controls.Add(_slots, 0, 0);
            _mapSplit.Controls.Add(_bits, 1, 0);
            _mapSplit.SizeChanged += (_, __) => SizeMapGrid();
            _slots.ColumnWidthChanged += (_, __) => SizeMapGrid();
            _slots.HandleCreated += (_, __) => FitMapStateColumns();
            _slots.FontChanged += (_, __) => FitMapStateColumns();
            _slots.DpiChangedAfterParent += (_, __) => FitMapStateColumns();

            var tab = new TabPageEx { Text = "MapEffect", Padding = new Padding(14) };
            var layout = MakeRows();
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(BuildMapToolbar(layout), 0, 0);
            layout.Controls.Add(_mapSplit, 0, 1);
            tab.Controls.Add(layout);
            return tab;
        }

        private TableLayoutPanel BuildMapToolbar(TableLayoutPanel layout)
        {
            var inputs = new FlowLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false,
                Anchor = AnchorStyles.Right, Margin = Padding.Empty
            };
            inputs.Controls.Add(new LabelEx { Text = "ACT日志字段", AutoSize = true, Tone = LabelTone.Muted, Margin = new Padding(0, 10, 5, 0) });
            _mask.Margin = new Padding(0, 7, 0, 0);
            inputs.Controls.Add(_mask);

            var toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Margin = Padding.Empty, ColumnCount = 3, RowCount = 2
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            toolbar.Controls.Add(_effectNow, 0, 0);
            toolbar.Controls.Add(_expandSidebar, 1, 0);
            toolbar.Controls.Add(inputs, 2, 0);
            layout.SizeChanged += (_, __) =>
            {
                var actionWidth = _expandSidebar.Width + _expandSidebar.Margin.Horizontal + inputs.PreferredSize.Width;
                var wrap = layout.ClientSize.Width < actionWidth;
                if (wrap == (toolbar.GetRow(inputs) == 1))
                    return;

                toolbar.SuspendLayout();
                if (wrap)
                {
                    toolbar.SetCellPosition(inputs, new TableLayoutPanelCellPosition(0, 1));
                    toolbar.SetColumnSpan(inputs, 3);
                }
                else
                {
                    toolbar.SetColumnSpan(inputs, 1);
                    toolbar.SetCellPosition(inputs, new TableLayoutPanelCellPosition(2, 0));
                }
                layout.RowStyles[0].Height = toolbar.RowStyles[0].Height + (wrap ? inputs.PreferredSize.Height : 0);
                toolbar.ResumeLayout();
            };
            return toolbar;
        }

        private static DataGridViewTextBoxColumn MakeContentColumn(string title)
        {
            var column = MakeColumn(title, 80);
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            return column;
        }

        private void FitMapStateColumns()
        {
            var style = _slots.Columns[FirstStateColumn].DefaultCellStyle;
            var font = style.Font ?? _slots.Font;
            var markerWidth = Math.Max(TextRenderer.MeasureText(ActiveStateMarker, font).Width,
                TextRenderer.MeasureText(AvailableStateMarker, font).Width);
            var markerCellWidth = markerWidth + style.Padding.Horizontal + _slots.LogicalToDeviceUnits(6);
            var columns = _slots.Columns.Cast<DataGridViewColumn>().Skip(FirstStateColumn);
            var width = Math.Max(markerCellWidth,
                columns.Max(column => column.GetPreferredWidth(DataGridViewAutoSizeColumnMode.ColumnHeader, true)));

            // Keep all state columns the same width regardless of which markers are visible.
            foreach (var column in columns)
                column.Width = width;
            SizeMapGrid();
        }

        private void SizeMapGrid()
        {
            var left = _mapSplit.ClientSize.Width;
            if (_expandSidebar.Checked)
            {
                var height = _mapSplit.ClientSize.Height;
                var width = _slots.Columns.GetColumnsWidth(DataGridViewElementStates.Visible) + GetGridScrollbarWidth(_slots, height);
                var right = Math.Min(GetGridContentWidth(_bits, height), left / 2);
                left = Math.Min(width, Math.Max(0, left - _bits.Margin.Horizontal - right));
            }
            if (_mapSplit.ColumnStyles[0].Width != left)
                _mapSplit.ColumnStyles[0].Width = left;
        }

        private int GetMapContentWidth(int height)
            => GetGridContentWidth(_slots, height)
                + (_expandSidebar.Checked ? _bits.Margin.Horizontal + GetGridContentWidth(_bits, height) : 0);

        private void ShowMapSlots((int Index, uint LayoutId)[] slots)
        {
            foreach (var slot in slots)
            {
                _resources.Data.MapEffects.TryGetValue(slot.Index, out var resource);
                var values = new object[FirstStateColumn + StateCount];
                values[0] = $"0x{slot.Index:X2}";
                values[1] = resource?.Name ?? $"布局 #{slot.LayoutId}";
                var row = _slots.Rows[_slots.Rows.Add(values)];
                var item = new MapSlot(slot.Index, slot.LayoutId, row, resource);
                row.Tag = item;
                row.Cells[0].ToolTipText = slot.Index.ToString(CultureInfo.InvariantCulture);
                row.Cells[1].ToolTipText = resource?.Path ?? $"布局 ID {slot.LayoutId}";
                PaintMapSlot(item);
            }

            var first = _slots.CurrentRow ?? _slots.Rows.Cast<DataGridViewRow>().FirstOrDefault();
            ShowSlotDetails(first?.Tag as MapSlot);
            var nameColumn = _bits.Columns[2];
            var nameFont = nameColumn.InheritedStyle.Font;
            var namePadding = nameColumn.InheritedStyle.Padding.Horizontal + _bits.LogicalToDeviceUnits(6);
            nameColumn.MinimumWidth = Math.Max(5, _resources.Data.MapEffects.Values.SelectMany(resource => resource.Timelines)
                .Select(state => TextRenderer.MeasureText(state.Name, nameFont).Width + namePadding).DefaultIfEmpty(0).Max());
            FitMapStateColumns();
        }

        private void ShowSlotDetails(MapSlot slot)
        {
            if (slot != null && slot == _inspectedSlot)
                return;

            _inspectedSlot = slot;
            _syncingBits = true;
            try
            {
                _bits.Rows.Clear();
                _mask.Clear();
                if (slot != null)
                {
                    for (var bit = 0; bit < StateCount; bit++)
                    {
                        var state = StateAt(bit);
                        var name = slot.Names[bit];
                        var row = _bits.Rows[_bits.Rows.Add(bit.ToString("X", CultureInfo.InvariantCulture), ToStateValue(state), name ?? "—")];
                        if (name != null)
                            row.Tag = state;
                    }

                    if (slot.State.HasValue && IsSingleState(slot.State.Value))
                        _mask.Text = FormatActLogField(slot.State.Value, slot.Index);
                }
            }
            finally
            {
                _syncingBits = false;
            }

            _effectNow.Text = slot != null ? $"槽位 0x{slot.Index:X2}" :
                _resources == null || _slots.Rows.Count > 0 ? string.Empty : "当前区域没有可用的 MapEffect 槽位";
            PaintMapDetails();
            UpdateMask();
        }

        /// <summary>刷新当前播放与槽位记录，保留右侧尚未应用的选择。</summary>
        private async void RefreshMapEffects()
        {
            if (_readingEffects || _applyingEffects || _tabs.SelectedIndex != 1 || _resources == null || !_hasSnapshot || _slots.Rows.Count == 0)
                return;

            var resources = _resources;
            var shown = _shown;
            var revision = _effectRevision;
            var simulated = _host.ZoneService?.IsInSimulatedTerritory == true;
            var slots = _slots.Rows.Cast<DataGridViewRow>().Select(row => (MapSlot)row.Tag).ToArray();
            var ids = slots.Select(slot => (slot.Index, slot.LayoutId)).ToArray();
            _readingEffects = true;
            try
            {
                var current = await Task.Run(() => _host.EnvironmentService.ReadMapEffectPlayback(shown, ids));
                if (!IsDisposed && !Disposing && ReferenceEquals(_resources, resources) && _shown.IsSameLayout(shown) && revision == _effectRevision)
                {
                    for (var i = 0; i < current.Length; i++)
                    {
                        if (current[i].HasValue)
                        {
                            var (recorded, playing, lastStarted) = current[i].Value;
                            if (slots[i].Observe(recorded, playing, lastStarted, simulated))
                                PaintMapSlot(slots[i]);
                        }
                    }
                    PaintMapDetails();
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing && ReferenceEquals(_resources, resources) && revision == _effectRevision)
                    SetFeedback("读取场地效果失败。", ex);
            }
            finally
            {
                _readingEffects = false;
            }
        }

        private void ApplyEffectRow(DataGridViewRow row)
        {
            if (_inspectedSlot != null && row?.Tag is ushort state)
                ApplyMapEffects(new[] { _inspectedSlot }, state);
        }

        private void QuickApplyMapEffect(int rowIndex, int bit)
        {
            if (rowIndex >= 0 && !_slots.Rows[rowIndex].Selected)
            {
                var clicked = _slots.Rows[rowIndex];
                _slots.ClearSelection();
                _slots.CurrentCell = clicked.Cells[0];
                clicked.Selected = true;
            }

            var rows = rowIndex < 0 ? _slots.Rows.Cast<DataGridViewRow>() : _slots.SelectedRows.Cast<DataGridViewRow>();
            var slots = rows.Select(row => row.Tag).OfType<MapSlot>().Where(slot => slot.Names[bit] != null)
                .OrderBy(slot => slot.Index).ToArray();
            if (slots.Length > 0)
                ApplyMapEffects(slots, StateAt(bit));
        }

        private async void ApplyMapEffects(MapSlot[] slots, ushort mask)
        {
            if (_applyingEffects || !_hasSnapshot || _resources == null)
                return;

            var resources = _resources;
            var shown = _shown;
            var service = _host.EnvironmentService;
            var applied = new bool[slots.Length];
            _effectRevision++;
            _applyingEffects = true;
            RefreshEffectInput();
            Exception failure = null;
            try
            {
                await Task.Run(() =>
                {
                    for (var i = 0; i < slots.Length; i++)
                    {
                        var slot = slots[i];
                        applied[i] = service.TryPlayCurrentMapEffect(shown, slot.Index, slot.LayoutId, mask);
                    }
                });
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                _applyingEffects = false;
            }

            if (IsDisposed || Disposing)
                return;

            RefreshEffectInput();
            if (!ReferenceEquals(_resources, resources) || !_shown.IsSameLayout(shown))
                return;

            var count = 0;
            for (var i = 0; i < slots.Length; i++)
            {
                if (!applied[i])
                    continue;

                slots[i].Apply(mask);
                PaintMapSlot(slots[i]);
                if (_inspectedSlot == slots[i])
                    _mask.Text = FormatActLogField(mask, slots[i].Index);
                count++;
            }

            PaintMapDetails();
            SetFeedback($"状态 {ToStateValue(mask)} 已应用到 {count} / {slots.Length} 个槽位。", failure, failed: count != slots.Length);
            RefreshMapEffects();
        }

        private static ushort StateAt(int bit) => (ushort)(1 << bit);
        private static string ToStateValue(ushort value) => $"0x{value:X}";
        private static string FormatActLogField(ushort mask, int index) => $"{mask:X4}{LogFieldSeparator}{index:X2}";
        private static bool IsSingleState(ushort value) => (value & (value - 1)) == 0;

        private static void PaintMapSlot(MapSlot slot)
        {
            for (var bit = 0; bit < StateCount; bit++)
            {
                var state = StateAt(bit);
                var cell = slot.Row.Cells[FirstStateColumn + bit];
                var active = (slot.State.GetValueOrDefault() & state) != 0;
                var name = slot.Names[bit];
                cell.Value = active ? ActiveStateMarker : name == null ? null : AvailableStateMarker;
                cell.Style.BackColor = active ? WorkbenchTheme.Blue : Color.Empty;
                cell.Style.ForeColor = active ? WorkbenchTheme.Canvas : WorkbenchTheme.Muted;
                cell.Style.SelectionBackColor = active ? WorkbenchTheme.Blue : Color.Empty;
                cell.Style.SelectionForeColor = active ? WorkbenchTheme.Canvas : WorkbenchTheme.Muted;
                cell.ToolTipText = name == null ? $"{ToStateValue(state)} · 此槽位没有对应的时间轴" :
                    $"{ToStateValue(state)}  {name}" + (active ? $"  ·  {slot.StateSource}" : string.Empty);
            }
        }

        private void PaintMapDetails()
        {
            if (_inspectedSlot != null)
            {
                var info = _inspectedSlot.StateSource;
                _effectNow.Text = string.IsNullOrEmpty(info) ? $"槽位 0x{_inspectedSlot.Index:X2}" :
                    $"槽位 0x{_inspectedSlot.Index:X2}  ·  {info}";
            }

            var active = _inspectedSlot?.State ?? 0;
            foreach (DataGridViewRow row in _bits.Rows)
            {
                var isActive = (active & StateAt(row.Index)) != 0;
                row.DefaultCellStyle.BackColor = isActive ? WorkbenchTheme.AccentOutline : Color.Empty;
                row.DefaultCellStyle.ForeColor = isActive ? WorkbenchTheme.Text : row.Tag == null ? WorkbenchTheme.Dim : Color.Empty;
            }
        }

        private void UpdateMask()
        {
            var valid = TryReadMask(out var mask);
            _mask.IsInvalid = _mask.TextLength > 0 && !valid;
            if (!_syncingBits)
            {
                _syncingBits = true;
                try
                {
                    var row = valid
                        ? _bits.Rows.Cast<DataGridViewRow>().FirstOrDefault(item => item.Tag is ushort state && state == mask)
                        : null;
                    if (row != _bits.CurrentRow)
                        _bits.CurrentCell = row?.Cells[0];
                    if (row == null)
                        _bits.ClearSelection();
                }
                finally
                {
                    _syncingBits = false;
                }
            }
            RefreshEffectInput();
        }

        private bool TryReadMask(out ushort mask)
        {
            var text = _mask.Text.Trim();
            if (text.Length == LogFieldLength && text.IndexOf(LogFieldSeparator, StringComparison.Ordinal) == 4)
            {
                return ushort.TryParse(text.Substring(0, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out mask)
                    && byte.TryParse(text.Substring(9), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var slot)
                    && slot == _inspectedSlot?.Index && IsSingleState(mask);
            }

            var parsed = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? ushort.TryParse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out mask)
                : ushort.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out mask);
            return parsed && IsSingleState(mask);
        }

        private sealed class MapSlot
        {
            private ushort? _recorded;
            private ushort? _applied;
            private ushort? _lastPlayed;
            private ushort? _lastStarted;

            internal MapSlot(int index, uint layoutId, DataGridViewRow row, MapEffectResource resource)
            {
                Index = index;
                LayoutId = layoutId;
                Row = row;
                if (resource != null)
                    foreach (var timeline in resource.Timelines)
                        for (var bit = 0; bit < StateCount; bit++)
                            if (timeline.Mask == StateAt(bit))
                                Names[bit] = timeline.Name;
            }

            internal int Index { get; }
            internal uint LayoutId { get; }
            internal DataGridViewRow Row { get; }
            internal string[] Names { get; } = new string[StateCount];
            internal ushort? State { get; private set; }
            internal string StateSource { get; private set; }

            internal bool Observe(ushort? recorded, ushort? playing, ushort? lastStarted, bool simulated)
            {
                if (recorded.HasValue && recorded != _recorded)
                {
                    _applied = null;
                    _lastPlayed = null;
                }
                _recorded = recorded;

                if (lastStarted.HasValue && lastStarted != _lastStarted)
                {
                    if (_lastStarted.HasValue && _applied.HasValue && _applied != lastStarted)
                        _applied = null;
                    _lastStarted = lastStarted;
                }
                if (playing.GetValueOrDefault() != 0)
                {
                    if (_applied.HasValue && (_applied.Value & playing.Value) == 0)
                        _applied = null;
                    _lastPlayed = playing;
                    return SetState(playing, "正在播放");
                }

                if (_applied.HasValue)
                    return SetState(_applied, "最近应用");

                // Phase data calls the playback routine directly. The packet's stored state can predate it.
                if (simulated && _lastStarted.HasValue)
                    return SetState(_lastStarted, "最近启动");
                if (recorded.HasValue)
                    return SetState(recorded, "槽位记录");
                if (_lastStarted.HasValue || _lastPlayed.HasValue)
                    return SetState(_lastStarted ?? _lastPlayed, "最近启动");
                return SetState(null, playing.HasValue ? "场景默认，未记录时间轴" : null);
            }

            internal void Apply(ushort state)
            {
                _applied = state;
                _lastPlayed = null;
                SetState(state, "最近应用");
            }

            private bool SetState(ushort? state, string source)
            {
                var changed = State != state || StateSource != source;
                State = state;
                StateSource = source;
                return changed;
            }
        }

        private sealed class MapEffectDetailGrid : DataGridViewEx
        {
            protected override void OnMouseDown(MouseEventArgs e)
            {
                var hit = HitTest(e.X, e.Y);
                if (e.Button == MouseButtons.Left && hit.Type == DataGridViewHitTestType.Cell && Rows[hit.RowIndex].Tag == null)
                    return;

                base.OnMouseDown(e);
            }
        }

        private sealed class MapEffectSlotGrid : DataGridViewEx
        {
            internal event Action<int, int> QuickApplyRequested;

            protected override void OnMouseDown(MouseEventArgs e)
            {
                var hit = HitTest(e.X, e.Y);
                if (e.Button == MouseButtons.Left && hit.ColumnIndex >= FirstStateColumn &&
                    (hit.Type == DataGridViewHitTestType.Cell || hit.Type == DataGridViewHitTestType.ColumnHeader))
                {
                    // Handle state clicks before the grid changes the selection for a batch.
                    if (e.Clicks == 1)
                        QuickApplyRequested?.Invoke(hit.RowIndex, hit.ColumnIndex - FirstStateColumn);
                    return;
                }

                base.OnMouseDown(e);
            }
        }
    }
}
