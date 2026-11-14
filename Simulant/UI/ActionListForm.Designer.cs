namespace Simulant.UI
{
    partial class ActionListForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CancelPendingWork();
                components?.Dispose();
            }

            base.Dispose(disposing);
        }

        #region 窗体设计器生成的代码
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ActionListForm));
            this.layoutMain = new System.Windows.Forms.TableLayoutPanel();
            this.panelTop = new System.Windows.Forms.TableLayoutPanel();
            this.chkNewOnly = new System.Windows.Forms.CheckBox();
            this.panelButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnReset = new System.Windows.Forms.Button();
            this.btnExport = new System.Windows.Forms.Button();
            this.btnSearch = new System.Windows.Forms.Button();
            this.lblResult = new System.Windows.Forms.Label();
            this.panelShapes = new System.Windows.Forms.FlowLayoutPanel();
            this.chkShapeAll = new System.Windows.Forms.CheckBox();
            this.chkShape0 = new System.Windows.Forms.CheckBox();
            this.chkShape1 = new System.Windows.Forms.CheckBox();
            this.chkShape2 = new System.Windows.Forms.CheckBox();
            this.chkShape3 = new System.Windows.Forms.CheckBox();
            this.chkShape4 = new System.Windows.Forms.CheckBox();
            this.chkShape5 = new System.Windows.Forms.CheckBox();
            this.chkShape6 = new System.Windows.Forms.CheckBox();
            this.chkShape7 = new System.Windows.Forms.CheckBox();
            this.chkShape8 = new System.Windows.Forms.CheckBox();
            this.chkShape9 = new System.Windows.Forms.CheckBox();
            this.chkShape10 = new System.Windows.Forms.CheckBox();
            this.chkShape11 = new System.Windows.Forms.CheckBox();
            this.chkShape12 = new System.Windows.Forms.CheckBox();
            this.chkShape13 = new System.Windows.Forms.CheckBox();
            this.chkShape14 = new System.Windows.Forms.CheckBox();
            this.chkShape15 = new System.Windows.Forms.CheckBox();
            this.chkShape16 = new System.Windows.Forms.CheckBox();
            this.chkShape17 = new System.Windows.Forms.CheckBox();
            this.panelFilters = new System.Windows.Forms.TableLayoutPanel();
            this.filterHexId = new System.Windows.Forms.TableLayoutPanel();
            this.lblHexId = new System.Windows.Forms.Label();
            this.txtHexId = new System.Windows.Forms.TextBox();
            this.filterName = new System.Windows.Forms.TableLayoutPanel();
            this.lblName = new System.Windows.Forms.Label();
            this.txtName = new System.Windows.Forms.TextBox();
            this.filterExpression = new System.Windows.Forms.TableLayoutPanel();
            this.lblExpression = new System.Windows.Forms.Label();
            this.txtExpression = new System.Windows.Forms.TextBox();
            this.filterOmen = new System.Windows.Forms.TableLayoutPanel();
            this.lblOmen = new System.Windows.Forms.Label();
            this.txtOmen = new System.Windows.Forms.TextBox();
            this.filterAnimationEnd = new System.Windows.Forms.TableLayoutPanel();
            this.lblAnimationEnd = new System.Windows.Forms.Label();
            this.txtAnimationEnd = new System.Windows.Forms.TextBox();
            this.gridActions = new System.Windows.Forms.DataGridView();
            this.contextActions = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.miCopyCell = new System.Windows.Forms.ToolStripMenuItem();
            this.miCopyTemplate = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.miOmen = new System.Windows.Forms.ToolStripMenuItem();
            this.miGimmick = new System.Windows.Forms.ToolStripMenuItem();
            this.col0 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.col1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.col2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.col3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.col4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.col5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.col6 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.col7 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.col8 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.col9 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.col10 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.col11 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.layoutMain.SuspendLayout();
            this.panelTop.SuspendLayout();
            this.panelButtons.SuspendLayout();
            this.panelShapes.SuspendLayout();
            this.panelFilters.SuspendLayout();
            this.filterHexId.SuspendLayout();
            this.filterName.SuspendLayout();
            this.filterExpression.SuspendLayout();
            this.filterOmen.SuspendLayout();
            this.filterAnimationEnd.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridActions)).BeginInit();
            this.contextActions.SuspendLayout();
            this.SuspendLayout();
            //
            // layoutMain
            //
            this.layoutMain.ColumnCount = 2;
            this.layoutMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.layoutMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.layoutMain.Controls.Add(this.gridActions, 0, 0);
            this.layoutMain.Controls.Add(this.panelShapes, 1, 0);
            this.layoutMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.layoutMain.Name = "layoutMain";
            this.layoutMain.RowCount = 1;
            this.layoutMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.layoutMain.TabIndex = 1;
            //
            // panelTop
            //
            this.panelTop.AutoSize = true;
            this.panelTop.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panelTop.ColumnCount = 3;
            this.panelTop.RowCount = 1;
            this.panelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.panelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.panelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.panelTop.Controls.Add(this.chkNewOnly, 0, 0);
            this.panelTop.Controls.Add(this.lblResult, 1, 0);
            this.panelTop.Controls.Add(this.panelButtons, 2, 0);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Name = "panelTop";
            this.panelTop.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.panelTop.TabIndex = 0;
            //
            // chkNewOnly
            //
            this.chkNewOnly.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.chkNewOnly.AutoSize = true;
            this.chkNewOnly.Name = "chkNewOnly";
            this.chkNewOnly.TabIndex = 0;
            this.chkNewOnly.Text = "仅显示新增项 (?)";
            this.toolTip.SetToolTip(this.chkNewOnly, "仅显示在最新版本中新增的技能。当前暂不比较旧版本，所有技能均视为非新增。");
            //
            // panelButtons
            //
            this.panelButtons.AutoSize = true;
            this.panelButtons.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panelButtons.Controls.Add(this.btnReset);
            this.panelButtons.Controls.Add(this.btnExport);
            this.panelButtons.Controls.Add(this.btnSearch);
            this.panelButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.panelButtons.Name = "panelButtons";
            this.panelButtons.TabIndex = 1;
            this.panelButtons.WrapContents = false;
            //
            // btnReset
            //
            this.btnReset.AutoSize = true;
            this.btnReset.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnReset.Enabled = false;
            this.btnReset.Name = "btnReset";
            this.btnReset.TabIndex = 0;
            this.btnReset.Text = "重置";
            this.btnReset.Click += new System.EventHandler(this.btnReset_Click);
            //
            // btnExport
            //
            this.btnExport.AutoSize = true;
            this.btnExport.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnExport.Name = "btnExport";
            this.btnExport.TabIndex = 1;
            this.btnExport.Text = "复制所选行";
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            //
            // btnSearch
            //
            this.btnSearch.AutoSize = true;
            this.btnSearch.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnSearch.Enabled = false;
            this.btnSearch.Name = "btnSearch";
            this.btnSearch.TabIndex = 2;
            this.btnSearch.Text = "搜索";
            this.btnSearch.Click += new System.EventHandler(this.btnSearch_Click);
            //
            // lblResult
            //
            this.lblResult.AutoSize = true;
            this.lblResult.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblResult.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblResult.Name = "lblResult";
            this.lblResult.TabIndex = 3;
            this.lblResult.Text = "正在准备……";
            //
            // panelShapes
            //
            this.panelShapes.AutoScroll = true;
            this.panelShapes.AutoSize = true;
            this.panelShapes.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panelShapes.Controls.Add(this.chkShapeAll);
            this.panelShapes.Controls.Add(this.chkShape0);
            this.panelShapes.Controls.Add(this.chkShape1);
            this.panelShapes.Controls.Add(this.chkShape2);
            this.panelShapes.Controls.Add(this.chkShape3);
            this.panelShapes.Controls.Add(this.chkShape4);
            this.panelShapes.Controls.Add(this.chkShape5);
            this.panelShapes.Controls.Add(this.chkShape6);
            this.panelShapes.Controls.Add(this.chkShape7);
            this.panelShapes.Controls.Add(this.chkShape8);
            this.panelShapes.Controls.Add(this.chkShape9);
            this.panelShapes.Controls.Add(this.chkShape10);
            this.panelShapes.Controls.Add(this.chkShape11);
            this.panelShapes.Controls.Add(this.chkShape12);
            this.panelShapes.Controls.Add(this.chkShape13);
            this.panelShapes.Controls.Add(this.chkShape14);
            this.panelShapes.Controls.Add(this.chkShape15);
            this.panelShapes.Controls.Add(this.chkShape16);
            this.panelShapes.Controls.Add(this.chkShape17);
            this.panelShapes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelShapes.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.panelShapes.Name = "panelShapes";
            this.panelShapes.TabIndex = 1;
            this.panelShapes.WrapContents = false;
            this.panelShapes.Layout += new System.Windows.Forms.LayoutEventHandler(this.panelShapes_Layout);
            //
            // chkShapeAll
            //
            this.chkShapeAll.AutoSize = true;
            this.chkShapeAll.Name = "chkShapeAll";
            this.chkShapeAll.TabIndex = 0;
            this.chkShapeAll.Text = "全选";
            this.chkShapeAll.CheckedChanged += new System.EventHandler(this.chkShapeAll_CheckedChanged);
            //
            // chkShape0
            //
            this.chkShape0.AutoSize = true;
            this.chkShape0.Name = "chkShape0";
            this.chkShape0.TabIndex = 1;
            this.chkShape0.Text = "无 (0)";
            this.chkShape0.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            //
            // chkShape1
            //
            this.chkShape1.AutoSize = true;
            this.chkShape1.Name = "chkShape1";
            this.chkShape1.TabIndex = 2;
            this.chkShape1.Text = "单体 (1)";
            this.chkShape1.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            //
            // chkShape2
            //
            this.chkShape2.AutoSize = true;
            this.chkShape2.Name = "chkShape2";
            this.chkShape2.TabIndex = 3;
            this.chkShape2.Text = "圆形 (2)";
            this.chkShape2.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            //
            // chkShape3
            //
            this.chkShape3.AutoSize = true;
            this.chkShape3.Name = "chkShape3";
            this.chkShape3.TabIndex = 4;
            this.chkShape3.Text = "扇形+ (3)";
            this.chkShape3.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            //
            // chkShape4
            //
            this.chkShape4.AutoSize = true;
            this.chkShape4.Name = "chkShape4";
            this.chkShape4.TabIndex = 5;
            this.chkShape4.Text = "矩形+ (4)";
            this.chkShape4.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            //
            // chkShape5
            //
            this.chkShape5.AutoSize = true;
            this.chkShape5.Name = "chkShape5";
            this.chkShape5.TabIndex = 6;
            this.chkShape5.Text = "圆形+ (5)";
            this.chkShape5.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            //
            // chkShape6
            //
            this.chkShape6.AutoSize = true;
            this.chkShape6.Name = "chkShape6";
            this.chkShape6.TabIndex = 7;
            this.chkShape6.Text = "未知 (6)";
            this.chkShape6.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            //
            // chkShape7
            //
            this.chkShape7.AutoSize = true;
            this.chkShape7.Name = "chkShape7";
            this.chkShape7.TabIndex = 8;
            this.chkShape7.Text = "未知 (7)";
            this.chkShape7.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            //
            // chkShape8
            //
            this.chkShape8.AutoSize = true;
            this.chkShape8.Name = "chkShape8";
            this.chkShape8.TabIndex = 9;
            this.chkShape8.Text = "矩形至 (8)";
            this.chkShape8.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            //
            // chkShape9
            //
            this.chkShape9.AutoSize = true;
            this.chkShape9.Name = "chkShape9";
            this.chkShape9.TabIndex = 10;
            this.chkShape9.Text = "未知 (9)";
            this.chkShape9.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            //
            // chkShape10
            //
            this.chkShape10.AutoSize = true;
            this.chkShape10.Name = "chkShape10";
            this.chkShape10.TabIndex = 11;
            this.chkShape10.Text = "月环 (10)";
            this.chkShape10.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            //
            // chkShape11
            //
            this.chkShape11.AutoSize = true;
            this.chkShape11.Name = "chkShape11";
            this.chkShape11.TabIndex = 12;
            this.chkShape11.Text = "十字 (11)";
            this.chkShape11.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            //
            // chkShape12
            //
            this.chkShape12.AutoSize = true;
            this.chkShape12.Name = "chkShape12";
            this.chkShape12.TabIndex = 13;
            this.chkShape12.Text = "矩形 (12)";
            this.chkShape12.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            //
            // chkShape13
            //
            this.chkShape13.AutoSize = true;
            this.chkShape13.Name = "chkShape13";
            this.chkShape13.TabIndex = 14;
            this.chkShape13.Text = "扇形 (13)";
            this.chkShape13.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            //
            // chkShape14
            //
            this.chkShape14.AutoSize = true;
            this.chkShape14.Name = "chkShape14";
            this.chkShape14.TabIndex = 15;
            this.chkShape14.Text = "三角 (14)";
            this.chkShape14.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            //
            // chkShape15
            //
            this.chkShape15.AutoSize = true;
            this.chkShape15.Name = "chkShape15";
            this.chkShape15.TabIndex = 16;
            this.chkShape15.Text = "矩形穿 (15)";
            this.chkShape15.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            //
            // chkShape16
            //
            this.chkShape16.AutoSize = true;
            this.chkShape16.Name = "chkShape16";
            this.chkShape16.TabIndex = 17;
            this.chkShape16.Text = "右矩形至 (16)";
            this.chkShape16.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            //
            // chkShape17
            //
            this.chkShape17.AutoSize = true;
            this.chkShape17.Name = "chkShape17";
            this.chkShape17.TabIndex = 18;
            this.chkShape17.Text = "左矩形至 (17)";
            this.chkShape17.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            //
            // panelFilters
            //
            this.panelFilters.AutoSize = true;
            this.panelFilters.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panelFilters.ColumnCount = 5;
            this.panelFilters.RowCount = 1;
            this.panelFilters.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15F));
            this.panelFilters.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.panelFilters.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this.panelFilters.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 17.5F));
            this.panelFilters.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 17.5F));
            this.panelFilters.Controls.Add(this.filterHexId, 0, 0);
            this.panelFilters.Controls.Add(this.filterName, 1, 0);
            this.panelFilters.Controls.Add(this.filterExpression, 2, 0);
            this.panelFilters.Controls.Add(this.filterOmen, 3, 0);
            this.panelFilters.Controls.Add(this.filterAnimationEnd, 4, 0);
            this.panelFilters.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelFilters.Name = "panelFilters";
            this.panelFilters.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.panelFilters.TabIndex = 2;
            //
            // filterHexId
            //
            this.filterHexId.AutoSize = true;
            this.filterHexId.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.filterHexId.ColumnCount = 1;
            this.filterHexId.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.filterHexId.Controls.Add(this.lblHexId, 0, 0);
            this.filterHexId.Controls.Add(this.txtHexId, 0, 1);
            this.filterHexId.Dock = System.Windows.Forms.DockStyle.Fill;
            this.filterHexId.Name = "filterHexId";
            this.filterHexId.RowCount = 2;
            this.filterHexId.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.filterHexId.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.filterHexId.TabIndex = 0;
            //
            // lblHexId
            //
            this.lblHexId.AutoSize = true;
            this.lblHexId.Name = "lblHexId";
            this.lblHexId.TabIndex = 0;
            this.lblHexId.Text = "HexID";
            this.toolTip.SetToolTip(this.lblHexId, "使用正则表达式过滤技能 HexID，大小写不敏感。");
            //
            // txtHexId
            //
            this.txtHexId.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtHexId.Name = "txtHexId";
            this.txtHexId.TabIndex = 1;
            this.toolTip.SetToolTip(this.txtHexId, "使用正则表达式过滤技能 HexID，大小写不敏感。");
            //
            // filterName
            //
            this.filterName.AutoSize = true;
            this.filterName.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.filterName.ColumnCount = 1;
            this.filterName.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.filterName.Controls.Add(this.lblName, 0, 0);
            this.filterName.Controls.Add(this.txtName, 0, 1);
            this.filterName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.filterName.Name = "filterName";
            this.filterName.RowCount = 2;
            this.filterName.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.filterName.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.filterName.TabIndex = 1;
            //
            // lblName
            //
            this.lblName.AutoSize = true;
            this.lblName.Name = "lblName";
            this.lblName.TabIndex = 0;
            this.lblName.Text = "技能名";
            this.toolTip.SetToolTip(this.lblName, "使用正则表达式过滤技能名称，大小写不敏感。");
            //
            // txtName
            //
            this.txtName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtName.Name = "txtName";
            this.txtName.TabIndex = 1;
            this.toolTip.SetToolTip(this.txtName, "使用正则表达式过滤技能名称，大小写不敏感。");
            //
            // filterExpression
            //
            this.filterExpression.AutoSize = true;
            this.filterExpression.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.filterExpression.ColumnCount = 1;
            this.filterExpression.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.filterExpression.Controls.Add(this.lblExpression, 0, 0);
            this.filterExpression.Controls.Add(this.txtExpression, 0, 1);
            this.filterExpression.Dock = System.Windows.Forms.DockStyle.Fill;
            this.filterExpression.Name = "filterExpression";
            this.filterExpression.RowCount = 2;
            this.filterExpression.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.filterExpression.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.filterExpression.TabIndex = 2;
            //
            // lblExpression
            //
            this.lblExpression.AutoSize = true;
            this.lblExpression.Name = "lblExpression";
            this.lblExpression.TabIndex = 0;
            this.lblExpression.Text = "过滤表达式";
            this.toolTip.SetToolTip(this.lblExpression, resources.GetString("lblExpression.ToolTip"));
            //
            // txtExpression
            //
            this.txtExpression.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtExpression.Name = "txtExpression";
            this.txtExpression.TabIndex = 1;
            this.toolTip.SetToolTip(this.txtExpression, resources.GetString("txtExpression.ToolTip"));
            //
            // filterOmen
            //
            this.filterOmen.AutoSize = true;
            this.filterOmen.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.filterOmen.ColumnCount = 1;
            this.filterOmen.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.filterOmen.Controls.Add(this.lblOmen, 0, 0);
            this.filterOmen.Controls.Add(this.txtOmen, 0, 1);
            this.filterOmen.Dock = System.Windows.Forms.DockStyle.Fill;
            this.filterOmen.Name = "filterOmen";
            this.filterOmen.RowCount = 2;
            this.filterOmen.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.filterOmen.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.filterOmen.TabIndex = 3;
            //
            // lblOmen
            //
            this.lblOmen.AutoSize = true;
            this.lblOmen.Name = "lblOmen";
            this.lblOmen.TabIndex = 0;
            this.lblOmen.Text = "预兆 (Omen)";
            this.toolTip.SetToolTip(this.lblOmen, "使用正则表达式过滤预兆名称。");
            //
            // txtOmen
            //
            this.txtOmen.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtOmen.Name = "txtOmen";
            this.txtOmen.TabIndex = 1;
            this.toolTip.SetToolTip(this.txtOmen, "使用正则表达式过滤预兆名称。");
            //
            // filterAnimationEnd
            //
            this.filterAnimationEnd.AutoSize = true;
            this.filterAnimationEnd.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.filterAnimationEnd.ColumnCount = 1;
            this.filterAnimationEnd.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.filterAnimationEnd.Controls.Add(this.lblAnimationEnd, 0, 0);
            this.filterAnimationEnd.Controls.Add(this.txtAnimationEnd, 0, 1);
            this.filterAnimationEnd.Dock = System.Windows.Forms.DockStyle.Fill;
            this.filterAnimationEnd.Name = "filterAnimationEnd";
            this.filterAnimationEnd.RowCount = 2;
            this.filterAnimationEnd.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.filterAnimationEnd.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.filterAnimationEnd.TabIndex = 4;
            //
            // lblAnimationEnd
            //
            this.lblAnimationEnd.AutoSize = true;
            this.lblAnimationEnd.Name = "lblAnimationEnd";
            this.lblAnimationEnd.TabIndex = 0;
            this.lblAnimationEnd.Text = "结束时间轴";
            this.toolTip.SetToolTip(this.lblAnimationEnd, "使用正则表达式过滤结束时间轴名称。");
            //
            // txtAnimationEnd
            //
            this.txtAnimationEnd.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtAnimationEnd.Name = "txtAnimationEnd";
            this.txtAnimationEnd.TabIndex = 1;
            this.toolTip.SetToolTip(this.txtAnimationEnd, "使用正则表达式过滤结束时间轴名称。");
            //
            // gridActions
            //
            this.gridActions.AllowUserToAddRows = false;
            this.gridActions.AllowUserToDeleteRows = false;
            this.gridActions.AllowUserToResizeRows = false;
            this.gridActions.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridActions.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridActions.ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.Disable;
            this.gridActions.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[]
            {
                this.col0,
                this.col1,
                this.col2,
                this.col3,
                this.col4,
                this.col5,
                this.col6,
                this.col7,
                this.col8,
                this.col9,
                this.col10,
                this.col11
            });
            this.gridActions.ContextMenuStrip = this.contextActions;
            this.gridActions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridActions.Name = "gridActions";
            this.gridActions.ReadOnly = true;
            this.gridActions.RowHeadersVisible = false;
            this.gridActions.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridActions.TabIndex = 3;
            this.gridActions.VirtualMode = true;
            this.gridActions.CellMouseDown += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.gridActions_CellMouseDown);
            this.gridActions.CellValueNeeded += new System.Windows.Forms.DataGridViewCellValueEventHandler(this.gridActions_CellValueNeeded);
            this.gridActions.ColumnHeaderMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.gridActions_ColumnHeaderMouseClick);
            //
            // contextActions
            //
            this.contextActions.Items.AddRange(new System.Windows.Forms.ToolStripItem[]
            {
                this.miCopyCell,
                this.miCopyTemplate,
                this.menuSeparator,
                this.miOmen,
                this.miGimmick
            });
            this.contextActions.Name = "contextActions";
            this.contextActions.Opening += new System.ComponentModel.CancelEventHandler(this.contextActions_Opening);
            //
            // miCopyCell
            //
            this.miCopyCell.Name = "miCopyCell";
            this.miCopyCell.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C)));
            this.miCopyCell.Text = "复制当前单元格";
            this.miCopyCell.Click += new System.EventHandler(this.miCopyCell_Click);
            //
            // miCopyTemplate
            //
            this.miCopyTemplate.Name = "miCopyTemplate";
            this.miCopyTemplate.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift)
            | System.Windows.Forms.Keys.C)));
            this.miCopyTemplate.Text = "将选中行复制为简易播报动作模板";
            this.miCopyTemplate.Click += new System.EventHandler(this.miCopyTemplate_Click);
            //
            // menuSeparator
            //
            this.menuSeparator.Name = "menuSeparator";
            //
            // miOmen
            //
            this.miOmen.Name = "miOmen";
            this.miOmen.Text = "显示 Omen";
            this.miOmen.Click += new System.EventHandler(this.miOmen_Click);
            //
            // miGimmick
            //
            this.miGimmick.Name = "miGimmick";
            this.miGimmick.Text = "显示 Gimmick VFX";
            this.miGimmick.ToolTipText = "在当前目标（未选中目标时为自己）身上播放全部关联的时间轴特效。";
            this.miGimmick.Click += new System.EventHandler(this.miGimmick_Click);
            //
            // col0
            //
            this.col0.FillWeight = 75F;
            this.col0.HeaderText = "ID";
            this.col0.Name = "col0";
            this.col0.ReadOnly = true;
            this.col0.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            //
            // col1
            //
            this.col1.FillWeight = 65F;
            this.col1.HeaderText = "Hex";
            this.col1.Name = "col1";
            this.col1.ReadOnly = true;
            this.col1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            //
            // col2
            //
            this.col2.FillWeight = 180F;
            this.col2.HeaderText = "技能名";
            this.col2.Name = "col2";
            this.col2.ReadOnly = true;
            this.col2.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            //
            // col3
            //
            this.col3.HeaderText = "咏唱";
            this.col3.Name = "col3";
            this.col3.ReadOnly = true;
            this.col3.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            //
            // col4
            //
            this.col4.FillWeight = 140F;
            this.col4.HeaderText = "形状";
            this.col4.Name = "col4";
            this.col4.ReadOnly = true;
            this.col4.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            //
            // col5
            //
            this.col5.FillWeight = 70F;
            this.col5.HeaderText = "X";
            this.col5.Name = "col5";
            this.col5.ReadOnly = true;
            this.col5.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            //
            // col6
            //
            this.col6.FillWeight = 70F;
            this.col6.HeaderText = "Y";
            this.col6.Name = "col6";
            this.col6.ReadOnly = true;
            this.col6.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            //
            // col7
            //
            this.col7.HeaderText = "分类";
            this.col7.Name = "col7";
            this.col7.ReadOnly = true;
            this.col7.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            //
            // col8
            //
            this.col8.HeaderText = "攻击";
            this.col8.Name = "col8";
            this.col8.ReadOnly = true;
            this.col8.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            //
            // col9
            //
            this.col9.FillWeight = 80F;
            this.col9.HeaderText = "属性";
            this.col9.Name = "col9";
            this.col9.ReadOnly = true;
            this.col9.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            //
            // col10
            //
            this.col10.FillWeight = 280F;
            this.col10.HeaderText = "预兆名称 (ID)";
            this.col10.Name = "col10";
            this.col10.ReadOnly = true;
            this.col10.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            //
            // col11
            //
            this.col11.FillWeight = 500F;
            this.col11.HeaderText = "结束时间轴名称 (ID)";
            this.col11.Name = "col11";
            this.col11.ReadOnly = true;
            this.col11.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            //
            // toolTip
            //
            this.toolTip.AutoPopDelay = 30000;
            this.toolTip.InitialDelay = 300;
            this.toolTip.ReshowDelay = 100;
            this.toolTip.ShowAlways = true;
            //
            // ActionListForm
            //
            this.AcceptButton = this.btnSearch;
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.layoutMain);
            this.Controls.Add(this.panelFilters);
            this.Controls.Add(this.panelTop);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.Name = "ActionListForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "FFXIV 技能检索";
            this.Shown += new System.EventHandler(this.ActionListForm_Shown);
            this.layoutMain.ResumeLayout(false);
            this.layoutMain.PerformLayout();
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            this.panelButtons.ResumeLayout(false);
            this.panelButtons.PerformLayout();
            this.panelShapes.ResumeLayout(false);
            this.panelShapes.PerformLayout();
            this.panelFilters.ResumeLayout(false);
            this.panelFilters.PerformLayout();
            this.filterHexId.ResumeLayout(false);
            this.filterHexId.PerformLayout();
            this.filterName.ResumeLayout(false);
            this.filterName.PerformLayout();
            this.filterExpression.ResumeLayout(false);
            this.filterExpression.PerformLayout();
            this.filterOmen.ResumeLayout(false);
            this.filterOmen.PerformLayout();
            this.filterAnimationEnd.ResumeLayout(false);
            this.filterAnimationEnd.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridActions)).EndInit();
            this.contextActions.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TableLayoutPanel layoutMain;
        private System.Windows.Forms.TableLayoutPanel panelTop;
        private System.Windows.Forms.FlowLayoutPanel panelButtons;
        private System.Windows.Forms.FlowLayoutPanel panelShapes;
        private System.Windows.Forms.TableLayoutPanel panelFilters;
        private System.Windows.Forms.CheckBox chkNewOnly;
        private System.Windows.Forms.CheckBox chkShapeAll;
        private System.Windows.Forms.Label lblResult;
        private System.Windows.Forms.Button btnSearch;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.TableLayoutPanel filterHexId;
        private System.Windows.Forms.Label lblHexId;
        private System.Windows.Forms.TextBox txtHexId;
        private System.Windows.Forms.TableLayoutPanel filterName;
        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.TextBox txtName;
        private System.Windows.Forms.TableLayoutPanel filterExpression;
        private System.Windows.Forms.Label lblExpression;
        private System.Windows.Forms.TextBox txtExpression;
        private System.Windows.Forms.TableLayoutPanel filterOmen;
        private System.Windows.Forms.Label lblOmen;
        private System.Windows.Forms.TextBox txtOmen;
        private System.Windows.Forms.TableLayoutPanel filterAnimationEnd;
        private System.Windows.Forms.Label lblAnimationEnd;
        private System.Windows.Forms.TextBox txtAnimationEnd;
        private System.Windows.Forms.CheckBox chkShape0;
        private System.Windows.Forms.CheckBox chkShape1;
        private System.Windows.Forms.CheckBox chkShape2;
        private System.Windows.Forms.CheckBox chkShape3;
        private System.Windows.Forms.CheckBox chkShape4;
        private System.Windows.Forms.CheckBox chkShape5;
        private System.Windows.Forms.CheckBox chkShape6;
        private System.Windows.Forms.CheckBox chkShape7;
        private System.Windows.Forms.CheckBox chkShape8;
        private System.Windows.Forms.CheckBox chkShape9;
        private System.Windows.Forms.CheckBox chkShape10;
        private System.Windows.Forms.CheckBox chkShape11;
        private System.Windows.Forms.CheckBox chkShape12;
        private System.Windows.Forms.CheckBox chkShape13;
        private System.Windows.Forms.CheckBox chkShape14;
        private System.Windows.Forms.CheckBox chkShape15;
        private System.Windows.Forms.CheckBox chkShape16;
        private System.Windows.Forms.CheckBox chkShape17;
        private System.Windows.Forms.DataGridView gridActions;
        private System.Windows.Forms.DataGridViewTextBoxColumn col0;
        private System.Windows.Forms.DataGridViewTextBoxColumn col1;
        private System.Windows.Forms.DataGridViewTextBoxColumn col2;
        private System.Windows.Forms.DataGridViewTextBoxColumn col3;
        private System.Windows.Forms.DataGridViewTextBoxColumn col4;
        private System.Windows.Forms.DataGridViewTextBoxColumn col5;
        private System.Windows.Forms.DataGridViewTextBoxColumn col6;
        private System.Windows.Forms.DataGridViewTextBoxColumn col7;
        private System.Windows.Forms.DataGridViewTextBoxColumn col8;
        private System.Windows.Forms.DataGridViewTextBoxColumn col9;
        private System.Windows.Forms.DataGridViewTextBoxColumn col10;
        private System.Windows.Forms.DataGridViewTextBoxColumn col11;
        private System.Windows.Forms.ContextMenuStrip contextActions;
        private System.Windows.Forms.ToolStripMenuItem miCopyCell;
        private System.Windows.Forms.ToolStripMenuItem miCopyTemplate;
        private System.Windows.Forms.ToolStripMenuItem miOmen;
        private System.Windows.Forms.ToolStripMenuItem miGimmick;
        private System.Windows.Forms.ToolStripSeparator menuSeparator;
        private System.Windows.Forms.ToolTip toolTip;
    }
}

