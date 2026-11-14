using System.Buffers.Binary;
using System.Numerics;

namespace Lumina.Data.Files
{
    struct PlatePos
    {
        public short X;
        public short Y;
    }

    public class TeraFile : FileResource
    {
        public uint Version;
        public uint PlateCount;
        public uint PlateSize;
        public float ClipDistance;
        public float Unknown;
        private byte[] Padding;
        private PlatePos[] _positions;
        public override void LoadFile()
        {
            Version = Reader.ReadUInt32();
            PlateCount = Reader.ReadUInt32();
            PlateSize = Reader.ReadUInt32();
            ClipDistance = Reader.ReadSingle();
            Unknown = Reader.ReadSingle();
            Padding = Reader.ReadBytes(32);
            _positions = Reader.ReadStructuresAsArray<PlatePos>((int)PlateCount);
        }

        public Vector2 GetPlatePosition(int plateIndex)
        {
            var pos = _positions[plateIndex];
            return new Vector2(PlateSize * (pos.X + 0.5f), PlateSize * (pos.Y + 0.5f));
        }
    }
}
