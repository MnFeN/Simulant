using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lumina.Extensions
{
    public static class SpanExtensions
    {
        public static unsafe T ReadStructure<T>(this Span<byte> span)
            where T : struct
        {
            return MemoryMarshal.Read<T>(span);
        }

        public static unsafe T ReadStructure<T>(this Span<byte> span, int offset)
            where T : struct
        {
            return MemoryMarshal.Read<T>(span.Slice(offset));
        }

        public static ref T UnsafeAt<T>(this Span<T> span, int index) => ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index);
        public static ref readonly T UnsafeAt<T>(this ReadOnlySpan<T> span, int index) => ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index);
        public static ref T UnsafeAt<T>(this T[] array, int index) => ref array[index];
    }
}
