using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Simulant.UI
{
    public class ComboBoxEx : ComboBox
    {
        private ScrollBarTheme _listScrollBar;

        public ComboBoxEx()
        {
            BackColor = WorkbenchTheme.Field;
            ForeColor = WorkbenchTheme.Text;
            FlatStyle = FlatStyle.Flat;
            DrawMode = DrawMode.OwnerDrawFixed;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            UpdateItemHeight();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _listScrollBar = ScrollBarTheme.AttachComboList(Handle);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _listScrollBar?.Detach();
            _listScrollBar = null;
            base.OnHandleDestroyed(e);
        }

        protected override void OnDropDown(EventArgs e)
        {
            if (_listScrollBar == null || _listScrollBar.Handle == IntPtr.Zero)
                _listScrollBar = ScrollBarTheme.AttachComboList(Handle);

            base.OnDropDown(e);
        }

        protected override void OnDropDownStyleChanged(EventArgs e)
        {
            SetStyle(ControlStyles.UserPaint, DropDownStyle == ComboBoxStyle.DropDownList);
            base.OnDropDownStyleChanged(e);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            UpdateItemHeight();
        }

        private void UpdateItemHeight()
        {
            if (DrawMode == DrawMode.OwnerDrawFixed)
                ItemHeight = TextRenderer.MeasureText("国", Font).Height + 6;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            var selected = (e.State & DrawItemState.Selected) != 0;
            var editor = (e.State & DrawItemState.ComboBoxEdit) != 0;
            using (var fill = new SolidBrush(selected && !editor ? WorkbenchTheme.Selection : BackColor))
                e.Graphics.FillRectangle(fill, e.Bounds);

            if (e.Index >= 0 && e.Index < Items.Count)
            {
                var bounds = e.Bounds;
                bounds.Inflate(-8, 0);
                TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]).TrimStart(), Font, bounds,
                    Enabled ? ForeColor : WorkbenchTheme.Dim, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }

            base.OnDrawItem(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? WorkbenchTheme.Surface);
            if (Width < 2 || Height < 2)
                return;

            var hovered = Enabled && ClientRectangle.Contains(PointToClient(MousePosition));
            var border = Enabled && (DroppedDown || Focused && ShowFocusCues) ? WorkbenchTheme.AccentOutline : WorkbenchTheme.Stroke;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = WorkbenchTheme.RoundedRect(new RectangleF(0.5F, 0.5F, Width - 1, Height - 1), 7))
            using (var fill = new SolidBrush(hovered ? WorkbenchTheme.Hover : BackColor))
            using (var outline = new Pen(border))
            using (var arrow = new Pen(Enabled ? WorkbenchTheme.Muted : WorkbenchTheme.Dim, 1.3F)
            {
                LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round
            })
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(outline, path);
                var x = Width - 13;
                var y = Height / 2F;
                e.Graphics.DrawLines(arrow, new[] { new PointF(x - 3.5F, y - 1.5F),
                    new PointF(x, y + 2F), new PointF(x + 3.5F, y - 1.5F) });
            }

            TextRenderer.DrawText(e.Graphics, GetItemText(SelectedItem).TrimStart(), Font,
                new Rectangle(9, 0, Math.Max(0, Width - 30), Height), Enabled ? ForeColor : WorkbenchTheme.Dim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            Invalidate();
        }

        protected override void OnDropDownClosed(EventArgs e)
        {
            base.OnDropDownClosed(e);
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Invalidate();
        }
    }
}
