using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Simulant.UI
{
    public class SplitContainerEx : SplitContainer
    {
        private DragLine _line;
        private int _dragOffset;
        private int _pendingDistance;

        public SplitContainerEx()
        {
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            ScrollBarTheme.Attach(Panel1);
            ScrollBarTheme.Attach(Panel2);
        }

        private bool Vertical => Orientation == Orientation.Vertical;
        private bool Mirrored => Vertical && RightToLeft == RightToLeft.Yes;
        private bool CanDrag => !IsSplitterFixed && !Panel1Collapsed && !Panel2Collapsed;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            var splitter = SplitterRectangle;
            if (CanDrag && e.Button == MouseButtons.Left && splitter.Contains(e.Location))
            {
                _dragOffset = Vertical ? e.X - splitter.Left : e.Y - splitter.Top;
                _pendingDistance = SplitterDistance;
                _line = new DragLine(Handle, RectangleToScreen(PreviewBounds(splitter)));
                Capture = true;
                return;
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_line != null)
            {
                MovePreview(e);
                return;
            }

            base.OnMouseMove(e);
            Invalidate(SplitterRectangle);
        }

        private void MovePreview(MouseEventArgs e)
        {
            var size = Vertical ? ClientSize.Width : ClientSize.Height;
            var max = size - SplitterWidth - Panel2MinSize;
            if (!CanDrag || max < Panel1MinSize)
                return;

            var position = (Vertical ? e.X : e.Y) - _dragOffset;
            var distance = Mirrored ? size - position - SplitterWidth : position;
            var increment = SplitterIncrement;
            distance = SplitterDistance + (int)(((long)distance - SplitterDistance) / increment * increment);
            distance = Math.Max(Panel1MinSize, Math.Min(max, distance));
            if (distance == _pendingDistance)
                return;

            position = Mirrored ? size - distance - SplitterWidth : distance;
            var moving = new SplitterCancelEventArgs(e.X, e.Y, Vertical ? position : 0, Vertical ? 0 : position);
            OnSplitterMoving(moving);
            if (moving.Cancel || _line == null)
                return;

            _pendingDistance = distance;
            var bounds = SplitterRectangle;
            if (Vertical)
                bounds.X = position;
            else
                bounds.Y = position;
            _line.Move(RectangleToScreen(PreviewBounds(bounds)));
        }

        private Rectangle PreviewBounds(Rectangle splitter)
            => Vertical
                ? new Rectangle(splitter.Left + splitter.Width / 2, splitter.Top, 2, splitter.Height)
                : new Rectangle(splitter.Left, splitter.Top + splitter.Height / 2, splitter.Width, 2);

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_line != null && e.Button == MouseButtons.Left)
            {
                EndDrag(true);
                return;
            }

            base.OnMouseUp(e);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (_line != null && !Capture)
                EndDrag(false);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (_line != null)
                EndDrag(false);

            base.OnHandleDestroyed(e);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            if (_line != null && !Visible)
                EndDrag(false);
            base.OnVisibleChanged(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Invalidate(SplitterRectangle);
        }

        private void EndDrag(bool commit)
        {
            var line = _line;
            _line = null;
            if (line.Handle != IntPtr.Zero)
                line.DestroyHandle();
            Capture = false;
            if (commit && CanDrag && _pendingDistance != SplitterDistance)
                SplitterDistance = _pendingDistance;

            Invalidate(true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var bar = SplitterRectangle;
            if (CanDrag && _line == null && bar.Contains(PointToClient(MousePosition)))
                using (var pen = new SolidBrush(WorkbenchTheme.AccentOutline))
                    e.Graphics.FillRectangle(pen, PreviewBounds(bar));
        }

        // Windows composites the preview separately, so moving it leaves the panels' painted surfaces intact.
        private sealed class DragLine : NativeWindow
        {
            private const int WmPaint = 0x000F;
            private const uint GaRoot = 2;
            private const uint LwaAlpha = 2;
            private const uint SwpNoSize = 0x0001;
            private const uint SwpNoZOrder = 0x0004;
            private const uint SwpNoActivate = 0x0010;
            private const uint SwpShowWindow = 0x0040;
            private Rectangle _bounds;

            internal DragLine(IntPtr splitter, Rectangle bounds)
            {
                _bounds = bounds;
                CreateHandle(new CreateParams
                {
                    Parent = GetAncestor(splitter, GaRoot),
                    X = bounds.X,
                    Y = bounds.Y,
                    Width = bounds.Width,
                    Height = bounds.Height,
                    // WS_POPUP | WS_DISABLED
                    Style = unchecked((int)0x88000000),
                    // WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT
                    ExStyle = 0x080800A0
                });
                if (!SetLayeredWindowAttributes(Handle, 0, 255, LwaAlpha)
                    || !SetWindowPos(Handle, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height, SwpNoActivate | SwpShowWindow))
                    DestroyHandle();
            }

            internal void Move(Rectangle bounds)
            {
                if (Handle == IntPtr.Zero || _bounds == bounds)
                    return;

                var flags = SwpNoActivate | SwpNoZOrder;
                if (bounds.Size == _bounds.Size)
                    flags |= SwpNoSize;
                _bounds = bounds;
                SetWindowPos(Handle, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height, flags);
            }

            protected override void WndProc(ref Message message)
            {
                if (message.Msg != WmPaint)
                {
                    base.WndProc(ref message);
                    return;
                }

                var dc = BeginPaint(Handle, out var paint);
                try
                {
                    if (dc != IntPtr.Zero)
                        using (var graphics = Graphics.FromHdc(dc))
                        using (var brush = new SolidBrush(WorkbenchTheme.Accent))
                            graphics.FillRectangle(brush, 0, 0, _bounds.Width, _bounds.Height);
                }
                finally
                {
                    EndPaint(Handle, ref paint);
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            private unsafe struct PaintStruct
            {
                public IntPtr Dc;
                public int Erase, Left, Top, Right, Bottom, Restore, IncUpdate;
                public fixed byte Reserved[32];
            }

            [DllImport("user32.dll")]
            private static extern IntPtr BeginPaint(IntPtr window, out PaintStruct paint);

            [DllImport("user32.dll")]
            private static extern bool EndPaint(IntPtr window, ref PaintStruct paint);

            [DllImport("user32.dll")]
            private static extern IntPtr GetAncestor(IntPtr window, uint flags);
            [DllImport("user32.dll")]
            private static extern bool SetLayeredWindowAttributes(IntPtr window, uint key, byte alpha, uint flags);
            [DllImport("user32.dll")]
            private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        }
    }
}
