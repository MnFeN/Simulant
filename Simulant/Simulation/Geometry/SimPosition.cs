using System;
using System.Numerics;

namespace Simulant.Simulation.Geometry
{
    /// <summary>
    /// 点位及其允许的落点范围，使用 ACT 的水平 XY 坐标。
    /// Pos 是场地坐标；角度均为弧度，相对于机制自己的北方：π 为北，π/2 为东，0 为南。
    /// 取点时传入该机制北方在场地中的实际朝向，只有范围方向随之旋转，Pos 不重复旋转。
    /// </summary>
    public readonly struct SimPosition
    {
        private enum Shape : byte { Ellipse, Rectangle, AnnularSector }
        private const double Tolerance = 0.00001;
        private readonly Shape _shape;
        // 椭圆：半长轴、半短轴；矩形：半长、半宽；扇环：外半径、内半径。
        private readonly float _extent, _secondary;
        private readonly double _halfAngle;

        public Vector2 Pos { get; }
        public double Heading { get; }

        private SimPosition(Shape shape, Vector2 pos, float extent, float secondary, double heading, double halfAngle = 0)
        {
            RequirePosition(pos);
            RequireFinite(heading, nameof(heading));
            _shape = shape;
            Pos = pos;
            _extent = extent;
            _secondary = secondary;
            Heading = Math.IEEERemainder(heading, 2 * Math.PI);
            _halfAngle = halfAngle;
        }

        /// <summary>椭圆；省略半短轴时为圆，半径为零时为精确点位。</summary>
        public static SimPosition Ellipse(Vector2 pos, float semiMajor, float? semiMinor = null, double heading = Math.PI)
        {
            var minor = semiMinor ?? semiMajor;
            RequireLength(semiMajor, nameof(semiMajor));
            RequireLength(minor, nameof(semiMinor));
            if (minor > semiMajor)
                throw new ArgumentOutOfRangeException(nameof(semiMinor), "半短轴不能大于半长轴。");
            return new SimPosition(Shape.Ellipse, pos, semiMajor, minor, heading);
        }

        /// <summary>以 Pos 为中心的矩形，沿 heading 前后各 halfLength，垂直方向两侧各 halfWidth。</summary>
        public static SimPosition Rectangle(Vector2 pos, float halfLength, float halfWidth, double heading = Math.PI)
        {
            RequireLength(halfLength, nameof(halfLength));
            RequireLength(halfWidth, nameof(halfWidth));
            if (halfWidth > halfLength)
                throw new ArgumentOutOfRangeException(nameof(halfWidth), "半宽不能大于半长，heading 始终表示长边朝向。");
            return new SimPosition(Shape.Rectangle, pos, halfLength, halfWidth, heading);
        }

        /// <summary>
        /// 以 Pos 为圆心的扇环，依次指定中间半径、相对朝向、径向半宽、角度半宽。
        /// 范围为 radius ± radialTolerance、heading ± angularTolerance；角度单位为弧度。
        /// </summary>
        public static SimPosition AnnularSector(Vector2 pos, float radius, double heading, float radialTolerance, double angularTolerance)
        {
            RequireLength(radius, nameof(radius));
            RequireLength(radialTolerance, nameof(radialTolerance));
            RequireFinite(angularTolerance, nameof(angularTolerance));
            if (radialTolerance > radius)
                throw new ArgumentOutOfRangeException(nameof(radialTolerance), "径向半宽不能大于中间半径。");
            if (angularTolerance < 0 || angularTolerance > Math.PI)
                throw new ArgumentOutOfRangeException(nameof(angularTolerance), "扇环角度半宽必须在 0 至 π 弧度之间。");
            var outer = radius + radialTolerance;
            RequireLength(outer, nameof(radius));
            return new SimPosition(Shape.AnnularSector, pos, outer, radius - radialTolerance, heading, angularTolerance);
        }

        /// <summary>旧的精确坐标直接作为零容差点位使用，保留原有移动终点。</summary>
        public static implicit operator SimPosition(Vector2 pos) => Ellipse(pos, 0);

        /// <summary>只替换中心坐标，保留范围尺寸和相对于机制北方的角度。</summary>
        public SimPosition WithPosition(Vector2 pos) => new SimPosition(_shape, pos, _extent, _secondary, Heading, _halfAngle);

        /// <summary>返回范围的代表落点；扇环取中间半径与中轴交点，不能把扇环圆心当作站位。</summary>
        public Vector2 Midpoint(double northHeading = Math.PI)
        {
            var heading = WorldHeading(northHeading);
            if (_shape != Shape.AnnularSector) return Pos;
            var radius = ((double)_extent + _secondary) * 0.5;
            return Pos + new Vector2((float)(radius * Math.Sin(heading)), (float)(radius * Math.Cos(heading)));
        }

        /// <summary>范围内任一点到代表落点的距离上界，用于为独立随机落点保留判定余量。</summary>
        internal double DeviationRadius
        {
            get
            {
                if (_shape == Shape.Rectangle) return Math.Sqrt((double)_extent * _extent + (double)_secondary * _secondary);
                if (_shape != Shape.AnnularSector) return _extent;
                var radius = ((double)_extent + _secondary) * 0.5;
                var halfWidth = ((double)_extent - _secondary) * 0.5;
                return Math.Sqrt(halfWidth * halfWidth + 2 * _extent * radius * (1 - Math.Cos(_halfAngle)));
            }
        }

        /// <summary>按比例缩小容差，扇环保持中间半径；可同时调整相对中轴以保留指定角边界。</summary>
        internal SimPosition ScaleTolerance(float scale, double? heading = null)
        {
            RequireLength(scale, nameof(scale));
            if (scale > 1) throw new ArgumentOutOfRangeException(nameof(scale), "缩小容差的比例不能大于一。");
            if (_shape != Shape.AnnularSector)
                return new SimPosition(_shape, Pos, _extent * scale, _secondary * scale, heading ?? Heading);
            var radius = (float)(((double)_extent + _secondary) * 0.5);
            var halfWidth = (float)(((double)_extent - _secondary) * 0.5 * scale);
            return new SimPosition(_shape, Pos, radius + halfWidth, radius - halfWidth, heading ?? Heading, _halfAngle * scale);
        }

        /// <summary>
        /// 随机取一个落点；不要求面积均匀。northHeading 为本机制相对北的场地朝向，默认场地正北。
        /// 应在接收移动指令时取一次点，而不是在移动更新时不断重抽。
        /// </summary>
        public Vector2 Sample(Random random, double northHeading = Math.PI)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var heading = WorldHeading(northHeading);
            if (_extent == 0 && _secondary == 0) return Pos;
            double forward, lateral;
            switch (_shape)
            {
                case Shape.Rectangle:
                    forward = (random.NextDouble() * 2 - 1) * _extent;
                    lateral = (random.NextDouble() * 2 - 1) * _secondary;
                    break;
                case Shape.AnnularSector:
                    var radius = _secondary + random.NextDouble() * ((double)_extent - _secondary);
                    var offset = (random.NextDouble() * 2 - 1) * _halfAngle;
                    forward = radius * Math.Cos(offset);
                    lateral = radius * Math.Sin(offset);
                    break;
                default:
                    var scale = random.NextDouble();
                    var angle = random.NextDouble() * 2 * Math.PI;
                    forward = _extent * scale * Math.Cos(angle);
                    lateral = _secondary * scale * Math.Sin(angle);
                    break;
            }
            var sin = Math.Sin(heading);
            var cos = Math.Cos(heading);
            var point = new Vector2((float)(Pos.X + forward * sin + lateral * cos),
                (float)(Pos.Y + forward * cos - lateral * sin));
            RequirePosition(point);
            return point;
        }

        /// <summary>判断场地点是否落在允许范围内，包含边界；相对北必须与取点时一致。</summary>
        public bool Contains(Vector2 point, double northHeading = Math.PI)
        {
            RequirePosition(point);
            var heading = WorldHeading(northHeading);
            var dx = (double)point.X - Pos.X;
            var dy = (double)point.Y - Pos.Y;
            var forward = dx * Math.Sin(heading) + dy * Math.Cos(heading);
            var lateral = dx * Math.Cos(heading) - dy * Math.Sin(heading);
            if (_shape == Shape.Rectangle)
                return Math.Abs(forward) <= _extent + Tolerance && Math.Abs(lateral) <= _secondary + Tolerance;
            if (_shape == Shape.AnnularSector)
            {
                var radius = Math.Sqrt(dx * dx + dy * dy);
                return radius >= _secondary - Tolerance && radius <= _extent + Tolerance &&
                    forward >= radius * Math.Cos(_halfAngle) - Tolerance;
            }
            // 同时支持退化成线段或单点的椭圆，不对零轴长做除法。
            if (_secondary == 0)
                return Math.Abs(forward) <= _extent + Tolerance && Math.Abs(lateral) <= Tolerance;
            var x = forward / (_extent + Tolerance);
            var y = lateral / (_secondary + Tolerance);
            return x * x + y * y <= 1;
        }

        private double WorldHeading(double northHeading)
        {
            RequireFinite(northHeading, nameof(northHeading));
            return Math.IEEERemainder(northHeading, 2 * Math.PI) + Heading - Math.PI;
        }

        private static void RequirePosition(Vector2 pos)
        {
            if (!IsFinite(pos.X) || !IsFinite(pos.Y))
                throw new ArgumentOutOfRangeException(nameof(pos), "点位坐标必须是有限数值。");
        }

        private static void RequireLength(float value, string name)
        {
            if (!IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(name, "点位范围的轴长、边长和半径必须是非负有限数值。");
        }

        private static void RequireFinite(double value, string name)
        {
            if (!IsFinite(value)) throw new ArgumentOutOfRangeException(name, "点位角度必须是有限数值，单位为弧度。");
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
