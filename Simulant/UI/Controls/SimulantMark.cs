using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Simulant.UI
{
    public sealed class SimulantMark : Control
    {
        private const float GridSize = 128F;
        private const float Center = GridSize / 2F;
        private static readonly PointF[] InnerOutline = CreateHexagon(39.5F);
        private static readonly PointF[] OuterLight = CreateOuterSegment();
        private static readonly PointF[] OuterDark = Array.ConvertAll(OuterLight, RotateHalfTurn);
        private static readonly PointF[] Glyph = CreateGlyph();

        public SimulantMark()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var side = Math.Min(ClientSize.Width, ClientSize.Height);
            if (side < 2)
                return;

            var graphics = e.Graphics;
            var state = graphics.Save();
            try
            {
                graphics.TranslateTransform((ClientSize.Width - side) / 2F, (ClientSize.Height - side) / 2F);
                graphics.ScaleTransform(side / GridSize, side / GridSize);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using (var outline = new Pen(WorkbenchTheme.Mark.InnerOutline, 2.45F) { LineJoin = LineJoin.Round })
                    graphics.DrawPolygon(outline, InnerOutline);

                DrawStroke(graphics, OuterLight, WorkbenchTheme.Mark.OuterLight, 4.8F);
                DrawStroke(graphics, OuterDark, WorkbenchTheme.Mark.OuterDark, 4.8F);
                DrawStroke(graphics, Glyph, WorkbenchTheme.Mark.Glyph, 8.2F);
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private static PointF[] CreateHexagon(float radius)
        {
            var halfWidth = radius * (float)Math.Sqrt(3) / 2F;
            var halfHeight = radius / 2F;
            return new[]
            {
                new PointF(Center, Center - radius),
                new PointF(Center + halfWidth, Center - halfHeight),
                new PointF(Center + halfWidth, Center + halfHeight),
                new PointF(Center, Center + radius),
                new PointF(Center - halfWidth, Center + halfHeight),
                new PointF(Center - halfWidth, Center - halfHeight)
            };
        }

        private static PointF[] CreateOuterSegment()
        {
            var hexagon = CreateHexagon(49F);
            return new[]
            {
                Interpolate(hexagon[0], hexagon[1], 0.45F),
                hexagon[0],
                hexagon[5],
                hexagon[4],
                Interpolate(hexagon[4], hexagon[3], 0.25F)
            };
        }

        private static PointF[] CreateGlyph()
        {
            var slope = 1F / (float)Math.Sqrt(3);
            var upper = new[]
            {
                new PointF(81F, 40F),
                new PointF(61F, 40F),
                new PointF(44F, 40F + (61F - 44F) * slope),
                new PointF(44F, Center - (Center - 44F) * slope),
                new PointF(Center, Center)
            };
            var points = new PointF[upper.Length * 2 - 1];
            upper.CopyTo(points, 0);
            for (var i = 0; i < upper.Length - 1; i++)
                points[points.Length - 1 - i] = RotateHalfTurn(upper[i]);

            return points;
        }

        private static PointF RotateHalfTurn(PointF point) => new PointF(GridSize - point.X, GridSize - point.Y);

        private static PointF Interpolate(PointF from, PointF to, float fraction)
            => new PointF(from.X + (to.X - from.X) * fraction, from.Y + (to.Y - from.Y) * fraction);

        private static void DrawStroke(Graphics graphics, PointF[] points, Color color, float width)
        {
            using (var pen = new Pen(color, width)
            {
                LineJoin = LineJoin.Round,
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            })
                graphics.DrawLines(pen, points);
        }
    }
}
