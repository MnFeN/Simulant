namespace Simulant.UI
{
    partial class SimulantUI
    {
        /// <summary> 
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SimulantUI));
            this.splitMainV = new System.Windows.Forms.SplitContainer();
            this.tableL = new System.Windows.Forms.TableLayoutPanel();
            this.btnDebug = new System.Windows.Forms.Button();
            this.btnInitPlugin = new System.Windows.Forms.Button();
            this.btnSelectTerritory = new System.Windows.Forms.Button();
            this.btnSimEnter = new System.Windows.Forms.Button();
            this.btnSimExit = new System.Windows.Forms.Button();
            this.lblTerritory = new System.Windows.Forms.Label();
            this.picLogo = new System.Windows.Forms.PictureBox();
            this.cbxPhase = new System.Windows.Forms.ComboBox();
            this.chkToggleFirewall = new System.Windows.Forms.CheckBox();
            this.lblTerritoryId = new System.Windows.Forms.Label();
            this.numTerritoryId = new System.Windows.Forms.NumericUpDown();
            this.splitMainH = new System.Windows.Forms.SplitContainer();
            this.presetControl = new Simulant.UI.PresetControl();
            this.logView = new Simulant.UI.LogView();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            this.btnAddBNpc = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.splitMainV)).BeginInit();
            this.splitMainV.Panel1.SuspendLayout();
            this.splitMainV.Panel2.SuspendLayout();
            this.splitMainV.SuspendLayout();
            this.tableL.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTerritoryId)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitMainH)).BeginInit();
            this.splitMainH.Panel1.SuspendLayout();
            this.splitMainH.Panel2.SuspendLayout();
            this.splitMainH.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitMainV
            // 
            this.splitMainV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMainV.Font = new System.Drawing.Font("等距更纱黑体 SC", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.splitMainV.Location = new System.Drawing.Point(0, 0);
            this.splitMainV.Name = "splitMainV";
            // 
            // splitMainV.Panel1
            // 
            this.splitMainV.Panel1.Controls.Add(this.tableL);
            // 
            // splitMainV.Panel2
            // 
            this.splitMainV.Panel2.Controls.Add(this.splitMainH);
            this.splitMainV.Size = new System.Drawing.Size(1209, 892);
            this.splitMainV.SplitterDistance = 340;
            this.splitMainV.TabIndex = 0;
            // 
            // tableL
            // 
            this.tableL.AutoSize = true;
            this.tableL.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableL.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.tableL.ColumnCount = 3;
            this.tableL.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableL.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 22.19101F));
            this.tableL.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 27.80899F));
            this.tableL.Controls.Add(this.btnAddBNpc, 0, 6);
            this.tableL.Controls.Add(this.btnDebug, 1, 6);
            this.tableL.Controls.Add(this.btnInitPlugin, 0, 1);
            this.tableL.Controls.Add(this.btnSelectTerritory, 0, 2);
            this.tableL.Controls.Add(this.btnSimEnter, 0, 5);
            this.tableL.Controls.Add(this.btnSimExit, 1, 5);
            this.tableL.Controls.Add(this.lblTerritory, 0, 3);
            this.tableL.Controls.Add(this.picLogo, 0, 0);
            this.tableL.Controls.Add(this.cbxPhase, 0, 4);
            this.tableL.Controls.Add(this.chkToggleFirewall, 1, 1);
            this.tableL.Controls.Add(this.lblTerritoryId, 1, 2);
            this.tableL.Controls.Add(this.numTerritoryId, 2, 2);
            this.tableL.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableL.Location = new System.Drawing.Point(0, 0);
            this.tableL.Name = "tableL";
            this.tableL.RowCount = 9;
            this.tableL.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 156F));
            this.tableL.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableL.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableL.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableL.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableL.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableL.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableL.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableL.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableL.Size = new System.Drawing.Size(340, 892);
            this.tableL.TabIndex = 0;
            this.tableL.TabStop = true;
            // 
            // btnDebug
            // 
            this.btnDebug.AutoSize = true;
            this.btnDebug.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableL.SetColumnSpan(this.btnDebug, 2);
            this.btnDebug.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnDebug.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnDebug.Location = new System.Drawing.Point(185, 476);
            this.btnDebug.Margin = new System.Windows.Forms.Padding(15, 10, 15, 10);
            this.btnDebug.Name = "btnDebug";
            this.btnDebug.Padding = new System.Windows.Forms.Padding(5);
            this.btnDebug.Size = new System.Drawing.Size(140, 42);
            this.btnDebug.TabIndex = 6;
            this.btnDebug.TabStop = false;
            this.btnDebug.Text = "调试";
            this.btnDebug.UseVisualStyleBackColor = true;
            this.btnDebug.Click += new System.EventHandler(this.btnDebug_Click);
            // 
            // btnInitPlugin
            // 
            this.btnInitPlugin.AutoSize = true;
            this.btnInitPlugin.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnInitPlugin.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnInitPlugin.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnInitPlugin.Location = new System.Drawing.Point(15, 166);
            this.btnInitPlugin.Margin = new System.Windows.Forms.Padding(15, 10, 15, 10);
            this.btnInitPlugin.Name = "btnInitPlugin";
            this.btnInitPlugin.Padding = new System.Windows.Forms.Padding(5);
            this.btnInitPlugin.Size = new System.Drawing.Size(140, 42);
            this.btnInitPlugin.TabIndex = 0;
            this.btnInitPlugin.TabStop = false;
            this.btnInitPlugin.Text = "初始化插件";
            this.btnInitPlugin.UseVisualStyleBackColor = true;
            this.btnInitPlugin.Click += new System.EventHandler(this.btnInitPlugin_Click);
            // 
            // btnSelectTerritory
            // 
            this.btnSelectTerritory.AutoSize = true;
            this.btnSelectTerritory.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnSelectTerritory.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnSelectTerritory.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnSelectTerritory.Location = new System.Drawing.Point(15, 228);
            this.btnSelectTerritory.Margin = new System.Windows.Forms.Padding(15, 10, 15, 10);
            this.btnSelectTerritory.Name = "btnSelectTerritory";
            this.btnSelectTerritory.Padding = new System.Windows.Forms.Padding(5);
            this.btnSelectTerritory.Size = new System.Drawing.Size(140, 42);
            this.btnSelectTerritory.TabIndex = 0;
            this.btnSelectTerritory.TabStop = false;
            this.btnSelectTerritory.Text = "选择地图";
            this.btnSelectTerritory.UseVisualStyleBackColor = true;
            this.btnSelectTerritory.Click += new System.EventHandler(this.btnSelectTerritory_Click);
            // 
            // btnSimEnter
            // 
            this.btnSimEnter.AutoSize = true;
            this.btnSimEnter.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnSimEnter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnSimEnter.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnSimEnter.Location = new System.Drawing.Point(15, 414);
            this.btnSimEnter.Margin = new System.Windows.Forms.Padding(15, 10, 15, 10);
            this.btnSimEnter.Name = "btnSimEnter";
            this.btnSimEnter.Padding = new System.Windows.Forms.Padding(5);
            this.btnSimEnter.Size = new System.Drawing.Size(140, 42);
            this.btnSimEnter.TabIndex = 0;
            this.btnSimEnter.TabStop = false;
            this.btnSimEnter.Text = "加载区域";
            this.btnSimEnter.UseVisualStyleBackColor = true;
            this.btnSimEnter.Click += new System.EventHandler(this.btnSimEnter_Click);
            // 
            // btnSimExit
            // 
            this.btnSimExit.AutoSize = true;
            this.btnSimExit.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableL.SetColumnSpan(this.btnSimExit, 2);
            this.btnSimExit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnSimExit.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnSimExit.Location = new System.Drawing.Point(185, 414);
            this.btnSimExit.Margin = new System.Windows.Forms.Padding(15, 10, 15, 10);
            this.btnSimExit.Name = "btnSimExit";
            this.btnSimExit.Padding = new System.Windows.Forms.Padding(5);
            this.btnSimExit.Size = new System.Drawing.Size(140, 42);
            this.btnSimExit.TabIndex = 0;
            this.btnSimExit.TabStop = false;
            this.btnSimExit.Text = "退出模拟";
            this.btnSimExit.UseVisualStyleBackColor = true;
            this.btnSimExit.Click += new System.EventHandler(this.btnSimExit_Click);
            // 
            // lblTerritory
            // 
            this.lblTerritory.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblTerritory.AutoSize = true;
            this.tableL.SetColumnSpan(this.lblTerritory, 3);
            this.lblTerritory.Location = new System.Drawing.Point(15, 290);
            this.lblTerritory.Margin = new System.Windows.Forms.Padding(15, 10, 15, 10);
            this.lblTerritory.Name = "lblTerritory";
            this.lblTerritory.Size = new System.Drawing.Size(106, 44);
            this.lblTerritory.TabIndex = 0;
            this.lblTerritory.Text = "选中区域：无\r\n选中副本：无";
            // 
            // picLogo
            // 
            this.tableL.SetColumnSpan(this.picLogo, 3);
            this.picLogo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picLogo.Image = ((System.Drawing.Image)(resources.GetObject("picLogo.Image")));
            this.picLogo.Location = new System.Drawing.Point(15, 15);
            this.picLogo.Margin = new System.Windows.Forms.Padding(15);
            this.picLogo.Name = "picLogo";
            this.picLogo.Size = new System.Drawing.Size(310, 126);
            this.picLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picLogo.TabIndex = 2;
            this.picLogo.TabStop = false;
            // 
            // cbxPhase
            // 
            this.tableL.SetColumnSpan(this.cbxPhase, 3);
            this.cbxPhase.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cbxPhase.DropDownHeight = 500;
            this.cbxPhase.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbxPhase.FormattingEnabled = true;
            this.cbxPhase.IntegralHeight = false;
            this.cbxPhase.Location = new System.Drawing.Point(15, 359);
            this.cbxPhase.Margin = new System.Windows.Forms.Padding(15);
            this.cbxPhase.MaxDropDownItems = 15;
            this.cbxPhase.Name = "cbxPhase";
            this.cbxPhase.Size = new System.Drawing.Size(310, 30);
            this.cbxPhase.TabIndex = 8;
            this.cbxPhase.SelectedIndexChanged += new System.EventHandler(this.cbxPhase_SelectedIndexChanged);
            // 
            // chkToggleFirewall
            // 
            this.chkToggleFirewall.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.chkToggleFirewall.AutoSize = true;
            this.chkToggleFirewall.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.tableL.SetColumnSpan(this.chkToggleFirewall, 2);
            this.chkToggleFirewall.Location = new System.Drawing.Point(173, 169);
            this.chkToggleFirewall.Name = "chkToggleFirewall";
            this.chkToggleFirewall.Padding = new System.Windows.Forms.Padding(0, 5, 5, 5);
            this.chkToggleFirewall.Size = new System.Drawing.Size(117, 36);
            this.chkToggleFirewall.TabIndex = 7;
            this.chkToggleFirewall.Text = "启用防火墙";
            this.chkToggleFirewall.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.chkToggleFirewall.UseVisualStyleBackColor = true;
            this.chkToggleFirewall.CheckedChanged += new System.EventHandler(this.chkToggleFirewall_CheckedChanged);
            // 
            // lblTerritoryId
            // 
            this.lblTerritoryId.AutoSize = true;
            this.lblTerritoryId.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTerritoryId.Location = new System.Drawing.Point(173, 218);
            this.lblTerritoryId.Margin = new System.Windows.Forms.Padding(3, 0, 0, 0);
            this.lblTerritoryId.Name = "lblTerritoryId";
            this.lblTerritoryId.Size = new System.Drawing.Size(72, 62);
            this.lblTerritoryId.TabIndex = 9;
            this.lblTerritoryId.Text = "地图 ID";
            this.lblTerritoryId.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // numTerritoryId
            // 
            this.numTerritoryId.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.numTerritoryId.Location = new System.Drawing.Point(255, 234);
            this.numTerritoryId.Margin = new System.Windows.Forms.Padding(10, 10, 15, 10);
            this.numTerritoryId.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.numTerritoryId.Name = "numTerritoryId";
            this.numTerritoryId.Size = new System.Drawing.Size(70, 29);
            this.numTerritoryId.TabIndex = 10;
            this.numTerritoryId.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numTerritoryId.ValueChanged += new System.EventHandler(this.numTerritoryId_ValueChanged);
            // 
            // splitMainH
            // 
            this.splitMainH.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMainH.Font = new System.Drawing.Font("等距更纱黑体 SC", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.splitMainH.Location = new System.Drawing.Point(0, 0);
            this.splitMainH.Name = "splitMainH";
            this.splitMainH.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitMainH.Panel1
            // 
            this.splitMainH.Panel1.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.splitMainH.Panel1.Controls.Add(this.presetControl);
            // 
            // splitMainH.Panel2
            // 
            this.splitMainH.Panel2.Controls.Add(this.logView);
            this.splitMainH.Size = new System.Drawing.Size(865, 892);
            this.splitMainH.SplitterDistance = 423;
            this.splitMainH.TabIndex = 0;
            // 
            // presetControl
            // 
            this.presetControl.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.presetControl.BackColor = System.Drawing.SystemColors.Control;
            this.presetControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.presetControl.Font = new System.Drawing.Font("等距更纱黑体 SC", 10.2F);
            this.presetControl.Location = new System.Drawing.Point(0, 0);
            this.presetControl.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.presetControl.Name = "presetControl";
            this.presetControl.Size = new System.Drawing.Size(865, 423);
            this.presetControl.TabIndex = 0;
            this.presetControl.TabStop = false;
            // 
            // logView
            // 
            this.logView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logView.Location = new System.Drawing.Point(0, 0);
            this.logView.Name = "logView";
            this.logView.Size = new System.Drawing.Size(865, 465);
            this.logView.TabIndex = 0;
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem2});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(106, 28);
            // 
            // toolStripMenuItem2
            // 
            this.toolStripMenuItem2.Name = "toolStripMenuItem2";
            this.toolStripMenuItem2.Size = new System.Drawing.Size(105, 24);
            this.toolStripMenuItem2.Text = "111";
            // 
            // btnAddBNpc
            // 
            this.btnAddBNpc.AutoSize = true;
            this.btnAddBNpc.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnAddBNpc.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnAddBNpc.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnAddBNpc.Location = new System.Drawing.Point(15, 476);
            this.btnAddBNpc.Margin = new System.Windows.Forms.Padding(15, 10, 15, 10);
            this.btnAddBNpc.Name = "btnAddBNpc";
            this.btnAddBNpc.Padding = new System.Windows.Forms.Padding(5);
            this.btnAddBNpc.Size = new System.Drawing.Size(140, 42);
            this.btnAddBNpc.TabIndex = 11;
            this.btnAddBNpc.TabStop = false;
            this.btnAddBNpc.Text = "添加实体";
            this.btnAddBNpc.UseVisualStyleBackColor = true;
            this.btnAddBNpc.Click += new System.EventHandler(this.btnAddBNpc_Click);
            // 
            // SimulantUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitMainV);
            this.Name = "SimulantUI";
            this.Size = new System.Drawing.Size(1209, 892);
            this.splitMainV.Panel1.ResumeLayout(false);
            this.splitMainV.Panel1.PerformLayout();
            this.splitMainV.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMainV)).EndInit();
            this.splitMainV.ResumeLayout(false);
            this.tableL.ResumeLayout(false);
            this.tableL.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTerritoryId)).EndInit();
            this.splitMainH.Panel1.ResumeLayout(false);
            this.splitMainH.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMainH)).EndInit();
            this.splitMainH.ResumeLayout(false);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitMainV;
        private System.Windows.Forms.TableLayoutPanel tableL;
        private System.Windows.Forms.SplitContainer splitMainH;
        private Simulant.UI.LogView logView;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.Label lblTerritory;
        private System.Windows.Forms.Button btnSimEnter;
        private System.Windows.Forms.Button btnSimExit;
        private System.Windows.Forms.Button btnInitPlugin;
        private System.Windows.Forms.Button btnSelectTerritory;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem2;
        private System.Windows.Forms.PictureBox picLogo;
        private System.Windows.Forms.Button btnDebug;
        private System.Windows.Forms.CheckBox chkToggleFirewall;
        private System.Windows.Forms.ComboBox cbxPhase;
        private System.Windows.Forms.Label lblTerritoryId;
        private System.Windows.Forms.NumericUpDown numTerritoryId;
        internal PresetControl presetControl;
        private System.Windows.Forms.Button btnAddBNpc;
    }
}