using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
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
