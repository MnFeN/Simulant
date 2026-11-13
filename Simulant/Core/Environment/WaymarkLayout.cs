using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Simulant.Core.Environment
{
    /// <summary>场地标点布局。坐标沿用模拟器约定：XY 为地面，Z 为高度；输出 place 参数时统一交换 Y/Z。</summary>
    public sealed class WaymarkLayout
    {
        private const string Marks = "ABCD1234";
        private static readonly string[] PlaceNames = { "A", "B", "C", "D", "One", "Two", "Three", "Four" };
        private readonly Vector3 _origin;
        private readonly Vector3?[] _positions = new Vector3?[8];
        private double _northHeading = Math.PI;

        private WaymarkLayout(Vector3 origin)
        {
            ValidatePosition(origin);
            _origin = origin;
        }

        public static WaymarkLayout Center(float x, float y, float z = 0)
            => new WaymarkLayout(new Vector3(x, y, z));

        public Vector3? this[string mark] => Position(Index(mark));

        /// <summary>设置整套布局的相对北朝向；默认 π 朝北，0 朝南、π/2 朝东。重复调用替换朝向，不累加。</summary>
        public WaymarkLayout Rotate(double northHeading)
        {
            ValidateAngle(northHeading);
            foreach (var position in _positions)
                if (position.HasValue) ToWorld(position.Value, northHeading);
            _northHeading = northHeading;
            return this;
        }

        /// <summary>设置相对于布局原点的位置；未设置的标点会被清除。</summary>
        public WaymarkLayout At(string mark, double x, double y, double z = 0)
        {
            var position = new Vector3((float)x, (float)y, (float)z);
            ToWorld(position, _northHeading);
            _positions[Index(mark)] = position;
            return this;
        }

        /// <summary>设置布局内的极坐标，再按 Rotate 的相对北旋转。角度单位为弧度，0 朝南、π 朝北，正向为逆时针。</summary>
        public WaymarkLayout Polar(string mark, double radius, double angle, float height = 0)
        {
            ValidateRadius(radius);
            ValidateAngle(angle);
            return At(mark, (float)(radius * Math.Sin(angle)), (float)(radius * Math.Cos(angle)), height);
        }

        /// <summary>按指定顺序逆时针均分圆周，半径依次循环；startFromNorth 为 false 时，相对北位于首末两点之间的角平分线上。</summary>
        public WaymarkLayout Circle(string order, bool startFromNorth, params double[] radii)
        {
            if (string.IsNullOrEmpty(order) || order.Length > 8)
                throw new ArgumentException("圆周排列需要提供 1～8 个标点名。", nameof(order));
            if (radii == null || radii.Length == 0)
                throw new ArgumentException("圆周排列需要提供至少一个半径。", nameof(radii));
            foreach (var radius in radii) ValidateRadius(radius);
            var used = 0;
            for (var i = 0; i < order.Length; i++)
            {
                var index = Marks.IndexOf(char.ToUpperInvariant(order[i]));
                if (index < 0 || (used & (1 << index)) != 0)
                    throw new ArgumentException("标点名必须为 ABCD1234，且不能重复。", nameof(order));
                used |= 1 << index;
            }
            for (var i = 0; i < order.Length; i++)
                Polar(order[i].ToString(), radii[i % radii.Length],
                    Math.PI + 2 * Math.PI * (i + (startFromNorth ? 0 : 0.5)) / order.Length);
            return this;
        }

        /// <summary>生成 PostNamazu place 的完整本地布局；一次提交八个标点，不保留上一套布局的残留。</summary>
        public string ToPlaceJson()
        {
            var json = new StringBuilder("{\"LocalOnly\":true,\"Log\":false");
            for (var i = 0; i < _positions.Length; i++)
            {
                json.Append(",\"").Append(PlaceNames[i]).Append("\":{");
                if (Position(i) is Vector3 position)
                {
                    json.Append("\"X\":").Append(position.X.ToString("R", CultureInfo.InvariantCulture))
                        .Append(",\"Y\":").Append(position.Z.ToString("R", CultureInfo.InvariantCulture))
                        .Append(",\"Z\":").Append(position.Y.ToString("R", CultureInfo.InvariantCulture))
                        .Append(",\"Active\":true");
                }
                else json.Append("\"Active\":false");
                json.Append('}');
            }
            return json.Append('}').ToString();
        }

        public static string ClearJson { get; } = Center(0, 0).ToPlaceJson();

        private Vector3? Position(int index)
            => _positions[index].HasValue ? ToWorld(_positions[index].Value, _northHeading) : (Vector3?)null;

        private Vector3 ToWorld(Vector3 offset, double northHeading)
        {
            var angle = northHeading - Math.PI;
            var sin = Math.Sin(angle);
            var cos = Math.Cos(angle);
            var position = new Vector3((float)(_origin.X + offset.X * cos + offset.Y * sin),
                (float)(_origin.Y + offset.Y * cos - offset.X * sin), _origin.Z + offset.Z);
            ValidatePosition(position);
            return position;
        }

        private static int Index(string mark)
        {
            var index = mark != null && mark.Length == 1 ? Marks.IndexOf(char.ToUpperInvariant(mark[0])) : -1;
            if (index < 0) throw new ArgumentException("标点名必须为单个 A～D 或 1～4。", nameof(mark));
            return index;
        }

        private static void ValidateAngle(double angle)
        {
            if (double.IsNaN(angle) || double.IsInfinity(angle))
                throw new ArgumentOutOfRangeException(nameof(angle), "标点角度必须是有限数值。");
        }

        private static void ValidateRadius(double radius)
        {
            if (double.IsNaN(radius) || double.IsInfinity(radius) || radius < 0)
                throw new ArgumentOutOfRangeException(nameof(radius), "标点半径必须是有限的非负数。");
        }

        private static void ValidatePosition(Vector3 position)
        {
            if (float.IsNaN(position.X) || float.IsInfinity(position.X) ||
                float.IsNaN(position.Y) || float.IsInfinity(position.Y) ||
                float.IsNaN(position.Z) || float.IsInfinity(position.Z))
                throw new ArgumentOutOfRangeException(nameof(position), "标点坐标必须是有限数值。");
        }
    }
}
