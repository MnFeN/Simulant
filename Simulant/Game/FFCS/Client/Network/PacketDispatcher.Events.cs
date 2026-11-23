using System;

namespace Simulant.Game.FFCS.Client.Network
{
    public partial struct PacketDispatcher
    {
        // 国服 7.58。EventStart/EventFinish 的参数来自回放分发到两个独立原生函数的调用点。
        [SigPattern("E8 * * * * 0F B6 8E ? ? ? ? 48 8D 41 01 48 83 F8 08")]
        public static IntPtr HandleEventStartPacketFuncPtr { get; set; }

        [SigPattern("48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 57 48 83 EC 40 B8 00 00 00 E0")]
        public static IntPtr HandleEventPlayPacketFuncPtr { get; set; }

        [SigPattern("E8 * * * * 0F B6 8E ? ? ? ? 8D 41 FF 3C 06 0F 87")]
        public static IntPtr HandleEventFinishPacketFuncPtr { get; set; }

        internal static bool EventHandlersReady => HandleEventStartPacketFuncPtr != IntPtr.Zero &&
            HandleEventPlayPacketFuncPtr != IntPtr.Zero && HandleEventFinishPacketFuncPtr != IntPtr.Zero;

        internal static void HandleEventStartPacket(uint eventId, ulong targetId, byte eventType, uint eventArg, byte flags)
            => HandleEventStartPacketFuncPtr.Call(eventId, targetId, eventType, eventArg, flags);

        internal static void HandleEventPlayPacket(ulong actorId, uint eventId, short scene, ulong flags, uint[] sceneData)
        {
            var data = new byte[sceneData.Length * sizeof(uint)];
            Buffer.BlockCopy(sceneData, 0, data, 0, data.Length);
            HandleEventPlayPacketFuncPtr.Call(actorId, eventId, scene, flags, RemoteInput.FromBytes(data), checked((byte)sceneData.Length));
        }

        internal static void HandleEventFinishPacket(uint eventId, byte eventType, uint eventArg, uint result)
            => HandleEventFinishPacketFuncPtr.Call(eventId, eventType, eventArg, result);
    }
}
