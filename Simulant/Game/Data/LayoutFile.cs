using System;
using System.Text;

namespace Simulant.Game.Data
{
    internal sealed class LayoutFile
    {
        private readonly byte[] _data;

        internal LayoutFile(byte[] data) => _data = data ?? Array.Empty<byte>();

        internal int SceneBody => HasTag(12, "SCN1") ? 20 : HasTag(32, "SCN1") ? 40 : -1;

        internal bool ContainsArray(int offset, int count, int size)
            => offset >= 0 && offset <= _data.Length && count >= 0 && count <= (_data.Length - offset) / size;

        internal int Int32(int offset) => ContainsArray(offset, 1, 4) ? BitConverter.ToInt32(_data, offset) : 0;
        internal ushort UInt16(int offset) => BitConverter.ToUInt16(_data, offset);
        internal byte ByteAt(int offset) => _data[offset];

        internal int Follow(int basis, int field)
        {
            var delta = Int32(field);
            var target = (long)basis + delta;
            return basis >= 0 && delta > 0 && target < _data.Length ? (int)target : -1;
        }

        internal bool HasTag(int offset, string tag)
            => ContainsArray(offset, tag.Length, 1) && Encoding.ASCII.GetString(_data, offset, tag.Length) == tag;

        internal string StringAt(int offset)
        {
            if (!ContainsArray(offset, 1, 1))
                return string.Empty;

            var length = Array.IndexOf(_data, (byte)0, offset) - offset;
            return length > 0 ? Encoding.UTF8.GetString(_data, offset, length) : string.Empty;
        }
    }
}
