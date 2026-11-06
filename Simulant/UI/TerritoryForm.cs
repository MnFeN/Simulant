using Simulant.Core.Environment;
using Simulant.Game.ExtractedCsv;
using Simulant.Game.ExtractedCsv.Rows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using static Simulant.Game.ExtractedCsv.Rows.TerritoryType;

namespace Simulant.UI
{
    internal partial class TerritoryForm : Form
    {
        private List<TerritoryViewItem> _allTerritories;
        private List<TerritoryViewItem> _filteredTerritories;
        private readonly ErrorProvider _filterErrors;
        private bool _invalidRegex;
        private string _lastSuccessfulFilterText = string.Empty;
        private bool _lastSuccessfulPresetOnly;

        // 记录上一次的搜索条件，以便在重新打开窗口时恢复
        private static string _lastFilterText = string.Empty;
        private static bool _lastPresetOnly;
        private static int? _lastSelectedTerritoryId;

        public int TerritoryIdResult { get; private set; }

        public TerritoryForm()
        {
            InitializeComponent();
            if (components == null) components = new System.ComponentModel.Container();
            _filterErrors = new ErrorProvider(components)
            {
                ContainerControl = this,
                BlinkStyle = ErrorBlinkStyle.NeverBlink,
            };
            LoadTerritoryData();

            RestoreViewState();
            ApplySearch();
            RestoreSelectedTerritory();
        }

        private void RestoreViewState()
        {
            chkPresetOnly.Checked = _lastPresetOnly;
            txtFilter.Text = _lastFilterText ?? string.Empty;
        }

        private void RestoreSelectedTerritory()
        {
            if (!_lastSelectedTerritoryId.HasValue)
                return;

            var rowIndex = _filteredTerritories.FindIndex(t => t.Id == _lastSelectedTerritoryId.Value);
            if (rowIndex < 0 || rowIndex >= dgvTerritory.RowCount)
                return;

            dgvTerritory.ClearSelection();
            dgvTerritory.CurrentCell = dgvTerritory.Rows[rowIndex].Cells[0];
            dgvTerritory.Rows[rowIndex].Selected = true;

            try
            {
                if (dgvTerritory.Visible && dgvTerritory.DisplayedRowCount(false) > 0)
                    dgvTerritory.FirstDisplayedScrollingRowIndex = rowIndex;
            }
            catch (InvalidOperationException)
            {
                // 没有可供显示行的空间时跳过滚动。
            }
        }

        private void SaveViewState()
        {
            // 错误输入保留在本次窗口内，下次打开恢复当前列表实际使用的条件。
            _lastFilterText = _lastSuccessfulFilterText;
            _lastPresetOnly = _lastSuccessfulPresetOnly;

            var rowIndex = dgvTerritory.CurrentCell?.RowIndex ?? -1;
            _lastSelectedTerritoryId = rowIndex >= 0 && rowIndex < _filteredTerritories.Count
                ? (int?)_filteredTerritories[rowIndex].Id
                : null;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveViewState();
            base.OnFormClosing(e);
        }

        #region Territory

        private sealed class TerritoryViewItem
        {
            public int Id;
            public string IdString;
            public string PlaceName;
            public string InstanceName;
            public string RegionName;
            public string TypeName;
            public bool HasPreset;
        }

        private void LoadTerritoryData()
        {
            var territoryData = CsvManager.Instance.Get<TerritoryType>();
            var presetTerritoryIds = GetPresetTerritoryIds();

            // 预留一些空间，游戏更新时不更新数据也能进入地图
            var size = territoryData.Select(t => (int)t.Key).Max() + 100;

            _allTerritories = Enumerable.Range(0, size)
                .Select(i => territoryData.TryGetValue(i, out var t) ? t : null)
                .Select((territory, i) => new TerritoryViewItem
                {
                    Id = i,
                    IdString = i.ToString(),
                    PlaceName = territory?.PlaceName?.Name ?? string.Empty,
                    InstanceName = territory?.ContentFinderCondition?.Name ?? string.Empty,
                    RegionName = territory?.RegionPlaceName?.Name ?? string.Empty,
                    TypeName = ((TerritoryIntendedUseEnum?)territory?.TerritoryIntendedUse)?.ToString() ?? string.Empty,
                    HasPreset = presetTerritoryIds.Contains(i),
                })
                .ToList();

            _filteredTerritories = new List<TerritoryViewItem>(_allTerritories);
            dgvTerritory.RowCount = _filteredTerritories.Count;
        }

        private static HashSet<int> GetPresetTerritoryIds()
        {
            var result = new HashSet<int>();

            foreach (var type in typeof(TerritoryForm).Assembly.GetTypes())
            {
                foreach (var attribute in CustomAttributeData.GetCustomAttributes(type))
                {
                    if (attribute.AttributeType != typeof(SimTerritoryAttribute))
                        continue;

                    if (attribute.ConstructorArguments.Count == 0)
                        continue;

                    var value = attribute.ConstructorArguments[0].Value;
                    if (value is int territoryId)
                    {
                        result.Add(territoryId);
                    }
                }
            }

            return result;
        }

        private Regex _cachedRegex;

        private void txtFilter_TextChanged(object sender, EventArgs e)
        {
            _invalidRegex = false;
            _filterErrors.SetError(txtFilter, string.Empty);
            try
            {
                _cachedRegex = string.IsNullOrWhiteSpace(txtFilter.Text)
                    ? null
                    : new Regex(txtFilter.Text, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50));

                txtFilter.BackColor = SystemColors.Window;
            }
            catch (ArgumentException)
            {
                RejectSearch("正则表达式格式不正确，请修改后重试。");
                return;
            }
            ApplySearch();
        }

        private void ApplySearch()
        {
            if (_invalidRegex || _allTerritories == null) return;
            var result = new List<TerritoryViewItem>();
            var timer = Stopwatch.StartNew();

            bool Matches(string text)
            {
                if (timer.ElapsedMilliseconds >= 200)
                    throw new TimeoutException();
                return _cachedRegex.IsMatch(text ?? string.Empty);
            }

            try
            {
                foreach (var territory in _allTerritories)
                {
                    if (chkPresetOnly.Checked && !territory.HasPreset) continue;
                    if (_cachedRegex != null && !Matches(territory.IdString) &&
                        !Matches(territory.InstanceName) && !Matches(territory.PlaceName) && !Matches(territory.RegionName))
                        continue;
                    result.Add(territory);
                }
            }
            catch (TimeoutException)
            {
                RejectSearch("正则筛选耗时过长，已保留原列表。请简化或清空表达式后重试。");
                return;
            }

            // 全部匹配成功后才替换列表，输入错误或超时不会丢失当前结果和选择。
            dgvTerritory.Rows.Clear(); // 不加这个就会卡
            _filteredTerritories = result;
            dgvTerritory.RowCount = _filteredTerritories.Count;
            _lastSuccessfulFilterText = txtFilter.Text ?? string.Empty;
            _lastSuccessfulPresetOnly = chkPresetOnly.Checked;
        }

        private void RejectSearch(string message)
        {
            _invalidRegex = true;
            txtFilter.BackColor = Color.MistyRose;
            _filterErrors.SetError(txtFilter, message);
        }

        private void dgvTerritory_SelectionChanged(object sender, EventArgs e)
        {
            // preset 相关内容已移除；这里暂时不需要处理选中变化。
        }

        private void dgvTerritory_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if ((uint)e.RowIndex >= (uint)_filteredTerritories.Count)
                return;

            var territory = _filteredTerritories[e.RowIndex];

            switch (dgvTerritory.Columns[e.ColumnIndex].Name)
            {
                case "colId":
                    e.Value = territory.IdString;
                    break;

                case "colPlace":
                    e.Value = territory.PlaceName;
                    break;

                case "colInstance":
                    e.Value = territory.InstanceName;
                    break;

                case "colRegion":
                    e.Value = territory.RegionName;
                    break;

                case "colType":
                    e.Value = territory.TypeName;
                    break;
            }
        }

        private void dgvTerritory_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
        }

        private void dgvTerritory_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            btnOk_Click(sender, e);
        }

        private void chkPresetOnly_CheckedChanged(object sender, EventArgs e)
        {
            ApplySearch();
        }

        #endregion Territory

        private void btnOk_Click(object sender, EventArgs e)
        {
            var rowIndex = dgvTerritory.CurrentCell?.RowIndex ?? -1;
            if (rowIndex < 0 || rowIndex >= _filteredTerritories.Count)
                return;

            TerritoryIdResult = _filteredTerritories[rowIndex].Id;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
