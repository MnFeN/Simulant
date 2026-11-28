#pragma warning disable IDE1006
using Lumina.Excel.Sheets;
using Simulant.Content.Local;
using Simulant.Core.Environment;
using Simulant.Core.Zone;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Simulation;
using System;
using System.IO;
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
        private bool _restoredTerritorySelection;
        private readonly LocalEncounterLibrary _localEncounters;
        private GameDataService _localData;

        private void LoadLocalEncounters()
        {
            try
            {
                var data = GameDataService.Instance;
                if (ReferenceEquals(data, _localData))
                    return;
                _localData = data;
                _restoredTerritorySelection = false;
                _localEncounters.Load(encounter => encounter.ValidateResources(data), _host.LogWarning);
            }
            catch (Exception error)
            {
                _host.LogWarning("读取本地副本目录失败：" + error.Message);
            }
        }

        private bool CanImport => !_host.Disposed && GameDataAvailable && !_switchingTerritory && !presetControl.HasActiveSession;
        private void UpdateImportButton() => btnImportEncounter.Enabled = CanImport;

        private async void btnImportEncounter_Click(object sender, EventArgs e)
        {
            if (!CanImport)
                return;
            using (var dialog = new OpenFileDialog { Title = "导入本地副本描述文件", Filter = "JSON 描述文件 (*.json)|*.json", RestoreDirectory = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || !CanImport)
                    return;
                try
                {
                    var data = GameDataService.Instance;
                    var preset = _localEncounters.Import(LocalEncounterLibrary.Read(dialog.FileName), encounter => encounter.ValidateResources(data),
                        name => MessageBox.Show(this, $"替换已导入的「{name}」？", "导入本地副本",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes && CanImport &&
                            ReferenceEquals(data, GameDataService.Instance));
                    if (preset == null)
                        return;
                    SelectTerritory(preset.TerritoryId, preset.ConfigurationKey);
                    SaveTerritorySelection();
                    _host.LogSim($"已导入「{preset.Name}」（区域 {preset.TerritoryId}）。");
                    if (_host.ZoneService?.IsInSimulatedTerritory == true && IsCurrentTerritorySelected())
                        await _host.ZoneService.EnterPhase(preset.Phase);
                }
                catch (Exception error)
                {
                    var message = Path.GetFileName(dialog.FileName) + "：" + error.Message;
                    _host.LogError(message);
                    MessageBox.Show(this, message, "导入本地副本", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void btnSelectTerritory_Click(object sender, EventArgs e)
        {
            using (var form = new TerritoryForm())
            {
                try
                {
                    form.LoadTerritoryData(_localEncounters.Presets.Select(p => p.TerritoryId));
                }
                catch (Exception error)
                {
                    _host.LogError("读取区域列表失败：" + error.Message);
                    return;
                }

                if (form.ShowDialog(this) == DialogResult.OK)
                    numTerritoryId.Value = form.TerritoryIdResult;
            }
        }

        private void RestoreTerritorySelection()
        {
            if (_restoredTerritorySelection)
                return;

            _restoredTerritorySelection = true;
            var saved = _host.Configuration;
            var territoryId = saved.SelectedTerritoryId;
            if (territoryId <= 0 || territoryId > numTerritoryId.Maximum)
                return;

            SelectTerritory(territoryId, saved.SelectedPhaseKey);
        }

        private void SelectTerritory(int territoryId, string phaseKey)
        {
            _updatingTerritoryIdInput = true;

            try
            {
                numTerritoryId.Value = territoryId;
            }
            finally
            {
                _updatingTerritoryIdInput = false;
            }

            TryUpdateTerritoryData(territoryId, phaseKey);
        }

        private void numTerritoryId_ValueChanged(object sender, EventArgs e)
        {
            if (_updatingTerritoryIdInput)
                return;

            if (TryUpdateTerritoryData((int)numTerritoryId.Value))
                SaveTerritorySelection();
        }

        private void SaveTerritorySelection()
            => _host.SaveTerritorySelection(_selectedTerritoryId, GetPhaseSelectionKey(cbxPhase.SelectedItem));

        private static string GetPhaseSelectionKey(object item)
            => item is SimPresetBase preset ? preset.ConfigurationKey : (item as PhaseData)?.Name;

        private bool TryUpdateTerritoryData(int territoryId, string phaseKey = null)
        {
            _selectedTerritoryId = territoryId;
            TerritoryType? territory;
            var territoryName = "无";
            var instanceName = "无";

            try
            {
                territory = territoryId > 0 ? GameDataService.Instance.GetSheet<TerritoryType>().GetRowOrDefault((uint)territoryId) : null;
                if (territory.HasValue)
                {
                    territoryName = territoryId == GreenScreenStage.TerritoryId ? GreenScreenStage.Name
                        : territory.Value.PlaceName.ValueNullable?.Name.ExtractText() ?? "无";
                    var contentName = territory.Value.ContentFinderCondition.ValueNullable?.Name.ExtractText();
                    if (!string.IsNullOrWhiteSpace(contentName))
                        instanceName = contentName;
                }
            }
            catch (Exception error)
            {
                _host.LogError("读取区域数据失败：" + error.Message);
                _selectedTerritoryId = 0;
                territory = null;
                territoryName = "区域数据读取失败";
                instanceName = "无";
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
                cbxPhase.Items.AddRange(_localEncounters.Presets.Where(p => p.TerritoryId == territoryId)
                    .OrderBy(p => p.Name).ThenBy(p => p.ConfigurationKey, StringComparer.Ordinal).ToArray());

                var savedPhase = phaseKey == null ? null
                    : cbxPhase.Items.Cast<object>().FirstOrDefault(item => GetPhaseSelectionKey(item) == phaseKey);
                if (savedPhase != null)
                    cbxPhase.SelectedItem = savedPhase;
                else
                    cbxPhase.SelectedIndex = cbxPhase.Items.Count > 0 ? 0 : -1;
            }
            finally
            {
                _updatingPhaseSelection = false;
            }

            UpdateSelectedPresetControl();
            return _selectedTerritoryId == territoryId;
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
            SaveTerritorySelection();

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
