using System;
using System.Collections;
using System.Collections.Generic;

namespace Lumina.Excel
{
    /// <summary>Lazy generated-schema array. Managed delegates replace C# function pointers.</summary>
    public readonly struct Collection<T> : IList<T>, IReadOnlyList<T> where T : struct
    {
        private readonly ExcelPage page;
        private readonly uint parentOffset, offset;
        private readonly Func<ExcelPage, uint, uint, uint, T> create;
        public int Count { get; }
        public Collection(ExcelPage page, uint parentOffset, uint offset, Func<ExcelPage, uint, uint, uint, T> ctor, int size)
        {
            if (ctor == null) throw new ArgumentNullException(nameof(ctor));
            if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
            this.page = page; this.parentOffset = parentOffset; this.offset = offset; create = ctor; Count = size;
        }
        public T this[int index]
        { get { if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index)); return create(page, parentOffset, offset, (uint)index); } }
        T IList<T>.this[int index] { get { return this[index]; } set { throw new NotSupportedException(); } }
        bool ICollection<T>.IsReadOnly { get { return true; } }
        public int IndexOf(T item) { for (int i = 0; i < Count; i++) if (EqualityComparer<T>.Default.Equals(this[i], item)) return i; return -1; }
        public bool Contains(T item) { return IndexOf(item) >= 0; }
        public void CopyTo(T[] array, int index) { ReadOnlyCollectionHelpers.CheckCopy(array, index, Count); for (int i = 0; i < Count; i++) array[index++] = this[i]; }
        void IList<T>.Insert(int index, T item) { throw new NotSupportedException(); }
        void IList<T>.RemoveAt(int index) { throw new NotSupportedException(); }
        void ICollection<T>.Add(T item) { throw new NotSupportedException(); }
        void ICollection<T>.Clear() { throw new NotSupportedException(); }
        bool ICollection<T>.Remove(T item) { throw new NotSupportedException(); }
        public IEnumerator<T> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }
}
