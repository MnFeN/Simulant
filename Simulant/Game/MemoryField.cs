using System;
using System.Globalization;

namespace Simulant.Game
{
    public readonly struct MemoryField<T>
        where T : struct
    {
        private readonly IntPtr _basePtr;
        private readonly int _offset;

        internal MemoryField(IntPtr basePtr, int offset)
        {
            _basePtr = basePtr;
            _offset = offset;
        }

        private T Value
        {
            get => _basePtr.Read<T>(_offset);
            set => _basePtr.Write(value, _offset);
        }

        public IntPtr Ptr => _basePtr + _offset;

        public T Get() => Value;
        public void Set(T value) => Value = value;

        public override string ToString()
        {
            var value = Value;
            return value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value.ToString();
        }

        public static implicit operator T(MemoryField<T> field)
            => field.Value;
    }
}
