using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Simulant.UI
{
    public class TextBoxEx : TextBox
    {
        private const int WmPaint = 0x000F;
        private const int WmEraseBackground = 0x0014;
        private const int WmPrintClient = 0x0318;
        private const int PrfClient = 0x0004;
        private bool _isInvalid;
        private bool _themedCaret;
        private bool _caretVisible;
        private Timer _blinkTimer;
        private Bitmap _editBuffer;

        public TextBoxEx()
        {
            BackColor = WorkbenchTheme.Field;
            ForeColor = WorkbenchTheme.Text;
            BorderStyle = BorderStyle.FixedSingle;
        }

        private bool ThemeText => !Multiline && !SystemInformation.HighContrast;

        [DefaultValue(false)]
        public bool IsInvalid
        {
            get => _isInvalid;
            set
            {
                if (_isInvalid == value)
                    return;

                _isInvalid = value;
                BackColor = value ? WorkbenchTheme.Invalid : WorkbenchTheme.Field;
            }
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            if (!ThemeText || !Focused)
                return;

            _themedCaret = HideCaret(Handle);
            RestartCaret();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            StopCaret();
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            StopCaret();
            _editBuffer?.Dispose();
            _editBuffer = null;
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            var isPaint = m.Msg == WmPaint || m.Msg == WmPrintClient || m.Msg == WmEraseBackground;
            if (isPaint && ClientSize.Width > 0 && ClientSize.Height > 0 && ThemeText && (Focused || !HideSelection))
            {
                if (m.Msg == WmEraseBackground)
                {
                    m.Result = (IntPtr)1;
                    return;
                }

                if (m.Msg == WmPaint)
                {
                    var dc = NativeControlPaint.BeginPaint(Handle, out var paint);
                    try
                    {
                        if (dc != IntPtr.Zero)
                            PaintEdit(dc, m.HWnd, Focused);
                    }
                    finally
                    {
                        NativeControlPaint.EndPaint(m.HWnd, ref paint);
                    }

                    return;
                }

                if (m.Msg == WmPrintClient && m.WParam != IntPtr.Zero)
                {
                    PaintEdit(m.WParam, m.HWnd, false);
                    return;
                }
            }

            base.WndProc(ref m);
            if (ChangesTextOrSelection(m.Msg) && IsHandleCreated && ThemeText && (Focused || !HideSelection))
                RestartCaret();
        }

        private static bool ChangesTextOrSelection(int message)
        {
            const int wmSetText = 0x000C, emSetSel = 0x00B1, emReplaceSel = 0x00C2, emUndo = 0x00C7;
            const int wmKeyDown = 0x0100, wmChar = 0x0102, wmImeComposition = 0x010F;
            const int wmMouseMove = 0x0200, wmLButtonDown = 0x0201, wmLButtonDblClk = 0x0203;
            const int wmCut = 0x0300, wmUndo = 0x0304;
            return message == wmSetText || message == emSetSel || message == emReplaceSel || message == emUndo ||
                message == wmKeyDown || message == wmChar || message == wmImeComposition ||
                message == wmMouseMove && (MouseButtons & MouseButtons.Left) != 0 ||
                message >= wmLButtonDown && message <= wmLButtonDblClk ||
                message >= wmCut && message <= wmUndo;
        }

        // Render the native edit off screen to keep its text layout and input behavior.
        private void PaintEdit(IntPtr target, IntPtr window, bool drawCaret)
        {
            var size = ClientSize;
            if (_editBuffer == null || _editBuffer.Size != size)
            {
                _editBuffer?.Dispose();
                _editBuffer = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppRgb);
            }

            using (var graphics = Graphics.FromImage(_editBuffer))
            {
                graphics.Clear(BackColor);
                var dc = graphics.GetHdc();
                try
                {
                    var print = Message.Create(window, WmPrintClient, dc, (IntPtr)PrfClient);
                    base.WndProc(ref print);
                }
                finally
                {
                    graphics.ReleaseHdc(dc);
                }
            }

            if (SelectionLength != 0)
                RecolorSelection(_editBuffer);

            using (var graphics = Graphics.FromHdc(target))
            {
                graphics.DrawImageUnscaled(_editBuffer, 0, 0);
                if (drawCaret && _themedCaret && _caretVisible)
                {
                    var info = new GuiThreadInfo { Size = Marshal.SizeOf(typeof(GuiThreadInfo)) };
                    if (GetGUIThreadInfo(GetCurrentThreadId(), ref info) && info.Caret == window)
                        using (var brush = new SolidBrush(WorkbenchTheme.Accent))
                            graphics.FillRectangle(brush, info.Left, info.Top,
                                Math.Max(1, info.Right - info.Left), info.Bottom - info.Top);
                }
            }
        }

        private static unsafe void RecolorSelection(Bitmap bitmap)
        {
            var bounds = new Rectangle(Point.Empty, bitmap.Size);
            var data = bitmap.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppRgb);
            try
            {
                const uint rgb = 0x00ffffff;
                var highlight = (uint)SystemColors.Highlight.ToArgb() & rgb;
                var themed = (uint)WorkbenchTheme.AccentOutline.ToArgb() & rgb;
                for (var y = 0; y < data.Height; y++)
                {
                    var row = (uint*)((byte*)data.Scan0 + y * data.Stride);
                    for (var x = 0; x < data.Width; x++)
                        if ((row[x] & rgb) == highlight)
                            row[x] = (row[x] & ~rgb) | themed;
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private void RestartCaret()
        {
            _caretVisible = true;
            if (_themedCaret)
            {
                var interval = GetCaretBlinkTime();
                if (interval > 0 && interval <= int.MaxValue)
                {
                    if (_blinkTimer == null)
                    {
                        _blinkTimer = new Timer();
                        _blinkTimer.Tick += (_, __) =>
                        {
                            _caretVisible = !_caretVisible;
                            Invalidate();
                        };
                    }

                    _blinkTimer.Stop();
                    _blinkTimer.Interval = (int)interval;
                    _blinkTimer.Start();
                }
            }

            Invalidate();
        }

        private void StopCaret()
        {
            _themedCaret = _caretVisible = false;
            _blinkTimer?.Dispose();
            _blinkTimer = null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GuiThreadInfo
        {
            public int Size, Flags;
            public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
            public int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool HideCaret(IntPtr window);

        [DllImport("user32.dll")]
        private static extern uint GetCaretBlinkTime();

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);
    }
}
