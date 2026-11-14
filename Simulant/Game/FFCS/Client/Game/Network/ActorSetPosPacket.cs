using System.Numerics;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.Game.Network
{
    // FFXIVClientStructs：ActorSetPosPacket。位置使用游戏的 X/Z 水平面与 Y 高度。
    [StructLayout(LayoutKind.Explicit, Size = 0x18)]
    public struct ActorSetPosPacket
    {
        [FieldOffset(0x00)]
        public ushort Rotation;
        [FieldOffset(0x02)]
        public byte WarpType;
        [FieldOffset(0x03)]
        public byte Arg;
        [FieldOffset(0x04)]
        public uint TransitionTerritoryFilterKey;
        [FieldOffset(0x08)]
        public Vector3 Position;
    }
}
