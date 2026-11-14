using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Object;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.Game.Network
{
    /// <summary>移动包的状态标志；击退期间保留面向，不使用普通跑步动画。</summary>
    [Flags]
    public enum ActorMoveFlags : ushort
    {
        None = 0,
        Knockback = 0x08
    }

    // ACT 日志：270 10E:40004574:-1.2434:0000:003C:100.3891:99.2904:0.0457
    [StructLayout(LayoutKind.Explicit, Size = 0x10, Pack = 1)]
    public struct ActorMovePacket
    {
        [FieldOffset(0x00)]
        public ushort RawHeading;

        [FieldOffset(0x02)]
        public ushort Flags;

        [FieldOffset(0x04)]
        public byte SpeedParam; // 仅用到了低 8 位，如 0x14 = 20, 0x3C = 60

        [FieldOffset(0x05)]
        public byte Unk05;

        [FieldOffset(0x06)]
        public ushort RawX;

        [FieldOffset(0x08)]
        public ushort RawY;

        [FieldOffset(0x0A)]
        public ushort RawZ;

        [FieldOffset(0x0C)]
        public uint Unk0C;
    }

}
