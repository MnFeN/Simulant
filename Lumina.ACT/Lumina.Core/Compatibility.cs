using System;
using System.Runtime.InteropServices;
using System.IO;
using Lumina.Extensions;

namespace Lumina
{
    internal static class Compatibility
    {
        public static void ReadExactly(Stream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer, total, buffer.Length - total);
                if (read == 0)
                    throw new EndOfStreamException();
                total += read;
            }
        }

        public static int ILogB(int value)
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            int result = 0;
            while ((value >>= 1) != 0)
                result++;
            return result;
        }

        public static unsafe int SingleToInt32Bits(float value)
        {
            return *(int*)&value;
        }

        public static unsafe float Int32BitsToSingle(int value)
        {
            return *(float*)&value;
        }

        public static T[] ArraySlice<T>(T[] array, int start, int length)
        {
            var result = new T[length];
            Array.Copy(array, start, result, 0, length);
            return result;
        }
    }
}

namespace System
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 2)]
    public struct Half
    {
        private ushort bits;
        public static explicit operator Half(float value)
        {
            return new Half
            {
                bits = value.Pack()
            };
        }

        public static implicit operator float (Half value)
        {
            return value.bits.Unpack();
        }
    }
}
