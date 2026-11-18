using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Simulant.UI
{
    // Keep Windows scroll input and range handling underneath an opaque themed scrollbar.
    internal sealed class ScrollBarTheme : NativeWindow
    {
        private const int ObjIdClient = -4;
        private const int ObjIdVScroll = -5;
        private const int ObjIdHScroll = -6;
        private const int WmPaint = 0x000F;
        private const int WmEnable = 0x000A;
        private const int WmNcPaint = 0x0085;
        private const int WmNcActivate = 0x0086;
        private const int WmHScroll = 0x0114;
        private const int WmVScroll = 0x0115;
        private const int WmPrint = 0x0317;
        private const int WmPrintClient = 0x0318;
        private const int PrfNonClient = 0x2;
        private const int StateInvisible = 0x8000;
        private const int StateUnavailable = 0x1;

        private const int GwlStyle = -16;
        private const int GwlExStyle = -20;
        private const int WsExTopMost = 0x00000008;
        private const int WsChild = 0x40000000;
        private const int WsClipSiblings = 0x04000000;
        private const uint GaParent = 1;
        private const uint GaRoot = 2;

        private readonly ScrollBar _bar;
        private readonly IntPtr _combo;
        private Overlay _vertical;
        private Overlay _horizontal;

        private ScrollBarTheme(Control control)
        {
            _bar = control as ScrollBar;
            control.HandleCreated += (_, __) => AttachHandle(control.Handle);
            control.VisibleChanged += (_, __) =>
            {
                RefreshOverlays();
                if (control.Visible && control.IsHandleCreated)
                    control.BeginInvoke((Action)RefreshOverlays);
            };
            if (control is RichTextBox)
                control.TextChanged += (_, __) => RefreshOverlays();
            if (control.IsHandleCreated)
                AttachHandle(control.Handle);
        }

        private ScrollBarTheme(IntPtr handle, IntPtr combo)
        {
            _combo = combo;
            AttachHandle(handle);
        }

        private void AttachHandle(IntPtr handle)
        {
            var style = GetWindowLong(handle, GwlStyle);
            if ((style & WsChild) != 0 && (style & WsClipSiblings) == 0)
                SetWindowLong(handle, GwlStyle, style | WsClipSiblings);

            AssignHandle(handle);
            RefreshOverlays();
        }

        private void RefreshOverlays()
        {
            if (Handle != IntPtr.Zero)
                UpdateOverlays();
        }

        internal static void Attach(Control control)
            => new ScrollBarTheme(control);

        internal static ScrollBarTheme AttachComboList(IntPtr combo)
        {
            var info = new ComboBoxInfo { Size = Marshal.SizeOf(typeof(ComboBoxInfo)) };
            return GetComboBoxInfo(combo, ref info) && info.List != IntPtr.Zero
                ? new ScrollBarTheme(info.List, combo)
                : null;
        }

        internal void Detach()
        {
            DestroyOverlays();
            ReleaseHandle();
        }

        private void DestroyOverlays()
        {
            _vertical?.DestroyHandle();
            _horizontal?.DestroyHandle();
            _vertical = _horizontal = null;
        }

        protected override void WndProc(ref Message message)
        {
            const int wmDestroy = 0x0002;
            var kind = message.Msg;
            if (kind == wmDestroy)
                DestroyOverlays();

            // The native scrollbar runs a modal mouse tracking loop. Update the visible thumb during it.
            var tracking = _bar != null && kind == 0x0201 ||
                kind == 0x00A1 && (message.WParam == (IntPtr)6 || message.WParam == (IntPtr)7);
            Timer trackingTimer = null;
            if (tracking)
            {
                trackingTimer = new Timer { Interval = 16 };
                trackingTimer.Tick += (_, __) => RefreshOverlays();
                trackingTimer.Start();
            }

            try
            {
                base.WndProc(ref message);
            }
            finally
            {
                trackingTimer?.Dispose();
            }

            if (Handle == IntPtr.Zero || kind == wmDestroy)
                return;

            if (kind == WmPrint || kind == WmPrintClient)
            {
                if (_bar != null || kind == WmPrint && (message.LParam.ToInt64() & PrfNonClient) != 0)
                    Draw(message.WParam);
                return;
            }

            const int wmShowWindow = 0x0018, wmWindowPosChanged = 0x0047;
            if (tracking || kind == WmPaint || kind == WmNcPaint || kind == WmNcActivate ||
                kind == WmEnable || kind == WmHScroll || kind == WmVScroll ||
                kind == wmShowWindow || kind == wmWindowPosChanged ||
                _bar != null && IsScrollBarMessage(kind))
                UpdateOverlays();
        }

        private static bool IsScrollBarMessage(int message)
        {
            const int sbmSetPos = 0x00E0;
            const int sbmSetRange = 0x00E2;
            const int sbmEnableArrows = 0x00E4;
            const int sbmSetRangeRedraw = 0x00E6;
            const int sbmSetScrollInfo = 0x00E9;
            return message == sbmSetPos || message == sbmSetRange || message == sbmEnableArrows ||
                message == sbmSetRangeRedraw || message == sbmSetScrollInfo;
        }

        private void UpdateOverlays()
        {
            if (!IsWindowVisible(Handle) || _combo != IntPtr.Zero && !IsWindowVisible(_combo))
            {
                _vertical?.Hide();
                _horizontal?.Hide();
                return;
            }

            var hasVertical = TryGetBarInfo(true, out var vertical);
            var hasHorizontal = TryGetBarInfo(false, out var horizontal);
            if (!hasVertical && !hasHorizontal)
            {
                _vertical?.Hide();
                _horizontal?.Hide();
                return;
            }

            var parent = GetAncestor(Handle, GaParent);
            // ComboLBox can be a WS_CHILD of the desktop and still act as a floating window.
            var popup = parent == GetDesktopWindow() || (GetWindowLong(Handle, GwlStyle) & WsChild) == 0;
            var origin = Point.Empty;
            if (!popup && !ScreenToClient(parent, ref origin))
                return;

            UpdateOverlay(ref _vertical, hasVertical, vertical, true, parent, popup, origin, 0, Handle);
            var cornerWidth = hasVertical && hasHorizontal && horizontal.Bounds.Right == vertical.Bounds.Left
                ? vertical.Bounds.Right - vertical.Bounds.Left : 0;
            var lowerWindow = hasVertical && _vertical.Handle != IntPtr.Zero ? _vertical.Handle : Handle;
            UpdateOverlay(ref _horizontal, hasHorizontal, horizontal, false, parent, popup, origin, cornerWidth, lowerWindow);
        }

        private void UpdateOverlay(ref Overlay overlay, bool visible, ScrollBarInfo info, bool vertical,
            IntPtr parent, bool popup, Point origin, int cornerWidth, IntPtr lowerWindow)
        {
            if (!visible)
            {
                overlay?.Hide();
                return;
            }

            if (overlay == null)
            {
                var owner = popup ? (_combo == IntPtr.Zero ? Handle : GetAncestor(_combo, GaRoot)) : parent;
                var topMost = popup && (GetWindowLong(Handle, GwlExStyle) & WsExTopMost) != 0;
                overlay = new Overlay(owner, popup, vertical, topMost);
            }

            var bounds = info.Bounds.RelativeTo(default);
            bounds.Offset(origin);
            overlay.Update(info, bounds, cornerWidth, lowerWindow);
        }

        private bool TryGetBarInfo(bool vertical, out ScrollBarInfo info)
        {
            info = default;
            if (_bar != null && vertical != (_bar is VScrollBar))
                return false;

            return TryGetInfo(_bar != null ? ObjIdClient : vertical ? ObjIdVScroll : ObjIdHScroll, out info);
        }

        private sealed class Overlay : NativeWindow
        {
            private const uint SwpNoSize = 1, SwpNoMove = 2, SwpNoActivate = 0x10, SwpShowWindow = 0x40;
            private readonly bool _vertical;
            private readonly bool _popup;
            private readonly IntPtr _popupZOrder;
            private Rectangle _bounds;
            private ScrollBarInfo _info;
            private int _cornerWidth;
            private bool _visible;

            internal Overlay(IntPtr parent, bool popup, bool vertical, bool topMost)
            {
                _vertical = vertical;
                _popup = popup;
                _popupZOrder = topMost ? new IntPtr(-1) : IntPtr.Zero;
                CreateHandle(new CreateParams
                {
                    Parent = parent,
                    Style = popup ? unchecked((int)0x80000000) : WsChild | WsClipSiblings,
                    // Owned popup for the native combo list only. Child overlays stay inside their parent.
                    ExStyle = popup ? 0x080800A0 : 0x08000000
                });

                if (popup && !SetLayeredWindowAttributes(Handle, 0, 255, 2))
                    DestroyHandle();
            }

            internal void Update(ScrollBarInfo info, Rectangle bounds, int cornerWidth, IntPtr target)
            {
                if (Handle == IntPtr.Zero)
                    return;

                var full = bounds;
                full.Width += cornerWidth;
                var moved = _bounds != bounds || _cornerWidth != cornerWidth;
                var changed = moved || !_info.HasSameAppearance(info);
                _info = info;
                _bounds = bounds;
                _cornerWidth = cornerWidth;

                // Place the overlay immediately above its target so unrelated siblings retain their z-order.
                var previous = _popup ? _popupZOrder : GetWindow(target, 3);
                var ordered = previous == Handle;
                if (ordered)
                    previous = GetWindow(Handle, 3);

                if (!_visible || moved)
                {
                    _visible = true;
                    SetWindowPos(Handle, previous, full.X, full.Y, full.Width, full.Height, SwpNoActivate | SwpShowWindow);
                }
                else if (!_popup && !ordered)
                    SetWindowPos(Handle, previous, 0, 0, 0, 0, SwpNoActivate | SwpNoMove | SwpNoSize);

                if (changed)
                    InvalidateRect(Handle, IntPtr.Zero, false);
            }

            internal void Hide()
            {
                if (_visible && Handle != IntPtr.Zero)
                    ShowWindow(Handle, 0);
                _visible = false;
            }

            protected override void WndProc(ref Message message)
            {
                if (message.Msg == 0x0084) // WM_NCHITTEST
                {
                    message.Result = new IntPtr(-1); // HTTRANSPARENT
                    return;
                }

                if (message.Msg == WmPrintClient)
                {
                    PaintBar(message.WParam);
                    return;
                }

                if (message.Msg != WmPaint)
                {
                    base.WndProc(ref message);
                    return;
                }

                var dc = NativeControlPaint.BeginPaint(message.HWnd, out var paint);
                try
                {
                    PaintBar(dc);
                }
                finally
                {
                    NativeControlPaint.EndPaint(message.HWnd, ref paint);
                }
            }

            private void PaintBar(IntPtr dc)
            {
                if (dc == IntPtr.Zero || _bounds.IsEmpty)
                    return;

                using (var graphics = Graphics.FromHdc(dc))
                using (var buffer = BufferedGraphicsManager.Current.Allocate(graphics,
                    new Rectangle(0, 0, _bounds.Width + _cornerWidth, _bounds.Height)))
                {
                    buffer.Graphics.Clear(WorkbenchTheme.Canvas);
                    buffer.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    DrawBar(buffer.Graphics, _info, new Rectangle(Point.Empty, _bounds.Size), _vertical);
                    buffer.Render(graphics);
                }
            }
        }

        private void Draw(IntPtr dc)
        {
            if (dc == IntPtr.Zero || !GetWindowRect(Handle, out var window))
                return;

            var vertical = TryGetBarInfo(true, out var verticalInfo);
            var horizontal = TryGetBarInfo(false, out var horizontalInfo);
            if (!vertical && !horizontal)
                return;

            using (var graphics = Graphics.FromHdc(dc))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                if (vertical)
                    DrawBar(graphics, verticalInfo, verticalInfo.Bounds.RelativeTo(window), true);
                if (horizontal)
                    DrawBar(graphics, horizontalInfo, horizontalInfo.Bounds.RelativeTo(window), false);
                if (vertical && horizontal)
                {
                    var v = verticalInfo.Bounds.RelativeTo(window);
                    var h = horizontalInfo.Bounds.RelativeTo(window);
                    using (var fill = new SolidBrush(WorkbenchTheme.Canvas))
                        graphics.FillRectangle(fill, v.Left, h.Top, v.Width, h.Height);
                }
            }
        }

        private bool TryGetInfo(int objectId, out ScrollBarInfo info)
        {
            info = new ScrollBarInfo { Size = Marshal.SizeOf(typeof(ScrollBarInfo)) };
            return GetScrollBarInfo(Handle, objectId, ref info) && (info.State & StateInvisible) == 0 &&
                info.Bounds.Right > info.Bounds.Left && info.Bounds.Bottom > info.Bounds.Top;
        }

        private static void DrawBar(Graphics graphics, ScrollBarInfo info, Rectangle bounds, bool vertical)
        {
            var size = vertical ? bounds.Width : bounds.Height;
            var length = vertical ? bounds.Height : bounds.Width;
            var arrow = Math.Min(info.ArrowSize, length / 2);
            using (var fill = new SolidBrush(WorkbenchTheme.Canvas))
                graphics.FillRectangle(fill, bounds);

            var padding = Math.Max(2, size / 4);
            var start = Math.Max(arrow, info.ThumbStart) + 1;
            var end = Math.Min(length - arrow, info.ThumbEnd) - 1;
            if (end > start && size > padding * 2 && (info.ThumbState & StateUnavailable) == 0)
            {
                var thumb = vertical
                    ? new Rectangle(bounds.X + padding, bounds.Y + start, size - 2 * padding, end - start)
                    : new Rectangle(bounds.X + start, bounds.Y + padding, end - start, size - 2 * padding);
                using (var path = WorkbenchTheme.RoundedRect(thumb, padding))
                using (var fill = new SolidBrush(WorkbenchTheme.Dim))
                    graphics.FillPath(fill, path);
            }

            if (arrow >= 8)
            {
                DrawArrow(graphics, bounds, vertical, arrow / 2F, -1, info.FirstArrowState);
                DrawArrow(graphics, bounds, vertical, length - arrow / 2F, 1, info.LastArrowState);
            }
        }

        private static void DrawArrow(Graphics graphics, Rectangle bounds, bool vertical, float along, int direction, int state)
        {
            var color = (state & StateUnavailable) != 0 ? WorkbenchTheme.Stroke : WorkbenchTheme.Muted;
            using (var pen = new Pen(color, 1.25F))
            {
                if (vertical)
                {
                    var x = bounds.Left + bounds.Width / 2F;
                    var y = bounds.Top + along;
                    graphics.DrawLines(pen, new[]
                    {
                        new PointF(x - 3, y - direction), new PointF(x, y + direction * 2), new PointF(x + 3, y - direction)
                    });
                }
                else
                {
                    var x = bounds.Left + along;
                    var y = bounds.Top + bounds.Height / 2F;
                    graphics.DrawLines(pen, new[]
                    {
                        new PointF(x - direction, y - 3), new PointF(x + direction * 2, y), new PointF(x - direction, y + 3)
                    });
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left, Top, Right, Bottom;

            internal Rectangle RelativeTo(NativeRect origin)
                => Rectangle.FromLTRB(Left - origin.Left, Top - origin.Top, Right - origin.Left, Bottom - origin.Top);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ScrollBarInfo
        {
            public int Size;
            public NativeRect Bounds;
            public int ArrowSize, ThumbStart, ThumbEnd, Reserved;
            public int State, FirstArrowState, FirstTrackState, ThumbState, LastTrackState, LastArrowState;

            internal bool HasSameAppearance(ScrollBarInfo other)
                => ArrowSize == other.ArrowSize && ThumbStart == other.ThumbStart && ThumbEnd == other.ThumbEnd &&
                    FirstArrowState == other.FirstArrowState && ThumbState == other.ThumbState && LastArrowState == other.LastArrowState;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ComboBoxInfo
        {
            public int Size;
            public NativeRect Item, Button;
            public int ButtonState;
            public IntPtr Combo, Edit, List;
        }

        [DllImport("user32.dll")]
        private static extern bool GetComboBoxInfo(IntPtr window, ref ComboBoxInfo info);

        [DllImport("user32.dll")]
        private static extern bool GetScrollBarInfo(IntPtr window, int objectId, ref ScrollBarInfo info);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr window, uint flags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, uint command);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr window, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr window, int index, int value);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr window, ref Point position);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(IntPtr window, IntPtr rect, bool erase);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr window, uint color, byte alpha, uint flags);
    }
}
