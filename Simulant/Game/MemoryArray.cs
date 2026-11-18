using SimuMagic;
using System;
using System.Runtime.InteropServices;

namespace Simulant.Game
{
    /// <summary>Reads a fixed-length inline array from game memory.</summary>
    public readonly struct MemoryArray<T> where T : unmanaged
    {
        private readonly IntPtr _basePtr;
        private readonly int _offset;
        private readonly int _byteCount;

        internal unsafe MemoryArray(IntPtr basePtr, int offset, int length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            _basePtr = basePtr;
            _offset = offset;
            Length = length;
            _byteCount = checked(length * sizeof(T));
        }

        public IntPtr Ptr => _basePtr + _offset;

        public int Length { get; }

        public T[] Get() => Decode(_basePtr.ReadBytes(_byteCount, _offset))
            ?? throw new InvalidOperationException("读取定长数组不完整。");

        internal bool TryGet(ExternalProcessMemory memory, out T[] values)
        {
            _basePtr.ThrowIfZero(nameof(TryGet), _offset, "读取定长数组");
            values = Decode(memory.ReadBytes(Ptr, _byteCount));
            return values != null;
        }

        private T[] Decode(byte[] bytes)
        {
            if (bytes?.Length != _byteCount)
                return null;

            return typeof(T) == typeof(byte) ? (T[])(object)bytes : MemoryMarshal.Cast<byte, T>(bytes.AsSpan()).ToArray();
        }
    }
}
