using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Simulant.UI
{
    partial class SimulantUI
    {
        private IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();

            base.Dispose(disposing);
        }

        #region Windows Forms Designer generated code

        private void InitializeComponent()
        {
            this.components = new Container();
            this.frame = new TableLayoutPanel();
            this.headerCard = new PanelEx();
            this.headerRow = new TableLayoutPanel();
            this.mark = new SimulantMark();
            this.wordmark = new TableLayoutPanel();
            this.lblBrand = new LabelEx();
            this.lblBrandEnglish = new LabelEx();
            this.lblConnection = new LabelEx();
            this.btnInitPlugin = new ButtonEx();
            this.chkToggleFirewall = new CheckBoxEx();
            this.btnMenu = new ButtonEx();
            this.workspace = new SplitContainerEx();
            this.splitMainV = new SplitContainerEx();
            this.sidebarCard = new PanelEx();
            this.sidebar = new TableLayoutPanel();
            this.lblRegionTitle = new LabelEx();
            this.mapPicker = new TableLayoutPanel();
            this.btnSelectTerritory = new ButtonEx();
            this.idField = new TableLayoutPanel();
            this.lblTerritoryId = new LabelEx();
            this.numTerritoryId = new NumericTextBoxEx();
            this.mapDetails = new PanelEx();
            this.mapText = new TableLayoutPanel();
            this.lblZoneName = new LabelEx();
            this.lblTerritory = new LabelEx();
            this.lblPhase = new LabelEx();
            this.cbxPhase = new ComboBoxEx();
            this.mapActions = new TableLayoutPanel();
            this.btnSimEnter = new ButtonEx();
            this.btnSimExit = new ButtonEx();
            this.btnEnvironment = new ButtonEx();
            this.spawnPicker = new TableLayoutPanel();
            this.lblSpawnPoint = new LabelEx();
            this.cbxSpawnPoint = new ComboBoxEx();
            this.btnMoveToSpawnPoint = new ButtonEx();
            this.presetControl = new PresetControl();
            this.logView = new LogView();
            this.frame.SuspendLayout();
            this.headerCard.SuspendLayout();
            this.headerRow.SuspendLayout();
            this.wordmark.SuspendLayout();
            ((ISupportInitialize)this.workspace).BeginInit();
            this.workspace.Panel1.SuspendLayout();
            this.workspace.Panel2.SuspendLayout();
            this.workspace.SuspendLayout();
            ((ISupportInitialize)this.splitMainV).BeginInit();
            this.splitMainV.Panel1.SuspendLayout();
            this.splitMainV.Panel2.SuspendLayout();
            this.splitMainV.SuspendLayout();
            this.sidebarCard.SuspendLayout();
            this.sidebar.SuspendLayout();
            this.mapPicker.SuspendLayout();
            this.idField.SuspendLayout();
            this.mapDetails.SuspendLayout();
            this.mapText.SuspendLayout();
            this.mapActions.SuspendLayout();
            this.spawnPicker.SuspendLayout();
            this.SuspendLayout();
            //
            // frame
            //
            this.frame.ColumnCount = 1;
            this.frame.RowCount = 2;
            this.frame.Dock = DockStyle.Fill;
            this.frame.Margin = Padding.Empty;
            this.frame.Padding = new Padding(16, 14, 16, 14);
            this.frame.Name = "frame";
            this.frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));
            this.frame.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.frame.Controls.Add(this.headerCard, 0, 0);
            this.frame.Controls.Add(this.workspace, 0, 1);
            //
            // headerCard
            //
            this.headerCard.Dock = DockStyle.Fill;
            this.headerCard.Margin = new Padding(0, 0, 0, 12);
            this.headerCard.Padding = new Padding(14, 9, 14, 9);
            this.headerCard.Name = "headerCard";
            this.headerCard.Controls.Add(this.headerRow);
            //
            // headerRow
            //
            this.headerRow.ColumnCount = 6;
            this.headerRow.RowCount = 1;
            this.headerRow.Dock = DockStyle.Fill;
            this.headerRow.Margin = Padding.Empty;
            this.headerRow.Name = "headerRow";
            this.headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50F));
            this.headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98F));
            this.headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86F));
            this.headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 121F));
            this.headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64F));
            this.headerRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.headerRow.Controls.Add(this.mark, 0, 0);
            this.headerRow.Controls.Add(this.wordmark, 1, 0);
            this.headerRow.Controls.Add(this.lblConnection, 2, 0);
            this.headerRow.Controls.Add(this.btnInitPlugin, 3, 0);
            this.headerRow.Controls.Add(this.chkToggleFirewall, 4, 0);
            this.headerRow.Controls.Add(this.btnMenu, 5, 0);
            //
            // mark
            //
            this.mark.Dock = DockStyle.Fill;
            this.mark.Margin = new Padding(0, 0, 8, 0);
            this.mark.TabStop = false;
            this.mark.Name = "mark";
            //
            // wordmark
            //
            this.wordmark.ColumnCount = 2;
            this.wordmark.RowCount = 1;
            this.wordmark.Dock = DockStyle.Fill;
            this.wordmark.Margin = Padding.Empty;
            this.wordmark.Name = "wordmark";
            this.wordmark.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
            this.wordmark.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.wordmark.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.wordmark.Controls.Add(this.lblBrand, 0, 0);
            this.wordmark.Controls.Add(this.lblBrandEnglish, 1, 0);
            //
            // lblBrand
            //
            this.lblBrand.Text = "仿生石";
            this.lblBrand.Dock = DockStyle.Fill;
            this.lblBrand.Margin = Padding.Empty;
            this.lblBrand.TextAlign = ContentAlignment.MiddleLeft;
            this.lblBrand.AutoEllipsis = true;
            this.lblBrand.Tone = LabelTone.Title;
            this.lblBrand.Name = "lblBrand";
            //
            // lblBrandEnglish
            //
            this.lblBrandEnglish.Text = "SIMULANT";
            this.lblBrandEnglish.Dock = DockStyle.Fill;
            this.lblBrandEnglish.Margin = Padding.Empty;
            this.lblBrandEnglish.TextAlign = ContentAlignment.MiddleLeft;
            this.lblBrandEnglish.AutoEllipsis = true;
            this.lblBrandEnglish.Tone = LabelTone.Muted;
            this.lblBrandEnglish.Name = "lblBrandEnglish";
            //
            // lblConnection
            //
            this.lblConnection.Text = "未连接";
            this.lblConnection.Dock = DockStyle.Fill;
            this.lblConnection.Margin = new Padding(0, 0, 12, 0);
            this.lblConnection.TextAlign = ContentAlignment.MiddleRight;
            this.lblConnection.AutoEllipsis = true;
            this.lblConnection.Tone = LabelTone.Muted;
            this.lblConnection.Name = "lblConnection";
            //
            // btnInitPlugin
            //
            this.btnInitPlugin.Text = "重试连接";
            this.btnInitPlugin.Dock = DockStyle.Fill;
            this.btnInitPlugin.Margin = new Padding(0, 5, 8, 5);
            this.btnInitPlugin.TabIndex = 0;
            this.btnInitPlugin.Name = "btnInitPlugin";
            this.btnInitPlugin.Click += new EventHandler(this.btnInitPlugin_Click);
            //
            // chkToggleFirewall
            //
            this.chkToggleFirewall.Text = "防火墙未开启";
            this.chkToggleFirewall.Appearance = Appearance.Button;
            this.chkToggleFirewall.Style = CheckBoxStyle.ToggleButton;
            this.chkToggleFirewall.TextAlign = ContentAlignment.MiddleCenter;
            this.chkToggleFirewall.Dock = DockStyle.Fill;
            this.chkToggleFirewall.Margin = new Padding(0, 5, 8, 5);
            this.chkToggleFirewall.AccessibleName = "防火墙";
            this.chkToggleFirewall.TabIndex = 1;
            this.chkToggleFirewall.Name = "chkToggleFirewall";
            this.chkToggleFirewall.CheckedChanged += new EventHandler(this.chkToggleFirewall_CheckedChanged);
            //
            // btnMenu
            //
            this.btnMenu.Text = "菜单";
            this.btnMenu.Dock = DockStyle.Fill;
            this.btnMenu.Margin = new Padding(0, 5, 0, 5);
            this.btnMenu.TabIndex = 2;
            this.btnMenu.Name = "btnMenu";
            this.btnMenu.Click += new EventHandler(this.btnMenu_Click);
            //
            // workspace
            //
            this.workspace.Dock = DockStyle.Fill;
            this.workspace.Margin = Padding.Empty;
            this.workspace.TabStop = false;
            this.workspace.Orientation = Orientation.Horizontal;
            this.workspace.Size = new Size(1148, 656);
            this.workspace.SplitterWidth = 8;
            this.workspace.SplitterDistance = 316;
            this.workspace.Panel1MinSize = 174;
            this.workspace.Panel2MinSize = 150;
            this.workspace.Name = "workspace";
            this.workspace.Panel1.Controls.Add(this.splitMainV);
            this.workspace.Panel2.Controls.Add(this.logView);
            //
            // splitMainV
            //
            this.splitMainV.Dock = DockStyle.Fill;
            this.splitMainV.Margin = Padding.Empty;
            this.splitMainV.TabStop = false;
            this.splitMainV.Size = new Size(1148, 316);
            this.splitMainV.SplitterWidth = 8;
            this.splitMainV.SplitterDistance = 286;
            this.splitMainV.Panel1MinSize = 280;
            this.splitMainV.Panel2MinSize = 300;
            this.splitMainV.Name = "splitMainV";
            this.splitMainV.Panel1.AutoScroll = true;
            this.splitMainV.Panel1.Controls.Add(this.sidebarCard);
            this.splitMainV.Panel2.Controls.Add(this.presetControl);
            //
            // sidebarCard
            //
            this.sidebarCard.Dock = DockStyle.Fill;
            this.sidebarCard.Margin = Padding.Empty;
            this.sidebarCard.Padding = new Padding(16);
            this.sidebarCard.MinimumSize = new Size(0, 312);
            this.sidebarCard.Name = "sidebarCard";
            this.sidebarCard.Controls.Add(this.sidebar);
            //
            // sidebar
            //
            this.sidebar.ColumnCount = 1;
            this.sidebar.RowCount = 10;
            this.sidebar.Dock = DockStyle.Fill;
            this.sidebar.Margin = Padding.Empty;
            this.sidebar.Name = "sidebar";
            this.sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 27F));
            this.sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 67F));
            this.sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 8F));
            this.sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            this.sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            this.sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 33F));
            this.sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 9F));
            this.sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 37F));
            this.sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.sidebar.Controls.Add(this.lblRegionTitle, 0, 0);
            this.sidebar.Controls.Add(this.mapDetails, 0, 1);
            this.sidebar.Controls.Add(this.mapPicker, 0, 3);
            this.sidebar.Controls.Add(this.lblPhase, 0, 4);
            this.sidebar.Controls.Add(this.cbxPhase, 0, 5);
            this.sidebar.Controls.Add(this.mapActions, 0, 7);
            this.sidebar.Controls.Add(this.spawnPicker, 0, 8);
            //
            // lblRegionTitle
            //
            this.lblRegionTitle.Text = "模拟区域";
            this.lblRegionTitle.Dock = DockStyle.Fill;
            this.lblRegionTitle.Margin = Padding.Empty;
            this.lblRegionTitle.TextAlign = ContentAlignment.MiddleLeft;
            this.lblRegionTitle.AutoEllipsis = true;
            this.lblRegionTitle.Tone = LabelTone.Heading;
            this.lblRegionTitle.Name = "lblRegionTitle";
            //
            // mapPicker
            //
            this.mapPicker.ColumnCount = 2;
            this.mapPicker.RowCount = 1;
            this.mapPicker.Dock = DockStyle.Fill;
            this.mapPicker.Margin = Padding.Empty;
            this.mapPicker.Name = "mapPicker";
            this.mapPicker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.mapPicker.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
            this.mapPicker.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.mapPicker.Controls.Add(this.idField, 0, 0);
            this.mapPicker.Controls.Add(this.btnSelectTerritory, 1, 0);
            //
            // btnSelectTerritory
            //
            this.btnSelectTerritory.Text = "选择区域";
            this.btnSelectTerritory.Dock = DockStyle.Fill;
            this.btnSelectTerritory.Margin = new Padding(6, 0, 0, 0);
            this.btnSelectTerritory.TabIndex = 0;
            this.btnSelectTerritory.Name = "btnSelectTerritory";
            this.btnSelectTerritory.Click += new EventHandler(this.btnSelectTerritory_Click);
            //
            // idField
            //
            this.idField.ColumnCount = 3;
            this.idField.RowCount = 1;
            this.idField.Dock = DockStyle.Fill;
            this.idField.Margin = Padding.Empty;
            this.idField.Name = "idField";
            this.idField.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 49F));
            this.idField.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 53F));
            this.idField.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.idField.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.idField.Controls.Add(this.lblTerritoryId, 0, 0);
            this.idField.Controls.Add(this.numTerritoryId, 1, 0);
            //
            // lblTerritoryId
            //
            this.lblTerritoryId.Text = "区域 ID";
            this.lblTerritoryId.Dock = DockStyle.Fill;
            this.lblTerritoryId.Margin = Padding.Empty;
            this.lblTerritoryId.TextAlign = ContentAlignment.MiddleLeft;
            this.lblTerritoryId.AutoEllipsis = true;
            this.lblTerritoryId.Tone = LabelTone.Muted;
            this.lblTerritoryId.Name = "lblTerritoryId";
            //
            // numTerritoryId
            //
            this.numTerritoryId.AccessibleName = "区域 ID";
            this.numTerritoryId.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            this.numTerritoryId.Margin = Padding.Empty;
            this.numTerritoryId.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            this.numTerritoryId.TextAlign = HorizontalAlignment.Center;
            this.numTerritoryId.Name = "numTerritoryId";
            this.numTerritoryId.ValueChanged += new EventHandler(this.numTerritoryId_ValueChanged);
            //
            // mapDetails
            //
            this.mapDetails.Dock = DockStyle.Fill;
            this.mapDetails.Margin = new Padding(0, 6, 0, 0);
            this.mapDetails.Padding = new Padding(12, 7, 12, 7);
            this.mapDetails.Tone = PanelTone.Field;
            this.mapDetails.Name = "mapDetails";
            this.mapDetails.Controls.Add(this.mapText);
            //
            // mapText
            //
            this.mapText.ColumnCount = 1;
            this.mapText.RowCount = 2;
            this.mapText.Dock = DockStyle.Fill;
            this.mapText.Margin = Padding.Empty;
            this.mapText.Name = "mapText";
            this.mapText.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.mapText.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
            this.mapText.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
            this.mapText.Controls.Add(this.lblZoneName, 0, 0);
            this.mapText.Controls.Add(this.lblTerritory, 0, 1);
            //
            // lblZoneName
            //
            this.lblZoneName.Text = "未选择区域";
            this.lblZoneName.Dock = DockStyle.Fill;
            this.lblZoneName.Margin = Padding.Empty;
            this.lblZoneName.TextAlign = ContentAlignment.MiddleLeft;
            this.lblZoneName.AutoEllipsis = true;
            this.lblZoneName.Tone = LabelTone.Heading;
            this.lblZoneName.Name = "lblZoneName";
            //
            // lblTerritory
            //
            this.lblTerritory.Text = "";
            this.lblTerritory.Dock = DockStyle.Fill;
            this.lblTerritory.Margin = Padding.Empty;
            this.lblTerritory.TextAlign = ContentAlignment.MiddleLeft;
            this.lblTerritory.AutoEllipsis = true;
            this.lblTerritory.Tone = LabelTone.Muted;
            this.lblTerritory.Name = "lblTerritory";
            //
            // lblPhase
            //
            this.lblPhase.Text = "阶段 / 模拟";
            this.lblPhase.Dock = DockStyle.Fill;
            this.lblPhase.Margin = Padding.Empty;
            this.lblPhase.TextAlign = ContentAlignment.MiddleLeft;
            this.lblPhase.AutoEllipsis = true;
            this.lblPhase.Tone = LabelTone.Muted;
            this.lblPhase.Name = "lblPhase";
            //
            // cbxPhase
            //
            this.cbxPhase.AccessibleName = "阶段 / 预设";
            this.cbxPhase.Dock = DockStyle.Fill;
            this.cbxPhase.Margin = Padding.Empty;
            this.cbxPhase.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cbxPhase.DropDownHeight = 450;
            this.cbxPhase.IntegralHeight = false;
            this.cbxPhase.FormattingEnabled = true;
            this.cbxPhase.TabIndex = 2;
            this.cbxPhase.Name = "cbxPhase";
            this.cbxPhase.SelectedIndexChanged += new EventHandler(this.cbxPhase_SelectedIndexChanged);
            //
            // mapActions
            //
            this.mapActions.ColumnCount = 3;
            this.mapActions.RowCount = 1;
            this.mapActions.Dock = DockStyle.Fill;
            this.mapActions.Margin = Padding.Empty;
            this.mapActions.Name = "mapActions";
            this.mapActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33333F));
            this.mapActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33333F));
            this.mapActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33333F));
            this.mapActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.mapActions.Controls.Add(this.btnSimEnter, 0, 0);
            this.mapActions.Controls.Add(this.btnSimExit, 1, 0);
            this.mapActions.Controls.Add(this.btnEnvironment, 2, 0);
            //
            // btnSimEnter
            //
            this.btnSimEnter.Text = "加载区域";
            this.btnSimEnter.Dock = DockStyle.Fill;
            this.btnSimEnter.Margin = new Padding(0, 0, 5, 0);
            this.btnSimEnter.Tone = ButtonTone.Primary;
            this.btnSimEnter.Name = "btnSimEnter";
            this.btnSimEnter.Click += new EventHandler(this.btnSimEnter_Click);
            //
            // btnSimExit
            //
            this.btnSimExit.Text = "退出模拟";
            this.btnSimExit.Dock = DockStyle.Fill;
            this.btnSimExit.Margin = new Padding(0, 0, 5, 0);
            this.btnSimExit.TabIndex = 1;
            this.btnSimExit.Name = "btnSimExit";
            this.btnSimExit.Click += new EventHandler(this.btnSimExit_Click);
            //
            // btnEnvironment
            //
            this.btnEnvironment.Text = "场景调整";
            this.btnEnvironment.Dock = DockStyle.Fill;
            this.btnEnvironment.Margin = Padding.Empty;
            this.btnEnvironment.TabIndex = 2;
            this.btnEnvironment.Name = "btnEnvironment";
            this.btnEnvironment.Click += new EventHandler(this.btnEnvironment_Click);
            //
            // spawnPicker
            //
            this.spawnPicker.Name = "spawnPicker";
            this.spawnPicker.ColumnCount = 3;
            this.spawnPicker.RowCount = 1;
            this.spawnPicker.AutoSize = true;
            this.spawnPicker.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.spawnPicker.Dock = DockStyle.Fill;
            this.spawnPicker.Margin = new Padding(0, 8, 0, 0);
            this.spawnPicker.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46F));
            this.spawnPicker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.spawnPicker.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50F));
            this.spawnPicker.RowStyles.Add(new RowStyle(SizeType.Absolute, 33F));
            this.spawnPicker.Controls.Add(this.lblSpawnPoint, 0, 0);
            this.spawnPicker.Controls.Add(this.cbxSpawnPoint, 1, 0);
            this.spawnPicker.Controls.Add(this.btnMoveToSpawnPoint, 2, 0);
            this.spawnPicker.Visible = false;
            //
            // lblSpawnPoint
            //
            this.lblSpawnPoint.Name = "lblSpawnPoint";
            this.lblSpawnPoint.Text = "出生点";
            this.lblSpawnPoint.Dock = DockStyle.Fill;
            this.lblSpawnPoint.Margin = Padding.Empty;
            this.lblSpawnPoint.TextAlign = ContentAlignment.MiddleLeft;
            this.lblSpawnPoint.Tone = LabelTone.Muted;
            //
            // cbxSpawnPoint
            //
            this.cbxSpawnPoint.Name = "cbxSpawnPoint";
            this.cbxSpawnPoint.AccessibleName = "当前地图的出生点";
            this.cbxSpawnPoint.Dock = DockStyle.Fill;
            this.cbxSpawnPoint.Margin = Padding.Empty;
            this.cbxSpawnPoint.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cbxSpawnPoint.DropDownHeight = 300;
            this.cbxSpawnPoint.IntegralHeight = false;
            this.cbxSpawnPoint.FormattingEnabled = true;
            //
            // btnMoveToSpawnPoint
            //
            this.btnMoveToSpawnPoint.Name = "btnMoveToSpawnPoint";
            this.btnMoveToSpawnPoint.Text = "前往";
            this.btnMoveToSpawnPoint.Dock = DockStyle.Fill;
            this.btnMoveToSpawnPoint.Margin = new Padding(5, 0, 0, 0);
            this.btnMoveToSpawnPoint.TabIndex = 1;
            this.btnMoveToSpawnPoint.Click += new EventHandler(this.btnMoveToSpawnPoint_Click);
            //
            // presetControl
            //
            this.presetControl.Dock = DockStyle.Fill;
            this.presetControl.TabStop = false;
            this.presetControl.Name = "presetControl";
            //
            // logView
            //
            this.logView.Dock = DockStyle.Fill;
            this.logView.Name = "logView";
            //
            // SimulantUI
            //
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Controls.Add(this.frame);
            this.Size = new Size(1180, 760);
            this.Name = "SimulantUI";
            this.spawnPicker.ResumeLayout(false);
            this.mapActions.ResumeLayout(false);
            this.mapText.ResumeLayout(false);
            this.mapDetails.ResumeLayout(false);
            this.idField.ResumeLayout(false);
            this.mapPicker.ResumeLayout(false);
            this.sidebar.ResumeLayout(false);
            this.sidebarCard.ResumeLayout(false);
            this.splitMainV.Panel1.ResumeLayout(false);
            this.splitMainV.Panel2.ResumeLayout(false);
            ((ISupportInitialize)this.splitMainV).EndInit();
            this.splitMainV.ResumeLayout(false);
            this.workspace.Panel1.ResumeLayout(false);
            this.workspace.Panel2.ResumeLayout(false);
            ((ISupportInitialize)this.workspace).EndInit();
            this.workspace.ResumeLayout(false);
            this.wordmark.ResumeLayout(false);
            this.headerRow.ResumeLayout(false);
            this.headerCard.ResumeLayout(false);
            this.frame.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel frame;
        private PanelEx headerCard;
        private TableLayoutPanel headerRow;
        private SimulantMark mark;
        private TableLayoutPanel wordmark;
        private LabelEx lblBrand;
        private LabelEx lblBrandEnglish;
        private LabelEx lblConnection;
        private ButtonEx btnInitPlugin;
        private CheckBoxEx chkToggleFirewall;
        private ButtonEx btnMenu;
        private SplitContainerEx workspace;
        private SplitContainerEx splitMainV;
        private PanelEx sidebarCard;
        private TableLayoutPanel sidebar;
        private LabelEx lblRegionTitle;
        private TableLayoutPanel mapPicker;
        private ButtonEx btnSelectTerritory;
        private TableLayoutPanel idField;
        private LabelEx lblTerritoryId;
        private NumericTextBoxEx numTerritoryId;
        private PanelEx mapDetails;
        private TableLayoutPanel mapText;
        private LabelEx lblZoneName;
        private LabelEx lblTerritory;
        private LabelEx lblPhase;
        private ComboBoxEx cbxPhase;
        private TableLayoutPanel mapActions;
        private ButtonEx btnSimEnter;
        private ButtonEx btnSimExit;
        private ButtonEx btnEnvironment;
        private TableLayoutPanel spawnPicker;
        private LabelEx lblSpawnPoint;
        private ComboBoxEx cbxSpawnPoint;
        private ButtonEx btnMoveToSpawnPoint;
        internal PresetControl presetControl;
        private LogView logView;
    }
}
