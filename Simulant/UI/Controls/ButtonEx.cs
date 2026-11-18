using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace Simulant.UI
{
    public enum ButtonTone
    {
        Default,
        Primary,
        Repeat,
        Danger,
        Start
    }

    public class ButtonEx : Button
    {
        private ButtonTone _tone;

        public ButtonEx()
        {
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
            ApplyTone();
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        [DefaultValue(ButtonTone.Default)]
        public ButtonTone Tone
        {
            get => _tone;
            set
            {
                if (_tone == value)
                    return;

                _tone = value;
                ApplyTone();
                Invalidate();
            }
        }

        private void ApplyTone()
        {
            var colors = WorkbenchTheme.GetButtonColors(_tone);
            BackColor = colors.Background;
            ForeColor = colors.Foreground;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? WorkbenchTheme.Surface);
            if (Width < 2 || Height < 2)
                return;

            var hot = Enabled && ClientRectangle.Contains(PointToClient(MousePosition));
            var pressed = Capture && (MouseButtons & MouseButtons.Left) != 0;
            var background = Enabled ? BackColor : WorkbenchTheme.Field;
            if (hot)
                background = pressed ? ControlPaint.Dark(background, 0.12F) : ControlPaint.Light(background, 0.12F);

            var borderColor = Focused && ShowFocusCues ? WorkbenchTheme.AccentOutline :
                _tone == ButtonTone.Default || !Enabled ? WorkbenchTheme.Stroke : background;
            WorkbenchTheme.PaintButton(e.Graphics, this, background, Enabled ? ForeColor : WorkbenchTheme.Dim,
                borderColor, ShowKeyboardCues);
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
