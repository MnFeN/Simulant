namespace Simulant.UI
{
    partial class PresetControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PresetControl));
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.tableMetaData = new System.Windows.Forms.TableLayoutPanel();
            this.lblInfo = new System.Windows.Forms.Label();
            this.lblPreset = new System.Windows.Forms.Label();
            this.lblAuthor = new System.Windows.Forms.Label();
            this.btnStart = new System.Windows.Forms.Button();
            this.btnRetry = new System.Windows.Forms.Button();
            this.btnEnd = new System.Windows.Forms.Button();
            this.lblLastChanged = new System.Windows.Forms.Label();
            this.rtbInfo = new System.Windows.Forms.RichTextBox();
            this.tableOptions = new System.Windows.Forms.TableLayoutPanel();
            this.dummyLbl = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.dummyLblTxt = new System.Windows.Forms.Label();
            this.dummyLblChk = new System.Windows.Forms.Label();
            this.dummyChk = new System.Windows.Forms.CheckBox();
            this.dummyTxt = new System.Windows.Forms.TextBox();
            this.dummyCbx = new System.Windows.Forms.ComboBox();
            this.dummyNud = new System.Windows.Forms.NumericUpDown();
            this.dummyLblCbx = new System.Windows.Forms.Label();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.tableMetaData.SuspendLayout();
            this.tableOptions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dummyNud)).BeginInit();
            this.SuspendLayout();
            //
            // splitContainer
            //
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Font = new System.Drawing.Font("等距更纱黑体 SC", 10.2F);
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.splitContainer.Name = "splitContainer";
            //
            // splitContainer.Panel1
            //
            this.splitContainer.Panel1.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.splitContainer.Panel1.Controls.Add(this.tableMetaData);
            //
            // splitContainer.Panel2
            //
            this.splitContainer.Panel2.AutoScroll = true;
            this.splitContainer.Panel2.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.splitContainer.Panel2.Controls.Add(this.tableOptions);
            this.splitContainer.Panel2.Font = new System.Drawing.Font("等距更纱黑体 SC", 10.2F);
            this.splitContainer.Size = new System.Drawing.Size(926, 427);
            this.splitContainer.SplitterDistance = 316;
            this.splitContainer.TabIndex = 0;
            //
            // tableMetaData
            //
            this.tableMetaData.ColumnCount = 3;
            this.tableMetaData.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableMetaData.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableMetaData.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33334F));
            this.tableMetaData.Controls.Add(this.lblInfo, 0, 3);
            this.tableMetaData.Controls.Add(this.lblPreset, 0, 0);
            this.tableMetaData.Controls.Add(this.lblAuthor, 0, 1);
            this.tableMetaData.Controls.Add(this.btnStart, 0, 5);
            this.tableMetaData.Controls.Add(this.btnRetry, 1, 5);
            this.tableMetaData.Controls.Add(this.btnEnd, 2, 5);
            this.tableMetaData.Controls.Add(this.lblLastChanged, 0, 2);
            this.tableMetaData.Controls.Add(this.rtbInfo, 0, 4);
            this.tableMetaData.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableMetaData.Font = new System.Drawing.Font("等距更纱黑体 SC", 10.2F);
            this.tableMetaData.Location = new System.Drawing.Point(0, 0);
            this.tableMetaData.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableMetaData.Name = "tableMetaData";
            this.tableMetaData.RowCount = 6;
            this.tableMetaData.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableMetaData.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableMetaData.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableMetaData.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableMetaData.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableMetaData.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableMetaData.Size = new System.Drawing.Size(316, 427);
            this.tableMetaData.TabIndex = 0;
            //
            // lblInfo
            //
            this.lblInfo.AutoSize = true;
            this.tableMetaData.SetColumnSpan(this.lblInfo, 3);
            this.lblInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblInfo.Location = new System.Drawing.Point(0, 106);
            this.lblInfo.Margin = new System.Windows.Forms.Padding(0);
            this.lblInfo.Name = "lblInfo";
            this.lblInfo.Padding = new System.Windows.Forms.Padding(5);
            this.lblInfo.Size = new System.Drawing.Size(316, 32);
            this.lblInfo.TabIndex = 10;
            this.lblInfo.Text = "说明：";
            //
            // lblPreset
            //
            this.lblPreset.AutoSize = true;
            this.tableMetaData.SetColumnSpan(this.lblPreset, 3);
            this.lblPreset.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPreset.Location = new System.Drawing.Point(0, 10);
            this.lblPreset.Margin = new System.Windows.Forms.Padding(0, 10, 0, 0);
            this.lblPreset.Name = "lblPreset";
            this.lblPreset.Padding = new System.Windows.Forms.Padding(5);
            this.lblPreset.Size = new System.Drawing.Size(316, 32);
            this.lblPreset.TabIndex = 0;
            this.lblPreset.Text = "预设：";
            //
            // lblAuthor
            //
            this.lblAuthor.AutoSize = true;
            this.tableMetaData.SetColumnSpan(this.lblAuthor, 3);
            this.lblAuthor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblAuthor.Location = new System.Drawing.Point(0, 42);
            this.lblAuthor.Margin = new System.Windows.Forms.Padding(0);
            this.lblAuthor.Name = "lblAuthor";
            this.lblAuthor.Padding = new System.Windows.Forms.Padding(5);
            this.lblAuthor.Size = new System.Drawing.Size(316, 32);
            this.lblAuthor.TabIndex = 1;
            this.lblAuthor.Text = "作者：";
            //
            // btnStart
            //
            this.btnStart.AutoSize = true;
            this.btnStart.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnStart.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(255)))), ((int)(((byte)(235)))));
            this.btnStart.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnStart.ForeColor = System.Drawing.Color.Black;
            this.btnStart.Location = new System.Drawing.Point(5, 377);
            this.btnStart.Margin = new System.Windows.Forms.Padding(5, 0, 5, 8);
            this.btnStart.Name = "btnStart";
            this.btnStart.Padding = new System.Windows.Forms.Padding(5);
            this.btnStart.Size = new System.Drawing.Size(95, 42);
            this.btnStart.TabIndex = 5;
            this.btnStart.Text = "开始模拟";
            this.btnStart.UseVisualStyleBackColor = false;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            //
            // btnRetry
            //
            this.btnRetry.AutoSize = true;
            this.btnRetry.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnRetry.BackColor = System.Drawing.Color.FromArgb(255, 242, 196);
            this.btnRetry.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnRetry.ForeColor = System.Drawing.Color.Black;
            this.btnRetry.Location = new System.Drawing.Point(110, 377);
            this.btnRetry.Margin = new System.Windows.Forms.Padding(5, 0, 5, 8);
            this.btnRetry.Name = "btnRetry";
            this.btnRetry.Padding = new System.Windows.Forms.Padding(5);
            this.btnRetry.Size = new System.Drawing.Size(95, 42);
            this.btnRetry.TabIndex = 6;
            this.btnRetry.Text = "重复上次";
            this.btnRetry.UseVisualStyleBackColor = false;
            this.btnRetry.Click += new System.EventHandler(this.btnRetry_Click);
            //
            // btnEnd
            //
            this.btnEnd.AutoSize = true;
            this.btnEnd.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnEnd.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(230)))), ((int)(((byte)(230)))));
            this.btnEnd.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnEnd.ForeColor = System.Drawing.Color.Black;
            this.btnEnd.Location = new System.Drawing.Point(215, 377);
            this.btnEnd.Margin = new System.Windows.Forms.Padding(5, 0, 5, 8);
            this.btnEnd.Name = "btnEnd";
            this.btnEnd.Padding = new System.Windows.Forms.Padding(5);
            this.btnEnd.Size = new System.Drawing.Size(96, 42);
            this.btnEnd.TabIndex = 7;
            this.btnEnd.Text = "停止模拟";
            this.btnEnd.UseVisualStyleBackColor = false;
            this.btnEnd.Click += new System.EventHandler(this.btnEnd_Click);
            //
            // lblLastChanged
            //
            this.lblLastChanged.AutoSize = true;
            this.tableMetaData.SetColumnSpan(this.lblLastChanged, 3);
            this.lblLastChanged.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLastChanged.Location = new System.Drawing.Point(0, 74);
            this.lblLastChanged.Margin = new System.Windows.Forms.Padding(0);
            this.lblLastChanged.Name = "lblLastChanged";
            this.lblLastChanged.Padding = new System.Windows.Forms.Padding(5);
            this.lblLastChanged.Size = new System.Drawing.Size(316, 32);
            this.lblLastChanged.TabIndex = 2;
            this.lblLastChanged.Text = "更新时间：";
            //
            // rtbInfo
            //
            this.tableMetaData.SetColumnSpan(this.rtbInfo, 3);
            this.rtbInfo.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.rtbInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtbInfo.Location = new System.Drawing.Point(8, 146);
            this.rtbInfo.Margin = new System.Windows.Forms.Padding(8);
            this.rtbInfo.Name = "rtbInfo";
            this.rtbInfo.ReadOnly = true;
            this.rtbInfo.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.rtbInfo.Size = new System.Drawing.Size(300, 223);
            this.rtbInfo.TabIndex = 11;
            this.rtbInfo.Text = resources.GetString("rtbInfo.Text");
            this.rtbInfo.ZoomFactor = 1.05F;
            //
            // tableOptions
            //
            this.tableOptions.ColumnCount = 2;
            this.tableOptions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableOptions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableOptions.Controls.Add(this.dummyLbl, 0, 0);
            this.tableOptions.Controls.Add(this.label6, 0, 4);
            this.tableOptions.Controls.Add(this.dummyLblTxt, 0, 2);
            this.tableOptions.Controls.Add(this.dummyLblChk, 0, 1);
            this.tableOptions.Controls.Add(this.dummyChk, 1, 1);
            this.tableOptions.Controls.Add(this.dummyTxt, 1, 2);
            this.tableOptions.Controls.Add(this.dummyCbx, 1, 3);
            this.tableOptions.Controls.Add(this.dummyNud, 1, 4);
            this.tableOptions.Controls.Add(this.dummyLblCbx, 0, 3);
            this.tableOptions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableOptions.Location = new System.Drawing.Point(0, 0);
            this.tableOptions.Name = "tableOptions";
            this.tableOptions.RowCount = 6;
            this.tableOptions.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableOptions.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableOptions.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableOptions.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableOptions.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableOptions.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableOptions.Size = new System.Drawing.Size(606, 427);
            this.tableOptions.TabIndex = 2;
            //
            // dummyLbl
            //
            this.dummyLbl.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.dummyLbl.AutoSize = true;
            this.tableOptions.SetColumnSpan(this.dummyLbl, 2);
            this.dummyLbl.Location = new System.Drawing.Point(0, 0);
            this.dummyLbl.Margin = new System.Windows.Forms.Padding(0);
            this.dummyLbl.Name = "dummyLbl";
            this.dummyLbl.Padding = new System.Windows.Forms.Padding(15, 10, 5, 10);
            this.dummyLbl.Size = new System.Drawing.Size(461, 64);
            this.dummyLbl.TabIndex = 11;
            this.dummyLbl.Text = "跨两列的完整 Label 示例，用于显示说明文本，文本可换行\r\n跨两列的完整 Label 示例，用于显示说明文本，文本可换行";
            //
            // label6
            //
            this.label6.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(0, 208);
            this.label6.Margin = new System.Windows.Forms.Padding(0);
            this.label6.Name = "label6";
            this.label6.Padding = new System.Windows.Forms.Padding(15, 10, 5, 10);
            this.label6.Size = new System.Drawing.Size(190, 42);
            this.label6.TabIndex = 6;
            this.label6.Text = "数值文本框的标签说明";
            //
            // dummyLblTxt
            //
            this.dummyLblTxt.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.dummyLblTxt.AutoSize = true;
            this.dummyLblTxt.Location = new System.Drawing.Point(0, 109);
            this.dummyLblTxt.Margin = new System.Windows.Forms.Padding(0);
            this.dummyLblTxt.Name = "dummyLblTxt";
            this.dummyLblTxt.Padding = new System.Windows.Forms.Padding(15, 10, 5, 10);
            this.dummyLblTxt.Size = new System.Drawing.Size(158, 42);
            this.dummyLblTxt.TabIndex = 2;
            this.dummyLblTxt.Text = "文本框的标签说明";
            //
            // dummyLblChk
            //
            this.dummyLblChk.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.dummyLblChk.AutoSize = true;
            this.dummyLblChk.Location = new System.Drawing.Point(0, 64);
            this.dummyLblChk.Margin = new System.Windows.Forms.Padding(0);
            this.dummyLblChk.Name = "dummyLblChk";
            this.dummyLblChk.Padding = new System.Windows.Forms.Padding(15, 10, 5, 10);
            this.dummyLblChk.Size = new System.Drawing.Size(158, 42);
            this.dummyLblChk.TabIndex = 0;
            this.dummyLblChk.Text = "选项框的标签说明";
            //
            // dummyChk
            //
            this.dummyChk.AutoSize = true;
            this.dummyChk.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dummyChk.Location = new System.Drawing.Point(190, 64);
            this.dummyChk.Margin = new System.Windows.Forms.Padding(0);
            this.dummyChk.Name = "dummyChk";
            this.dummyChk.Padding = new System.Windows.Forms.Padding(30, 10, 5, 10);
            this.dummyChk.Size = new System.Drawing.Size(416, 42);
            this.dummyChk.TabIndex = 7;
            this.dummyChk.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.dummyChk.UseVisualStyleBackColor = true;
            //
            // dummyTxt
            //
            this.dummyTxt.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dummyTxt.Location = new System.Drawing.Point(220, 116);
            this.dummyTxt.Margin = new System.Windows.Forms.Padding(30, 10, 20, 10);
            this.dummyTxt.Name = "dummyTxt";
            this.dummyTxt.Size = new System.Drawing.Size(366, 29);
            this.dummyTxt.TabIndex = 8;
            this.dummyTxt.Text = "示例文本";
            //
            // dummyCbx
            //
            this.dummyCbx.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dummyCbx.DropDownHeight = 200;
            this.dummyCbx.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.dummyCbx.FormattingEnabled = true;
            this.dummyCbx.IntegralHeight = false;
            this.dummyCbx.Items.AddRange(new object[]
            {
                "测试选项1",
                "测试选项2",
                "测试选项3"
            });
            this.dummyCbx.Location = new System.Drawing.Point(220, 165);
            this.dummyCbx.Margin = new System.Windows.Forms.Padding(30, 10, 20, 10);
            this.dummyCbx.Name = "dummyCbx";
            this.dummyCbx.Size = new System.Drawing.Size(366, 30);
            this.dummyCbx.TabIndex = 0;
            this.dummyCbx.TabStop = false;
            //
            // dummyNud
            //
            this.dummyNud.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dummyNud.Location = new System.Drawing.Point(220, 215);
            this.dummyNud.Margin = new System.Windows.Forms.Padding(30, 10, 20, 10);
            this.dummyNud.Name = "dummyNud";
            this.dummyNud.Size = new System.Drawing.Size(366, 29);
            this.dummyNud.TabIndex = 10;
            //
            // dummyLblCbx
            //
            this.dummyLblCbx.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.dummyLblCbx.AutoSize = true;
            this.dummyLblCbx.Location = new System.Drawing.Point(0, 159);
            this.dummyLblCbx.Margin = new System.Windows.Forms.Padding(0);
            this.dummyLblCbx.Name = "dummyLblCbx";
            this.dummyLblCbx.Padding = new System.Windows.Forms.Padding(15, 10, 5, 10);
            this.dummyLblCbx.Size = new System.Drawing.Size(190, 42);
            this.dummyLblCbx.TabIndex = 4;
            this.dummyLblCbx.Text = "下拉选项框的标签说明";
            //
            // toolTip
            //
            this.toolTip.AutoPopDelay = 60000;
            this.toolTip.InitialDelay = 500;
            this.toolTip.IsBalloon = true;
            this.toolTip.ReshowDelay = 100;
            this.toolTip.ToolTipIcon = System.Windows.Forms.ToolTipIcon.Info;
            this.toolTip.ToolTipTitle = "说明";
            //
            // PresetControl
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 22F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitContainer);
            this.Font = new System.Drawing.Font("等距更纱黑体 SC", 10.2F);
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "PresetControl";
            this.Size = new System.Drawing.Size(926, 427);
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.tableMetaData.ResumeLayout(false);
            this.tableMetaData.PerformLayout();
            this.tableOptions.ResumeLayout(false);
            this.tableOptions.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dummyNud)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.TableLayoutPanel tableMetaData;
        private System.Windows.Forms.Label lblPreset;
        private System.Windows.Forms.Label lblAuthor;
        private System.Windows.Forms.Label lblLastChanged;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnRetry;
        private System.Windows.Forms.Button btnEnd;
        private System.Windows.Forms.Label lblInfo;
        private System.Windows.Forms.RichTextBox rtbInfo;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label dummyLblTxt;
        private System.Windows.Forms.Label dummyLblChk;
        private System.Windows.Forms.CheckBox dummyChk;
        private System.Windows.Forms.TextBox dummyTxt;
        private System.Windows.Forms.ComboBox dummyCbx;
        private System.Windows.Forms.NumericUpDown dummyNud;
        private System.Windows.Forms.Label dummyLblCbx;
        private System.Windows.Forms.Label dummyLbl;
        private System.Windows.Forms.ToolTip toolTip;
        internal System.Windows.Forms.TableLayoutPanel tableOptions;
    }
}
