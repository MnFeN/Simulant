using System;
using System.Text;
using Lumina.Excel;

namespace Lumina.Text.ReadOnly
{
    /// <summary>Read-only SeString bytes and literal-text extraction. Does not evaluate game macros.</summary>
    public readonly struct ReadOnlySeString : IEquatable<ReadOnlySeString>
    {
        private readonly ReadOnlyMemory<byte> bytes;
        private readonly Func<string, string> resolver;
        public ReadOnlySeString(ReadOnlyMemory<byte> data) : this(data, null) { }
        public ReadOnlySeString(string text) : this(Encoding.UTF8.GetBytes(text ?? string.Empty), null) { }
        internal ReadOnlySeString(ReadOnlyMemory<byte> data, Func<string, string> rsvResolver) { bytes = data; resolver = rsvResolver; }
        public ReadOnlyMemory<byte> Data { get { return bytes; } }
        public ReadOnlySpan<byte> Span { get { return bytes.Span; } }
        public bool IsEmpty { get { return bytes.IsEmpty; } }
        public string ExtractText() { return new SeStringValue(bytes.ToArray(), resolver).Text; }
        public override string ToString() { return ExtractText(); }
        public bool Equals(ReadOnlySeString other) { return bytes.Span.SequenceEqual(other.bytes.Span); }
        public override bool Equals(object obj) { return obj is ReadOnlySeString && Equals((ReadOnlySeString)obj); }
        public override int GetHashCode() { unchecked { int hash = 17; foreach (byte b in bytes.Span) hash = hash * 31 + b; return hash; } }
        public static bool operator ==(ReadOnlySeString a, ReadOnlySeString b) { return a.Equals(b); }
        public static bool operator !=(ReadOnlySeString a, ReadOnlySeString b) { return !a.Equals(b); }
    }
}
