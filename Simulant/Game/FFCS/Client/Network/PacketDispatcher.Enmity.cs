using Simulant.Game.FFCS.Client.Game.Network;
using System;

namespace Simulant.Game.FFCS.Client.Network
{
    public partial struct PacketDispatcher
    {
        // KodakkuAssist e5bd79f8：国服 2026.10.06 的当前目标仇恨入口。
        // https://github.com/moewcorp/KodakkuAssist/blob/e5bd79f841fe483e14a3dd22a04f845eab239026/KodakkuAssist/Module/GameEvent/Types/EnmityUpdate.cs
        [SigPattern("0F B6 02 45 33 C0")]
        public static IntPtr HandleUpdateHatePacketFuncPtr { get; set; }

        /// <summary>通知客户端更新指定目标的小队仇恨；只传目标编号和远程包地址。</summary>
        public static void HandleUpdateHatePacket(uint targetId, UpdateHatePacket packet)
        {
            if (targetId == 0 || targetId == 0xE0000000)
                throw new ArgumentOutOfRangeException(nameof(targetId), "目标仇恨更新必须指定有效实体编号。");
            if (packet.MemberCount > UpdateHatePacket.Capacity)
                throw new ArgumentOutOfRangeException(nameof(packet), "目标仇恨包最多包含 8 名成员。");
            for (var i = 0; i < packet.MemberCount; i++)
            {
                var member = packet.GetMember(i);
                if (member.EntityId == 0 || member.EntityId == 0xE0000000 || member.Enmity > 100)
                    throw new ArgumentException("仇恨成员必须具有有效实体编号，百分比范围为 0-100。", nameof(packet));
            }
            var function = HandleUpdateHatePacketFuncPtr;
            if (function == IntPtr.Zero)
                throw new InvalidOperationException("原生目标仇恨更新函数尚未定位。");

            var memory = Memory;
            var packetPtr = IntPtr.Zero;
            try
            {
                packetPtr = memory.AllocateMemory(UpdateHatePacket.Size);
                if (packetPtr == IntPtr.Zero)
                    throw new InvalidOperationException("无法分配目标仇恨更新包的内存。");
                memory.Write(packetPtr, packet);
                function.Call<long>(targetId, packetPtr);
            }
            finally
            {
                if (packetPtr != IntPtr.Zero)
                    memory.FreeMemory(packetPtr);
            }
        }
    }
}
