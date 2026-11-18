using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Simulant.UI
{
    partial class PresetControl
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
            this.toolTip = new ToolTip(this.components);
            this.presetCard = new PanelEx();
            this.layout = new SplitContainerEx();
            this.overview = new TableLayoutPanel();
            this.overviewHeader = new TableLayoutPanel();
            this.lblCurrentPreset = new LabelEx();
            this.lblRunState = new LabelEx();
            this.lblPreset = new LabelEx();
            this.metadata = new TableLayoutPanel();
            this.lblAuthor = new LabelEx();
            this.lblLastChanged = new LabelEx();
            this.rtbInfo = new RichTextBoxEx();
            this.actions = new TableLayoutPanel();
            this.btnStart = new ButtonEx();
            this.btnRetry = new ButtonEx();
            this.btnEnd = new ButtonEx();
            this.optionsCard = new PanelEx();
            this.optionsLayout = new TableLayoutPanel();
            this.lblOptionsHeading = new LabelEx();
            this.optionsScroll = new ScrollPanelEx();
            this.optionsContent = new TableLayoutPanel();
            this.tableOptions = new TableLayoutPanel();
            this.lblNoOptions = new LabelEx();
            this.presetCard.SuspendLayout();
            ((ISupportInitialize)this.layout).BeginInit();
            this.layout.Panel1.SuspendLayout();
            this.layout.Panel2.SuspendLayout();
            this.layout.SuspendLayout();
            this.overview.SuspendLayout();
            this.overviewHeader.SuspendLayout();
            this.metadata.SuspendLayout();
            this.actions.SuspendLayout();
            this.optionsCard.SuspendLayout();
            this.optionsLayout.SuspendLayout();
            this.optionsScroll.SuspendLayout();
            this.optionsContent.SuspendLayout();
            this.tableOptions.SuspendLayout();
            this.SuspendLayout();
            //
            // toolTip
            //
            //
            // presetCard
            //
            this.presetCard.Dock = DockStyle.Fill;
            this.presetCard.Margin = Padding.Empty;
            this.presetCard.Padding = new Padding(18, 15, 14, 14);
            this.presetCard.Name = "presetCard";
            this.presetCard.Controls.Add(this.layout);
            //
            // layout
            //
            this.layout.Dock = DockStyle.Fill;
            this.layout.Margin = Padding.Empty;
            this.layout.Name = "layout";
            this.layout.Size = new Size(808, 271);
            this.layout.SplitterWidth = 9;
            this.layout.SplitterDistance = 378;
            this.layout.Panel1MinSize = 120;
            this.layout.Panel2MinSize = 140;
            this.layout.TabStop = false;
            this.layout.Panel1.Controls.Add(this.overview);
            this.layout.Panel2.Controls.Add(this.optionsCard);
            //
            // overview
            //
            this.overview.ColumnCount = 1;
            this.overview.RowCount = 5;
            this.overview.Dock = DockStyle.Fill;
            this.overview.Margin = Padding.Empty;
            this.overview.Name = "overview";
            this.overview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.overview.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            this.overview.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            this.overview.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            this.overview.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.overview.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            this.overview.Controls.Add(this.overviewHeader, 0, 0);
            this.overview.Controls.Add(this.lblPreset, 0, 1);
            this.overview.Controls.Add(this.metadata, 0, 2);
            this.overview.Controls.Add(this.rtbInfo, 0, 3);
            this.overview.Controls.Add(this.actions, 0, 4);
            //
            // overviewHeader
            //
            this.overviewHeader.ColumnCount = 2;
            this.overviewHeader.RowCount = 1;
            this.overviewHeader.Dock = DockStyle.Fill;
            this.overviewHeader.Margin = Padding.Empty;
            this.overviewHeader.Name = "overviewHeader";
            this.overviewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            this.overviewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            this.overviewHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.overviewHeader.Controls.Add(this.lblCurrentPreset, 0, 0);
            this.overviewHeader.Controls.Add(this.lblRunState, 1, 0);
            //
            // lblCurrentPreset
            //
            this.lblCurrentPreset.Text = "当前模拟";
            this.lblCurrentPreset.Dock = DockStyle.Fill;
            this.lblCurrentPreset.Margin = Padding.Empty;
            this.lblCurrentPreset.TextAlign = ContentAlignment.MiddleLeft;
            this.lblCurrentPreset.AutoEllipsis = true;
            this.lblCurrentPreset.Tone = LabelTone.Muted;
            this.lblCurrentPreset.Name = "lblCurrentPreset";
            //
            // lblRunState
            //
            this.lblRunState.Text = "";
            this.lblRunState.Dock = DockStyle.Fill;
            this.lblRunState.Margin = Padding.Empty;
            this.lblRunState.TextAlign = ContentAlignment.MiddleRight;
            this.lblRunState.AutoEllipsis = true;
            this.lblRunState.Tone = LabelTone.Muted;
            this.lblRunState.Name = "lblRunState";
            //
            // lblPreset
            //
            this.lblPreset.Text = "选择模拟";
            this.lblPreset.Dock = DockStyle.Fill;
            this.lblPreset.Margin = Padding.Empty;
            this.lblPreset.TextAlign = ContentAlignment.MiddleLeft;
            this.lblPreset.AutoEllipsis = true;
            this.lblPreset.Tone = LabelTone.PresetTitle;
            this.lblPreset.Name = "lblPreset";
            //
            // metadata
            //
            this.metadata.ColumnCount = 2;
            this.metadata.RowCount = 1;
            this.metadata.Dock = DockStyle.Fill;
            this.metadata.Margin = Padding.Empty;
            this.metadata.Name = "metadata";
            this.metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));
            this.metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56F));
            this.metadata.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.metadata.Controls.Add(this.lblAuthor, 0, 0);
            this.metadata.Controls.Add(this.lblLastChanged, 1, 0);
            //
            // lblAuthor
            //
            this.lblAuthor.Text = "";
            this.lblAuthor.Dock = DockStyle.Fill;
            this.lblAuthor.Margin = Padding.Empty;
            this.lblAuthor.TextAlign = ContentAlignment.MiddleLeft;
            this.lblAuthor.AutoEllipsis = true;
            this.lblAuthor.Tone = LabelTone.Muted;
            this.lblAuthor.Name = "lblAuthor";
            //
            // lblLastChanged
            //
            this.lblLastChanged.Text = "";
            this.lblLastChanged.Dock = DockStyle.Fill;
            this.lblLastChanged.Margin = Padding.Empty;
            this.lblLastChanged.TextAlign = ContentAlignment.MiddleRight;
            this.lblLastChanged.AutoEllipsis = true;
            this.lblLastChanged.Tone = LabelTone.Muted;
            this.lblLastChanged.Name = "lblLastChanged";
            //
            // rtbInfo
            //
            this.rtbInfo.AccessibleName = "预设说明";
            this.rtbInfo.Dock = DockStyle.Fill;
            this.rtbInfo.Margin = new Padding(0, 9, 0, 10);
            this.rtbInfo.ReadOnly = true;
            this.rtbInfo.DetectUrls = false;
            this.rtbInfo.ScrollBars = RichTextBoxScrollBars.Vertical;
            this.rtbInfo.Name = "rtbInfo";
            //
            // actions
            //
            this.actions.ColumnCount = 3;
            this.actions.RowCount = 1;
            this.actions.Dock = DockStyle.Fill;
            this.actions.Margin = Padding.Empty;
            this.actions.Name = "actions";
            this.actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 1F));
            this.actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 1F));
            this.actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 1F));
            this.actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.actions.Controls.Add(this.btnStart, 0, 0);
            this.actions.Controls.Add(this.btnRetry, 1, 0);
            this.actions.Controls.Add(this.btnEnd, 2, 0);
            //
            // btnStart
            //
            this.btnStart.Text = "开始模拟";
            this.btnStart.Dock = DockStyle.Fill;
            this.btnStart.Margin = new Padding(0, 0, 7, 0);
            this.btnStart.Tone = ButtonTone.Start;
            this.btnStart.TabIndex = 0;
            this.btnStart.Name = "btnStart";
            this.btnStart.Click += new EventHandler(this.btnStart_Click);
            //
            // btnRetry
            //
            this.btnRetry.Text = "重复上场";
            this.btnRetry.Dock = DockStyle.Fill;
            this.btnRetry.Margin = new Padding(0, 0, 7, 0);
            this.btnRetry.Tone = ButtonTone.Repeat;
            this.btnRetry.TabIndex = 1;
            this.btnRetry.Name = "btnRetry";
            this.btnRetry.Click += new EventHandler(this.btnRetry_Click);
            //
            // btnEnd
            //
            this.btnEnd.Text = "停止模拟";
            this.btnEnd.Dock = DockStyle.Fill;
            this.btnEnd.Margin = Padding.Empty;
            this.btnEnd.Tone = ButtonTone.Danger;
            this.btnEnd.TabIndex = 2;
            this.btnEnd.Name = "btnEnd";
            this.btnEnd.Click += new EventHandler(this.btnEnd_Click);
            //
            // optionsCard
            //
            this.optionsCard.Dock = DockStyle.Fill;
            this.optionsCard.Margin = Padding.Empty;
            this.optionsCard.Padding = new Padding(14, 8, 14, 8);
            this.optionsCard.Tone = PanelTone.Inset;
            this.optionsCard.Name = "optionsCard";
            this.optionsCard.Controls.Add(this.optionsLayout);
            //
            // optionsLayout
            //
            this.optionsLayout.ColumnCount = 1;
            this.optionsLayout.RowCount = 2;
            this.optionsLayout.Dock = DockStyle.Fill;
            this.optionsLayout.Margin = Padding.Empty;
            this.optionsLayout.Name = "optionsLayout";
            this.optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.optionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 33F));
            this.optionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.optionsLayout.Controls.Add(this.lblOptionsHeading, 0, 0);
            this.optionsLayout.Controls.Add(this.optionsScroll, 0, 1);
            //
            // lblOptionsHeading
            //
            this.lblOptionsHeading.Text = "模拟参数";
            this.lblOptionsHeading.Dock = DockStyle.Fill;
            this.lblOptionsHeading.Margin = Padding.Empty;
            this.lblOptionsHeading.TextAlign = ContentAlignment.MiddleLeft;
            this.lblOptionsHeading.AutoEllipsis = true;
            this.lblOptionsHeading.Tone = LabelTone.Heading;
            this.lblOptionsHeading.Name = "lblOptionsHeading";
            //
            // optionsScroll
            //
            this.optionsScroll.Dock = DockStyle.Fill;
            this.optionsScroll.Margin = Padding.Empty;
            this.optionsScroll.Name = "optionsScroll";
            this.optionsScroll.Controls.Add(this.optionsContent);
            //
            // optionsContent
            //
            this.optionsContent.ColumnCount = 1;
            this.optionsContent.RowCount = 2;
            this.optionsContent.Margin = Padding.Empty;
            this.optionsContent.Name = "optionsContent";
            this.optionsContent.Size = new Size(320, 39);
            this.optionsContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.optionsContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.optionsContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.optionsContent.Controls.Add(this.lblNoOptions, 0, 0);
            this.optionsContent.Controls.Add(this.tableOptions, 0, 1);
            //
            // tableOptions
            //
            this.tableOptions.ColumnCount = 2;
            this.tableOptions.RowCount = 1;
            this.tableOptions.Dock = DockStyle.Top;
            this.tableOptions.Margin = Padding.Empty;
            this.tableOptions.AutoSize = true;
            this.tableOptions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.tableOptions.Name = "tableOptions";
            this.tableOptions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            this.tableOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.tableOptions.RowStyles.Add(new RowStyle(SizeType.Absolute, 5F));
            //
            // lblNoOptions
            //
            this.lblNoOptions.Text = "选择模拟后可调整参数";
            this.lblNoOptions.Dock = DockStyle.Top;
            this.lblNoOptions.Margin = Padding.Empty;
            this.lblNoOptions.TextAlign = ContentAlignment.MiddleLeft;
            this.lblNoOptions.AutoEllipsis = true;
            this.lblNoOptions.Tone = LabelTone.Caption;
            this.lblNoOptions.Size = new Size(100, 34);
            this.lblNoOptions.Name = "lblNoOptions";
            //
            // PresetControl
            //
            this.AutoScaleMode = AutoScaleMode.Inherit;
            this.Controls.Add(this.presetCard);
            this.Size = new Size(840, 300);
            this.Name = "PresetControl";
            this.tableOptions.ResumeLayout(false);
            this.optionsContent.ResumeLayout(false);
            this.optionsScroll.ResumeLayout(false);
            this.optionsLayout.ResumeLayout(false);
            this.optionsCard.ResumeLayout(false);
            this.actions.ResumeLayout(false);
            this.metadata.ResumeLayout(false);
            this.overviewHeader.ResumeLayout(false);
            this.overview.ResumeLayout(false);
            this.layout.Panel1.ResumeLayout(false);
            this.layout.Panel2.ResumeLayout(false);
            ((ISupportInitialize)this.layout).EndInit();
            this.layout.ResumeLayout(false);
            this.presetCard.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private ToolTip toolTip;
        private PanelEx presetCard;
        private SplitContainerEx layout;
        private TableLayoutPanel overview;
        private TableLayoutPanel overviewHeader;
        private LabelEx lblCurrentPreset;
        private LabelEx lblRunState;
        private LabelEx lblPreset;
        private TableLayoutPanel metadata;
        private LabelEx lblAuthor;
        private LabelEx lblLastChanged;
        private RichTextBoxEx rtbInfo;
        private TableLayoutPanel actions;
        private ButtonEx btnStart;
        private ButtonEx btnRetry;
        private ButtonEx btnEnd;
        private PanelEx optionsCard;
        private TableLayoutPanel optionsLayout;
        private LabelEx lblOptionsHeading;
        private ScrollPanelEx optionsScroll;
        private TableLayoutPanel optionsContent;
        private TableLayoutPanel tableOptions;
        private LabelEx lblNoOptions;
    }
}
