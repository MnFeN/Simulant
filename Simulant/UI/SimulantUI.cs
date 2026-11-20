#pragma warning disable IDE1006
using Simulant.ACT;
using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Core.Environment;
using Simulant.Core.Firewall;
using Simulant.Game;
using Simulant.Game.Data;
using Lumina.Excel.Sheets;
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
    public partial class SimulantUI : UserControlEx
    {
        private readonly PluginHost _host;
        private ToolStripMenuItem _addEntityMenuItem;
        private ToolStripMenuItem _debugMenuItem;
        private ToolStripMenuItem _actionResourceMenuItem;
        private ActionListForm _actionListForm;
        private ToolStripMenuItem _exdResourceMenuItem;
        private ExdBrowserForm _exdBrowserForm;
        private EnvironmentEditorForm _environmentEditorForm;
        private Form _resizeHost;
        private bool _resizeLayoutSuspended;

        public SimulantUI()
        {
            InitializeComponent();
            UpdateFirewallButton(false);
        }

        internal SimulantUI(PluginHost host) : this()
        {
            _host = host;
            ConfigureMainMenu();
            new ToolTip(components).SetToolTip(btnInitPlugin, "插件会自动连接游戏。需要重试时，请先关闭防火墙并完成清理。");
            logView.Bind(_host); // would RefreshLogView
            presetControl.Bind(_host);
            UpdateTerritoryData(0);
            UpdateControlStates();
            Disposed += (_, __) => _actionListForm?.Close();
            Disposed += (_, __) => _exdBrowserForm?.Close();
            Disposed += (_, __) => _environmentEditorForm?.Close();
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            AttachResizeHost(FindForm());
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            AttachResizeHost(FindForm());
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            AttachResizeHost(null);
            base.OnHandleDestroyed(e);
        }

        private void AttachResizeHost(Form host)
        {
            if (ReferenceEquals(_resizeHost, host))
                return;

            if (_resizeHost != null)
            {
                _resizeHost.ResizeBegin -= HostResizeBegin;
                _resizeHost.ResizeEnd -= HostResizeEnd;
                ResumeResizeLayout();
            }

            _resizeHost = host;
            if (host != null)
            {
                host.ResizeBegin += HostResizeBegin;
                host.ResizeEnd += HostResizeEnd;
            }
        }

        private void HostResizeBegin(object sender, EventArgs e)
        {
            if (_resizeLayoutSuspended)
                return;

            // The host can keep resizing the tab. Hold the child layout until the final bounds are known.
            _resizeLayoutSuspended = true;
            SuspendLayout();
        }

        private void HostResizeEnd(object sender, EventArgs e)
            => ResumeResizeLayout();

        private void ResumeResizeLayout()
        {
            if (!_resizeLayoutSuspended)
                return;

            _resizeLayoutSuspended = false;
            ResumeLayout(!IsDisposed && !Disposing);
            if (!IsDisposed && !Disposing)
                Invalidate(true);
        }

        private void btnInitPlugin_Click(object sender, EventArgs e)
            => _host.RetryGameConnection();

        private void btnConfiguration_Click(object sender, EventArgs e)
        {
            using (var form = new ConfigurationForm(_host))
                form.ShowDialog(this);
        }

        private void btnMenu_Click(object sender, EventArgs e)
            => btnMenu.ContextMenuStrip.Show(btnMenu, 0, btnMenu.Height);

        private void ConfigureMainMenu()
        {
            var menu = new ContextMenuStripEx(components) { ShowImageMargin = false };
            menu.Items.Add("配置", null, btnConfiguration_Click);
            var update = new ToolStripMenuItem("更新");
            update.DropDownItems.Add("检查更新", null, async (_, __) => await _host.Updater.CheckAsync());
            update.DropDownItems.Add("安装更新", null, async (_, __) => await _host.Updater.InstallAsync());

            update.DropDownItems.Add("打开发布页面", null, (_, __) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(PluginUpdater.ReleasesUrl)
                    {
                        UseShellExecute = true
                    });
                }
                catch (Exception error)
                {
                    _host.LogError("打开发布页面失败：" + error.Message);
                }
            });

            menu.Items.Add(update);
            var debug = new ToolStripMenuItem("调试功能");
            _addEntityMenuItem = new ToolStripMenuItem("添加实体")
            {
                ToolTipText = "单击生成网络 NPC 或场景物件；右键可生成玩家、使用本地备用方式或清理实体。未开启开发者模式时，生成实体需要启用防火墙。"
            };
            _debugMenuItem = new ToolStripMenuItem("调试")
            {
                ToolTipText = "单击测试网络 NPC；右键选择网络玩家、小队列表或本地三实体测试。运行时再次点击停止。"
            };
            debug.DropDownItems.AddRange(new ToolStripItem[]
            {
                _addEntityMenuItem, _debugMenuItem
            });
            var resources = new ToolStripMenuItem("游戏资源");

            _exdResourceMenuItem = new ToolStripMenuItem("通用 exd", null, (_, __) => OpenExdBrowser());

            resources.DropDownItems.Add(_exdResourceMenuItem);

            _actionResourceMenuItem = new ToolStripMenuItem("Action", null, (_, __) => OpenActionList());

            resources.DropDownItems.Add(_actionResourceMenuItem);
            menu.Items.Add(resources);
            debug.DropDown.ShowItemToolTips = true;
            menu.Items.Add(debug);
            BindDebugMenuItem(menu, _addEntityMenuItem, ConfigureNetworkEntityMenu(), btnAddBNpc_Click);
            BindDebugMenuItem(menu, _debugMenuItem, ConfigureEntityTestMenu(), btnDebug_Click);
            menu.Opening += (_, __) => UpdateControlStates();
            btnMenu.ContextMenuStrip = menu;
        }

        // 菜单项沿用旧按钮的左键操作和右键选项；右键不能同时触发生成或测试。
        private static void BindDebugMenuItem(ContextMenuStrip menu, ToolStripMenuItem item,
            ContextMenuStrip options, EventHandler command)
        {
            var rightClick = false;
            menu.Opening += (_, __) => rightClick = false;
            item.MouseDown += (_, e) => rightClick = e.Button == MouseButtons.Right;

            item.Click += (sender, e) =>
            {
                if (!rightClick)
                    command(sender, e);
            };

            item.MouseUp += (_, e) =>
            {
                if (e.Button != MouseButtons.Right)
                    return;

                var position = Cursor.Position;
                menu.Close();
                options.Show(position);
            };
        }

        internal void StopCurrentSession()
            => presetControl.StopCurrentSession();

        internal void ReceiveSimulationCommand(string command)
        {
            System.Action action;
            switch (command?.Trim().ToLowerInvariant())
            {
                case "start":
                    action = presetControl.StartCurrentSession;
                    break;
                case "stop":
                    action = StopCurrentSession;
                    break;
                case "retry":
                    action = presetControl.RetryCurrentSession;
                    break;
                case "a":
                case "action":
                    action = OpenActionList;
                    break;
                case "exd":
                    action = OpenExdBrowser;
                    break;
                case "env":
                    action = OpenEnvironmentEditor;
                    break;
                default:
                    return;
            }

            if (IsDisposed || Disposing || _host.Disposed || !IsHandleCreated)
                return;

            // 日志线程只投递命令，与按钮操作共用界面线程，串行完成停止和重开。
            BeginInvoke(new System.Action(() =>
            {
                if (IsDisposed || Disposing || _host.Disposed)
                    return;

                try
                {
                    action();
                }
                catch (Exception error)
                {
                    _host.LogError("执行模拟命令失败：" + error);
                }
            }));
        }

        internal void OnEntityWorldChanged()
        {
            if (IsDisposed || Disposing || _host.Disposed)
                return;

            if (InvokeRequired)
            {
                if (IsHandleCreated)
                    BeginInvoke(new System.Action(OnEntityWorldChanged));

                return;
            }

            StopCurrentSession();
            _host.StopDebugEntities();
            _host.CompleteEntityWorldChange();
            UpdateControlStates();
        }

        internal void UpdateControlStates()
        {
            if (IsDisposed || Disposing || _host.Disposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new System.Action(UpdateControlStates));
                return;
            }

            var gameDataReady = GameDataAvailable;
            var pluginReady = _host.PluginReady && !_host.IsInitializing;
            var firewallEnabled = _host.FirewallEnabled;
            var firewallReady = pluginReady && firewallEnabled;
            var firewallPending = _host.FirewallService?.HasPendingCleanup == true;
            lblConnection.Text = _host.IsInitializing ? "正在连接" : pluginReady ? "游戏已连接" : "未连接";
            lblConnection.ForeColor = pluginReady ? WorkbenchTheme.Accent : WorkbenchTheme.Muted;

            // 连接中或仍需清理防火墙时禁用手动重试。
            btnInitPlugin.Enabled = !_host.IsInitializing && !_switchingTerritory && !firewallPending;

            // 游戏数据加载完：允许选地图、输入区域 ID、选择阶段
            btnSelectTerritory.Enabled = gameDataReady && !_switchingTerritory;
            lblTerritoryId.Enabled = gameDataReady;
            numTerritoryId.Enabled = gameDataReady && !_switchingTerritory;
            lblTerritory.Enabled = gameDataReady;
            cbxPhase.Enabled = gameDataReady && !_switchingTerritory;

            // 插件初始化完成：允许防火墙和调试
            chkToggleFirewall.Enabled = !_host.IsInitializing && (pluginReady || firewallPending);
            // 保留清理入口，状态文字以防火墙实际启用状态为准。
            SetFirewallToggleChecked(firewallPending);
            UpdateFirewallButton(firewallEnabled);
            _debugMenuItem.Enabled = _host.CanRunDebug || _host.DebugTest?.HasPendingEntityTestCleanup == true;
            _addEntityMenuItem.Enabled = _host.CanRunDebug || _host.HasPendingDebugEntityCleanup;
            _actionResourceMenuItem.Enabled = gameDataReady;
            _exdResourceMenuItem.Enabled = gameDataReady;

            // 防火墙开启：允许加载区域、退出模拟
            btnSimEnter.Enabled = firewallReady && !_switchingTerritory;
            btnSimExit.Enabled = firewallReady && !_switchingTerritory;
            presetControl.UpdateQuickControls();
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
            var firewall = _host.FirewallService;
            try
            {
                firewall.Enable();
                _host.LogSim("防火墙已成功开启。");
            }
            catch (Exception ex)
            {
                _host.LogError("防火墙开启失败：" + ex.Message);
                SetFirewallToggleChecked(firewall.HasPendingCleanup);
                return;
            }
            finally
            {
                UpdateControlStates();
            }

            if (!firewall.IsInteractionBlocked)
                MessageBox.Show(this, FirewallService.InteractionWarning, "防火墙警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void TryDisableFirewall()
        {
            void TryCleanup(string description, System.Action cleanup)
            {
                try
                {
                    cleanup();
                }
                catch (Exception ex)
                {
                    _host.LogWarning(description + "：" + ex.Message + "；继续退出模拟并关闭防火墙。");
                }
            }

            try
            {
                TryCleanup("取消阶段等待失败", _host.ZoneService.CancelPendingPhase);
                TryCleanup("停止模拟失败", StopCurrentSession);
                TryCleanup("停止调试失败", _host.StopDebugEntities);
                TryCleanup("恢复小队失败", () => _host.PartyService?.RestoreAll());
                TryCleanup("回收模拟实体失败", _host.EntitySpawner.Clear);

                if (_host.ZoneService.HasPendingReturn && !_host.ZoneService.ExitToInitialTerritory())
                    throw new InvalidOperationException("未能返回真实区域，防火墙保持开启；请处理错误后重试。");

                _host.FirewallService.Disable();
                _host.LogSim("防火墙已成功关闭。");
                SetFirewallToggleChecked(false);
            }
            catch (Exception ex)
            {
                if (_host.FirewallService.TryResetExitedProcess())
                    _host.LogWarning("关闭防火墙的清理失败，但原绑定的 FF14 进程已退出，防火墙已重置：" + ex.Message);
                else
                    _host.LogError("防火墙关闭失败：" + ex.Message);
                SetFirewallToggleChecked(_host.FirewallService.HasPendingCleanup);
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

        private void UpdateFirewallButton(bool enabled)
        {
            var colors = WorkbenchTheme.GetButtonColors(enabled ? ButtonTone.Primary : ButtonTone.Default);
            chkToggleFirewall.Text = enabled ? "防火墙已开启" : "防火墙未开启";
            chkToggleFirewall.BackColor = colors.Background;
            chkToggleFirewall.ForeColor = enabled ? colors.Foreground : WorkbenchTheme.PaleRed;
        }

        #endregion Firewall
        #region Territory
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

            if (territoryId > 0 && GameDataService.Instance.GetSheet<TerritoryType>().TryGetRow((uint)_selectedTerritoryId, out var territory))
            {
                territoryName = territory.PlaceName.ValueNullable?.Name.ExtractText() ?? "无";

                if (!string.IsNullOrWhiteSpace(territory.ContentFinderCondition.ValueNullable?.Name.ExtractText()))
                {
                    instanceName = territory.ContentFinderCondition.Value.Name.ExtractText();
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

                await _host.ZoneService.EnterPhase(phaseData);
            }
            finally
            {
                _switchingTerritory = false;
                UpdateControlStates();
            }
        }

        private bool GameDataAvailable => _host.GameDataReady && !_host.GameDataLoading;

        private void OpenActionList()
            => ShowResourceWindow(ref _actionListForm, () => new ActionListForm(_host));

        private void OpenExdBrowser()
            => ShowResourceWindow(ref _exdBrowserForm, () => new ExdBrowserForm());

        private void ShowResourceWindow<T>(ref T window, Func<T> create) where T : Form
        {
            if (!GameDataAvailable)
            {
                _host.LogWarning("游戏数据尚未就绪，请等待加载完成后再打开检索窗口。");
                return;
            }

            ShowToolWindow(ref window, create);
        }

        private void btnEnvironment_Click(object sender, EventArgs e) => OpenEnvironmentEditor();

        private void OpenEnvironmentEditor()
        {
            if (_host != null)
                ShowToolWindow(ref _environmentEditorForm, () => new EnvironmentEditorForm(_host));
        }

        private static void ShowToolWindow<T>(ref T window, Func<T> create) where T : Form
        {
            if (window == null || window.IsDisposed)
            {
                window = create();
                window.ShowInTaskbar = true;
            }

            if (window.WindowState == FormWindowState.Minimized)
                window.WindowState = FormWindowState.Normal;

            window.Show();
            window.Activate();
        }

        private void btnSimExit_Click(object sender, EventArgs e)
        {
            _host.ZoneService.CancelPendingPhase();
            StopCurrentSession();
            _host.StopDebugEntities();
            _host.ZoneService.ExitToInitialTerritory();
        }

        #endregion Territory
        internal void RefreshLogView()
            => logView.RefreshLogView();

        private async void btnDebug_Click(object sender, EventArgs e)
            => await RunEntityTest((ModifierKeys & Keys.Shift) != 0
                ? Test.EntityTestKind.LocalEntities : Test.EntityTestKind.NetworkNpc);

        private ContextMenuStrip ConfigureEntityTestMenu()
        {
            var menu = new ContextMenuStripEx(components);
            menu.Items.Add("网络 NPC 测试", null, async (_, __) => await RunEntityTest(Test.EntityTestKind.NetworkNpc));
            menu.Items.Add("网络玩家测试（职业套装与复制外观）", null, async (_, __) => await RunEntityTest(Test.EntityTestKind.NetworkPlayer));
            menu.Items.Add("小队列表测试（血量、状态与仇恨）", null, async (_, __) => await RunEntityTest(Test.EntityTestKind.NetworkParty));
            menu.Items.Add("本地三实体测试", null, async (_, __) => await RunEntityTest(Test.EntityTestKind.LocalEntities));
            return menu;
        }

        private async Task RunEntityTest(Test.EntityTestKind kind)
        {
            try
            {
                await _host.DebugTest.Run(kind);
            }
            catch (Exception ex)
            {
                _host.LogError("测试执行出现异常：" + ex);
            }
        }

        string _addBNpcInputCache = null;
        private void btnAddBNpc_Click(object sender, EventArgs e)
            => SpawnManualEntity(false);

        private void SpawnManualEntity(bool local)
        {
            if (!_host.PluginReady || _host.IsInitializing)
            {
                _host.LogWarning("插件尚未完成初始化；如有未回收实体，请右键选择清理后再重试初始化。");
                return;
            }

            try
            {
                _host.EnsureDebugAllowed();
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

                    if (local)
                    {
                        var me = _host.EntityProvider.GetMyself() ?? throw new InvalidOperationException("当前无法获取玩家实体。");
                        var bnpc = _host.DebugEntities.SpawnBNpc(baseId, nameId, level);
                        bnpc.Pos3D = me.Pos3D;
                        bnpc.Heading = me.Heading;
                        bnpc.SetReadyToDraw();
                        bnpc.EnableDraw();

                        _host.LogSim($"已生成 BNpc: ID={bnpc.Id:X8}, BaseID={baseId}, NameID={nameId}, Index={bnpc.Entity.Identity.Slot}, Ptr={bnpc.Address.Hex()}");
                    }
                    else
                    {
                        var me = GetManualSpawnSource();
                        _host.DebugEntities.SpawnNetworkBNpc(new NetworkNpcSpawnSpec
                        {
                            BNpcBaseId = baseId,
                            BNpcNameId = nameId,
                            Level = level,
                            Pos = me.Pos3D,
                            Heading = me.Heading
                        }, LogManualNetworkEntity);
                    }
                }

                else if (baseId > 2000000 && baseId < 3000000) // EventObject
                {
                    var timelineState = checked((ushort)(a2 ?? 1));
                    var objectIndex = checked((byte)(a3 ?? 39));

                    var me = _host.EntityProvider.GetMyself() ?? throw new InvalidOperationException("当前无法获取玩家实体。");
                    var data = new EventObjectSpawnSpec
                    {
                        Index = objectIndex,
                        BaseId = baseId,
                        Pos = me.Pos3D,
                        Heading = me.Heading,
                        SharedTimelineState = timelineState,
                    };
                    var eobj = _host.DebugEntities.SpawnEObj(data);

                    _host.LogSim($"已生成 EObj: ID={eobj.Id:X8}, BaseID={baseId}, TimelineState={timelineState}, Index={eobj.Identity.Slot} ({data.Index}), Ptr={eobj.Address.Hex()}");
                }
                else
                    throw new ArgumentOutOfRangeException(nameof(baseId), baseId, "BaseID 不属于 BattleNpc / EventObject 的有效范围。");
            }
            catch (Exception ex)
            {
                _host.LogError("生成实体失败：" + ex);
            }
        }
    }
}
#pragma warning restore IDE1006
