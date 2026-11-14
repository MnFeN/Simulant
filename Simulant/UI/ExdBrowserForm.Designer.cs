namespace Simulant.UI
{
    partial class ExdBrowserForm
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
            this.layoutMain = new System.Windows.Forms.TableLayoutPanel();
            this.panelSheets = new System.Windows.Forms.TableLayoutPanel();
            this.lblSheets = new System.Windows.Forms.Label();
            this.txtSheets = new System.Windows.Forms.TextBox();
            this.listSheets = new System.Windows.Forms.ListBox();
            this.panelPreview = new System.Windows.Forms.TableLayoutPanel();
            this.panelSearch = new System.Windows.Forms.TableLayoutPanel();
            this.lblSearch = new System.Windows.Forms.Label();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.btnSearch = new System.Windows.Forms.Button();
            this.grid = new System.Windows.Forms.DataGridView();
            this.panelNavigation = new System.Windows.Forms.TableLayoutPanel();
            this.lblStatus = new System.Windows.Forms.Label();
            this.btnPrevious = new System.Windows.Forms.Button();
            this.btnNext = new System.Windows.Forms.Button();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            this.layoutMain.SuspendLayout();
            this.panelSheets.SuspendLayout();
            this.panelPreview.SuspendLayout();
            this.panelSearch.SuspendLayout();
            this.panelNavigation.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.grid)).BeginInit();
            // 左侧表目录和右侧预览均随窗口伸缩，不依赖固定像素尺寸。
            this.layoutMain.ColumnCount = 2;
            this.layoutMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.layoutMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 80F));
            this.layoutMain.RowCount = 1;
            this.layoutMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.layoutMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.layoutMain.Controls.Add(this.panelSheets, 0, 0);
            this.layoutMain.Controls.Add(this.panelPreview, 1, 0);
            this.panelSheets.ColumnCount = 1;
            this.panelSheets.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.panelSheets.RowCount = 3;
            this.panelSheets.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.panelSheets.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.panelSheets.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.panelSheets.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelSheets.Controls.Add(this.lblSheets, 0, 0);
            this.panelSheets.Controls.Add(this.txtSheets, 0, 1);
            this.panelSheets.Controls.Add(this.listSheets, 0, 2);
            this.lblSheets.Text = "表名筛选";
            this.lblSheets.AutoSize = true;
            this.lblSheets.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSheets.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSheets.Enabled = false;
            this.txtSheets.TextChanged += new System.EventHandler(this.txtSheets_TextChanged);
            this.listSheets.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listSheets.IntegralHeight = false;
            this.listSheets.HorizontalScrollbar = true;
            this.listSheets.SelectedIndexChanged += new System.EventHandler(this.listSheets_SelectedIndexChanged);
            this.panelPreview.ColumnCount = 1;
            this.panelPreview.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.panelPreview.RowCount = 3;
            this.panelPreview.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.panelPreview.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.panelPreview.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.panelPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPreview.Controls.Add(this.panelSearch, 0, 0);
            this.panelPreview.Controls.Add(this.grid, 0, 1);
            this.panelPreview.Controls.Add(this.panelNavigation, 0, 2);
            this.panelSearch.AutoSize = true;
            this.panelSearch.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panelSearch.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelSearch.ColumnCount = 3;
            this.panelSearch.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.panelSearch.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.panelSearch.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.panelSearch.RowCount = 1;
            this.panelSearch.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.panelSearch.Controls.Add(this.lblSearch, 0, 0);
            this.panelSearch.Controls.Add(this.txtSearch, 1, 0);
            this.panelSearch.Controls.Add(this.btnSearch, 2, 0);
            this.lblSearch.Text = "内容检索";
            this.lblSearch.AutoSize = true;
            this.lblSearch.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSearch.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.txtSearch.Dock = System.Windows.Forms.DockStyle.Fill;
            this.toolTip.SetToolTip(this.txtSearch, "检索当前表的 ID、字段值和关联名称；留空显示全部。按回车开始检索。");
            this.btnSearch.Text = "检索";
            this.btnSearch.AutoSize = true;
            this.btnSearch.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnSearch.Enabled = false;
            this.btnSearch.Click += new System.EventHandler(this.btnSearch_Click);
            this.grid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grid.ReadOnly = true;
            this.grid.VirtualMode = true;
            this.grid.AllowUserToAddRows = false;
            this.grid.AllowUserToDeleteRows = false;
            this.grid.AllowUserToOrderColumns = true;
            this.grid.RowHeadersVisible = false;
            this.grid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.DisplayedCells;
            this.grid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.grid.ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            this.grid.CellValueNeeded += new System.Windows.Forms.DataGridViewCellValueEventHandler(this.grid_CellValueNeeded);
            this.panelNavigation.AutoSize = true;
            this.panelNavigation.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panelNavigation.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelNavigation.ColumnCount = 3;
            this.panelNavigation.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.panelNavigation.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.panelNavigation.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.panelNavigation.RowCount = 1;
            this.panelNavigation.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.panelNavigation.Controls.Add(this.lblStatus, 0, 0);
            this.panelNavigation.Controls.Add(this.btnPrevious, 1, 0);
            this.panelNavigation.Controls.Add(this.btnNext, 2, 0);
            this.lblStatus.AutoSize = true;
            this.lblStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblStatus.Text = "正在读取表目录……";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnPrevious.Text = "上一页";
            this.btnPrevious.AutoSize = true;
            this.btnPrevious.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnPrevious.Enabled = false;
            this.btnPrevious.Click += new System.EventHandler(this.btnPrevious_Click);
            this.btnNext.Text = "下一页";
            this.btnNext.AutoSize = true;
            this.btnNext.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnNext.Enabled = false;
            this.btnNext.Click += new System.EventHandler(this.btnNext_Click);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.Controls.Add(this.layoutMain);
            this.AcceptButton = this.btnSearch;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "通用 exd";
            this.Shown += new System.EventHandler(this.ExdBrowserForm_Shown);
            ((System.ComponentModel.ISupportInitialize)(this.grid)).EndInit();
            this.panelNavigation.ResumeLayout(false);
            this.panelNavigation.PerformLayout();
            this.panelSearch.ResumeLayout(false);
            this.panelSearch.PerformLayout();
            this.panelPreview.ResumeLayout(false);
            this.panelPreview.PerformLayout();
            this.panelSheets.ResumeLayout(false);
            this.panelSheets.PerformLayout();
            this.layoutMain.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.TableLayoutPanel layoutMain;
        private System.Windows.Forms.TableLayoutPanel panelSheets;
        private System.Windows.Forms.Label lblSheets;
        private System.Windows.Forms.TextBox txtSheets;
        private System.Windows.Forms.ListBox listSheets;
        private System.Windows.Forms.TableLayoutPanel panelPreview;
        private System.Windows.Forms.TableLayoutPanel panelSearch;
        private System.Windows.Forms.Label lblSearch;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.Button btnSearch;
        private System.Windows.Forms.DataGridView grid;
        private System.Windows.Forms.TableLayoutPanel panelNavigation;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Button btnPrevious;
        private System.Windows.Forms.Button btnNext;
        private System.Windows.Forms.ToolTip toolTip;
    }
}
