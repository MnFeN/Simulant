using System;
using System.Drawing;
using System.Windows.Forms;

namespace Simulant.UI
{
    public class RichTextBoxEx : RichTextBox
    {
        public RichTextBoxEx()
        {
            BackColor = WorkbenchTheme.Surface;
            ForeColor = WorkbenchTheme.Muted;
            BorderStyle = BorderStyle.None;
            ScrollBarTheme.Attach(this);
        }
    }

    public class ListBoxEx : ListBox
    {
        public ListBoxEx()
        {
            BackColor = WorkbenchTheme.Field;
            ForeColor = WorkbenchTheme.Text;
            BorderStyle = BorderStyle.FixedSingle;
            ScrollBarTheme.Attach(this);
        }
    }

    public class TabPageEx : TabPage
    {
        public TabPageEx()
        {
            UseVisualStyleBackColor = false;
            BackColor = WorkbenchTheme.Surface;
            ForeColor = WorkbenchTheme.Text;
            ScrollBarTheme.Attach(this);
        }
    }

    public class TabControlEx : TabControl
    {
        public TabControlEx()
        {
            DrawMode = TabDrawMode.OwnerDrawFixed;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(WorkbenchTheme.Canvas);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var page = DisplayRectangle;
            if (TabCount > 0 && page.Width > 0 && page.Height > 0)
            {
                page.Inflate(1, 1);
                using (var border = new Pen(WorkbenchTheme.Stroke))
                    e.Graphics.DrawRectangle(border, page);
            }

            for (var i = 0; i < TabCount; i++)
            {
                var state = i == SelectedIndex ? DrawItemState.Selected : DrawItemState.None;
                OnDrawItem(new DrawItemEventArgs(e.Graphics, Font, GetTabRect(i), i, state));
            }

            base.OnPaint(e);
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            Invalidate();
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            var selected = (e.State & DrawItemState.Selected) != 0;
            using (var fill = new SolidBrush(selected ? WorkbenchTheme.Surface : WorkbenchTheme.Field))
                e.Graphics.FillRectangle(fill, e.Bounds);
            if (e.Index >= 0 && e.Index < TabCount)
                TextRenderer.DrawText(e.Graphics, TabPages[e.Index].Text, Font, e.Bounds,
                    selected ? WorkbenchTheme.Accent : WorkbenchTheme.Muted, TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            base.OnDrawItem(e);
        }
    }
}
