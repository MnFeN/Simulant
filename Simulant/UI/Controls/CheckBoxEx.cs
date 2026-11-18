using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Simulant.UI
{
    public enum CheckBoxStyle
    {
        CheckBox,
        Switch,
        Chip,
        ToggleButton
    }

    public enum CheckBoxTone
    {
        Default,
        Error,
        Warning,
        Info,
        Neutral,
        Success
    }

    public class CheckBoxEx : CheckBox
    {
        private const int ChipTextLeft = 16;
        private const int ChipTextPadding = ChipTextLeft + 5;

        private CheckBoxStyle _style;
        private CheckBoxTone _tone;
        private Color _accent;
        private bool _useAccentText = true;

        public CheckBoxEx()
        {
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        [DefaultValue(CheckBoxStyle.CheckBox)]
        public CheckBoxStyle Style
        {
            get => _style;
            set
            {
                _style = value;
                Invalidate();
            }
        }

        [DefaultValue(CheckBoxTone.Default)]
        public CheckBoxTone Tone
        {
            get => _tone;
            set
            {
                _tone = value;
                Invalidate();
            }
        }

        [DefaultValue(typeof(Color), "Empty")]
        public Color Accent
        {
            get => _accent;
            set
            {
                _accent = value;
                Invalidate();
            }
        }

        [DefaultValue(true)]
        public bool UseAccentText
        {
            get => _useAccentText;
            set
            {
                _useAccentText = value;
                Invalidate();
            }
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var size = base.GetPreferredSize(proposedSize);
            if (Style == CheckBoxStyle.Chip)
                size.Width = Math.Max(size.Width, TextRenderer.MeasureText(Text, Font).Width + ChipTextPadding);
            return size;
        }

        private Color ResolveAccent()
        {
            if (!_accent.IsEmpty)
                return _accent;

            switch (_tone)
            {
                case CheckBoxTone.Error: return WorkbenchTheme.Red;
                case CheckBoxTone.Warning: return WorkbenchTheme.Yellow;
                case CheckBoxTone.Info: return WorkbenchTheme.Blue;
                case CheckBoxTone.Neutral: return WorkbenchTheme.Muted;
                case CheckBoxTone.Success: return WorkbenchTheme.Green;
                default: return WorkbenchTheme.Accent;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? WorkbenchTheme.Surface);
            if (Width < 2 || Height < 4)
                return;

            var accent = ResolveAccent();
            var active = CheckState != CheckState.Unchecked && Enabled;
            var borderColor = Focused && ShowFocusCues ? WorkbenchTheme.AccentOutline : WorkbenchTheme.Stroke;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            switch (Style)
            {
                case CheckBoxStyle.Chip:
                    PaintChip(e.Graphics, active, accent, borderColor);
                    break;
                case CheckBoxStyle.ToggleButton:
                    PaintToggleButton(e.Graphics, borderColor);
                    break;
                case CheckBoxStyle.Switch:
                    PaintSwitch(e.Graphics, active, accent, borderColor);
                    break;
                default:
                    PaintCheck(e.Graphics, active, accent, borderColor);
                    break;
            }
        }

        private void PaintChip(Graphics graphics, bool active, Color accent, Color border)
        {
            var bounds = new RectangleF(0.5F, 0.5F, Width - 1, Height - 1);
            PaintTrack(graphics, bounds, 7, active, border);
            using (var mark = new SolidBrush(active ? accent : WorkbenchTheme.Dim))
            {
                var y = (Height - 5) / 2F;
                if (CheckState == CheckState.Indeterminate)
                    graphics.FillRectangle(mark, 8, y + 2, 5, 2);
                else
                    graphics.FillEllipse(mark, 8, y, 5, 5);
            }

            var ink = Enabled ? (UseAccentText ? active ? accent : WorkbenchTheme.Muted : ForeColor) : WorkbenchTheme.Dim;
            PaintText(graphics, new Rectangle(ChipTextLeft, 0, Math.Max(0, Width - ChipTextPadding), Height), ink);
        }

        private void PaintToggleButton(Graphics graphics, Color border)
        {
            var hot = Enabled && ClientRectangle.Contains(PointToClient(MousePosition));
            var pressed = Capture && (MouseButtons & MouseButtons.Left) != 0;
            var fill = BackColor;
            if (hot)
                fill = pressed ? ControlPaint.Dark(fill, 0.12F) : ControlPaint.Light(fill, 0.12F);

            WorkbenchTheme.PaintButton(graphics, this, fill, ForeColor, border, ShowKeyboardCues);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (Style == CheckBoxStyle.ToggleButton)
                Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (Style == CheckBoxStyle.ToggleButton)
                Invalidate();
        }

        private void PaintSwitch(Graphics graphics, bool active, Color accent, Color border)
        {
            var height = Math.Min(Height - 3, 22);
            var track = new RectangleF(0.5F, (Height - height) / 2F, Math.Min(37, Width - 1), height);
            PaintTrack(graphics, track, height / 2, active, border);
            var dot = Math.Max(1, height - 8);
            using (var thumb = new SolidBrush(active ? accent : WorkbenchTheme.Dim))
                graphics.FillEllipse(thumb, active ? track.Right - dot - 4 : track.Left + 4,
                    track.Top + (height - dot) / 2, dot, dot);

            if (!string.IsNullOrEmpty(Text))
                PaintText(graphics, new Rectangle(42, 0, Math.Max(0, Width - 42), Height),
                    Enabled ? ForeColor : WorkbenchTheme.Dim);
        }

        private void PaintCheck(Graphics graphics, bool active, Color accent, Color border)
        {
            var side = Math.Min(14, Height - 2);
            var right = CheckAlign == ContentAlignment.TopRight || CheckAlign == ContentAlignment.MiddleRight ||
                CheckAlign == ContentAlignment.BottomRight;
            var box = new RectangleF(right ? Width - side - 1 : 1, (Height - side) / 2F, side, side);
            PaintTrack(graphics, box, 3, active, border);

            if (CheckState != CheckState.Unchecked)
                using (var pen = new Pen(Enabled ? accent : WorkbenchTheme.Dim, 1.5F)
                {
                    LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round
                })
                {
                    if (CheckState == CheckState.Indeterminate)
                        graphics.DrawLine(pen, box.Left + 3, box.Top + side / 2, box.Right - 3, box.Top + side / 2);
                    else
                        graphics.DrawLines(pen, new[]
                        {
                            new PointF(box.Left + side * 0.24F, box.Top + side * 0.52F),
                            new PointF(box.Left + side * 0.44F, box.Top + side * 0.73F),
                            new PointF(box.Left + side * 0.77F, box.Top + side * 0.29F)
                        });
                }

            PaintText(graphics, new Rectangle(right ? 0 : side + 4, 0, Math.Max(0, Width - side - 4), Height),
                Enabled ? ForeColor : WorkbenchTheme.Dim);
        }

        private static void PaintTrack(Graphics graphics, RectangleF bounds, float radius, bool active, Color border)
        {
            using (var path = WorkbenchTheme.RoundedRect(bounds, radius))
            using (var fill = new SolidBrush(active ? WorkbenchTheme.Selection : WorkbenchTheme.Field))
            using (var outline = new Pen(border))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(outline, path);
            }
        }

        private void PaintText(Graphics graphics, Rectangle bounds, Color color)
        {
            TextRenderer.DrawText(graphics, Text, Font, bounds, color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                (UseMnemonic ? (ShowKeyboardCues ? TextFormatFlags.Default : TextFormatFlags.HidePrefix) : TextFormatFlags.NoPrefix));
        }
    }
}
