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

        // FFCS 未声明。国服 2026.11.03.0000.0000 从副本回放收包 0x03D2 分支跟入，与实时收包共用。
        // rcx 为路由实体编号，rdx 为包地址。入口按条目写入 UIState.Hater，并用实体编号查找名字。
        [SigPattern("48 89 74 24 20 57 48 83 EC 20 0F B6 02 33 F6 89 05 ?? ?? ?? ?? 48 8B FA 40 38 32")]
        public static IntPtr HandleUpdateHaterPacketFuncPtr { get; set; }

        /// <summary>通知客户端更新指定目标的小队仇恨；只传目标编号和远程包地址。</summary>
        public static void HandleUpdateHatePacket(uint targetId, UpdateHatePacket packet)
        {
            if (packet.MemberCount > UpdateHatePacket.Capacity)
                throw new ArgumentOutOfRangeException(nameof(packet), "目标仇恨包最多包含 8 名成员。");

            var function = HandleUpdateHatePacketFuncPtr;

            if (function == IntPtr.Zero)
                throw new InvalidOperationException("原生目标仇恨更新函数尚未定位。");

            function.Call<long>(targetId, RemoteInput.Struct(packet));
        }

        internal static void HandleUpdateHaterPacket(uint playerId, UpdateHaterPacket packet)
        {
            if (packet.EnemyCount > UpdateHaterPacket.Capacity)
                throw new ArgumentOutOfRangeException(nameof(packet), "敌对列表更新包最多包含 32 个目标。");

            var function = HandleUpdateHaterPacketFuncPtr;
            if (function == IntPtr.Zero)
                throw new InvalidOperationException("原生敌对列表更新函数尚未定位。");

            function.Call(playerId, RemoteInput.Struct(packet));
        }
    }
}
