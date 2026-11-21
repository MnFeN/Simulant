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
            this.gridActions = new Simulant.UI.DataGridViewEx();
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
            this.contextActions = new Simulant.UI.ContextMenuStripEx(this.components);
            this.miCopyCell = new System.Windows.Forms.ToolStripMenuItem();
            this.miCopyTemplate = new System.Windows.Forms.ToolStripMenuItem();
            this.miOmen = new System.Windows.Forms.ToolStripMenuItem();
            this.miPreviewTimeline = new Simulant.UI.SplitMenuItem();
            this.layoutSidebar = new System.Windows.Forms.TableLayoutPanel();
            this.panelShapes = new System.Windows.Forms.FlowLayoutPanel();
            this.chkShapeAll = new Simulant.UI.CheckBoxEx();
            this.chkShape0 = new Simulant.UI.CheckBoxEx();
            this.chkShape1 = new Simulant.UI.CheckBoxEx();
            this.chkShape2 = new Simulant.UI.CheckBoxEx();
            this.chkShape3 = new Simulant.UI.CheckBoxEx();
            this.chkShape4 = new Simulant.UI.CheckBoxEx();
            this.chkShape5 = new Simulant.UI.CheckBoxEx();
            this.chkShape6 = new Simulant.UI.CheckBoxEx();
            this.chkShape7 = new Simulant.UI.CheckBoxEx();
            this.chkShape8 = new Simulant.UI.CheckBoxEx();
            this.chkShape9 = new Simulant.UI.CheckBoxEx();
            this.chkShape10 = new Simulant.UI.CheckBoxEx();
            this.chkShape11 = new Simulant.UI.CheckBoxEx();
            this.chkShape12 = new Simulant.UI.CheckBoxEx();
            this.chkShape13 = new Simulant.UI.CheckBoxEx();
            this.chkShape14 = new Simulant.UI.CheckBoxEx();
            this.chkShape15 = new Simulant.UI.CheckBoxEx();
            this.chkShape16 = new Simulant.UI.CheckBoxEx();
            this.chkShape17 = new Simulant.UI.CheckBoxEx();
            this.chkPreviewOnly = new Simulant.UI.CheckBoxEx();
            this.chkPlayerOnly = new Simulant.UI.CheckBoxEx();
            this.panelTop = new System.Windows.Forms.TableLayoutPanel();
            this.chkNewOnly = new Simulant.UI.CheckBoxEx();
            this.chkKeepOnTop = new Simulant.UI.CheckBoxEx();
            this.lblResult = new System.Windows.Forms.Label();
            this.panelButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnReset = new Simulant.UI.ButtonEx();
            this.btnExport = new Simulant.UI.ButtonEx();
            this.btnSearch = new Simulant.UI.ButtonEx();
            this.panelFilters = new System.Windows.Forms.TableLayoutPanel();
            this.filterHexId = new System.Windows.Forms.TableLayoutPanel();
            this.lblHexId = new System.Windows.Forms.Label();
            this.txtHexId = new Simulant.UI.TextBoxEx();
            this.filterName = new System.Windows.Forms.TableLayoutPanel();
            this.lblName = new System.Windows.Forms.Label();
            this.txtName = new Simulant.UI.TextBoxEx();
            this.filterExpression = new System.Windows.Forms.TableLayoutPanel();
            this.lblExpression = new System.Windows.Forms.Label();
            this.txtExpression = new Simulant.UI.TextBoxEx();
            this.filterOmen = new System.Windows.Forms.TableLayoutPanel();
            this.lblOmen = new System.Windows.Forms.Label();
            this.txtOmen = new Simulant.UI.TextBoxEx();
            this.filterAnimationEnd = new System.Windows.Forms.TableLayoutPanel();
            this.lblAnimationEnd = new System.Windows.Forms.Label();
            this.txtAnimationEnd = new Simulant.UI.TextBoxEx();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.layoutMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridActions)).BeginInit();
            this.contextActions.SuspendLayout();
            this.layoutSidebar.SuspendLayout();
            this.panelShapes.SuspendLayout();
            this.panelTop.SuspendLayout();
            this.panelButtons.SuspendLayout();
            this.panelFilters.SuspendLayout();
            this.filterHexId.SuspendLayout();
            this.filterName.SuspendLayout();
            this.filterExpression.SuspendLayout();
            this.filterOmen.SuspendLayout();
            this.filterAnimationEnd.SuspendLayout();
            this.SuspendLayout();
            // 
            // layoutMain
            // 
            this.layoutMain.ColumnCount = 2;
            this.layoutMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.layoutMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.layoutMain.Controls.Add(this.gridActions, 0, 0);
            this.layoutMain.Controls.Add(this.layoutSidebar, 1, 0);
            this.layoutMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.layoutMain.Location = new System.Drawing.Point(0, 103);
            this.layoutMain.Name = "layoutMain";
            this.layoutMain.RowCount = 1;
            this.layoutMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.layoutMain.Size = new System.Drawing.Size(1153, 606);
            this.layoutMain.TabIndex = 1;
            // 
            // gridActions
            // 
            this.gridActions.AllowUserToAddRows = false;
            this.gridActions.AllowUserToDeleteRows = false;
            this.gridActions.AllowUserToResizeRows = false;
            this.gridActions.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridActions.ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.Disable;
            this.gridActions.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridActions.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
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
            this.col11});
            this.gridActions.ContextMenuStrip = this.contextActions;
            this.gridActions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridActions.Location = new System.Drawing.Point(3, 3);
            this.gridActions.Name = "gridActions";
            this.gridActions.ReadOnly = true;
            this.gridActions.RowHeadersVisible = false;
            this.gridActions.RowHeadersWidth = 51;
            this.gridActions.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridActions.Size = new System.Drawing.Size(1016, 600);
            this.gridActions.TabIndex = 3;
            this.gridActions.VirtualMode = true;
            this.gridActions.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.gridActions_CellFormatting);
            this.gridActions.CellMouseDown += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.gridActions_CellMouseDown);
            this.gridActions.CellValueNeeded += new System.Windows.Forms.DataGridViewCellValueEventHandler(this.gridActions_CellValueNeeded);
            this.gridActions.ColumnHeaderMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.gridActions_ColumnHeaderMouseClick);
            // 
            // col0
            // 
            this.col0.FillWeight = 75F;
            this.col0.HeaderText = "ID";
            this.col0.MinimumWidth = 6;
            this.col0.Name = "col0";
            this.col0.ReadOnly = true;
            this.col0.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            // 
            // col1
            // 
            this.col1.FillWeight = 65F;
            this.col1.HeaderText = "Hex";
            this.col1.MinimumWidth = 6;
            this.col1.Name = "col1";
            this.col1.ReadOnly = true;
            this.col1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            // 
            // col2
            // 
            this.col2.FillWeight = 180F;
            this.col2.HeaderText = "技能名";
            this.col2.MinimumWidth = 6;
            this.col2.Name = "col2";
            this.col2.ReadOnly = true;
            this.col2.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            // 
            // col3
            // 
            this.col3.HeaderText = "咏唱";
            this.col3.MinimumWidth = 6;
            this.col3.Name = "col3";
            this.col3.ReadOnly = true;
            this.col3.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            // 
            // col4
            // 
            this.col4.FillWeight = 140F;
            this.col4.HeaderText = "形状";
            this.col4.MinimumWidth = 6;
            this.col4.Name = "col4";
            this.col4.ReadOnly = true;
            this.col4.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            // 
            // col5
            // 
            this.col5.FillWeight = 70F;
            this.col5.HeaderText = "X";
            this.col5.MinimumWidth = 6;
            this.col5.Name = "col5";
            this.col5.ReadOnly = true;
            this.col5.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            // 
            // col6
            // 
            this.col6.FillWeight = 70F;
            this.col6.HeaderText = "Y";
            this.col6.MinimumWidth = 6;
            this.col6.Name = "col6";
            this.col6.ReadOnly = true;
            this.col6.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            // 
            // col7
            // 
            this.col7.HeaderText = "分类";
            this.col7.MinimumWidth = 6;
            this.col7.Name = "col7";
            this.col7.ReadOnly = true;
            this.col7.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            // 
            // col8
            // 
            this.col8.HeaderText = "攻击";
            this.col8.MinimumWidth = 6;
            this.col8.Name = "col8";
            this.col8.ReadOnly = true;
            this.col8.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            // 
            // col9
            // 
            this.col9.FillWeight = 80F;
            this.col9.HeaderText = "属性";
            this.col9.MinimumWidth = 6;
            this.col9.Name = "col9";
            this.col9.ReadOnly = true;
            this.col9.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            // 
            // col10
            // 
            this.col10.FillWeight = 280F;
            this.col10.HeaderText = "预兆名称 (ID)";
            this.col10.MinimumWidth = 6;
            this.col10.Name = "col10";
            this.col10.ReadOnly = true;
            this.col10.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            // 
            // col11
            // 
            this.col11.FillWeight = 500F;
            this.col11.HeaderText = "结束时间轴名称 (ID)";
            this.col11.MinimumWidth = 6;
            this.col11.Name = "col11";
            this.col11.ReadOnly = true;
            this.col11.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            // 
            // contextActions
            // 
            this.contextActions.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.contextActions.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.contextActions.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miCopyCell,
            this.miCopyTemplate,
            this.miOmen,
            this.miPreviewTimeline});
            this.contextActions.Name = "contextActions";
            this.contextActions.Size = new System.Drawing.Size(448, 122);
            this.contextActions.Opening += new System.ComponentModel.CancelEventHandler(this.contextActions_Opening);
            // 
            // miCopyCell
            // 
            this.miCopyCell.Name = "miCopyCell";
            this.miCopyCell.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C)));
            this.miCopyCell.Size = new System.Drawing.Size(447, 28);
            this.miCopyCell.Text = "复制当前单元格";
            this.miCopyCell.Click += new System.EventHandler(this.miCopyCell_Click);
            // 
            // miCopyTemplate
            // 
            this.miCopyTemplate.Name = "miCopyTemplate";
            this.miCopyTemplate.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.C)));
            this.miCopyTemplate.Size = new System.Drawing.Size(447, 28);
            this.miCopyTemplate.Text = "将选中行复制为简易播报动作模板";
            this.miCopyTemplate.Click += new System.EventHandler(this.miCopyTemplate_Click);
            // 
            // miOmen
            // 
            this.miOmen.Name = "miOmen";
            this.miOmen.Size = new System.Drawing.Size(447, 28);
            this.miOmen.Text = "显示 Omen";
            this.miOmen.Click += new System.EventHandler(this.miOmen_Click);
            // 
            // miPreviewTimeline
            //
            this.miPreviewTimeline.Name = "miPreviewTimeline";
            this.miPreviewTimeline.Text = "预览结束时间轴特效";
            //
            // layoutSidebar
            //
            this.layoutSidebar.AutoSize = true;
            this.layoutSidebar.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.layoutSidebar.ColumnCount = 1;
            this.layoutSidebar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.layoutSidebar.Controls.Add(this.panelShapes, 0, 0);
            this.layoutSidebar.Controls.Add(this.chkPreviewOnly, 0, 1);
            this.layoutSidebar.Controls.Add(this.chkPlayerOnly, 0, 2);
            this.layoutSidebar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.layoutSidebar.Margin = System.Windows.Forms.Padding.Empty;
            this.layoutSidebar.Name = "layoutSidebar";
            this.layoutSidebar.RowCount = 3;
            this.layoutSidebar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.layoutSidebar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.layoutSidebar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.layoutSidebar.TabIndex = 1;
            //
            // chkPreviewOnly
            //
            this.chkPreviewOnly.AutoSize = true;
            this.chkPreviewOnly.Margin = new System.Windows.Forms.Padding(3, 8, 3, 3);
            this.chkPreviewOnly.Name = "chkPreviewOnly";
            this.chkPreviewOnly.Style = Simulant.UI.CheckBoxStyle.Switch;
            this.chkPreviewOnly.TabIndex = 1;
            this.chkPreviewOnly.Text = "可预览动画";
            this.chkPreviewOnly.CheckedChanged += new System.EventHandler(this.filterSwitch_CheckedChanged);
            this.toolTip.SetToolTip(this.chkPreviewOnly, "仅显示可以播放实体动画或独立特效的结束时间轴。");
            //
            // chkPlayerOnly
            //
            this.chkPlayerOnly.AutoSize = true;
            this.chkPlayerOnly.Name = "chkPlayerOnly";
            this.chkPlayerOnly.Style = Simulant.UI.CheckBoxStyle.Switch;
            this.chkPlayerOnly.TabIndex = 2;
            this.chkPlayerOnly.Text = "玩家技能";
            this.chkPlayerOnly.CheckedChanged += new System.EventHandler(this.filterSwitch_CheckedChanged);
            this.toolTip.SetToolTip(this.chkPlayerOnly, "仅显示玩家技能。");
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
            this.panelShapes.Location = new System.Drawing.Point(3, 3);
            this.panelShapes.Name = "panelShapes";
            this.panelShapes.Size = new System.Drawing.Size(125, 600);
            this.panelShapes.TabIndex = 0;
            this.panelShapes.WrapContents = false;
            this.panelShapes.Layout += new System.Windows.Forms.LayoutEventHandler(this.panelShapes_Layout);
            // 
            // chkShapeAll
            // 
            this.chkShapeAll.Accent = System.Drawing.Color.Empty;
            this.chkShapeAll.AutoSize = true;
            this.chkShapeAll.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShapeAll.Location = new System.Drawing.Point(3, 3);
            this.chkShapeAll.Name = "chkShapeAll";
            this.chkShapeAll.Size = new System.Drawing.Size(57, 24);
            this.chkShapeAll.TabIndex = 0;
            this.chkShapeAll.Text = "全选";
            this.chkShapeAll.UseVisualStyleBackColor = false;
            this.chkShapeAll.CheckedChanged += new System.EventHandler(this.chkShapeAll_CheckedChanged);
            // 
            // chkShape0
            // 
            this.chkShape0.Accent = System.Drawing.Color.Empty;
            this.chkShape0.AutoSize = true;
            this.chkShape0.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShape0.Location = new System.Drawing.Point(3, 33);
            this.chkShape0.Name = "chkShape0";
            this.chkShape0.Size = new System.Drawing.Size(65, 24);
            this.chkShape0.TabIndex = 1;
            this.chkShape0.Text = "无 (0)";
            this.chkShape0.UseVisualStyleBackColor = false;
            this.chkShape0.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            // 
            // chkShape1
            // 
            this.chkShape1.Accent = System.Drawing.Color.Empty;
            this.chkShape1.AutoSize = true;
            this.chkShape1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShape1.Location = new System.Drawing.Point(3, 63);
            this.chkShape1.Name = "chkShape1";
            this.chkShape1.Size = new System.Drawing.Size(80, 24);
            this.chkShape1.TabIndex = 2;
            this.chkShape1.Text = "单体 (1)";
            this.chkShape1.UseVisualStyleBackColor = false;
            this.chkShape1.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            // 
            // chkShape2
            // 
            this.chkShape2.Accent = System.Drawing.Color.Empty;
            this.chkShape2.AutoSize = true;
            this.chkShape2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShape2.Location = new System.Drawing.Point(3, 93);
            this.chkShape2.Name = "chkShape2";
            this.chkShape2.Size = new System.Drawing.Size(80, 24);
            this.chkShape2.TabIndex = 3;
            this.chkShape2.Text = "圆形 (2)";
            this.chkShape2.UseVisualStyleBackColor = false;
            this.chkShape2.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            // 
            // chkShape3
            // 
            this.chkShape3.Accent = System.Drawing.Color.Empty;
            this.chkShape3.AutoSize = true;
            this.chkShape3.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShape3.Location = new System.Drawing.Point(3, 123);
            this.chkShape3.Name = "chkShape3";
            this.chkShape3.Size = new System.Drawing.Size(91, 24);
            this.chkShape3.TabIndex = 4;
            this.chkShape3.Text = "扇形+ (3)";
            this.chkShape3.UseVisualStyleBackColor = false;
            this.chkShape3.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            // 
            // chkShape4
            // 
            this.chkShape4.Accent = System.Drawing.Color.Empty;
            this.chkShape4.AutoSize = true;
            this.chkShape4.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShape4.Location = new System.Drawing.Point(3, 153);
            this.chkShape4.Name = "chkShape4";
            this.chkShape4.Size = new System.Drawing.Size(91, 24);
            this.chkShape4.TabIndex = 5;
            this.chkShape4.Text = "矩形+ (4)";
            this.chkShape4.UseVisualStyleBackColor = false;
            this.chkShape4.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            // 
            // chkShape5
            // 
            this.chkShape5.Accent = System.Drawing.Color.Empty;
            this.chkShape5.AutoSize = true;
            this.chkShape5.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShape5.Location = new System.Drawing.Point(3, 183);
            this.chkShape5.Name = "chkShape5";
            this.chkShape5.Size = new System.Drawing.Size(91, 24);
            this.chkShape5.TabIndex = 6;
            this.chkShape5.Text = "圆形+ (5)";
            this.chkShape5.UseVisualStyleBackColor = false;
            this.chkShape5.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            // 
            // chkShape6
            // 
            this.chkShape6.Accent = System.Drawing.Color.Empty;
            this.chkShape6.AutoSize = true;
            this.chkShape6.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShape6.Location = new System.Drawing.Point(3, 213);
            this.chkShape6.Name = "chkShape6";
            this.chkShape6.Size = new System.Drawing.Size(80, 24);
            this.chkShape6.TabIndex = 7;
            this.chkShape6.Text = "未知 (6)";
            this.chkShape6.UseVisualStyleBackColor = false;
            this.chkShape6.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            // 
            // chkShape7
            // 
            this.chkShape7.Accent = System.Drawing.Color.Empty;
            this.chkShape7.AutoSize = true;
            this.chkShape7.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShape7.Location = new System.Drawing.Point(3, 243);
            this.chkShape7.Name = "chkShape7";
            this.chkShape7.Size = new System.Drawing.Size(80, 24);
            this.chkShape7.TabIndex = 8;
            this.chkShape7.Text = "未知 (7)";
            this.chkShape7.UseVisualStyleBackColor = false;
            this.chkShape7.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            // 
            // chkShape8
            // 
            this.chkShape8.Accent = System.Drawing.Color.Empty;
            this.chkShape8.AutoSize = true;
            this.chkShape8.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShape8.Location = new System.Drawing.Point(3, 273);
            this.chkShape8.Name = "chkShape8";
            this.chkShape8.Size = new System.Drawing.Size(95, 24);
            this.chkShape8.TabIndex = 9;
            this.chkShape8.Text = "矩形至 (8)";
            this.chkShape8.UseVisualStyleBackColor = false;
            this.chkShape8.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            // 
            // chkShape9
            // 
            this.chkShape9.Accent = System.Drawing.Color.Empty;
            this.chkShape9.AutoSize = true;
            this.chkShape9.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShape9.Location = new System.Drawing.Point(3, 303);
            this.chkShape9.Name = "chkShape9";
            this.chkShape9.Size = new System.Drawing.Size(80, 24);
            this.chkShape9.TabIndex = 10;
            this.chkShape9.Text = "未知 (9)";
            this.chkShape9.UseVisualStyleBackColor = false;
            this.chkShape9.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            // 
            // chkShape10
            // 
            this.chkShape10.Accent = System.Drawing.Color.Empty;
            this.chkShape10.AutoSize = true;
            this.chkShape10.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShape10.Location = new System.Drawing.Point(3, 333);
            this.chkShape10.Name = "chkShape10";
            this.chkShape10.Size = new System.Drawing.Size(89, 24);
            this.chkShape10.TabIndex = 11;
            this.chkShape10.Text = "月环 (10)";
            this.chkShape10.UseVisualStyleBackColor = false;
            this.chkShape10.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            // 
            // chkShape11
            // 
            this.chkShape11.Accent = System.Drawing.Color.Empty;
            this.chkShape11.AutoSize = true;
            this.chkShape11.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShape11.Location = new System.Drawing.Point(3, 363);
            this.chkShape11.Name = "chkShape11";
            this.chkShape11.Size = new System.Drawing.Size(89, 24);
            this.chkShape11.TabIndex = 12;
            this.chkShape11.Text = "十字 (11)";
            this.chkShape11.UseVisualStyleBackColor = false;
            this.chkShape11.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            // 
            // chkShape12
            // 
            this.chkShape12.Accent = System.Drawing.Color.Empty;
            this.chkShape12.AutoSize = true;
            this.chkShape12.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShape12.Location = new System.Drawing.Point(3, 393);
            this.chkShape12.Name = "chkShape12";
            this.chkShape12.Size = new System.Drawing.Size(89, 24);
            this.chkShape12.TabIndex = 13;
            this.chkShape12.Text = "矩形 (12)";
            this.chkShape12.UseVisualStyleBackColor = false;
            this.chkShape12.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            // 
            // chkShape13
            // 
            this.chkShape13.Accent = System.Drawing.Color.Empty;
            this.chkShape13.AutoSize = true;
            this.chkShape13.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShape13.Location = new System.Drawing.Point(3, 423);
            this.chkShape13.Name = "chkShape13";
            this.chkShape13.Size = new System.Drawing.Size(89, 24);
            this.chkShape13.TabIndex = 14;
            this.chkShape13.Text = "扇形 (13)";
            this.chkShape13.UseVisualStyleBackColor = false;
            this.chkShape13.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            // 
            // chkShape14
            // 
            this.chkShape14.Accent = System.Drawing.Color.Empty;
            this.chkShape14.AutoSize = true;
            this.chkShape14.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShape14.Location = new System.Drawing.Point(3, 453);
            this.chkShape14.Name = "chkShape14";
            this.chkShape14.Size = new System.Drawing.Size(89, 24);
            this.chkShape14.TabIndex = 15;
            this.chkShape14.Text = "三角 (14)";
            this.chkShape14.UseVisualStyleBackColor = false;
            this.chkShape14.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            // 
            // chkShape15
            // 
            this.chkShape15.Accent = System.Drawing.Color.Empty;
            this.chkShape15.AutoSize = true;
            this.chkShape15.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShape15.Location = new System.Drawing.Point(3, 483);
            this.chkShape15.Name = "chkShape15";
            this.chkShape15.Size = new System.Drawing.Size(104, 24);
            this.chkShape15.TabIndex = 16;
            this.chkShape15.Text = "矩形穿 (15)";
            this.chkShape15.UseVisualStyleBackColor = false;
            this.chkShape15.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            // 
            // chkShape16
            // 
            this.chkShape16.Accent = System.Drawing.Color.Empty;
            this.chkShape16.AutoSize = true;
            this.chkShape16.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShape16.Location = new System.Drawing.Point(3, 513);
            this.chkShape16.Name = "chkShape16";
            this.chkShape16.Size = new System.Drawing.Size(119, 24);
            this.chkShape16.TabIndex = 17;
            this.chkShape16.Text = "右矩形至 (16)";
            this.chkShape16.UseVisualStyleBackColor = false;
            this.chkShape16.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            // 
            // chkShape17
            // 
            this.chkShape17.Accent = System.Drawing.Color.Empty;
            this.chkShape17.AutoSize = true;
            this.chkShape17.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkShape17.Location = new System.Drawing.Point(3, 543);
            this.chkShape17.Name = "chkShape17";
            this.chkShape17.Size = new System.Drawing.Size(119, 24);
            this.chkShape17.TabIndex = 18;
            this.chkShape17.Text = "左矩形至 (17)";
            this.chkShape17.UseVisualStyleBackColor = false;
            this.chkShape17.CheckedChanged += new System.EventHandler(this.Shape_CheckedChanged);
            // 
            // panelTop
            // 
            this.panelTop.AutoSize = true;
            this.panelTop.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panelTop.ColumnCount = 4;
            this.panelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.panelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.panelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.panelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 130F));
            this.panelTop.Controls.Add(this.chkNewOnly, 0, 0);
            this.panelTop.Controls.Add(this.lblResult, 1, 0);
            this.panelTop.Controls.Add(this.panelButtons, 2, 0);
            this.panelTop.Controls.Add(this.chkKeepOnTop, 3, 0);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.RowCount = 1;
            this.panelTop.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.panelTop.Size = new System.Drawing.Size(1153, 44);
            this.panelTop.TabIndex = 0;
            // 
            // chkNewOnly
            // 
            this.chkNewOnly.Accent = System.Drawing.Color.Empty;
            this.chkNewOnly.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.chkNewOnly.AutoSize = true;
            this.chkNewOnly.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkNewOnly.Location = new System.Drawing.Point(3, 10);
            this.chkNewOnly.Name = "chkNewOnly";
            this.chkNewOnly.Size = new System.Drawing.Size(138, 24);
            this.chkNewOnly.TabIndex = 0;
            this.chkNewOnly.Text = "仅显示新增项 (?)";
            this.toolTip.SetToolTip(this.chkNewOnly, "仅显示在最新版本中新增的技能。当前暂不比较旧版本，所有技能均视为非新增。");
            this.chkNewOnly.UseVisualStyleBackColor = false;
            // 
            // chkKeepOnTop
            //
            this.chkKeepOnTop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chkKeepOnTop.Location = new System.Drawing.Point(1023, 0);
            this.chkKeepOnTop.Margin = System.Windows.Forms.Padding.Empty;
            this.chkKeepOnTop.Name = "chkKeepOnTop";
            this.chkKeepOnTop.Size = new System.Drawing.Size(130, 44);
            this.chkKeepOnTop.Style = Simulant.UI.CheckBoxStyle.Switch;
            this.chkKeepOnTop.TabIndex = 2;
            this.chkKeepOnTop.Text = "窗口置顶";
            //
            // lblResult
            // 
            this.lblResult.AutoSize = true;
            this.lblResult.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblResult.Location = new System.Drawing.Point(147, 0);
            this.lblResult.Name = "lblResult";
            this.lblResult.Size = new System.Drawing.Size(651, 44);
            this.lblResult.TabIndex = 3;
            this.lblResult.Text = "正在准备……";
            this.lblResult.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
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
            this.panelButtons.Location = new System.Drawing.Point(804, 3);
            this.panelButtons.Name = "panelButtons";
            this.panelButtons.Size = new System.Drawing.Size(216, 38);
            this.panelButtons.TabIndex = 1;
            this.panelButtons.WrapContents = false;
            // 
            // btnReset
            // 
            this.btnReset.AutoSize = true;
            this.btnReset.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnReset.Enabled = false;
            this.btnReset.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnReset.Location = new System.Drawing.Point(162, 3);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(51, 32);
            this.btnReset.TabIndex = 0;
            this.btnReset.Text = "重置";
            this.btnReset.UseVisualStyleBackColor = false;
            this.btnReset.Click += new System.EventHandler(this.btnReset_Click);
            // 
            // btnExport
            // 
            this.btnExport.AutoSize = true;
            this.btnExport.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnExport.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExport.Location = new System.Drawing.Point(60, 3);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(96, 32);
            this.btnExport.TabIndex = 1;
            this.btnExport.Text = "复制所选行";
            this.btnExport.UseVisualStyleBackColor = false;
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // btnSearch
            // 
            this.btnSearch.AutoSize = true;
            this.btnSearch.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnSearch.Enabled = false;
            this.btnSearch.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSearch.Location = new System.Drawing.Point(3, 3);
            this.btnSearch.Name = "btnSearch";
            this.btnSearch.Size = new System.Drawing.Size(51, 32);
            this.btnSearch.TabIndex = 2;
            this.btnSearch.Text = "搜索";
            this.btnSearch.UseVisualStyleBackColor = false;
            this.btnSearch.Click += new System.EventHandler(this.btnSearch_Click);
            // 
            // panelFilters
            // 
            this.panelFilters.AutoSize = true;
            this.panelFilters.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panelFilters.ColumnCount = 5;
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
            this.panelFilters.Location = new System.Drawing.Point(0, 44);
            this.panelFilters.Name = "panelFilters";
            this.panelFilters.RowCount = 1;
            this.panelFilters.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.panelFilters.Size = new System.Drawing.Size(1153, 59);
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
            this.filterHexId.Location = new System.Drawing.Point(3, 3);
            this.filterHexId.Name = "filterHexId";
            this.filterHexId.RowCount = 2;
            this.filterHexId.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.filterHexId.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.filterHexId.Size = new System.Drawing.Size(166, 53);
            this.filterHexId.TabIndex = 0;
            // 
            // lblHexId
            // 
            this.lblHexId.AutoSize = true;
            this.lblHexId.Location = new System.Drawing.Point(3, 0);
            this.lblHexId.Name = "lblHexId";
            this.lblHexId.Size = new System.Drawing.Size(53, 20);
            this.lblHexId.TabIndex = 0;
            this.lblHexId.Text = "HexID";
            this.toolTip.SetToolTip(this.lblHexId, "使用正则表达式过滤技能 HexID，大小写不敏感。");
            // 
            // txtHexId
            // 
            this.txtHexId.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtHexId.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtHexId.Location = new System.Drawing.Point(3, 23);
            this.txtHexId.Name = "txtHexId";
            this.txtHexId.Size = new System.Drawing.Size(160, 27);
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
            this.filterName.Location = new System.Drawing.Point(175, 3);
            this.filterName.Name = "filterName";
            this.filterName.RowCount = 2;
            this.filterName.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.filterName.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.filterName.Size = new System.Drawing.Size(224, 53);
            this.filterName.TabIndex = 1;
            // 
            // lblName
            // 
            this.lblName.AutoSize = true;
            this.lblName.Location = new System.Drawing.Point(3, 0);
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(54, 20);
            this.lblName.TabIndex = 0;
            this.lblName.Text = "技能名";
            this.toolTip.SetToolTip(this.lblName, "使用正则表达式过滤技能名称，大小写不敏感。");
            // 
            // txtName
            // 
            this.txtName.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtName.Location = new System.Drawing.Point(3, 23);
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(218, 27);
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
            this.filterExpression.Location = new System.Drawing.Point(405, 3);
            this.filterExpression.Name = "filterExpression";
            this.filterExpression.RowCount = 2;
            this.filterExpression.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.filterExpression.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.filterExpression.Size = new System.Drawing.Size(339, 53);
            this.filterExpression.TabIndex = 2;
            // 
            // lblExpression
            // 
            this.lblExpression.AutoSize = true;
            this.lblExpression.Location = new System.Drawing.Point(3, 0);
            this.lblExpression.Name = "lblExpression";
            this.lblExpression.Size = new System.Drawing.Size(84, 20);
            this.lblExpression.TabIndex = 0;
            this.lblExpression.Text = "过滤表达式";
            this.toolTip.SetToolTip(this.lblExpression, resources.GetString("lblExpression.ToolTip"));
            // 
            // txtExpression
            // 
            this.txtExpression.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtExpression.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtExpression.Location = new System.Drawing.Point(3, 23);
            this.txtExpression.Name = "txtExpression";
            this.txtExpression.Size = new System.Drawing.Size(333, 27);
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
            this.filterOmen.Location = new System.Drawing.Point(750, 3);
            this.filterOmen.Name = "filterOmen";
            this.filterOmen.RowCount = 2;
            this.filterOmen.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.filterOmen.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.filterOmen.Size = new System.Drawing.Size(195, 53);
            this.filterOmen.TabIndex = 3;
            // 
            // lblOmen
            // 
            this.lblOmen.AutoSize = true;
            this.lblOmen.Location = new System.Drawing.Point(3, 0);
            this.lblOmen.Name = "lblOmen";
            this.lblOmen.Size = new System.Drawing.Size(97, 20);
            this.lblOmen.TabIndex = 0;
            this.lblOmen.Text = "预兆 (Omen)";
            this.toolTip.SetToolTip(this.lblOmen, "使用正则表达式过滤预兆名称。");
            // 
            // txtOmen
            // 
            this.txtOmen.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtOmen.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtOmen.Location = new System.Drawing.Point(3, 23);
            this.txtOmen.Name = "txtOmen";
            this.txtOmen.Size = new System.Drawing.Size(189, 27);
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
            this.filterAnimationEnd.Location = new System.Drawing.Point(951, 3);
            this.filterAnimationEnd.Name = "filterAnimationEnd";
            this.filterAnimationEnd.RowCount = 2;
            this.filterAnimationEnd.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.filterAnimationEnd.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.filterAnimationEnd.Size = new System.Drawing.Size(199, 53);
            this.filterAnimationEnd.TabIndex = 4;
            // 
            // lblAnimationEnd
            // 
            this.lblAnimationEnd.AutoSize = true;
            this.lblAnimationEnd.Location = new System.Drawing.Point(3, 0);
            this.lblAnimationEnd.Name = "lblAnimationEnd";
            this.lblAnimationEnd.Size = new System.Drawing.Size(84, 20);
            this.lblAnimationEnd.TabIndex = 0;
            this.lblAnimationEnd.Text = "结束时间轴";
            this.toolTip.SetToolTip(this.lblAnimationEnd, "使用正则表达式过滤结束时间轴名称。");
            // 
            // txtAnimationEnd
            // 
            this.txtAnimationEnd.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtAnimationEnd.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtAnimationEnd.Location = new System.Drawing.Point(3, 23);
            this.txtAnimationEnd.Name = "txtAnimationEnd";
            this.txtAnimationEnd.Size = new System.Drawing.Size(193, 27);
            this.txtAnimationEnd.TabIndex = 1;
            this.toolTip.SetToolTip(this.txtAnimationEnd, "使用正则表达式过滤结束时间轴名称。");
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
            this.ClientSize = new System.Drawing.Size(1153, 709);
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
            ((System.ComponentModel.ISupportInitialize)(this.gridActions)).EndInit();
            this.contextActions.ResumeLayout(false);
            this.panelShapes.ResumeLayout(false);
            this.panelShapes.PerformLayout();
            this.layoutSidebar.ResumeLayout(false);
            this.layoutSidebar.PerformLayout();
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            this.panelButtons.ResumeLayout(false);
            this.panelButtons.PerformLayout();
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
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TableLayoutPanel layoutMain;
        private System.Windows.Forms.TableLayoutPanel panelTop;
        private System.Windows.Forms.FlowLayoutPanel panelButtons;
        private System.Windows.Forms.TableLayoutPanel layoutSidebar;
        private System.Windows.Forms.FlowLayoutPanel panelShapes;
        private System.Windows.Forms.TableLayoutPanel panelFilters;
        private CheckBoxEx chkNewOnly;
        private CheckBoxEx chkKeepOnTop;
        private CheckBoxEx chkShapeAll;
        private CheckBoxEx chkPreviewOnly;
        private CheckBoxEx chkPlayerOnly;
        private System.Windows.Forms.Label lblResult;
        private ButtonEx btnSearch;
        private ButtonEx btnExport;
        private ButtonEx btnReset;
        private System.Windows.Forms.TableLayoutPanel filterHexId;
        private System.Windows.Forms.Label lblHexId;
        private TextBoxEx txtHexId;
        private System.Windows.Forms.TableLayoutPanel filterName;
        private System.Windows.Forms.Label lblName;
        private TextBoxEx txtName;
        private System.Windows.Forms.TableLayoutPanel filterExpression;
        private System.Windows.Forms.Label lblExpression;
        private TextBoxEx txtExpression;
        private System.Windows.Forms.TableLayoutPanel filterOmen;
        private System.Windows.Forms.Label lblOmen;
        private TextBoxEx txtOmen;
        private System.Windows.Forms.TableLayoutPanel filterAnimationEnd;
        private System.Windows.Forms.Label lblAnimationEnd;
        private TextBoxEx txtAnimationEnd;
        private CheckBoxEx chkShape0;
        private CheckBoxEx chkShape1;
        private CheckBoxEx chkShape2;
        private CheckBoxEx chkShape3;
        private CheckBoxEx chkShape4;
        private CheckBoxEx chkShape5;
        private CheckBoxEx chkShape6;
        private CheckBoxEx chkShape7;
        private CheckBoxEx chkShape8;
        private CheckBoxEx chkShape9;
        private CheckBoxEx chkShape10;
        private CheckBoxEx chkShape11;
        private CheckBoxEx chkShape12;
        private CheckBoxEx chkShape13;
        private CheckBoxEx chkShape14;
        private CheckBoxEx chkShape15;
        private CheckBoxEx chkShape16;
        private CheckBoxEx chkShape17;
        private DataGridViewEx gridActions;
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
        private ContextMenuStripEx contextActions;
        private System.Windows.Forms.ToolStripMenuItem miCopyCell;
        private System.Windows.Forms.ToolStripMenuItem miCopyTemplate;
        private System.Windows.Forms.ToolStripMenuItem miOmen;
        private SplitMenuItem miPreviewTimeline;
        private System.Windows.Forms.ToolTip toolTip;
    }
}

