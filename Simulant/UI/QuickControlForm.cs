using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Simulant.UI
{
    /// <summary>模拟副本内的快捷控制；单击执行，拖动移动，边缘缩放。</summary>
    internal sealed partial class QuickControlForm : FormEx
    {
        private const int StartButton = 0;
        private const int RetryButton = 1;
        private const int StopButton = 2;
        private const int SessionButtonCount = 3;
        private const int SprintButton = SessionButtonCount;
        private const int KnockbackButton = 4;
        private const int InvulnerabilityButton = 5;
        private static readonly Color[] SessionAccents =
        {
            WorkbenchTheme.Mint,
            WorkbenchTheme.Yellow,
            WorkbenchTheme.Red
        };

        private readonly Action[] _actions;
        private readonly bool[] _enabled;
        private int _hover = -1;
        private int _pressed = -1;
        private Point _mouseOrigin;
        private Rectangle _originalBounds;
        private AnchorStyles _resizeEdges;
        private bool _dragging;
        private bool _updatingLimits;
        internal event Action<Point> LocationCommitted;
        internal Func<bool> IsGameForeground { get; set; }

        internal QuickControlForm(Action start, Action retry, Action stop, Action<string> command, Action<string> logWarning,
            Func<uint, Core.SimActionCooldown> cooldown)
        {
            _actions = new[] { start, retry, stop, () => command("ac 冲刺"),
                () => command(_surecast ? "ac 沉稳咏唱" : "ac 亲疏自行"), () => command("ac 无敌") };
            _enabled = new bool[_actions.Length];
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
            BackColor = WorkbenchTheme.QuickControl.Background;
            AccessibleName = "模拟快捷控制";
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);

            var screen = Screen.PrimaryScreen;
            Location = screen.WorkingArea.Location;
            UpdateScreenLimits();
            var width = screen.Bounds.Width / 16;
            Size = new Size(width, CompactHeight(width, screen.Bounds.Height / 12));
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

        internal void UpdateState(bool canStart, bool canRetry, bool canStop, bool? surecast, uint invulnerability = 0)
        {
            var stateChanged = _enabled[StartButton] != canStart || _enabled[RetryButton] != canRetry ||
                _enabled[StopButton] != canStop || _invulnerability != invulnerability ||
                _enabled[SprintButton] != surecast.HasValue || (surecast.HasValue && _surecast != surecast.Value);
            _enabled[StartButton] = canStart;
            _enabled[RetryButton] = canRetry;
            _enabled[StopButton] = canStop;
            var skillCountChanged = (_invulnerability == 0) != (invulnerability == 0);
            _invulnerability = invulnerability;
            if (skillCountChanged)
                Height = CompactHeight(Width, Height);
            _enabled[SprintButton] = _enabled[KnockbackButton] = surecast.HasValue;
            _enabled[InvulnerabilityButton] = surecast.HasValue && invulnerability != 0;
            if (surecast.HasValue)
                _surecast = surecast.Value;
            if (stateChanged)
                Invalidate();
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

        private int ButtonCount => _invulnerability == 0 ? InvulnerabilityButton : InvulnerabilityButton + 1;
        private int SkillButtonCount => ButtonCount - SessionButtonCount;

        private int CompactHeight(int width, int requestedHeight)
        {
            // 两行各自以一格的宽度为高度上限，技能图标贴合下排，保留边距。
            var layoutHeight = width / (float)SessionButtonCount + width / (float)SkillButtonCount;
            return Math.Max(MinimumSize.Height, Math.Min(requestedHeight, (int)layoutHeight));
        }

        private RectangleF ButtonBounds(int index)
        {
            var sessionButton = index < SessionButtonCount;
            var width = ClientSize.Width / (float)(sessionButton ? SessionButtonCount : SkillButtonCount);
            var topHeight = ClientSize.Height * SkillButtonCount / (float)ButtonCount;
            var top = sessionButton ? 0 : topHeight;
            var height = sessionButton ? topHeight : ClientSize.Height - topHeight;
            var column = sessionButton ? index : index - SessionButtonCount;
            return new RectangleF(column * width + Inset, top + Inset, width - Inset * 2, height - Inset * 2);
        }

        private int ButtonAt(Point point)
        {
            for (var i = 0; i < ButtonCount; i++)
                if (ButtonBounds(i).Contains(point))
                    return i;
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            for (var i = 0; i < ButtonCount; i++)
            {
                var bounds = ButtonBounds(i);
                var active = CanClick(i);
                var hover = active && _hover == i && !_dragging;
                var background = hover ? WorkbenchTheme.QuickControl.ButtonHover : WorkbenchTheme.QuickControl.Button;
                if (hover && Capture && _pressed == i)
                    background = WorkbenchTheme.QuickControl.ButtonPressed;
                using (var shape = WorkbenchTheme.RoundedRect(bounds, Inset))
                using (var fill = new SolidBrush(background))
                    e.Graphics.FillPath(fill, shape);

                if (i >= SessionButtonCount)
                {
                    PaintSkill(e.Graphics, i, bounds);
                    continue;
                }

                var size = Math.Min(bounds.Width, bounds.Height) * 0.44f;
                var x = bounds.Left + bounds.Width / 2;
                var y = bounds.Top + bounds.Height / 2;
                var accent = SessionAccents[i];
                using (var icon = new SolidBrush(active ? accent : WorkbenchTheme.QuickControl.DisabledAction(accent)))
                {
                    if (i == StartButton)
                        e.Graphics.FillPolygon(icon, new[]
                        {
                            new PointF(x - size * 0.38f, y - size * 0.6f),
                            new PointF(x - size * 0.38f, y + size * 0.6f),
                            new PointF(x + size * 0.66f, y)
                        });
                    else if (i == RetryButton)
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
                var minWidth = screen.Bounds.Width / 24;
                MaximumSize = new Size(screen.Bounds.Width / 10, screen.Bounds.Height / 10);
                MinimumSize = new Size(minWidth, Math.Min(screen.Bounds.Height / 20, minWidth * 2 / SessionButtonCount));
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
    }
}
