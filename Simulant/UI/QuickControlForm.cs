using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Simulant.UI
{
    /// <summary>模拟副本内的快捷控制；单击执行，拖动移动，边缘缩放。</summary>
    internal sealed partial class QuickControlForm : Form
    {
        private readonly Action[] _actions;
        private readonly bool[] _enabled = new bool[4];
        private int _hover = -1;
        private int _pressed = -1;
        private Point _mouseOrigin;
        private Rectangle _originalBounds;
        private AnchorStyles _resizeEdges;
        private bool _dragging;
        private bool _updatingLimits;
        internal event Action<Point> LocationCommitted;
        internal Func<bool> IsGameForeground { get; set; }

        internal QuickControlForm(Action start, Action stop, Action<string> command, Action<string> logWarning,
            Func<uint, Core.SimActionCooldown> cooldown)
        {
            _actions = new[] { start, stop, () => command("ac 冲刺"),
                () => command(_surecast ? "ac 沉稳咏唱" : "ac 亲疏自行") };
            _logWarning = logWarning;
            _cooldown = cooldown;
            _animationTimer = new Timer { Interval = 33 };
            _animationTimer.Tick += (_, __) =>
            {
                UpdateVisibility();
                if (Visible)
                    Invalidate();
            };
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            Opacity = 0.88;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(30, 33, 39);
            AccessibleName = "模拟快捷控制";
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);

            var screen = Screen.PrimaryScreen;
            Location = screen.WorkingArea.Location;
            UpdateScreenLimits();
            Size = new Size(screen.Bounds.Width / 16, screen.Bounds.Height / 12);
            // 隐藏后仍检测前台窗口，复用冷却动画计时器，不销毁窗口或重新加载图标。
            _animationTimer.Start();
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

        internal void UpdateState(bool canStart, bool canStop, bool? surecast)
        {
            var skillStateChanged = _enabled[2] != surecast.HasValue || (surecast.HasValue && _surecast != surecast.Value);
            _enabled[2] = _enabled[3] = surecast.HasValue;
            if (surecast.HasValue)
                _surecast = surecast.Value;
            if (skillStateChanged)
                Invalidate();
            if (_enabled[0] != canStart || _enabled[1] != canStop)
            {
                _enabled[0] = canStart;
                _enabled[1] = canStop;
                Invalidate();
            }
            if (WindowState != FormWindowState.Normal)
                WindowState = FormWindowState.Normal;
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            var foreground = IsGameForeground?.Invoke() ?? true;
            if (foreground && !Visible)
                Show();
            else if (!foreground && Visible)
                Hide();
        }

        private float Inset => Math.Min(ClientSize.Width / 2f, ClientSize.Height / 2f) * 0.12f;

        private RectangleF ButtonBounds(int index)
        {
            var half = ClientSize.Width / 2f;
            var topHeight = ClientSize.Height * 0.36f;
            var top = index < 2 ? 0 : topHeight;
            var height = index < 2 ? topHeight : ClientSize.Height - topHeight;
            return new RectangleF(index % 2 * half + Inset, top + Inset, half - Inset * 2, height - Inset * 2);
        }

        private int ButtonAt(Point point)
        {
            for (var i = 0; i < 4; i++)
                if (ButtonBounds(i).Contains(point))
                    return i;
            return -1;
        }

        private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            for (var i = 0; i < 4; i++)
            {
                var bounds = ButtonBounds(i);
                var active = CanClick(i);
                var hover = active && _hover == i && !_dragging;
                var accent = i == 0 ? Color.FromArgb(89, 215, 150) : Color.FromArgb(236, 111, 124);
                var background = hover ? Color.FromArgb(58, 62, 70) : Color.FromArgb(40, 44, 51);
                if (hover && Capture && _pressed == i)
                    background = Color.FromArgb(69, 74, 82);
                using (var shape = RoundedRectangle(bounds, Inset))
                using (var fill = new SolidBrush(background))
                    e.Graphics.FillPath(fill, shape);

                if (i >= 2)
                {
                    PaintSkill(e.Graphics, i, bounds);
                    continue;
                }

                var size = Math.Min(bounds.Width, bounds.Height) * 0.44f;
                var x = bounds.Left + bounds.Width / 2;
                var y = bounds.Top + bounds.Height / 2;
                using (var icon = new SolidBrush(active ? accent : Color.FromArgb(accent.R / 2, accent.G / 2, accent.B / 2)))
                {
                    if (i == 0)
                        e.Graphics.FillPolygon(icon, new[]
                        {
                            new PointF(x - size * 0.38f, y - size * 0.6f),
                            new PointF(x - size * 0.38f, y + size * 0.6f),
                            new PointF(x + size * 0.66f, y)
                        });
                    else
                        using (var square = RoundedRectangle(new RectangleF(x - size / 2, y - size / 2, size, size), size * 0.12f))
                            e.Graphics.FillPath(icon, square);
                }
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
                return;
            using (var outline = RoundedRectangle(ClientRectangle, Inset * 1.7f))
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
            height = Math.Max(MinimumSize.Height, Math.Min(MaximumSize.Height, height));
            Bounds = new Rectangle(left ? _originalBounds.Right - width : _originalBounds.Left,
                top ? _originalBounds.Bottom - height : _originalBounds.Top, width, height);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !Capture)
                return;
            // 松开前保存点击结果；拖动越过阈值后，即使移回原处也不执行按钮。
            var action = !_dragging && _resizeEdges == AnchorStyles.None && _pressed == ButtonAt(e.Location) ? _pressed : -1;
            Capture = false;
            RestoreLocation(Location);
            if (Location != _originalBounds.Location)
                LocationCommitted?.Invoke(Location);
            if (action >= 0 && CanClick(action))
            {
                _actions[action]();
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
                _pressed = -1;
                _dragging = false;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
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
                MaximumSize = new Size(screen.Bounds.Width / 10, screen.Bounds.Height / 10);
                MinimumSize = new Size(screen.Bounds.Width / 24, screen.Bounds.Height / 20);
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
    }
}
