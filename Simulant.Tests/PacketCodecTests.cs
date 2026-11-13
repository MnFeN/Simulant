using Simulant.Game;
using System;

namespace Simulant.Tests
{
    internal static class PacketCodecTests
    {
        public static void CoordinateEncodingClampsBeforeIntegerConversion()
        {
            foreach (var coordinate in new[] { 1001f, 1e10f, float.MaxValue })
                Assert(PacketCodec.EncodeUShortCoord(coordinate) == ushort.MaxValue,
                    "超出上界的有限坐标必须截断至上界，不能因整数溢出落到地图另一侧：" + coordinate);
            foreach (var coordinate in new[] { -1001f, -1e10f, float.MinValue })
                Assert(PacketCodec.EncodeUShortCoord(coordinate) == 0,
                    "超出下界的有限坐标必须截断至下界：" + coordinate);
        }

        public static void CoordinateAndHeadingEncodingRejectNonFiniteInputs()
        {
            foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                ThrowsArgumentOutOfRange(() => PacketCodec.EncodeUShortCoord(value), "coord");
                ThrowsArgumentOutOfRange(() => PacketCodec.EncodeUShortHeading(value), "heading");
            }
        }

        public static void PacketCoordinateAndHeadingQuantizationRemainStable()
        {
            Assert(PacketCodec.EncodeUShortCoord(-1000) == 0 && PacketCodec.EncodeUShortCoord(0) == 32768 &&
                PacketCodec.EncodeUShortCoord(1000) == ushort.MaxValue && PacketCodec.EncodeUShortHeading(0) == 32768,
                "正常坐标的边界、原点或零朝向编码发生了变化。");
            for (var raw = 0; raw <= ushort.MaxValue; raw++)
            {
                var code = (ushort)raw;
                Assert(PacketCodec.EncodeUShortCoord(PacketCodec.DecodeUShortCoord(code)) == code,
                    "可表示坐标解码后再编码必须回到原有量化格：" + raw);
                var headingCode = PacketCodec.EncodeUShortHeading(PacketCodec.DecodeUShortHeading(code));
                Assert(headingCode == code ||
                    ((code == 0 || code == ushort.MaxValue) && (headingCode == 0 || headingCode == ushort.MaxValue)),
                    "可表示朝向必须保持原有量化格，首尾边界可表示同一个方向：" + raw);
            }
            var heading = 0.75f;
            var expected = PacketCodec.EncodeUShortHeading(heading);
            for (var turns = -8; turns <= 8; turns++)
            {
                var wrapped = PacketCodec.EncodeUShortHeading((float)(heading + turns * Math.PI * 2));
                Assert(Math.Abs(wrapped - expected) <= 1, "增加整圈后的朝向应保持相同，允许单格浮点量化误差。");
            }
        }

        private static void ThrowsArgumentOutOfRange(Action action, string parameter)
        {
            try { action(); }
            catch (ArgumentOutOfRangeException error)
            {
                Assert(error.ParamName == parameter, "非有限输入应指出具体参数。");
                return;
            }
            throw new Exception("非有限参数必须在构包时被拒绝：" + parameter);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
