#pragma warning disable IDE1006
using Simulant.ACT;
using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Core.Environment;
using Simulant.Game;
using Simulant.Game.ExtractedCsv;
using Simulant.Game.ExtractedCsv.Rows;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Simulation;
using Simulant.Utils;
using System;
using System.Buffers.Text;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Simulant.UI
{
    public partial class SimulantUI : UserControl
    {
        private readonly PluginHost _host;

        internal SimulantUI(PluginHost host)
        {
            _host = host;
            InitializeComponent();
            logView.Bind(_host); // would RefreshLogView
            presetControl.Bind(_host);
            UpdateTerritoryData(0);
            UpdateControlStates();
        }

        private async void btnInitPlugin_Click(object sender, EventArgs e)
        {
            await _host.InitAsync();
        }

        internal void UpdateControlStates()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new System.Action(UpdateControlStates));
                return;
            }

            var csvReady = _host.CsvReady && !_host.CsvLoading;
            var pluginReady = _host.PluginReady && !_host.IsInitializing;
            var firewallReady = pluginReady && _host.FirewallEnabled;

            // 初始化按钮：初始化中禁用，初始化完成后仍可再次点击
            btnInitPlugin.Enabled = !_host.IsInitializing;

            // CSV 加载完：允许选地图、输入区域 ID、选择阶段
            btnSelectTerritory.Enabled = csvReady;
            lblTerritoryId.Enabled = csvReady;
            numTerritoryId.Enabled = csvReady;
            lblTerritory.Enabled = csvReady;
            cbxPhase.Enabled = csvReady;

            // 插件初始化完成：允许防火墙和调试
            chkToggleFirewall.Enabled = pluginReady;
            btnDebug.Enabled = pluginReady;
            btnAddBNpc.Enabled = pluginReady;

            // 防火墙开启：允许加载区域、退出模拟
            btnSimEnter.Enabled = firewallReady && !_switchingTerritory;
            btnSimExit.Enabled = firewallReady && !_switchingTerritory;
        }

        #region Firewall

        private bool _updatingFirewallToggle;

        private void chkToggleFirewall_CheckedChanged(object sender, EventArgs e)
        {
            if (_updatingFirewallToggle)
                return;

            if (chkToggleFirewall.Checked)
            {
                TryEnableFirewall();
            }
            else
            {
                TryDisableFirewall();
            }
        }

        private void TryEnableFirewall()
        {
            try
            {
                _host.FirewallService.Enable();
                _host.LogSim("防火墙已成功开启。");
            }
            catch (Exception ex)
            {
                _host.LogError("防火墙开启失败：" + ex.Message);
                SetFirewallToggleChecked(false);
            }
            finally
            {
                UpdateControlStates();
            }
        }

        private void TryDisableFirewall()
        {
            try
            {
                _host.ZoneService.ExitToInitialTerritory();

                _host.FirewallService.Disable();
                _host.LogSim("防火墙已成功关闭。");
                SetFirewallToggleChecked(false);
            }
            catch (Exception ex)
            {
                _host.LogError("防火墙关闭失败：" + ex.Message);
                SetFirewallToggleChecked(_host.FirewallEnabled);
            }
            finally
            {
                UpdateControlStates();
            }
        }

        private void SetFirewallToggleChecked(bool value)
        {
            _updatingFirewallToggle = true;
            chkToggleFirewall.Checked = value;
            _updatingFirewallToggle = false;
        }

        #endregion Firewall

        #region Territory

        private bool _switchingTerritory;
        private int _selectedTerritoryId;
        private bool _updatingTerritoryIdInput;
        private bool _updatingPhaseSelection;

        private void btnSelectTerritory_Click(object sender, EventArgs e)
        {
            var form = new TerritoryForm();
            if (form.ShowDialog() != DialogResult.OK)
                return;

            var territoryId = form.TerritoryIdResult;

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

            if (territoryId > 0 && CsvManager.Instance.Get<TerritoryType>().TryGetValue(_selectedTerritoryId, out var territory))
            {
                territoryName = territory.PlaceName.Name;
                if (!string.IsNullOrWhiteSpace(territory?.ContentFinderCondition.Name))
                {
                    instanceName = territory.ContentFinderCondition.Name;
                }
            }
            lblTerritory.Text = $"选中区域：{territoryName}\n选中副本：{instanceName}";

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
                    .OrderBy(preset => preset.Name) // to-do: 用 attribute 排序
                    .ToList();
                cbxPhase.Items.AddRange(sims.Cast<object>().ToArray());
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

            await _host.ZoneService.EnterPhase(phaseData, false);
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

                if (!_host.ZoneService.TryEnterTerritory(_selectedTerritoryId))
                    return;

                await _host.ZoneService.EnterPhase(phaseData, true);
            }
            finally
            {
                _switchingTerritory = false;
                UpdateControlStates();
            }
        }

        private void btnSimExit_Click(object sender, EventArgs e)
        {
            _host.ZoneService.ExitToInitialTerritory();
        }

        #endregion Territory

        internal void RefreshLogView()
            => logView.RefreshLogView();

        private async void btnDebug_Click(object sender, EventArgs e)
        {
            try
            {
                await new Test(_host).Run();
            }
            catch (Exception ex)
            {
                _host.LogError("测试执行出现异常：" + ex);
            }
        }

        string _addBNpcInputCache = null;
        private void btnAddBNpc_Click(object sender, EventArgs e)
        {
            try
            {
                string result;
                using (var form = new SimpleInputForm(
                    "输入实体数据",

                    "BattleNpc:\n　参数 1：BaseID (uint)\n　　对于 BNpc 应为 1-999999\n　参数 2（可选）：NameID (uint)\n　参数 3（可选）：等级 (byte)\n\n" +
                    "EventObject: \n　参数 1：BaseID (uint)\n　　对于 EObj 应为 2000001-2999999\n　参数 2（可选）：当前动画状态 (ushort)\n　　默认 1，通常为 2^n\n　参数 3（可选）：EObj 实体表索引\n　　默认 39，仅允许 0-39，重复时会覆盖已有实体",

                    _addBNpcInputCache ?? "17839, 13561, 100"))
                {
                    result = form.GetInput();
                }

                if (result == null) 
                    return;
                _addBNpcInputCache = result;

                var (baseId, a2, a3) = result.ParseArgs<uint, uint?, uint?>((1, null), (2, null));

                if (baseId > 0 && baseId < 1000000) // BattleNpc
                {
                    var nameId = a2 ?? 0;
                    var level = checked((byte)(a3 ?? 1));

                    var me = _host.EntityProvider.GetMyself() ?? throw new InvalidOperationException("当前无法获取玩家实体。");
                    var bnpc = _host.EntitySpawner.SpawnBNpc(baseId, nameId, level);
                    bnpc.Pos3D = me.Pos3D;
                    bnpc.Heading = me.Heading;
                    bnpc.SetReadyToDraw();
                    bnpc.EnableDraw();

                    _host.LogSim($"已生成 BNpc: ID={bnpc.Id:X8}, BaseID={baseId}, NameID={nameId}, Index={bnpc.Native.ObjectIndex}, Ptr={bnpc.Native.Ptr.Hex()}");
                }

                else if (baseId > 2000000 && baseId < 3000000) // EventObject
                {
                    var timelineState = checked((ushort)(a2 ?? 1));
                    var objectIndex = checked((byte)(a3 ?? 39));

                    var me = _host.EntityProvider.GetMyself() ?? throw new InvalidOperationException("当前无法获取玩家实体。");
                    var data = new EObjData
                    {
                        Index = objectIndex,
                        BaseId = baseId,
                        Pos = me.Pos3D,
                        SharedTimelineState = timelineState,
                    };
                    var eobj = _host.EntitySpawner.SpawnEObj(data);

                    _host.LogSim($"已生成 EObj: ID={eobj.Id:X8}, BaseID={baseId}, TimelineState={timelineState}, Index={eobj.Native.ObjectIndex} ({data.Index}), Ptr={eobj.Native.Ptr.Hex()}");
                }
                else throw new ArgumentOutOfRangeException(nameof(baseId), baseId, "BaseID 不属于 BattleNpc / EventObject 的有效范围。");
            }
            catch (Exception ex)
            {
                _host.LogError("生成实体失败：" + ex);
            }
        }
    }
}
#pragma warning restore IDE1006