using System;
using System.Numerics;
using static System.Math;

namespace Simulant.Utils.Maths
{
    public static class EquationHelper
    {

        /// <summary> 求解 Ax² + Bx + C = 0，递增顺序返回两个根或 null </summary>
        public static (double, double)? SolveQuadratic(double A, double B, double C, double eps = 1e-8)
        {
            if (Abs(A) <= eps)
            {
                throw new ArgumentException("A must not be zero");
            }

            var Δ = B * B - 4 * A * C;

            if (Δ < -eps)
                return null;
            else if (Abs(Δ) <= eps)
            {
                var x = -B / (2 * A);
                return (x, x);
            }
            else
            {
                var sqrtΔ = Sqrt(Δ);
                var x1 = (-B + sqrtΔ) / (2 * A);
                var x2 = (-B - sqrtΔ) / (2 * A);
                return (x2, x1);
            }
        }

        /// <summary>
        /// 求 (x - x0)² + (y - y0)² = r² 与 ax + by + c = 0 的交点，
        /// 按照 sorter 的顺序（如果提供了）返回两个交点坐标或 null（无交点）
        /// </summary>
        public static (Vector2, Vector2)? CircleLineIntersections(
            double x0, double y0, double r,
            double a, double b, double c,
            Func<Vector2, double> sorter = null)
        {
            if (Abs(a) > Abs(b))
            {
                // 直线偏向竖直方向，交换 x 和 y 复用代码
                var result = CircleLineIntersections(y0, x0, r, b, a, c, null);

                if (result == null)
                    return null;

                var p1 = new Vector2(result.Value.Item1.Y, result.Value.Item1.X);
                var p2 = new Vector2(result.Value.Item2.Y, result.Value.Item2.X);
                return sorter != null && sorter(p1) > sorter(p2) ? (p2, p1) : (p1, p2);
            }

            // y = -a/b x - c/b 代入圆方程
            // (x - x0)² + (a/b x + c/b + y0)² = r²
            // (1 + a²/b²) x² + 2(-x0 + a/b(c/b + y0)) x + (x0² + (c/b + y0)² - r²) = 0
            var yOffset = c / b + y0;
            var A = 1 + a * a / (b * b);
            var B = 2 * (-x0 + a / b * yOffset);
            var C = x0 * x0 + yOffset * yOffset - r * r;

            var x12 = SolveQuadratic(A, B, C);

            if (x12 == null) // 无交点
            {
                return null;
            }
            else // 1-2 个交点
            {
                var x1 = x12.Value.Item1;
                var y1 = -a / b * x1 - c / b;
                var x2 = x12.Value.Item2;
                var y2 = -a / b * x2 - c / b;
                var p1 = new Vector2((float)x1, (float)y1);
                var p2 = new Vector2((float)x2, (float)y2);
                return sorter != null && sorter(p1) > sorter(p2) ? (p2, p1) : (p1, p2);
            }
        }

        /// <summary>
        /// 求 (x - x0)² + (y - y0)² = r² 与 (x1, y1)、(x2, y2) 连线的交点，
        /// 按照 sorter 的顺序（如果提供了）返回两个交点坐标或 null（无交点）
        /// </summary>
        public static (Vector2, Vector2)? CircleLineIntersections(
            double x0, double y0, double r,
            double x1, double y1, double x2, double y2,
            Func<Vector2, double> sorter = null)
        {
            // ax + by + c = 0
            var dx = x2 - x1;
            var dy = y2 - y1;
            var a = dy;
            var b = -dx;
            var c = -(a * x1 + b * y1);
            return CircleLineIntersections(x0, y0, r, a, b, c, sorter);
        }

        /// <summary>
        /// 求 (x - x0)² + (y - y0)² = r² 与 point1、point2 连线的交点，
        /// 按照 sorter 的顺序（如果提供了）返回两个交点坐标或 null（无交点）
        /// </summary>
        public static (Vector2, Vector2)? CircleLineIntersections(
            double x0, double y0, double r, Vector2 point1, Vector2 point2,
            Func<Vector2, double> sorter = null)
            => CircleLineIntersections(x0, y0, r, point1.X, point1.Y, point2.X, point2.Y, sorter);
    }
}
