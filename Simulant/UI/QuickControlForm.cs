using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Simulant.UI
{
    /// <summary>模拟副本内的快捷控制；单击执行，拖动移动，边缘缩放。</summary>
    internal sealed class QuickControlForm : FormEx
    {
        private static readonly long FocusLossDelayTicks = Stopwatch.Frequency / 4;
        private static readonly IntPtr TopmostWindow = new IntPtr(-1);
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoActivate = 0x0010;

        private enum ButtonId
        {
            Start,
            Retry,
            Stop
        }

        private static readonly ButtonId[] SessionRow = { ButtonId.Start, ButtonId.Retry, ButtonId.Stop };
        private readonly Timer _visibilityTimer;
        private readonly Dictionary<ButtonId, (Action Execute, Color Accent)> _sessionButtons;
        private readonly HashSet<ButtonId> _enabled = new HashSet<ButtonId>();
        private ButtonId? _hover;
        private ButtonId? _pressed;
        private Point _mouseOrigin;
        private Rectangle _originalBounds;
        private AnchorStyles _resizeEdges;
        private bool _dragging;
        private bool _updatingLimits;
        private long? _focusLostAt;
        internal event Action<Point> LocationCommitted;
        internal Func<bool> IsGameForeground { get; set; }

        internal QuickControlForm(Action start, Action retry, Action stop)
        {
            _sessionButtons = new Dictionary<ButtonId, (Action Execute, Color Accent)>
            {
                [ButtonId.Start] = (start, WorkbenchTheme.Mint),
                [ButtonId.Retry] = (retry, WorkbenchTheme.Yellow),
                [ButtonId.Stop] = (stop, WorkbenchTheme.Red)
            };
            _visibilityTimer = new Timer { Interval = 33 };
            _visibilityTimer.Tick += (_, __) => UpdateVisibility();
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            Opacity = 0.88;
            StartPosition = FormStartPosition.Manual;
            BackColor = WorkbenchTheme.QuickControl.Background;
            AccessibleName = "模拟快捷控制";
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);

            var screen = Screen.PrimaryScreen;
            Location = screen.WorkingArea.Location;
            UpdateScreenLimits();
            var width = screen.Bounds.Width / 16;
            Size = new Size(width, CompactHeight(width, screen.Bounds.Height / 12));
            // 隐藏期间继续检测游戏窗口的焦点。
            _visibilityTimer.Start();
        }

        // 不设 ACT 为所属窗口，防止 ACT 最小化时连带隐藏；操作也不夺走游戏焦点。
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return parameters;
            }
        }

        internal void UpdateState(bool canStart, bool canRetry, bool canStop)
        {
            SetEnabled(ButtonId.Start, canStart);
            SetEnabled(ButtonId.Retry, canRetry);
            SetEnabled(ButtonId.Stop, canStop);
            Invalidate();
            UpdateVisibility();
        }

        private void SetEnabled(ButtonId button, bool enabled)
        {
            if (enabled)
                _enabled.Add(button);
            else
                _enabled.Remove(button);
        }

        private void UpdateVisibility()
        {
            if (IsGameForeground?.Invoke() == false)
            {
                if (!Visible)
                    return;

                // 切换焦点时前台句柄可能短暂为空，延后 250 毫秒再隐藏。
                var now = Stopwatch.GetTimestamp();
                var lostAt = _focusLostAt ?? now;
                _focusLostAt = lostAt;
                if (now - lostAt >= FocusLossDelayTicks)
                    Hide();
                return;
            }

            if (Visible && WindowState == FormWindowState.Normal && !_focusLostAt.HasValue)
                return;

            _focusLostAt = null;
            if (WindowState != FormWindowState.Normal)
                WindowState = FormWindowState.Normal;
            if (!Visible)
            {
                RestoreLocation(Location);
                Show();
            }

            SetWindowPos(Handle, TopmostWindow, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }

        private float Inset => Math.Min(ClientSize.Width / 2f, ClientSize.Height / 2f) * 0.12f;

        private int CompactHeight(int width, int requestedHeight)
            => Math.Max(MinimumSize.Height, Math.Min(requestedHeight, width / SessionRow.Length));

        private IEnumerable<(ButtonId Button, RectangleF Bounds)> LayoutButtons()
        {
            var width = ClientSize.Width / (float)SessionRow.Length;
            var inset = Inset;
            for (var column = 0; column < SessionRow.Length; column++)
            {
                var bounds = new RectangleF(column * width + inset, inset, width - inset * 2, ClientSize.Height - inset * 2);
                yield return (SessionRow[column], bounds);
            }
        }

        private ButtonId? ButtonAt(Point point)
        {
            foreach (var (button, bounds) in LayoutButtons())
                if (bounds.Contains(point))
                    return button;
            return null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var (button, bounds) in LayoutButtons())
            {
                var active = _enabled.Contains(button);
                var hover = active && _hover == button && !_dragging;
                var background = hover ? WorkbenchTheme.QuickControl.ButtonHover : WorkbenchTheme.QuickControl.Button;
                if (hover && Capture && _pressed == button)
                    background = WorkbenchTheme.QuickControl.ButtonPressed;
                using (var shape = WorkbenchTheme.RoundedRect(bounds, Inset))
                using (var fill = new SolidBrush(background))
                    e.Graphics.FillPath(fill, shape);

                var session = _sessionButtons[button];
                var size = Math.Min(bounds.Width, bounds.Height) * 0.44f;
                var x = bounds.Left + bounds.Width / 2;
                var y = bounds.Top + bounds.Height / 2;
                var accent = session.Accent;
                using (var icon = new SolidBrush(active ? accent : WorkbenchTheme.QuickControl.DisabledAction(accent)))
                {
                    if (button == ButtonId.Start)
                        e.Graphics.FillPolygon(icon, new[]
                        {
                            new PointF(x - size * 0.38f, y - size * 0.6f),
                            new PointF(x - size * 0.38f, y + size * 0.6f),
                            new PointF(x + size * 0.66f, y)
                        });
                    else if (button == ButtonId.Retry)
                        PaintRetryIcon(e.Graphics, icon, x, y, size);
                    else
                        using (var square = WorkbenchTheme.RoundedRect(new RectangleF(x - size / 2, y - size / 2, size, size), size * 0.12f))
                            e.Graphics.FillPath(icon, square);
                }
            }
        }

        private static void PaintRetryIcon(Graphics graphics, SolidBrush icon, float x, float y, float size)
        {
            var diameter = size * 0.86f;
            var tipX = x + size * 0.34f;
            using (var pen = new Pen(icon.Color, Math.Max(1.5f, size * 0.26f)) { StartCap = LineCap.Round })
                graphics.DrawArc(pen, tipX - diameter, y - diameter / 2, diameter, diameter, 82, 278);

            graphics.FillPolygon(icon, new[]
            {
                new PointF(tipX - size * 0.29f, y - size * 0.08f),
                new PointF(tipX + size * 0.29f, y - size * 0.08f),
                new PointF(tipX, y + size * 0.47f)
            });
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
                return;
            using (var outline = WorkbenchTheme.RoundedRect(ClientRectangle, Inset * 1.7f))
            {
                var old = Region;
                Region = new Region(outline);
                old?.Dispose();
            }
        }

        private AnchorStyles ResizeEdges(Point point)
        {
            var edges = AnchorStyles.None;
            if (point.X < Inset)
                edges |= AnchorStyles.Left;
            else if (point.X >= ClientSize.Width - Inset)
                edges |= AnchorStyles.Right;
            if (point.Y < Inset)
                edges |= AnchorStyles.Top;
            else if (point.Y >= ClientSize.Height - Inset)
                edges |= AnchorStyles.Bottom;
            return edges;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;
            _mouseOrigin = MousePosition;
            _originalBounds = Bounds;
            _resizeEdges = ResizeEdges(e.Location);
            _pressed = ButtonAt(e.Location);
            _dragging = false;
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (Capture)
            {
                var delta = Size.Subtract((Size)MousePosition, (Size)_mouseOrigin);
                var threshold = SystemInformation.DragSize;
                if (Math.Abs(delta.Width) >= threshold.Width / 2 || Math.Abs(delta.Height) >= threshold.Height / 2)
                    _dragging = true;
                if (_dragging)
                {
                    if (_resizeEdges == AnchorStyles.None)
                        Location = _originalBounds.Location + delta;
                    else
                        ResizeWindow(delta);
                    Invalidate();
                }
                return;
            }

            var edges = ResizeEdges(e.Location);
            var horizontal = (edges & (AnchorStyles.Left | AnchorStyles.Right)) != 0;
            var vertical = (edges & (AnchorStyles.Top | AnchorStyles.Bottom)) != 0;
            Cursor = horizontal && vertical
                ? (edges == (AnchorStyles.Left | AnchorStyles.Top) || edges == (AnchorStyles.Right | AnchorStyles.Bottom)
                    ? Cursors.SizeNWSE : Cursors.SizeNESW)
                : horizontal ? Cursors.SizeWE : vertical ? Cursors.SizeNS : Cursors.Hand;
            var hover = ButtonAt(e.Location);
            if (_hover != hover)
            {
                _hover = hover;
                Invalidate();
            }
        }

        private void ResizeWindow(Size delta)
        {
            var left = (_resizeEdges & AnchorStyles.Left) != 0;
            var top = (_resizeEdges & AnchorStyles.Top) != 0;
            var width = _originalBounds.Width + (left ? -delta.Width : (_resizeEdges & AnchorStyles.Right) != 0 ? delta.Width : 0);
            var height = _originalBounds.Height + (top ? -delta.Height : (_resizeEdges & AnchorStyles.Bottom) != 0 ? delta.Height : 0);
            width = Math.Max(MinimumSize.Width, Math.Min(MaximumSize.Width, width));
            height = CompactHeight(width, Math.Min(MaximumSize.Height, height));
            Bounds = new Rectangle(left ? _originalBounds.Right - width : _originalBounds.Left,
                top ? _originalBounds.Bottom - height : _originalBounds.Top, width, height);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !Capture)
                return;
            // 松开前保存点击结果；拖动越过阈值后，即使移回原处也不执行按钮。
            var button = !_dragging && _resizeEdges == AnchorStyles.None && _pressed == ButtonAt(e.Location) ? _pressed : null;
            Capture = false;
            RestoreLocation(Location);
            if (Location != _originalBounds.Location)
                LocationCommitted?.Invoke(Location);
            if (button is ButtonId clicked && _enabled.Contains(clicked))
            {
                _sessionButtons[clicked].Execute();
                Invalidate();
            }
        }

        internal void RestoreLocation(Point location)
        {
            Location = location;
            // 显示器数量或分辨率改变后，把保存的位置限制在当前可用屏幕内。
            var area = Screen.FromRectangle(Bounds).WorkingArea;
            Location = new Point(Math.Max(area.Left, Math.Min(Left, area.Right - Width)),
                Math.Max(area.Top, Math.Min(Top, area.Bottom - Height)));
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture)
            {
                _pressed = null;
                _dragging = false;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = null;
            Invalidate();
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            UpdateScreenLimits();
        }

        private void UpdateScreenLimits()
        {
            if (_updatingLimits)
                return;
            _updatingLimits = true;
            try
            {
                var screen = Screen.FromRectangle(Bounds);
                var minWidth = screen.Bounds.Width / 24;
                MaximumSize = new Size(screen.Bounds.Width / 10, screen.Bounds.Height / 20);
                MinimumSize = new Size(minWidth, Math.Min(screen.Bounds.Height / 40, minWidth / SessionRow.Length));
                Height = CompactHeight(Width, Height);
            }
            finally
            {
                _updatingLimits = false;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 只能由退出副本、关闭配置或卸载插件释放，不能在练习中单独关闭。
            if (e.CloseReason == CloseReason.UserClosing)
                e.Cancel = true;
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _visibilityTimer?.Dispose();
            base.Dispose(disposing);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    }
}
