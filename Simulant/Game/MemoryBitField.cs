using System;

namespace Simulant.Game
{
    public readonly struct MemoryBitField
    {
        private readonly IntPtr _basePtr;
        private readonly int _offset;
        private readonly int _bit;

        internal MemoryBitField(IntPtr basePtr, int offset, int bit)
        {
            if (bit < 0 || bit > 7)
                throw new ArgumentOutOfRangeException(nameof(bit), "byte bit index must be 0-7.");

            _basePtr = basePtr;
            _offset = offset;
            _bit = bit;
        }

        private byte Mask => (byte)(1 << _bit);

        private bool Value
        {
            get => (_basePtr.Read<byte>(_offset) & Mask) != 0;
            set
            {
                var flags = _basePtr.Read<byte>(_offset);
                var newFlags = value
                    ? (byte)(flags | Mask)
                    : (byte)(flags & ~Mask);

                if (newFlags == flags)
                    return;

                _basePtr.Write(newFlags, _offset);
            }
        }

        public IntPtr Ptr => _basePtr + _offset;

        public int Bit => _bit;

        public bool Get() => Value;

        public void Set(bool value) => Value = value;

        public void Enable() => Value = true;

        public void Disable() => Value = false;

        public void Toggle() => Value = !Value;

        public override string ToString()
            => Value.ToString();

        public static implicit operator bool(MemoryBitField field)
            => field.Value;
    }
}
