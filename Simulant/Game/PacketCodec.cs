using System;

using System.Text;

namespace Simulant.Game
{
    public static class PacketCodec
    {
        private static readonly UTF8Encoding ActorNameEncoding = new UTF8Encoding(false, true);

        /// <summary>角色包的名字保留一个零字节结尾，不截断 UTF-8 字符。</summary>
        internal static byte[] EncodeActorName(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (name.IndexOf('\0') >= 0) throw new ArgumentException("实体名字不能包含空字符。", nameof(name));
            var bytes = new byte[32];
            try
            {
                ActorNameEncoding.GetByteCount(name);
                var characters = name.ToCharArray();
                ActorNameEncoding.GetEncoder().Convert(characters, 0, characters.Length, bytes, 0, 31, true,
                    out _, out _, out _);
            }
            catch (EncoderFallbackException error)
            {
                throw new ArgumentException("实体名字包含无效的 Unicode 字符。", nameof(name), error);
            }
            return bytes;
        }

        private const double CoordScale = 3.0518043 * 0.0099999998;
        private const double HeadingScale = 0.009587526 * 0.0099999998;
        private const double TwoPi = Math.PI * 2.0;

        /// <summary>
        /// Decodes a packet coordinate encoded as UInt16.
        /// </summary>
        public static float DecodeUShortCoord(ushort value)
        {
            return (float)(value * CoordScale - 1000.0);
        }

        /// <summary>
        /// Encodes an in-game coordinate into the packet UInt16 format.
        /// Coordinates are clamped to the representable packet range.
        /// </summary>
        public static ushort EncodeUShortCoord(float coord)
        {
            if (float.IsNaN(coord) || float.IsInfinity(coord))
                throw new ArgumentOutOfRangeException(nameof(coord), "坐标必须为有限数。");
            // 先在浮点范围内截断，避免极大坐标转成整数时溢出并反向落到下界。
            var raw = Math.Round((coord + 1000.0) / CoordScale);

            if (raw < 0)
                raw = 0;
            else if (raw > ushort.MaxValue)
                raw = ushort.MaxValue;

            return (ushort)raw;
        }

        /// <summary>
        /// Decodes a packet heading encoded as UInt16 into an in-game heading.
        /// </summary>
        public static float DecodeUShortHeading(ushort heading)
        {
            return (float)(heading * HeadingScale - Math.PI);
        }

        /// <summary>
        /// Encodes an in-game heading into the packet UInt16 format.
        /// Heading is wrapped by 2π instead of clamped.
        /// </summary>
        public static ushort EncodeUShortHeading(float heading)
        {
            if (float.IsNaN(heading) || float.IsInfinity(heading))
                throw new ArgumentOutOfRangeException(nameof(heading), "朝向必须为有限数。");
            var normalized = (heading + Math.PI) % TwoPi;
            if (normalized < 0)
                normalized += TwoPi;

            var raw = (int)Math.Round(normalized / HeadingScale);
            return (ushort)(raw & 0xFFFF);
        }
    }
}
