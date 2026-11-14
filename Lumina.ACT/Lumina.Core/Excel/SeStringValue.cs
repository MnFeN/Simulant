using System;
using System.Collections.Generic;
using System.Text;

namespace Lumina.Excel
{
    /// <summary>Preserves SeString bytes while presenting literal text without evaluating game expressions.</summary>
    public sealed class SeStringValue
    {
        public byte[] RawData { get; private set; }
        public string Text { get; private set; }

        internal SeStringValue(byte[] bytes, Func<string, string> resolver)
        {
            RawData = bytes;
            var text = new List<byte>();
            for (int i = 0; i < bytes.Length;)
            {
                if (bytes[i] != 2)
                {
                    text.Add(bytes[i++]);
                    continue;
                }

                int payload = i;
                if (i + 2 >= bytes.Length)
                {
                    text.AddRange(Encoding.UTF8.GetBytes("[invalid payload]"));
                    break;
                }

                byte kind = bytes[i + 1];
                i += 2;
                uint length;
                if (!TryReadLength(bytes, ref i, out length) || length > int.MaxValue || i > bytes.Length - (int)length - 1 || bytes[i + (int)length] != 3)
                {
                    text.AddRange(Encoding.UTF8.GetBytes("[invalid payload]"));
                    break;
                }

                if (kind == 0x10)
                    text.Add((byte)'\n');
                i += (int)length + 1;
                if (i <= payload)
                    throw new InvalidOperationException();
            }

            Text = Encoding.UTF8.GetString(text.ToArray());
            if (resolver != null && Text.StartsWith("_rsv_", StringComparison.Ordinal))
                Text = resolver(Text) ?? Text;
        }

        private static bool TryReadLength(byte[] bytes, ref int position, out uint value)
        {
            value = 0;
            if (position >= bytes.Length)
                return false;
            byte first = bytes[position++];
            if (first > 0 && first < 0xD0)
            {
                value = (uint)first - 1;
                return true;
            }

            if (first < 0xF0 || first > 0xFE)
                return false;
            int flags = (first + 1) & 15;
            for (int bit = 3; bit >= 0; bit--)
            {
                if ((flags & (1 << bit)) == 0)
                    continue;
                if (position >= bytes.Length || bytes[position] == 0)
                    return false;
                value |= (uint)bytes[position++] << (bit * 8);
            }

            return true;
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
