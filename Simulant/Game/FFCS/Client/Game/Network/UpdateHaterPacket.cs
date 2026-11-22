using System;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.Game.Network
{
    // FFCS 未声明。国服 2026.11.03.0000.0000 从副本回放收包 0x03D2 分支核对，读取 1 字节计数及 +4 起每 8 字节一个条目。
    // 条目写入 HaterInfo.EntityId 和 Enmity；包长 0x108 及 32 项容量与国服回放、BossMod ServerIPC.UpdateHater 一致。
    /// <summary>发给本机玩家的敌对列表快照。</summary>
    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public unsafe struct UpdateHaterPacket
    {
        public const int Size = 0x108;
        public const int Capacity = 32;

        [FieldOffset(0)]
        public byte EnemyCount;

        [FieldOffset(4)]
        private fixed byte _entries[Capacity * UpdateHatePacketMember.Size];

        public UpdateHatePacketMember GetEntry(int index)
        {
            ValidateIndex(index);
            fixed (byte* entries = _entries)
                return ((UpdateHatePacketMember*)entries)[index];
        }

        public void SetEntry(int index, UpdateHatePacketMember entry)
        {
            ValidateIndex(index);
            if (entry.Enmity > 100)
                throw new ArgumentOutOfRangeException(nameof(entry), "敌对仇恨百分比必须为 0-100。");

            fixed (byte* entries = _entries)
                ((UpdateHatePacketMember*)entries)[index] = entry;
        }

        private static void ValidateIndex(int index)
        {
            if ((uint)index >= Capacity)
                throw new ArgumentOutOfRangeException(nameof(index), "敌对条目索引必须为 0-31。");
        }
    }
}
