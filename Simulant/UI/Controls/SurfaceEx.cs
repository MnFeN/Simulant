using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Simulant.UI
{
    public class FormEx : Form
    {
        protected static Icon PluginWindowIcon { get; } = new Icon(typeof(FormEx), "Simulant.ico");

        public FormEx()
        {
            BackColor = WorkbenchTheme.Canvas;
            ForeColor = WorkbenchTheme.Text;
            Font = WorkbenchTheme.BodyFont;
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && TopLevel && !DesignMode && !ShowWithoutActivation && IsHandleCreated)
                BeginInvoke((Action)BringToForeground);
        }

        internal void ShowInForeground()
        {
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            if (Visible)
                BringToForeground();
            else
                Show();
        }

        private void BringToForeground()
        {
            if (IsDisposed || Disposing || !Visible || !Enabled || ShowWithoutActivation)
                return;

            var topMost = TopMost;
            try
            {
                TopMost = true;
                BringToFront();
                Activate();
            }
            finally
            {
                if (!IsDisposed && TopMost != topMost)
                    TopMost = topMost;
            }
        }

        internal static StaWindow<T> ShowOnSta<T>(Func<T> create, Action<Exception> reportError, Action<T> open = null)
            where T : FormEx => new StaWindow<T>(create, reportError, open ?? (form => form.ShowInForeground()));

        internal sealed class StaWindow<T> : IDisposable where T : FormEx
        {
            private readonly object _gate = new object();
            private readonly Thread _thread;
            private readonly Action<Exception> _reportError;
            private Action<T> _pending;
            private T _form;
            private Control _dispatcher;
            private volatile bool _closing;
            private bool _closeQueued;
            private bool _finished;

            internal StaWindow(Func<T> create, Action<Exception> reportError, Action<T> open)
            {
                _reportError = reportError;
                _pending = open;
                _thread = new Thread(() => Run(create)) { IsBackground = true, Name = typeof(T).Name };
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.Start();
            }

            internal bool TryPost(Action<T> action)
            {
                lock (_gate)
                {
                    if (_closing || _finished)
                        return false;
                    if (_form == null)
                    {
                        _pending += action;
                        return true;
                    }
                    return Post(_form, action);
                }
            }

            private bool Post(T form, Action<T> action)
            {
                try
                {
                    _dispatcher.BeginInvoke((Action)(() =>
                    {
                        if (_closing || form.IsDisposed || form.Disposing)
                            return;
                        try { action(form); }
                        catch (Exception error) { _reportError(error); }
                    }));
                    return true;
                }
                catch (InvalidOperationException)
                {
                    // The form can lose its handle as the user closes it.
                    return false;
                }
            }

            private void Run(Func<T> create)
            {
                try
                {
                    using (var context = new ApplicationContext())
                    using (var dispatcher = new Control())
                    using (var form = create())
                    {
                        form.Disposed += (_, __) =>
                        {
                            lock (_gate)
                                _finished = true;
                            context.ExitThread();
                        };
                        _ = dispatcher.Handle;
                        lock (_gate)
                        {
                            if (_closing)
                                return;
                            _dispatcher = dispatcher;
                            _form = form;
                            Post(form, _pending);
                            _pending = null;
                        }
                        Application.Run(context);
                    }
                }
                catch (Exception error)
                {
                    _reportError(error);
                }
                finally
                {
                    lock (_gate)
                    {
                        _finished = true;
                        _pending = null;
                        _form = null;
                        _dispatcher = null;
                    }
                }
            }

            public void Dispose()
            {
                lock (_gate)
                {
                    if (!_finished)
                    {
                        _closing = true;
                        _pending = null;
                        var form = _form;
                        if (form != null && !_closeQueued)
                        {
                            try
                            {
                                _dispatcher.BeginInvoke((Action)(() => Close(form)));
                                _closeQueued = true;
                            }
                            catch (InvalidOperationException)
                            { /* The user is already closing the form. */ }
                        }
                    }
                }
                if (Thread.CurrentThread != _thread && !_thread.Join(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException($"等待 {typeof(T).Name} 关闭超时。");
            }

            private void Close(T form)
            {
                try
                {
                    form.Close();
                    if (!form.IsDisposed)
                        form.Dispose();
                }
                catch (Exception error) { _reportError(error); }
                finally
                {
                    lock (_gate)
                        _closeQueued = false;
                }
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!TopLevel || FormBorderStyle == FormBorderStyle.None || SystemInformation.HighContrast)
                return;

            // Unsupported DWM attributes leave the system caption colors in effect.
            const int dwmwaCaptionColor = 35;
            const int dwmwaTextColor = 36;
            var background = ColorTranslator.ToWin32(WorkbenchTheme.Surface);
            var foreground = ColorTranslator.ToWin32(WorkbenchTheme.Text);
            if (DwmSetWindowAttribute(Handle, dwmwaCaptionColor, ref background, sizeof(int)) == 0)
                DwmSetWindowAttribute(Handle, dwmwaTextColor, ref foreground, sizeof(int));
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    }

    public class UserControlEx : UserControl
    {
        public UserControlEx()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.ResizeRedraw, true);
            BackColor = WorkbenchTheme.Canvas;
            ForeColor = WorkbenchTheme.Text;
            Font = WorkbenchTheme.BodyFont;
        }
    }

    public enum PanelTone
    {
        Surface,
        Inset,
        Field
    }

    public class PanelEx : Panel
    {
        private PanelTone _tone;

        public PanelEx()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            BackColor = WorkbenchTheme.Surface;
        }

        [DefaultValue(PanelTone.Surface)]
        public PanelTone Tone
        {
            get => _tone;
            set
            {
                if (_tone == value)
                    return;

                _tone = value;
                BackColor = value == PanelTone.Field ? WorkbenchTheme.Field :
                    value == PanelTone.Inset ? WorkbenchTheme.Inset : WorkbenchTheme.Surface;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? WorkbenchTheme.Canvas);
            if (ClientSize.Width < 2 || ClientSize.Height < 2)
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = WorkbenchTheme.RoundedRect(new RectangleF(0.5F, 0.5F, Width - 1, Height - 1), 12))
            using (var fill = new SolidBrush(BackColor))
            using (var border = new Pen(WorkbenchTheme.Stroke))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }
        }
    }
}
