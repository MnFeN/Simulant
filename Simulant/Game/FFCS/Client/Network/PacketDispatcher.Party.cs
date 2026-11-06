using Simulant.Game.FFCS.Client.Game.Network;
using System;

namespace Simulant.Game.FFCS.Client.Network
{
    public partial struct PacketDispatcher
    {
        // AnoMech 1646a706：原生完整小队更新入口，第三个参数 0 表示主小队。
        [SigPattern("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 41 0F B6 D8")]
        public static IntPtr HandleUpdatePartyPacketFuncPtr { get; set; }

        /// <summary>通过客户端原生收包函数替换主小队名单。</summary>
        public static void HandleUpdatePartyPacket(UpdatePartyPacket packet)
        {
            if (packet.MemberCount > UpdatePartyPacket.Capacity)
                throw new ArgumentOutOfRangeException(nameof(packet), "小队更新包最多包含 8 名成员。");
            if (packet.MemberCount != 0 && packet.LeaderIndex >= packet.MemberCount)
                throw new ArgumentOutOfRangeException(nameof(packet), "队长索引必须指向小队中的成员。");
            var function = HandleUpdatePartyPacketFuncPtr;
            if (function == IntPtr.Zero)
                throw new InvalidOperationException("原生小队更新函数尚未定位。");

            var memory = Memory;
            var packetPtr = IntPtr.Zero;
            try
            {
                packetPtr = memory.AllocateMemory(UpdatePartyPacket.Size);
                if (packetPtr == IntPtr.Zero)
                    throw new InvalidOperationException("无法分配小队更新包的内存。");
                memory.Write(packetPtr, packet);
                function.Call(0u, packetPtr, (byte)0);
            }
            finally
            {
                if (packetPtr != IntPtr.Zero)
                    memory.FreeMemory(packetPtr);
            }
        }
    }
}
