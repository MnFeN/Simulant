namespace Simulant.UI
{
    internal partial class TerritoryForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLeft = new System.Windows.Forms.TableLayoutPanel();
            this.btnOk = new ButtonEx();
            this.txtFilter = new TextBoxEx();
            this.dgvTerritory = new DataGridViewEx();
            this.colId = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPlace = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colInstance = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRegion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.lblFilter = new System.Windows.Forms.Label();
            this.chkPresetOnly = new CheckBoxEx();
            this.tableLeft.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvTerritory)).BeginInit();
            this.SuspendLayout();
            //
            // tableLeft
            //
            this.tableLeft.AutoSize = true;
            this.tableLeft.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLeft.ColumnCount = 4;
            this.tableLeft.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLeft.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLeft.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLeft.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLeft.Controls.Add(this.btnOk, 3, 0);
            this.tableLeft.Controls.Add(this.txtFilter, 1, 0);
            this.tableLeft.Controls.Add(this.dgvTerritory, 0, 1);
            this.tableLeft.Controls.Add(this.lblFilter, 0, 0);
            this.tableLeft.Controls.Add(this.chkPresetOnly, 2, 0);
            this.tableLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLeft.Location = new System.Drawing.Point(0, 0);
            this.tableLeft.Name = "tableLeft";
            this.tableLeft.RowCount = 2;
            this.tableLeft.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLeft.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLeft.Size = new System.Drawing.Size(1059, 811);
            this.tableLeft.TabIndex = 1;
            //
            // btnOk
            //
            this.btnOk.AutoSize = true;
            this.btnOk.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnOk.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnOk.Location = new System.Drawing.Point(932, 3);
            this.btnOk.Name = "btnOk";
            this.btnOk.Padding = new System.Windows.Forms.Padding(20, 2, 20, 2);
            this.btnOk.Size = new System.Drawing.Size(124, 36);
            this.btnOk.TabIndex = 8;
            this.btnOk.Text = "读取预设";
            this.btnOk.Tone = ButtonTone.Primary;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            //
            // txtFilter
            //
            this.txtFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtFilter.Location = new System.Drawing.Point(85, 7);
            this.txtFilter.Margin = new System.Windows.Forms.Padding(5, 7, 20, 5);
            this.txtFilter.Name = "txtFilter";
            this.txtFilter.Size = new System.Drawing.Size(658, 29);
            this.txtFilter.TabIndex = 6;
            this.txtFilter.TextChanged += new System.EventHandler(this.txtFilter_TextChanged);
            //
            // dgvTerritory
            //
            this.dgvTerritory.AllowUserToAddRows = false;
            this.dgvTerritory.AllowUserToDeleteRows = false;
            this.dgvTerritory.AllowUserToResizeRows = false;
            this.dgvTerritory.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvTerritory.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[]
            {
                this.colId,
                this.colPlace,
                this.colInstance,
                this.colRegion,
                this.colType
            });
            this.tableLeft.SetColumnSpan(this.dgvTerritory, 4);
            this.dgvTerritory.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvTerritory.Location = new System.Drawing.Point(3, 45);
            this.dgvTerritory.MultiSelect = false;
            this.dgvTerritory.Name = "dgvTerritory";
            this.dgvTerritory.ReadOnly = true;
            this.dgvTerritory.RowHeadersVisible = false;
            this.dgvTerritory.RowHeadersWidth = 51;
            this.dgvTerritory.RowTemplate.Height = 27;
            this.dgvTerritory.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvTerritory.Size = new System.Drawing.Size(1053, 763);
            this.dgvTerritory.TabIndex = 0;
            this.dgvTerritory.TabStop = false;
            this.dgvTerritory.VirtualMode = true;
            this.dgvTerritory.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvTerritory_CellDoubleClick);
            this.dgvTerritory.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.dgvTerritory_CellFormatting);
            this.dgvTerritory.CellValueNeeded += new System.Windows.Forms.DataGridViewCellValueEventHandler(this.dgvTerritory_CellValueNeeded);
            this.dgvTerritory.SelectionChanged += new System.EventHandler(this.dgvTerritory_SelectionChanged);
            //
            // colId
            //
            this.colId.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colId.FillWeight = 30F;
            this.colId.HeaderText = "ID";
            this.colId.MinimumWidth = 6;
            this.colId.Name = "colId";
            this.colId.ReadOnly = true;
            //
            // colPlace
            //
            this.colPlace.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colPlace.HeaderText = "地点";
            this.colPlace.MinimumWidth = 6;
            this.colPlace.Name = "colPlace";
            this.colPlace.ReadOnly = true;
            //
            // colInstance
            //
            this.colInstance.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colInstance.FillWeight = 120F;
            this.colInstance.HeaderText = "副本";
            this.colInstance.MinimumWidth = 6;
            this.colInstance.Name = "colInstance";
            this.colInstance.ReadOnly = true;
            //
            // colRegion
            //
            this.colRegion.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colRegion.FillWeight = 70F;
            this.colRegion.HeaderText = "区域";
            this.colRegion.MinimumWidth = 6;
            this.colRegion.Name = "colRegion";
            this.colRegion.ReadOnly = true;
            //
            // colType
            //
            this.colType.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colType.FillWeight = 70F;
            this.colType.HeaderText = "类型";
            this.colType.MinimumWidth = 6;
            this.colType.Name = "colType";
            this.colType.ReadOnly = true;
            //
            // lblFilter
            //
            this.lblFilter.AutoSize = true;
            this.lblFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblFilter.Location = new System.Drawing.Point(3, 3);
            this.lblFilter.Margin = new System.Windows.Forms.Padding(3);
            this.lblFilter.Name = "lblFilter";
            this.lblFilter.Padding = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.lblFilter.Size = new System.Drawing.Size(74, 36);
            this.lblFilter.TabIndex = 3;
            this.lblFilter.Text = "正则搜索";
            this.lblFilter.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // chkPresetOnly
            //
            this.chkPresetOnly.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.chkPresetOnly.AutoSize = true;
            this.chkPresetOnly.Location = new System.Drawing.Point(766, 8);
            this.chkPresetOnly.Name = "chkPresetOnly";
            this.chkPresetOnly.Size = new System.Drawing.Size(160, 26);
            this.chkPresetOnly.TabIndex = 7;
            this.chkPresetOnly.Text = "仅显示含预设区域";
            this.chkPresetOnly.CheckedChanged += new System.EventHandler(this.chkPresetOnly_CheckedChanged);
            //
            // TerritoryForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(1059, 811);
            this.Controls.Add(this.tableLeft);
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "TerritoryForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "选择副本预设";
            this.tableLeft.ResumeLayout(false);
            this.tableLeft.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvTerritory)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TableLayoutPanel tableLeft;
        private ButtonEx btnOk;
        private TextBoxEx txtFilter;
        private DataGridViewEx dgvTerritory;
        private System.Windows.Forms.DataGridViewTextBoxColumn colId;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPlace;
        private System.Windows.Forms.DataGridViewTextBoxColumn colInstance;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRegion;
        private System.Windows.Forms.DataGridViewTextBoxColumn colType;
        private System.Windows.Forms.Label lblFilter;
        private CheckBoxEx chkPresetOnly;
    }
}
