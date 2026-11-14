namespace Simulant.UI
{
    partial class LogView
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
            this.tableLog = new System.Windows.Forms.TableLayoutPanel();
            this.chkLogFilterVerbose = new System.Windows.Forms.CheckBox();
            this.chkLogFilterSim = new System.Windows.Forms.CheckBox();
            this.chkLogFilterRuntime = new System.Windows.Forms.CheckBox();
            this.chkLogWarning = new System.Windows.Forms.CheckBox();
            this.chkLogFilterError = new System.Windows.Forms.CheckBox();
            this.chkLogFilterAll = new System.Windows.Forms.CheckBox();
            this.lblLogRegex = new System.Windows.Forms.Label();
            this.btnLogSearch = new System.Windows.Forms.Button();
            this.txtLogRegex = new System.Windows.Forms.TextBox();
            this.dgvLog = new System.Windows.Forms.DataGridView();
            this.colTime = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colText = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ctxRightClickMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.mnuLogCopySelected = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuLogDeleteSelected = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuLogClearAll = new System.Windows.Forms.ToolStripMenuItem();
            this.tableLog.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvLog)).BeginInit();
            this.ctxRightClickMenu.SuspendLayout();
            this.SuspendLayout();
            //
            // tableLog
            //
            this.tableLog.AutoSize = true;
            this.tableLog.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLog.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.tableLog.ColumnCount = 3;
            this.tableLog.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLog.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLog.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLog.Controls.Add(this.chkLogFilterVerbose, 2, 5);
            this.tableLog.Controls.Add(this.chkLogFilterSim, 2, 4);
            this.tableLog.Controls.Add(this.chkLogFilterRuntime, 2, 3);
            this.tableLog.Controls.Add(this.chkLogWarning, 2, 2);
            this.tableLog.Controls.Add(this.chkLogFilterError, 2, 1);
            this.tableLog.Controls.Add(this.chkLogFilterAll, 2, 0);
            this.tableLog.Controls.Add(this.lblLogRegex, 0, 10);
            this.tableLog.Controls.Add(this.btnLogSearch, 2, 10);
            this.tableLog.Controls.Add(this.txtLogRegex, 1, 10);
            this.tableLog.Controls.Add(this.dgvLog, 0, 0);
            this.tableLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLog.Font = new System.Drawing.Font("等距更纱黑体 SC", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tableLog.Location = new System.Drawing.Point(0, 0);
            this.tableLog.Name = "tableLog";
            this.tableLog.RowCount = 11;
            this.tableLog.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLog.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLog.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLog.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLog.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLog.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLog.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLog.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLog.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLog.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLog.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLog.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLog.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLog.Size = new System.Drawing.Size(1014, 649);
            this.tableLog.TabIndex = 1;
            //
            // chkLogFilterVerbose
            //
            this.chkLogFilterVerbose.AutoCheck = false;
            this.chkLogFilterVerbose.AutoSize = true;
            this.chkLogFilterVerbose.Checked = false;
            this.chkLogFilterVerbose.CheckState = System.Windows.Forms.CheckState.Unchecked;
            this.chkLogFilterVerbose.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chkLogFilterVerbose.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.chkLogFilterVerbose.Location = new System.Drawing.Point(900, 183);
            this.chkLogFilterVerbose.Margin = new System.Windows.Forms.Padding(0);
            this.chkLogFilterVerbose.Name = "chkLogFilterVerbose";
            this.chkLogFilterVerbose.Padding = new System.Windows.Forms.Padding(10, 5, 5, 5);
            this.chkLogFilterVerbose.Size = new System.Drawing.Size(114, 36);
            this.chkLogFilterVerbose.TabIndex = 0;
            this.chkLogFilterVerbose.TabStop = false;
            this.chkLogFilterVerbose.Text = "详细";
            this.chkLogFilterVerbose.UseVisualStyleBackColor = true;
            this.chkLogFilterVerbose.MouseClick += new System.Windows.Forms.MouseEventHandler(this.chkLogFilters_MouseClick);
            this.chkLogFilterVerbose.MouseDown += new System.Windows.Forms.MouseEventHandler(this.chkLogFilters_MouseDown);
            //
            // chkLogFilterSim
            //
            this.chkLogFilterSim.AutoCheck = false;
            this.chkLogFilterSim.AutoSize = true;
            this.chkLogFilterSim.Checked = true;
            this.chkLogFilterSim.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkLogFilterSim.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chkLogFilterSim.ForeColor = System.Drawing.Color.SlateBlue;
            this.chkLogFilterSim.Location = new System.Drawing.Point(900, 147);
            this.chkLogFilterSim.Margin = new System.Windows.Forms.Padding(0);
            this.chkLogFilterSim.Name = "chkLogFilterSim";
            this.chkLogFilterSim.Padding = new System.Windows.Forms.Padding(10, 5, 5, 5);
            this.chkLogFilterSim.Size = new System.Drawing.Size(114, 36);
            this.chkLogFilterSim.TabIndex = 0;
            this.chkLogFilterSim.TabStop = false;
            this.chkLogFilterSim.Text = "模拟";
            this.chkLogFilterSim.UseVisualStyleBackColor = true;
            this.chkLogFilterSim.MouseClick += new System.Windows.Forms.MouseEventHandler(this.chkLogFilters_MouseClick);
            this.chkLogFilterSim.MouseDown += new System.Windows.Forms.MouseEventHandler(this.chkLogFilters_MouseDown);
            //
            // chkLogFilterRuntime
            //
            this.chkLogFilterRuntime.AutoCheck = false;
            this.chkLogFilterRuntime.AutoSize = true;
            this.chkLogFilterRuntime.Checked = true;
            this.chkLogFilterRuntime.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkLogFilterRuntime.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chkLogFilterRuntime.ForeColor = System.Drawing.Color.SeaGreen;
            this.chkLogFilterRuntime.Location = new System.Drawing.Point(900, 111);
            this.chkLogFilterRuntime.Margin = new System.Windows.Forms.Padding(0);
            this.chkLogFilterRuntime.Name = "chkLogFilterRuntime";
            this.chkLogFilterRuntime.Padding = new System.Windows.Forms.Padding(10, 5, 5, 5);
            this.chkLogFilterRuntime.Size = new System.Drawing.Size(114, 36);
            this.chkLogFilterRuntime.TabIndex = 0;
            this.chkLogFilterRuntime.TabStop = false;
            this.chkLogFilterRuntime.Text = "运行";
            this.chkLogFilterRuntime.UseVisualStyleBackColor = true;
            this.chkLogFilterRuntime.MouseClick += new System.Windows.Forms.MouseEventHandler(this.chkLogFilters_MouseClick);
            this.chkLogFilterRuntime.MouseDown += new System.Windows.Forms.MouseEventHandler(this.chkLogFilters_MouseDown);
            //
            // chkLogWarning
            //
            this.chkLogWarning.AutoCheck = false;
            this.chkLogWarning.AutoSize = true;
            this.chkLogWarning.Checked = true;
            this.chkLogWarning.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkLogWarning.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chkLogWarning.ForeColor = System.Drawing.Color.SaddleBrown;
            this.chkLogWarning.Location = new System.Drawing.Point(900, 75);
            this.chkLogWarning.Margin = new System.Windows.Forms.Padding(0);
            this.chkLogWarning.Name = "chkLogWarning";
            this.chkLogWarning.Padding = new System.Windows.Forms.Padding(10, 5, 5, 5);
            this.chkLogWarning.Size = new System.Drawing.Size(114, 36);
            this.chkLogWarning.TabIndex = 0;
            this.chkLogWarning.TabStop = false;
            this.chkLogWarning.Text = "警告";
            this.chkLogWarning.UseVisualStyleBackColor = true;
            this.chkLogWarning.MouseClick += new System.Windows.Forms.MouseEventHandler(this.chkLogFilters_MouseClick);
            this.chkLogWarning.MouseDown += new System.Windows.Forms.MouseEventHandler(this.chkLogFilters_MouseDown);
            //
            // chkLogFilterError
            //
            this.chkLogFilterError.AutoCheck = false;
            this.chkLogFilterError.AutoSize = true;
            this.chkLogFilterError.Checked = true;
            this.chkLogFilterError.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkLogFilterError.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chkLogFilterError.ForeColor = System.Drawing.Color.Maroon;
            this.chkLogFilterError.Location = new System.Drawing.Point(900, 39);
            this.chkLogFilterError.Margin = new System.Windows.Forms.Padding(0);
            this.chkLogFilterError.Name = "chkLogFilterError";
            this.chkLogFilterError.Padding = new System.Windows.Forms.Padding(10, 5, 5, 5);
            this.chkLogFilterError.Size = new System.Drawing.Size(114, 36);
            this.chkLogFilterError.TabIndex = 0;
            this.chkLogFilterError.TabStop = false;
            this.chkLogFilterError.Text = "错误";
            this.chkLogFilterError.UseVisualStyleBackColor = true;
            this.chkLogFilterError.MouseClick += new System.Windows.Forms.MouseEventHandler(this.chkLogFilters_MouseClick);
            this.chkLogFilterError.MouseDown += new System.Windows.Forms.MouseEventHandler(this.chkLogFilters_MouseDown);
            //
            // chkLogFilterAll
            //
            this.chkLogFilterAll.AutoCheck = false;
            this.chkLogFilterAll.AutoSize = true;
            this.chkLogFilterAll.CheckState = System.Windows.Forms.CheckState.Indeterminate;
            this.chkLogFilterAll.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chkLogFilterAll.Location = new System.Drawing.Point(900, 3);
            this.chkLogFilterAll.Margin = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this.chkLogFilterAll.Name = "chkLogFilterAll";
            this.chkLogFilterAll.Padding = new System.Windows.Forms.Padding(10, 5, 5, 5);
            this.chkLogFilterAll.Size = new System.Drawing.Size(114, 36);
            this.chkLogFilterAll.TabIndex = 0;
            this.chkLogFilterAll.TabStop = false;
            this.chkLogFilterAll.Text = "全选";
            this.chkLogFilterAll.ThreeState = true;
            this.chkLogFilterAll.UseVisualStyleBackColor = true;
            this.chkLogFilterAll.MouseClick += new System.Windows.Forms.MouseEventHandler(this.chkLogFilterAll_MouseClick);
            this.chkLogFilterAll.MouseDown += new System.Windows.Forms.MouseEventHandler(this.chkLogFilterAll_MouseDown);
            //
            // lblLogRegex
            //
            this.lblLogRegex.AutoSize = true;
            this.lblLogRegex.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLogRegex.Location = new System.Drawing.Point(15, 604);
            this.lblLogRegex.Margin = new System.Windows.Forms.Padding(15, 10, 15, 10);
            this.lblLogRegex.Name = "lblLogRegex";
            this.lblLogRegex.Size = new System.Drawing.Size(74, 35);
            this.lblLogRegex.TabIndex = 0;
            this.lblLogRegex.Text = "正则搜索";
            this.lblLogRegex.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // btnLogSearch
            //
            this.btnLogSearch.AutoSize = true;
            this.btnLogSearch.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnLogSearch.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnLogSearch.Location = new System.Drawing.Point(915, 604);
            this.btnLogSearch.Margin = new System.Windows.Forms.Padding(15, 10, 15, 10);
            this.btnLogSearch.Name = "btnLogSearch";
            this.btnLogSearch.Size = new System.Drawing.Size(84, 35);
            this.btnLogSearch.TabIndex = 0;
            this.btnLogSearch.TabStop = false;
            this.btnLogSearch.Text = "　搜索　";
            this.btnLogSearch.UseVisualStyleBackColor = true;
            this.btnLogSearch.Click += new System.EventHandler(this.btnLogSearch_Click);
            //
            // txtLogRegex
            //
            this.txtLogRegex.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLogRegex.Location = new System.Drawing.Point(107, 607);
            this.txtLogRegex.Margin = new System.Windows.Forms.Padding(3, 13, 3, 13);
            this.txtLogRegex.Name = "txtLogRegex";
            this.txtLogRegex.Size = new System.Drawing.Size(790, 29);
            this.txtLogRegex.TabIndex = 2;
            this.txtLogRegex.TextChanged += new System.EventHandler(this.txtLogRegex_TextChanged);
            this.txtLogRegex.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtLogRegex_KeyDown);
            //
            // dgvLog
            //
            this.dgvLog.AllowUserToAddRows = false;
            this.dgvLog.AllowUserToDeleteRows = false;
            this.dgvLog.AllowUserToResizeRows = false;
            this.dgvLog.BackgroundColor = System.Drawing.SystemColors.Control;
            this.dgvLog.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvLog.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[]
            {
                this.colTime,
                this.colType,
                this.colText
            });
            this.tableLog.SetColumnSpan(this.dgvLog, 2);
            this.dgvLog.ContextMenuStrip = this.ctxRightClickMenu;
            this.dgvLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvLog.Location = new System.Drawing.Point(3, 3);
            this.dgvLog.Name = "dgvLog";
            this.dgvLog.ReadOnly = true;
            this.dgvLog.RowHeadersVisible = false;
            this.dgvLog.RowHeadersWidth = 51;
            this.tableLog.SetRowSpan(this.dgvLog, 10);
            this.dgvLog.RowTemplate.Height = 27;
            this.dgvLog.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvLog.Size = new System.Drawing.Size(894, 588);
            this.dgvLog.TabIndex = 0;
            this.dgvLog.TabStop = false;
            this.dgvLog.VirtualMode = true;
            this.dgvLog.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.dgvLog_CellFormatting);
            this.dgvLog.CellMouseDown += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dgvLog_CellMouseDown);
            this.dgvLog.CellValueNeeded += new System.Windows.Forms.DataGridViewCellValueEventHandler(this.dgvLog_CellValueNeeded);
            //
            // colTime
            //
            this.colTime.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.DisplayedCells;
            this.colTime.HeaderText = "时间";
            this.colTime.MinimumWidth = 6;
            this.colTime.Name = "colTime";
            this.colTime.ReadOnly = true;
            this.colTime.Width = 71;
            //
            // colType
            //
            this.colType.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.DisplayedCells;
            this.colType.HeaderText = "类别";
            this.colType.MinimumWidth = 6;
            this.colType.Name = "colType";
            this.colType.ReadOnly = true;
            this.colType.Width = 71;
            //
            // colText
            //
            this.colText.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colText.HeaderText = "信息";
            this.colText.MinimumWidth = 6;
            this.colText.Name = "colText";
            this.colText.ReadOnly = true;
            //
            // ctxRightClickMenu
            //
            this.ctxRightClickMenu.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.ctxRightClickMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[]
            {
                this.mnuLogCopySelected,
                this.mnuLogDeleteSelected,
                this.mnuLogClearAll
            });
            this.ctxRightClickMenu.Name = "ctxRightClickMenu";
            this.ctxRightClickMenu.Size = new System.Drawing.Size(154, 76);
            //
            // mnuLogCopySelected
            //
            this.mnuLogCopySelected.Name = "mnuLogCopySelected";
            this.mnuLogCopySelected.Size = new System.Drawing.Size(210, 24);
            this.mnuLogCopySelected.Text = "复制选中行";
            this.mnuLogCopySelected.Click += new System.EventHandler(this.mnuLogCopySelected_Click);
            //
            // mnuLogDeleteSelected
            //
            this.mnuLogDeleteSelected.Name = "mnuLogDeleteSelected";
            this.mnuLogDeleteSelected.Size = new System.Drawing.Size(210, 24);
            this.mnuLogDeleteSelected.Text = "删除选中行";
            this.mnuLogDeleteSelected.Click += new System.EventHandler(this.mnuLogDeleteSelected_Click);
            //
            // mnuLogClearAll
            //
            this.mnuLogClearAll.Name = "mnuLogClearAll";
            this.mnuLogClearAll.Size = new System.Drawing.Size(210, 24);
            this.mnuLogClearAll.Text = "全部清空";
            this.mnuLogClearAll.Click += new System.EventHandler(this.mnuLogClearAll_Click);
            //
            // LogView
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLog);
            this.Name = "LogView";
            this.Size = new System.Drawing.Size(1014, 649);
            this.tableLog.ResumeLayout(false);
            this.tableLog.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvLog)).EndInit();
            this.ctxRightClickMenu.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TableLayoutPanel tableLog;
        private System.Windows.Forms.CheckBox chkLogFilterVerbose;
        private System.Windows.Forms.CheckBox chkLogFilterSim;
        private System.Windows.Forms.CheckBox chkLogFilterRuntime;
        private System.Windows.Forms.CheckBox chkLogWarning;
        private System.Windows.Forms.CheckBox chkLogFilterError;
        private System.Windows.Forms.CheckBox chkLogFilterAll;
        private System.Windows.Forms.Label lblLogRegex;
        private System.Windows.Forms.Button btnLogSearch;
        private System.Windows.Forms.TextBox txtLogRegex;
        private System.Windows.Forms.DataGridView dgvLog;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTime;
        private System.Windows.Forms.DataGridViewTextBoxColumn colType;
        private System.Windows.Forms.DataGridViewTextBoxColumn colText;
        private System.Windows.Forms.ContextMenuStrip ctxRightClickMenu;
        private System.Windows.Forms.ToolStripMenuItem mnuLogCopySelected;
        private System.Windows.Forms.ToolStripMenuItem mnuLogDeleteSelected;
        private System.Windows.Forms.ToolStripMenuItem mnuLogClearAll;
    }
}
