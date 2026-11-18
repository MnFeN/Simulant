using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Simulant.UI
{
    public class ScrollPanelEx : Panel
    {
        private Control _content;
        private bool _layingOut;
        private int _scrollTop;
        private int _scrollMax;
        private int? _thumbDragOffset;
        private int _wheelDelta;

        public ScrollPanelEx()
        {
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.ResizeRedraw, true);
        }

        private int BarWidth => Math.Max(12, LogicalToDeviceUnits(14));

        private Rectangle Track => _scrollMax > 0
            ? new Rectangle(Math.Max(0, ClientSize.Width - BarWidth), 0, Math.Min(BarWidth, ClientSize.Width), ClientSize.Height)
            : Rectangle.Empty;

        private Rectangle Thumb
        {
            get
            {
                var track = Track;
                if (track.IsEmpty)
                    return Rectangle.Empty;

                var height = Math.Min(track.Height, Math.Max(LogicalToDeviceUnits(22),
                    (int)((long)track.Height * track.Height / (track.Height + _scrollMax))));
                var y = (int)((long)(track.Height - height) * _scrollTop / _scrollMax);
                return new Rectangle(track.X + 3, y, Math.Max(1, track.Width - 6), height);
            }
        }

        internal void SetContent(Control content)
        {
            if (_content != null)
                ObserveFocus(_content, false);

            _content = content;
            if (content != null)
                ObserveFocus(content, true);

            ResetScroll();
        }

        internal void ResetScroll()
        {
            _scrollTop = _wheelDelta = 0;
            PerformLayout();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            if (_layingOut)
                return;

            _layingOut = true;
            try
            {
                base.OnLayout(levent);
                if (_content == null)
                    return;

                var width = ClientSize.Width;
                var height = _content.GetPreferredSize(new Size(width, 0)).Height;
                if (height > ClientSize.Height)
                {
                    width = Math.Max(0, width - BarWidth);
                    height = Math.Max(height, _content.GetPreferredSize(new Size(width, 0)).Height);
                }

                _scrollMax = Math.Max(0, height - ClientSize.Height);
                _scrollTop = Math.Min(_scrollTop, _scrollMax);
                _content.Bounds = new Rectangle(0, -_scrollTop, width, height);
                Invalidate();
            }
            finally
            {
                _layingOut = false;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var thumb = Thumb;
            if (thumb.Width <= 0 || thumb.Height <= 0)
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var active = _thumbDragOffset.HasValue || Track.Contains(PointToClient(MousePosition));
            using (var path = WorkbenchTheme.RoundedRect(thumb, thumb.Width / 2F))
            using (var brush = new SolidBrush(active ? WorkbenchTheme.Accent : WorkbenchTheme.AccentOutline))
                e.Graphics.FillPath(brush, path);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !Track.Contains(e.Location))
                return;

            var thumb = Thumb;
            if (e.Y >= thumb.Top && e.Y < thumb.Bottom)
            {
                _thumbDragOffset = e.Y - thumb.Top;
                Capture = true;
                Invalidate(Track);
            }
            else
                ScrollTo(_scrollTop + (e.Y < thumb.Top ? -ClientSize.Height : ClientSize.Height));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_thumbDragOffset.HasValue)
            {
                var travel = ClientSize.Height - Thumb.Height;
                if (travel > 0)
                    ScrollTo((int)((long)Math.Max(0, Math.Min(travel, e.Y - _thumbDragOffset.Value)) * _scrollMax / travel));
            }

            Invalidate(Track);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _thumbDragOffset.HasValue)
                Capture = false;

            base.OnMouseUp(e);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture)
            {
                _thumbDragOffset = null;
                Invalidate(Track);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Invalidate(Track);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (_scrollMax == 0)
            {
                base.OnMouseWheel(e);
                return;
            }

            if (e is HandledMouseEventArgs handled)
                handled.Handled = true;

            _wheelDelta += e.Delta;
            var steps = _wheelDelta / SystemInformation.MouseWheelScrollDelta;
            _wheelDelta %= SystemInformation.MouseWheelScrollDelta;
            var lines = SystemInformation.MouseWheelScrollLines;
            var stepSize = lines < 0 ? ClientSize.Height : lines * Font.Height;
            ScrollTo(_scrollTop - steps * stepSize);
        }

        private void ScrollTo(int offset)
        {
            offset = Math.Max(0, Math.Min(_scrollMax, offset));
            if (_scrollTop == offset)
                return;

            _scrollTop = offset;
            _layingOut = true;
            try
            {
                _content.Top = -offset;
            }
            finally
            {
                _layingOut = false;
            }

            Invalidate(Track);
        }

        private void ObserveFocus(Control control, bool attach)
        {
            if (attach)
            {
                control.Enter += RevealFocusedControl;
                control.ControlAdded += ChildAdded;
                control.ControlRemoved += ChildRemoved;
            }
            else
            {
                control.Enter -= RevealFocusedControl;
                control.ControlAdded -= ChildAdded;
                control.ControlRemoved -= ChildRemoved;
            }

            foreach (Control child in control.Controls)
                ObserveFocus(child, attach);
        }

        private void ChildAdded(object sender, ControlEventArgs e)
            => ObserveFocus(e.Control, true);

        private void ChildRemoved(object sender, ControlEventArgs e)
            => ObserveFocus(e.Control, false);

        private void RevealFocusedControl(object sender, EventArgs e)
        {
            var control = (Control)sender;
            if (!control.CanSelect || !control.TabStop)
                return;

            var bounds = RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
            if (bounds.Top < 0)
                ScrollTo(_scrollTop + bounds.Top);
            else if (bounds.Bottom > ClientSize.Height)
                ScrollTo(_scrollTop + bounds.Bottom - ClientSize.Height);
        }
    }
}
