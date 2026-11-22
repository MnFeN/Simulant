#pragma warning disable IDE1006
using Lumina.Excel.Sheets;
using Simulant.Core.Environment;
using Simulant.Core.Zone;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Simulation;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Simulant.UI
{
    public partial class SimulantUI
    {
        private (uint TerritoryId, int Generation)? _spawnPickerWorld;
        private int _spawnPickerRequest;

        private void UpdateSpawnPointPicker(bool available, bool canMove)
        {
            spawnPicker.Visible = available;
            if (!available)
            {
                _spawnPickerWorld = null;
                cbxSpawnPoint.Items.Clear();
                return;
            }

            cbxSpawnPoint.Enabled = canMove && cbxSpawnPoint.SelectedItem is SpawnPoint;
            btnMoveToSpawnPoint.Enabled = cbxSpawnPoint.Enabled;
            if (!canMove)
                return;

            var world = (TerritoryId: _host.ZoneService.CurrentTerritoryId, Generation: _host.WorldGeneration);
            if (_spawnPickerWorld == world)
                return;

            _spawnPickerWorld = world;
            var request = ++_spawnPickerRequest;
            ShowSpawnPickerStatus(world.TerritoryId == 0 ? "当前地图未就绪" : "正在查找出生点…");
            if (world.TerritoryId != 0)
                _ = LoadSpawnPointsAsync(world, request);
        }

        private async Task LoadSpawnPointsAsync((uint TerritoryId, int Generation) world, int request)
        {
            bool IsCurrent() => !IsDisposed && !Disposing && !_host.Disposed && _host.FirewallEnabled &&
                _host.WorldGeneration == world.Generation && _spawnPickerWorld == world && _spawnPickerRequest == request;

            try
            {
                var points = await _host.ZoneService.GetSpawnPointsAsync(world.TerritoryId);
                if (!IsCurrent())
                    return;

                if (points.Length == 0)
                {
                    ShowSpawnPickerStatus("没有候选出生点");
                    return;
                }

                cbxSpawnPoint.BeginUpdate();
                try
                {
                    cbxSpawnPoint.Items.Clear();
                    cbxSpawnPoint.Items.AddRange(points);
                    cbxSpawnPoint.SelectedIndex = 0;
                    cbxSpawnPoint.DropDownWidth = Math.Max(cbxSpawnPoint.Width,
                        points.Max(point => TextRenderer.MeasureText(point.ToString(), cbxSpawnPoint.Font).Width) + 52);
                }
                finally
                {
                    cbxSpawnPoint.EndUpdate();
                }

                cbxSpawnPoint.Enabled = !_switchingTerritory;
                btnMoveToSpawnPoint.Enabled = cbxSpawnPoint.Enabled;
            }
            catch (Exception error)
            {
                if (IsCurrent())
                {
                    ShowSpawnPickerStatus("出生点读取失败");
                    _host.LogWarning("读取候选出生点失败：" + error.Message);
                }
            }
        }

        private void ShowSpawnPickerStatus(string message)
        {
            cbxSpawnPoint.Items.Clear();
            cbxSpawnPoint.SelectedIndex = cbxSpawnPoint.Items.Add(message);
            cbxSpawnPoint.Enabled = false;
            btnMoveToSpawnPoint.Enabled = false;
        }

        private void btnMoveToSpawnPoint_Click(object sender, EventArgs e)
        {
            if (_spawnPickerWorld.HasValue && cbxSpawnPoint.SelectedItem is SpawnPoint point)
            {
                var world = _spawnPickerWorld.Value;
                _host.ZoneService.TryMoveToSpawnPoint(world.TerritoryId, world.Generation, point);
            }
        }

        private bool _switchingTerritory;
        internal bool IsSwitchingTerritory => _switchingTerritory;

        private int _selectedTerritoryId;
        private bool _updatingTerritoryIdInput;
        private bool _updatingPhaseSelection;

        private void btnSelectTerritory_Click(object sender, EventArgs e)
        {
            int territoryId;

            using (var form = new TerritoryForm())
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                    return;

                territoryId = form.TerritoryIdResult;
            }

            // 同步数值框的地图 ID
            _updatingTerritoryIdInput = true;

            try
            {
                numTerritoryId.Value = territoryId;
            }
            finally
            {
                _updatingTerritoryIdInput = false;
            }

            UpdateTerritoryData(territoryId);
        }

        private void numTerritoryId_ValueChanged(object sender, EventArgs e)
        {
            if (_updatingTerritoryIdInput)
                return;

            var territoryId = (int)numTerritoryId.Value;
            UpdateTerritoryData(territoryId);
        }

        private void UpdateTerritoryData(int territoryId)
        {
            _selectedTerritoryId = territoryId;

            var territoryName = "无";
            var instanceName = "无";

            var territory = territoryId > 0 ? GameDataService.Instance.GetSheet<TerritoryType>().GetRowOrDefault((uint)territoryId) : null;
            if (territory.HasValue)
            {
                territoryName = territoryId == GreenScreenStage.TerritoryId ? GreenScreenStage.Name
                    : territory.Value.PlaceName.ValueNullable?.Name.ExtractText() ?? "无";

                var contentName = territory.Value.ContentFinderCondition.ValueNullable?.Name.ExtractText();
                if (!string.IsNullOrWhiteSpace(contentName))
                {
                    instanceName = contentName;
                }
            }

            lblZoneName.Text = territoryName == "无" ? "未选择区域" : territoryName;
            lblTerritory.Text = instanceName == "无" ? string.Empty : instanceName;

            presetControl.ClearPreset();

            _updatingPhaseSelection = true;

            try
            {
                cbxPhase.Items.Clear();

                var phases = PhaseData.GetPhases(_selectedTerritoryId);
                cbxPhase.Items.AddRange(phases.Cast<object>().ToArray());

                var sims = Assembly.GetExecutingAssembly().GetTypes() // 应该放在别的地方
                .Where(type =>
                        typeof(SimPresetBase).IsAssignableFrom(type)
                        && !type.IsAbstract
                        && type.GetConstructor(Type.EmptyTypes) != null)
                    .Select(type => (SimPresetBase)Activator.CreateInstance(type))
                    .Where(preset => preset.TerritoryId == _selectedTerritoryId)
                    .OrderBy(preset => preset.GetType().GetCustomAttribute<PhaseSelectionOrderAttribute>()?.SortIndex
                        ?? double.PositiveInfinity)
                    .ThenBy(preset => preset.Name)
                    .ToArray();
                cbxPhase.Items.AddRange(sims);
                if (cbxPhase.Items.Count == 0 && territory.HasValue)
                    cbxPhase.Items.Add(PhaseData.TerritoryDefaults);

                cbxPhase.SelectedIndex = cbxPhase.Items.Count > 0 ? 0 : -1;
            }
            finally
            {
                _updatingPhaseSelection = false;
            }

            UpdateSelectedPresetControl();
        }

        private void UpdateSelectedPresetControl()
        {
            if (cbxPhase.SelectedItem is SimPresetBase preset)
            {
                presetControl.LoadPreset(preset);
            }
            else
            {
                presetControl.ClearPreset();
            }
        }

        private bool TryGetSelectedPhase(out PhaseData phaseData)
        {
            if (cbxPhase.SelectedItem is PhaseData p)
            {
                phaseData = p;
                return true;
            }

            if (cbxPhase.SelectedItem is SimPresetBase s)
            {
                phaseData = s.Phase;
                return true;
            }

            phaseData = PhaseData.Empty;
            return false;
        }

        private bool IsCurrentTerritorySelected()
        {
            if (_selectedTerritoryId <= 0)
                return false;

            if (!_host.PluginReady)
                return false;

            return GameMain.Instance.CurrentTerritoryTypeId == _selectedTerritoryId;
        }

        private async void cbxPhase_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_updatingPhaseSelection || _switchingTerritory)
                return;

            UpdateSelectedPresetControl();

            if (!_host.PluginReady ||
                !TryGetSelectedPhase(out var phaseData) ||
                !IsCurrentTerritorySelected())
                return;

            await _host.ZoneService.EnterPhase(phaseData);
        }

        private async void btnSimEnter_Click(object sender, EventArgs e)
        {
            if (_switchingTerritory)
            {
                _host.LogWarning("正在切换区域，跳过重复操作。");
                return;
            }

            _switchingTerritory = true;
            UpdateControlStates();

            try
            {
                if (_selectedTerritoryId <= 0)
                {
                    _host.LogError("未选择有效的区域。");
                    return;
                }

                if (!TryGetSelectedPhase(out var phaseData))
                {
                    _host.LogError("未选择有效的阶段或模拟预设。");
                    return;
                }

                StopCurrentSession();

                if (!_host.ZoneService.TryEnterTerritory(_selectedTerritoryId))
                    return;

                if (await _host.ZoneService.EnterPhase(phaseData) && !IsDisposed && !Disposing && !_host.Disposed
                    && _host.ZoneService.ActiveGreenScreen != null)
                {
                    OpenGreenScreen();
                }
            }
            finally
            {
                _switchingTerritory = false;
                UpdateControlStates();
            }
        }

        private void btnSimExit_Click(object sender, EventArgs e)
        {
            _host.ZoneService.CancelPendingPhase();
            StopCurrentSession();
            _host.StopDebugEntities();
            try
            {
                _host.ZoneService.ExitToInitialTerritory();
            }
            finally
            {
                UpdateControlStates();
            }
        }
    }
}
