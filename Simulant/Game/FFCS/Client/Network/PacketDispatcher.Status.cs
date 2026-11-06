using Simulant.Game.FFCS.Client.Game.Network;
using System;

namespace Simulant.Game.FFCS.Client.Network
{
    public partial struct PacketDispatcher
    {
        // AnoMech 1646a706：完整 60 槽状态收包入口，第三个参数 0 表示实时角色。
        [SigPattern("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 83 3D ?? ?? ?? ?? 03 41 0F B6 F0 48 8B DA 8B F9 74 ?? 48 81 C2 68 01 00 00")]
        public static IntPtr HandleStatusEffectListDoublePacketFuncPtr { get; set; }

        /// <summary>由客户端收包管线处理角色血蓝量与全部状态。</summary>
        public static void HandleStatusEffectListDoublePacket(uint entityId, StatusEffectListDoublePacket packet)
        {
            var function = HandleStatusEffectListDoublePacketFuncPtr;
            if (function == IntPtr.Zero)
                throw new InvalidOperationException("原生角色状态更新函数尚未定位。");
            if (packet.CurrentHP > packet.MaxHP || packet.CurrentMP > packet.MaxMP)
                throw new ArgumentOutOfRangeException(nameof(packet), "当前血量和魔力不能超过上限。");

            var memory = Memory;
            var packetPtr = IntPtr.Zero;
            try
            {
                packetPtr = memory.AllocateMemory(StatusEffectListDoublePacket.Size);
                if (packetPtr == IntPtr.Zero)
                    throw new InvalidOperationException("无法分配角色状态更新包的内存。");
                memory.Write(packetPtr, packet);
                function.Call(entityId, packetPtr, (byte)0);
            }
            finally
            {
                if (packetPtr != IntPtr.Zero)
                    memory.FreeMemory(packetPtr);
            }
        }
    }
}
