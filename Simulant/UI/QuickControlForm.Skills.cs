using Simulant.Game.Data;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Simulant.Utils.Maths.Angles;

namespace Simulant.UI
{
    internal sealed partial class QuickControlForm
    {
        private readonly Func<uint, Core.SimActionCooldown> _cooldown;
        private readonly Timer _animationTimer;
        private readonly Action<string> _logWarning;
        private Bitmap[] _skillIcons;
        private bool _surecast;
        private uint _invulnerability;
        private static readonly uint[] SkillActions = Simulation.Runtime.SimEntityManager.LocalActions;

        private uint SkillAction(int index) => index == SprintButton ? 3u :
            index == InvulnerabilityButton ? _invulnerability : _surecast ? 7559u : 7548u;

        private bool CanClick(int index) => _enabled[index] && (index < SessionButtonCount ||
            (_skillIcons != null && _cooldown(SkillAction(index)).Remaining <= 0));

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                var data = GameDataService.Instance;
                var icons = await Task.Run(() =>
                {
                    var result = new Bitmap[SkillActions.Length];
                    try
                    {
                        var actions = SkillActions;
                        for (var i = 0; i < actions.Length; i++)
                            result[i] = GameIcon.LoadAction(data, actions[i]);
                        return result;
                    }
                    catch
                    {
                        foreach (var icon in result)
                            icon?.Dispose();
                        throw;
                    }
                });
                if (IsDisposed || Disposing)
                {
                    foreach (var icon in icons)
                        icon.Dispose();
                    return;
                }
                _skillIcons = icons;
                Invalidate();
            }
            catch (Exception error)
            {
                if (!IsDisposed && !Disposing)
                    _logWarning("读取快捷控制技能图标失败：" + error.Message);
            }
        }

        private void PaintSkill(Graphics graphics, int index, RectangleF bounds)
        {
            if (_skillIcons == null)
                return;
            var size = Math.Min(bounds.Width, bounds.Height);
            var square = new RectangleF(bounds.Left + (bounds.Width - size) / 2,
                bounds.Top + (bounds.Height - size) / 2, size, size);
            var icon = _skillIcons[Array.IndexOf(SkillActions, SkillAction(index))];
            var state = graphics.Save();
            using (var outline = WorkbenchTheme.RoundedRect(square, Inset))
            {
                try
                {
                    graphics.SetClip(outline, CombineMode.Intersect);
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(icon, square);
                    var cooldown = _cooldown(SkillAction(index));
                    var remaining = cooldown.Remaining;
                    if (!_enabled[index])
                    {
                        using (var shade = new SolidBrush(WorkbenchTheme.QuickControl.DisabledSkillShade))
                            graphics.FillRectangle(shade, square);
                    }
                    else if (remaining > 0)
                    {
                        // 大圆裁剪为方形图标；按技能实际冷却，从正上方顺时针扫开。
                        var angle = (float)(360 * Math.Min(1, remaining / cooldown.Duration));
                        PaintCooldown(graphics, square, angle);
                    }
                }
                finally
                {
                    graphics.Restore(state);
                }
                using (var border = new Pen(WorkbenchTheme.QuickControl.SkillBorder, Math.Max(1, size / 50)))
                    graphics.DrawPath(border, outline);
            }
        }

        internal static void PaintCooldown(Graphics graphics, RectangleF square, float angle)
        {
            var size = square.Width;
            // GDI+ 在极小圆弧上会误报内存不足；不足一个像素的尾段已不可辨认，直接略过。
            if (!(size > 0 && angle > 0) || float.IsInfinity(angle) ||
                size * 0.92 / 2 * Deg(angle) < 1)
                return;
            using (var shade = new SolidBrush(WorkbenchTheme.QuickControl.CooldownShade))
                graphics.FillPie(shade, square.X - size / 2, square.Y - size / 2,
                    size * 2, size * 2, 270 - angle, angle);
            using (var edge = new Pen(WorkbenchTheme.QuickControl.CooldownEdge, Math.Max(1, size / 35)))
                graphics.DrawArc(edge, square.X + size * 0.04f, square.Y + size * 0.04f,
                    size * 0.92f, size * 0.92f, 270 - angle, angle);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer?.Dispose();
                if (_skillIcons != null)
                {
                    foreach (var icon in _skillIcons)
                        icon.Dispose();
                    _skillIcons = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
