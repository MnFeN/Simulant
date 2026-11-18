using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Simulant.UI
{
    partial class LogView
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
            DataGridViewCellStyle timeStyle = new DataGridViewCellStyle();
            this.components = new Container();
            this.logCard = new PanelEx();
            this.tableLog = new TableLayoutPanel();
            this.toolbar = new FlowLayoutPanel();
            this.lblLogTitle = new LabelEx();
            this.chkLogFilterAll = new CheckBoxEx();
            this.chkLogFilterError = new CheckBoxEx();
            this.chkLogWarning = new CheckBoxEx();
            this.chkLogFilterRuntime = new CheckBoxEx();
            this.chkLogFilterSim = new CheckBoxEx();
            this.chkLogFilterVerbose = new CheckBoxEx();
            this.dgvLog = new DataGridViewEx();
            this.colTime = new DataGridViewTextBoxColumn();
            this.colType = new DataGridViewTextBoxColumn();
            this.colText = new DataGridViewTextBoxColumn();
            this.ctxRightClickMenu = new ContextMenuStripEx(this.components);
            this.mnuLogCopySelected = new ToolStripMenuItem();
            this.mnuLogDeleteSelected = new ToolStripMenuItem();
            this.mnuLogClearAll = new ToolStripMenuItem();
            this.logSearch = new TableLayoutPanel();
            this.lblLogRegex = new LabelEx();
            this.logSearchField = new PanelEx();
            this.txtLogRegex = new TextBoxEx();
            this.btnLogSearch = new ButtonEx();
            this.logCard.SuspendLayout();
            this.tableLog.SuspendLayout();
            this.toolbar.SuspendLayout();
            ((ISupportInitialize)this.dgvLog).BeginInit();
            this.ctxRightClickMenu.SuspendLayout();
            this.logSearch.SuspendLayout();
            this.logSearchField.SuspendLayout();
            this.SuspendLayout();
            //
            // logCard
            //
            this.logCard.Dock = DockStyle.Fill;
            this.logCard.Margin = Padding.Empty;
            this.logCard.Padding = new Padding(14, 12, 14, 12);
            this.logCard.Name = "logCard";
            this.logCard.Controls.Add(this.tableLog);
            //
            // tableLog
            //
            this.tableLog.ColumnCount = 1;
            this.tableLog.RowCount = 3;
            this.tableLog.Dock = DockStyle.Fill;
            this.tableLog.Margin = Padding.Empty;
            this.tableLog.Name = "tableLog";
            this.tableLog.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.tableLog.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.tableLog.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.tableLog.RowStyles.Add(new RowStyle(SizeType.Absolute, 41F));
            this.tableLog.Controls.Add(this.toolbar, 0, 0);
            this.tableLog.Controls.Add(this.dgvLog, 0, 1);
            this.tableLog.Controls.Add(this.logSearch, 0, 2);
            //
            // toolbar
            //
            this.toolbar.AutoSize = true;
            this.toolbar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.toolbar.Dock = DockStyle.Fill;
            this.toolbar.Margin = new Padding(0, 0, 0, 8);
            this.toolbar.Name = "toolbar";
            this.toolbar.Controls.Add(this.lblLogTitle);
            this.toolbar.Controls.Add(this.chkLogFilterAll);
            this.toolbar.Controls.Add(this.chkLogFilterError);
            this.toolbar.Controls.Add(this.chkLogWarning);
            this.toolbar.Controls.Add(this.chkLogFilterRuntime);
            this.toolbar.Controls.Add(this.chkLogFilterSim);
            this.toolbar.Controls.Add(this.chkLogFilterVerbose);
            //
            // lblLogTitle
            //
            this.lblLogTitle.Text = "运行日志";
            this.lblLogTitle.Dock = DockStyle.None;
            this.lblLogTitle.Margin = new Padding(0, 0, 7, 5);
            this.lblLogTitle.TextAlign = ContentAlignment.MiddleLeft;
            this.lblLogTitle.AutoEllipsis = true;
            this.lblLogTitle.Tone = LabelTone.Heading;
            this.lblLogTitle.Size = new Size(86, 31);
            this.lblLogTitle.Name = "lblLogTitle";
            //
            // chkLogFilterAll
            //
            this.chkLogFilterAll.Text = "全选";
            this.chkLogFilterAll.Style = CheckBoxStyle.Chip;
            this.chkLogFilterAll.UseAccentText = false;
            this.chkLogFilterAll.AutoCheck = false;
            this.chkLogFilterAll.CheckState = CheckState.Indeterminate;
            this.chkLogFilterAll.Font = new Font("Microsoft YaHei UI", 9F);
            this.chkLogFilterAll.Size = new Size(53, 27);
            this.chkLogFilterAll.Margin = new Padding(0, 2, 6, 5);
            this.chkLogFilterAll.Name = "chkLogFilterAll";
            this.chkLogFilterAll.Click += new EventHandler(this.chkLogFilterAll_Click);
            this.chkLogFilterAll.MouseDown += new MouseEventHandler(this.chkLogFilterAll_MouseDown);
            //
            // chkLogFilterError
            //
            this.chkLogFilterError.Text = "错误";
            this.chkLogFilterError.Style = CheckBoxStyle.Chip;
            this.chkLogFilterError.Tone = CheckBoxTone.Error;
            this.chkLogFilterError.Checked = true;
            this.chkLogFilterError.CheckState = CheckState.Checked;
            this.chkLogFilterError.AutoCheck = false;
            this.chkLogFilterError.Font = new Font("Microsoft YaHei UI", 9F);
            this.chkLogFilterError.Size = new Size(53, 27);
            this.chkLogFilterError.Margin = new Padding(0, 2, 6, 5);
            this.chkLogFilterError.Name = "chkLogFilterError";
            this.chkLogFilterError.Click += new EventHandler(this.chkLogFilters_Click);
            this.chkLogFilterError.MouseDown += new MouseEventHandler(this.chkLogFilters_MouseDown);
            //
            // chkLogWarning
            //
            this.chkLogWarning.Text = "警告";
            this.chkLogWarning.Style = CheckBoxStyle.Chip;
            this.chkLogWarning.Tone = CheckBoxTone.Warning;
            this.chkLogWarning.Checked = true;
            this.chkLogWarning.CheckState = CheckState.Checked;
            this.chkLogWarning.AutoCheck = false;
            this.chkLogWarning.Font = new Font("Microsoft YaHei UI", 9F);
            this.chkLogWarning.Size = new Size(53, 27);
            this.chkLogWarning.Margin = new Padding(0, 2, 6, 5);
            this.chkLogWarning.Name = "chkLogWarning";
            this.chkLogWarning.Click += new EventHandler(this.chkLogFilters_Click);
            this.chkLogWarning.MouseDown += new MouseEventHandler(this.chkLogFilters_MouseDown);
            //
            // chkLogFilterRuntime
            //
            this.chkLogFilterRuntime.Text = "运行";
            this.chkLogFilterRuntime.Style = CheckBoxStyle.Chip;
            this.chkLogFilterRuntime.Tone = CheckBoxTone.Success;
            this.chkLogFilterRuntime.Checked = true;
            this.chkLogFilterRuntime.CheckState = CheckState.Checked;
            this.chkLogFilterRuntime.AutoCheck = false;
            this.chkLogFilterRuntime.Font = new Font("Microsoft YaHei UI", 9F);
            this.chkLogFilterRuntime.Size = new Size(53, 27);
            this.chkLogFilterRuntime.Margin = new Padding(0, 2, 6, 5);
            this.chkLogFilterRuntime.Name = "chkLogFilterRuntime";
            this.chkLogFilterRuntime.Click += new EventHandler(this.chkLogFilters_Click);
            this.chkLogFilterRuntime.MouseDown += new MouseEventHandler(this.chkLogFilters_MouseDown);
            //
            // chkLogFilterSim
            //
            this.chkLogFilterSim.Text = "模拟";
            this.chkLogFilterSim.Style = CheckBoxStyle.Chip;
            this.chkLogFilterSim.Tone = CheckBoxTone.Info;
            this.chkLogFilterSim.Checked = true;
            this.chkLogFilterSim.CheckState = CheckState.Checked;
            this.chkLogFilterSim.AutoCheck = false;
            this.chkLogFilterSim.Font = new Font("Microsoft YaHei UI", 9F);
            this.chkLogFilterSim.Size = new Size(53, 27);
            this.chkLogFilterSim.Margin = new Padding(0, 2, 6, 5);
            this.chkLogFilterSim.Name = "chkLogFilterSim";
            this.chkLogFilterSim.Click += new EventHandler(this.chkLogFilters_Click);
            this.chkLogFilterSim.MouseDown += new MouseEventHandler(this.chkLogFilters_MouseDown);
            //
            // chkLogFilterVerbose
            //
            this.chkLogFilterVerbose.Text = "详细";
            this.chkLogFilterVerbose.Style = CheckBoxStyle.Chip;
            this.chkLogFilterVerbose.Tone = CheckBoxTone.Neutral;
            this.chkLogFilterVerbose.AutoCheck = false;
            this.chkLogFilterVerbose.Font = new Font("Microsoft YaHei UI", 9F);
            this.chkLogFilterVerbose.Size = new Size(53, 27);
            this.chkLogFilterVerbose.Margin = new Padding(0, 2, 6, 5);
            this.chkLogFilterVerbose.Name = "chkLogFilterVerbose";
            this.chkLogFilterVerbose.Click += new EventHandler(this.chkLogFilters_Click);
            this.chkLogFilterVerbose.MouseDown += new MouseEventHandler(this.chkLogFilters_MouseDown);
            //
            // dgvLog
            //
            this.dgvLog.AccessibleName = "运行日志";
            this.dgvLog.Dock = DockStyle.Fill;
            this.dgvLog.Margin = Padding.Empty;
            this.dgvLog.ReadOnly = true;
            this.dgvLog.VirtualMode = true;
            this.dgvLog.AllowUserToAddRows = false;
            this.dgvLog.AllowUserToDeleteRows = false;
            this.dgvLog.AllowUserToResizeRows = false;
            this.dgvLog.TabStop = false;
            this.dgvLog.RowHeadersVisible = false;
            this.dgvLog.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.dgvLog.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            this.dgvLog.ColumnHeadersHeight = 32;
            this.dgvLog.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvLog.ContextMenuStrip = this.ctxRightClickMenu;
            this.dgvLog.Name = "dgvLog";
            this.dgvLog.RowTemplate.Height = 26;
            this.dgvLog.Columns.AddRange(new DataGridViewColumn[] { this.colTime, this.colType, this.colText });
            this.dgvLog.CellFormatting += new DataGridViewCellFormattingEventHandler(this.dgvLog_CellFormatting);
            this.dgvLog.CellMouseDown += new DataGridViewCellMouseEventHandler(this.dgvLog_CellMouseDown);
            this.dgvLog.CellValueNeeded += new DataGridViewCellValueEventHandler(this.dgvLog_CellValueNeeded);
            //
            // colTime
            //
            timeStyle.Font = new Font("Consolas", 10F);
            this.colTime.HeaderText = "时间";
            this.colTime.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            this.colTime.MinimumWidth = 96;
            this.colTime.SortMode = DataGridViewColumnSortMode.NotSortable;
            this.colTime.DefaultCellStyle = timeStyle;
            this.colTime.Name = "colTime";
            //
            // colType
            //
            this.colType.HeaderText = "类别";
            this.colType.Width = 62;
            this.colType.SortMode = DataGridViewColumnSortMode.NotSortable;
            this.colType.Name = "colType";
            //
            // colText
            //
            this.colText.HeaderText = "信息";
            this.colText.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            this.colText.SortMode = DataGridViewColumnSortMode.NotSortable;
            this.colText.Name = "colText";
            //
            // ctxRightClickMenu
            //
            this.ctxRightClickMenu.ShowImageMargin = false;
            this.ctxRightClickMenu.Name = "ctxRightClickMenu";
            this.ctxRightClickMenu.Items.AddRange(new ToolStripItem[] { this.mnuLogCopySelected, this.mnuLogDeleteSelected, this.mnuLogClearAll });
            //
            // mnuLogCopySelected
            //
            this.mnuLogCopySelected.Text = "复制选中行";
            this.mnuLogCopySelected.Name = "mnuLogCopySelected";
            this.mnuLogCopySelected.Click += new EventHandler(this.mnuLogCopySelected_Click);
            //
            // mnuLogDeleteSelected
            //
            this.mnuLogDeleteSelected.Text = "删除选中行";
            this.mnuLogDeleteSelected.Name = "mnuLogDeleteSelected";
            this.mnuLogDeleteSelected.Click += new EventHandler(this.mnuLogDeleteSelected_Click);
            //
            // mnuLogClearAll
            //
            this.mnuLogClearAll.Text = "全部清空";
            this.mnuLogClearAll.Name = "mnuLogClearAll";
            this.mnuLogClearAll.Click += new EventHandler(this.mnuLogClearAll_Click);
            //
            // logSearch
            //
            this.logSearch.ColumnCount = 3;
            this.logSearch.RowCount = 1;
            this.logSearch.Dock = DockStyle.Fill;
            this.logSearch.Margin = new Padding(0, 9, 0, 0);
            this.logSearch.Name = "logSearch";
            this.logSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78F));
            this.logSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.logSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65F));
            this.logSearch.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.logSearch.Controls.Add(this.lblLogRegex, 0, 0);
            this.logSearch.Controls.Add(this.logSearchField, 1, 0);
            this.logSearch.Controls.Add(this.btnLogSearch, 2, 0);
            //
            // lblLogRegex
            //
            this.lblLogRegex.Text = "正则搜索";
            this.lblLogRegex.Dock = DockStyle.Fill;
            this.lblLogRegex.Margin = Padding.Empty;
            this.lblLogRegex.TextAlign = ContentAlignment.MiddleLeft;
            this.lblLogRegex.AutoEllipsis = true;
            this.lblLogRegex.Tone = LabelTone.Muted;
            this.lblLogRegex.Name = "lblLogRegex";
            //
            // logSearchField
            //
            this.logSearchField.Dock = DockStyle.Fill;
            this.logSearchField.Margin = new Padding(0, 0, 12, 0);
            this.logSearchField.Padding = Padding.Empty;
            this.logSearchField.Tone = PanelTone.Field;
            this.logSearchField.Size = new Size(638, 32);
            this.logSearchField.Name = "logSearchField";
            this.logSearchField.Controls.Add(this.txtLogRegex);
            //
            // txtLogRegex
            //
            this.txtLogRegex.AccessibleName = "正则搜索";
            this.txtLogRegex.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            this.txtLogRegex.Location = new Point(9, 6);
            this.txtLogRegex.Size = new Size(613, 20);
            this.txtLogRegex.BorderStyle = BorderStyle.None;
            this.txtLogRegex.Name = "txtLogRegex";
            this.txtLogRegex.KeyDown += new KeyEventHandler(this.txtLogRegex_KeyDown);
            //
            // btnLogSearch
            //
            this.btnLogSearch.Text = "搜索";
            this.btnLogSearch.Dock = DockStyle.Fill;
            this.btnLogSearch.Margin = Padding.Empty;
            this.btnLogSearch.Name = "btnLogSearch";
            this.btnLogSearch.Click += new EventHandler(this.btnLogSearch_Click);
            //
            // LogView
            //
            this.AutoScaleMode = AutoScaleMode.Inherit;
            this.Controls.Add(this.logCard);
            this.Size = new Size(840, 360);
            this.Name = "LogView";
            this.logSearchField.ResumeLayout(false);
            this.logSearch.ResumeLayout(false);
            this.ctxRightClickMenu.ResumeLayout(false);
            ((ISupportInitialize)this.dgvLog).EndInit();
            this.toolbar.ResumeLayout(false);
            this.toolbar.PerformLayout();
            this.tableLog.ResumeLayout(false);
            this.logCard.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private PanelEx logCard;
        private TableLayoutPanel tableLog;
        private FlowLayoutPanel toolbar;
        private LabelEx lblLogTitle;
        private CheckBoxEx chkLogFilterAll;
        private CheckBoxEx chkLogFilterError;
        private CheckBoxEx chkLogWarning;
        private CheckBoxEx chkLogFilterRuntime;
        private CheckBoxEx chkLogFilterSim;
        private CheckBoxEx chkLogFilterVerbose;
        private DataGridViewEx dgvLog;
        private DataGridViewTextBoxColumn colTime;
        private DataGridViewTextBoxColumn colType;
        private DataGridViewTextBoxColumn colText;
        private ContextMenuStripEx ctxRightClickMenu;
        private ToolStripMenuItem mnuLogCopySelected;
        private ToolStripMenuItem mnuLogDeleteSelected;
        private ToolStripMenuItem mnuLogClearAll;
        private TableLayoutPanel logSearch;
        private LabelEx lblLogRegex;
        private PanelEx logSearchField;
        private TextBoxEx txtLogRegex;
        private ButtonEx btnLogSearch;
    }
}
