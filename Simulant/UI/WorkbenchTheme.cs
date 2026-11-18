using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Simulant.UI
{
    internal static class WorkbenchTheme
    {
        internal static readonly Color Canvas = Color.FromArgb(15, 20, 30);
        internal static readonly Color Surface = Color.FromArgb(23, 31, 44);
        internal static readonly Color Field = Color.FromArgb(30, 40, 55);
        internal static readonly Color Hover = Color.FromArgb(42, 55, 73);
        internal static readonly Color Stroke = Color.FromArgb(49, 62, 81);
        internal static readonly Color Text = Color.FromArgb(234, 240, 248);
        internal static readonly Color Muted = Color.FromArgb(161, 178, 198);
        internal static readonly Color Dim = Color.FromArgb(125, 144, 166);
        internal static readonly Color InactiveTabText = Color.FromArgb(128, 128, 128);
        internal static readonly Color Mint = Color.FromArgb(108, 225, 194);
        internal static readonly Color Yellow = Color.FromArgb(247, 204, 111);
        internal static readonly Color Red = Color.FromArgb(247, 145, 154);
        internal static readonly Color PaleRed = Color.FromArgb(249, 190, 196);
        internal static readonly Color Green = Color.FromArgb(112, 209, 155);
        internal static readonly Color Blue = Color.FromArgb(146, 180, 240);
        internal static readonly Color Accent = Color.FromArgb(117, 178, 255);
        internal static readonly Color AccentOutline = Color.FromArgb(76, 120, 177);
        internal static readonly Color Selection = Color.FromArgb(42, 65, 87);
        internal static readonly Color Invalid = Color.FromArgb(62, 37, 49);

        internal static readonly Color Inset = Color.FromArgb(19, 26, 38);
        internal static readonly Color Alternate = Color.FromArgb(18, 25, 36);
        internal static readonly Color Danger = Color.FromArgb(74, 44, 57);

        private const string FontFamilyName = "Microsoft YaHei UI";

        internal static readonly Font BodyFont = new Font(FontFamilyName, 10F);
        internal static readonly Font SmallFont = new Font(FontFamilyName, 9F);
        internal static readonly Font SectionFont = new Font(FontFamilyName, 11F, FontStyle.Bold);
        internal static readonly Font TitleFont = new Font(FontFamilyName, 16F, FontStyle.Bold);
        internal static readonly Font PresetTitleFont = new Font(FontFamilyName, 13F, FontStyle.Bold);

        internal static class QuickControl
        {
            internal static readonly Color Background = Color.FromArgb(30, 33, 39);
            internal static readonly Color Button = Color.FromArgb(40, 44, 51);
            internal static readonly Color ButtonHover = Color.FromArgb(58, 62, 70);
            internal static readonly Color ButtonPressed = Color.FromArgb(69, 74, 82);
            internal static readonly Color DisabledSkillShade = Color.FromArgb(155, 22, 25, 31);
            internal static readonly Color SkillBorder = Color.FromArgb(95, 180, 185, 195);
            internal static readonly Color CooldownShade = Color.FromArgb(185, 12, 17, 27);
            internal static readonly Color CooldownEdge = Color.FromArgb(210, 200, 228, 255);

            internal static Color DisabledAction(Color accent)
                => Color.FromArgb(accent.R / 2, accent.G / 2, accent.B / 2);
        }

        internal static class Mark
        {
            internal static readonly Color InnerOutline = Color.FromArgb(65, 107, 170);
            internal static readonly Color OuterLight = Color.FromArgb(143, 184, 255);
            internal static readonly Color OuterDark = Color.FromArgb(114, 152, 215);
            internal static readonly Color Glyph = Color.FromArgb(213, 228, 255);
        }

        internal static (Color Background, Color Foreground) GetButtonColors(ButtonTone tone)
        {
            switch (tone)
            {
                case ButtonTone.Primary: return (Blue, Canvas);
                case ButtonTone.Start: return (Mint, Canvas);
                case ButtonTone.Repeat: return (Yellow, Canvas);
                case ButtonTone.Danger: return (Danger, Red);
                default: return (Field, Text);
            }
        }

        internal static void PaintButton(Graphics graphics, ButtonBase button, Color background, Color ink,
            Color border, bool showKeyboardCues)
        {
            var bounds = button.ClientRectangle;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRect(new RectangleF(0.5F, 0.5F, bounds.Width - 1, bounds.Height - 1), 8))
            using (var fill = new SolidBrush(background))
            using (var outline = new Pen(border))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(outline, path);
            }

            bounds.Inflate(-button.LogicalToDeviceUnits(5), 0);
            // The visible glyphs sit slightly below the center of the font's line box.
            bounds.Offset(0, -button.LogicalToDeviceUnits(1));
            var mnemonic = button.UseMnemonic
                ? (showKeyboardCues ? TextFormatFlags.Default : TextFormatFlags.HidePrefix)
                : TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(graphics, button.Text, button.Font, bounds, ink, mnemonic |
                TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        internal static GraphicsPath RoundedRect(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
