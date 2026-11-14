using System.IO;
using Lumina.Extensions;

namespace Lumina.Data.Parsing
{
    public class StringOffset
    {
        public StringOffset()
        {
        }

        public StringOffset(BinaryReader br, long start)
        {
            Offset = br.ReadUInt32();
            Read(br, start, Offset);
        }

        public uint Offset { get; set; }
        public string Data { get; set; }

        public void Read(BinaryReader br, long start, uint offset)
        {
            Data = br.ReadStringOffsetData(start + offset);
        }

        public static implicit operator string (StringOffset obj) => obj.Data;
    }
}
