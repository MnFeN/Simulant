namespace Simulant.UI
{
    partial class SimpleInputForm
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
            this.tableMain = new System.Windows.Forms.TableLayoutPanel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.txtInput = new System.Windows.Forms.TextBox();
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.lblDesc = new System.Windows.Forms.Label();
            this.tableMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableMain
            // 
            this.tableMain.AutoSize = true;
            this.tableMain.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableMain.ColumnCount = 2;
            this.tableMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableMain.Controls.Add(this.btnCancel, 1, 2);
            this.tableMain.Controls.Add(this.btnOk, 0, 2);
            this.tableMain.Controls.Add(this.txtInput, 0, 1);
            this.tableMain.Controls.Add(this.lblDesc, 0, 0);
            this.tableMain.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableMain.Location = new System.Drawing.Point(0, 0);
            this.tableMain.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableMain.MinimumSize = new System.Drawing.Size(300, 100);
            this.tableMain.Name = "tableMain";
            this.tableMain.RowCount = 3;
            this.tableMain.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableMain.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableMain.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableMain.Size = new System.Drawing.Size(411, 168);
            this.tableMain.TabIndex = 0;
            // 
            // btnCancel
            // 
            this.btnCancel.AutoSize = true;
            this.btnCancel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnCancel.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnCancel.Location = new System.Drawing.Point(218, 114);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(13);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Padding = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.btnCancel.Size = new System.Drawing.Size(180, 41);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.TabStop = false;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnOk
            // 
            this.btnOk.AutoSize = true;
            this.btnOk.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnOk.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnOk.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnOk.Location = new System.Drawing.Point(13, 114);
            this.btnOk.Margin = new System.Windows.Forms.Padding(13);
            this.btnOk.Name = "btnOk";
            this.btnOk.Padding = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.btnOk.Size = new System.Drawing.Size(179, 41);
            this.btnOk.TabIndex = 1;
            this.btnOk.TabStop = false;
            this.btnOk.Text = "确认";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // txtInput
            // 
            this.tableMain.SetColumnSpan(this.txtInput, 2);
            this.txtInput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtInput.Location = new System.Drawing.Point(15, 60);
            this.txtInput.Margin = new System.Windows.Forms.Padding(15);
            this.txtInput.MinimumSize = new System.Drawing.Size(300, 0);
            this.txtInput.Name = "txtInput";
            this.txtInput.Size = new System.Drawing.Size(381, 26);
            this.txtInput.TabIndex = 3;
            // 
            // lblDesc
            // 
            this.lblDesc.AutoSize = true;
            this.tableMain.SetColumnSpan(this.lblDesc, 2);
            this.lblDesc.Location = new System.Drawing.Point(13, 13);
            this.lblDesc.Margin = new System.Windows.Forms.Padding(13);
            this.lblDesc.Name = "lblDesc";
            this.lblDesc.Size = new System.Drawing.Size(37, 19);
            this.lblDesc.TabIndex = 4;
            this.lblDesc.Text = "描述";
            // 
            // SimpleInputForm
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 19F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(411, 170);
            this.Controls.Add(this.tableMain);
            this.Font = new System.Drawing.Font("等距更纱黑体 SC", 8.999999F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "SimpleInputForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "标题";
            this.tableMain.ResumeLayout(false);
            this.tableMain.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableMain;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private System.Windows.Forms.TextBox txtInput;
        private System.Windows.Forms.Label lblDesc;
    }
}