using System;
using System.Numerics;

namespace Simulant.Simulation.Geometry
{
    /// <summary>用于命中判定的二维范围；不依赖实体、预兆绘制或远程内存。</summary>
    public readonly partial struct ActionArea
    {
        private enum AreaKind : byte { None, Circle, Cone, Rectangle, Donut }

        // 只容纳浮点坐标旋转产生的舍入误差，不作为玩家碰撞半径。
        private const double BoundaryTolerance = 0.00001;
        private readonly AreaKind _kind;
        private readonly float _extent;
        private readonly float _secondary;
        private readonly double _halfAngleCosine;

        private ActionArea(AreaKind kind, float extent, float secondary = 0, double halfAngleCosine = 0)
        {
            _kind = kind;
            _extent = extent;
            _secondary = secondary;
            _halfAngleCosine = halfAngleCosine;
        }

        /// <summary>以原点为圆心，半径为 radius 的圆形。</summary>
        public static ActionArea Circle(float radius)
        {
            RequirePositive(radius, nameof(radius), "圆形半径");
            return new ActionArea(AreaKind.Circle, radius);
        }

        /// <summary>以原点为顶点的扇形；角度为完整张角，单位为度。</summary>
        public static ActionArea Cone(float radius, float fullAngleDegrees)
        {
            RequirePositive(radius, nameof(radius), "扇形半径");
            RequirePositive(fullAngleDegrees, nameof(fullAngleDegrees), "扇形完整张角");
            if (fullAngleDegrees > 360)
                throw new ArgumentOutOfRangeException(nameof(fullAngleDegrees), "扇形完整张角不能超过 360 度。");
            return new ActionArea(AreaKind.Cone, radius, halfAngleCosine: Math.Cos(fullAngleDegrees * Math.PI / 360));
        }

        /// <summary>从原点向前延伸 length、左右各延伸 width/2 的矩形。</summary>
        public static ActionArea Rectangle(float length, float width)
        {
            RequirePositive(length, nameof(length), "矩形长度");
            RequirePositive(width, nameof(width), "矩形完整宽度");
            return new ActionArea(AreaKind.Rectangle, length, width / 2);
        }

        /// <summary>以原点为圆心的圆环；内外圆周均属于范围。</summary>
        public static ActionArea Donut(float inner, float outer)
        {
            RequireNonNegative(inner, nameof(inner), "圆环内半径");
            RequirePositive(outer, nameof(outer), "圆环外半径");
            if (inner >= outer)
                throw new ArgumentOutOfRangeException(nameof(inner), "圆环内半径必须小于外半径。");
            return new ActionArea(AreaKind.Donut, outer, inner);
        }

        /// <summary>
        /// 判断点是否位于范围内，包含边界。朝向遵循 ACT 的 atan2(dx,dy)：0 朝 +Y，π/2 朝 +X。
        /// 判定对象为角色中心点，不自动附加角色碰撞半径。
        /// </summary>
        public bool Contains(Vector2 point, Vector2 origin, float heading)
        {
            if (_kind == AreaKind.None)
                throw new InvalidOperationException("技能范围尚未初始化，请使用 Circle、Cone、Rectangle、Donut 或 FromAction 创建范围。");
            if (!IsFinite(point.X) || !IsFinite(point.Y))
                throw new ArgumentOutOfRangeException(nameof(point), "判定点坐标必须是有限数值。");
            if (!IsFinite(origin.X) || !IsFinite(origin.Y))
                throw new ArgumentOutOfRangeException(nameof(origin), "范围原点坐标必须是有限数值。");
            if (!IsFinite(heading))
                throw new ArgumentOutOfRangeException(nameof(heading), "范围朝向必须是有限数值，单位为弧度。");

            // 使用双精度计算差值和平方，避免有限的单精度坐标在相减或平方时溢出。
            var dx = (double)point.X - origin.X;
            var dy = (double)point.Y - origin.Y;
            var distanceSquared = dx * dx + dy * dy;
            var outer = _extent + BoundaryTolerance;
            if (_kind != AreaKind.Rectangle && distanceSquared > outer * outer) return false;

            if (_kind == AreaKind.Circle) return true;
            if (_kind == AreaKind.Donut)
            {
                var inner = Math.Max(0, _secondary - BoundaryTolerance);
                return distanceSquared >= inner * inner;
            }

            var sin = Math.Sin(heading);
            var cos = Math.Cos(heading);
            var forward = dx * sin + dy * cos;
            if (_kind == AreaKind.Cone)
                return forward >= Math.Sqrt(distanceSquared) * _halfAngleCosine - BoundaryTolerance;

            var right = dx * cos - dy * sin;
            return forward >= -BoundaryTolerance && forward <= outer
                && Math.Abs(right) <= _secondary + BoundaryTolerance;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void RequirePositive(float value, string parameter, string description)
        {
            if (!IsFinite(value) || value <= 0)
                throw new ArgumentOutOfRangeException(parameter, description + "必须是大于零的有限数值。");
        }

        private static void RequireNonNegative(float value, string parameter, string description)
        {
            if (!IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(parameter, description + "必须是大于等于零的有限数值。");
        }
    }
}
