using Simulant.ACT;
using Simulant.Game.FFCS.Client.Game.Network;
using System;

namespace Simulant.Game.FFCS.Client.Network
{
    public partial struct PacketDispatcher : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        static GreyMagicExternalProcessMemory Memory => NamazuInterop.Plugin.Memory;

        // 收包函数 SpawnPlayer 分支调用的函数
        [SigPattern(
            "48 89 5C 24 ? 57 48 81 EC ? ? ? ? 48 8B DA 8B F9 0F B6 92",
            "2D ? ? ? ? 3D ? ? ? ? 0F 87 ? ? ? ? 48 8D 15 ? ? ? ? 48 98 0F B6 84 02 ? ? ? ? 8B 8C 82 ? ? ? ? 48 03 CA FF E1 8B 4F ? 48 8B D3 E8 * * * *")]
        public static IntPtr HandleSpawnPlayerPacketFuncPtr { get; set; }
        public static void HandleSpawnPlayerPacket(uint targetId, IntPtr packetPtr) // SpawnPlayerPacket*
        {
            if (HandleSpawnPlayerPacketFuncPtr == IntPtr.Zero)
                throw new InvalidOperationException("网络玩家生成函数尚未定位。");
            HandleSpawnPlayerPacketFuncPtr.Call(targetId, packetPtr);
        }

        public static unsafe void HandleSpawnPlayerPacket(uint targetId, SpawnPlayerPacket packet)
        {
            if (HandleSpawnPlayerPacketFuncPtr == IntPtr.Zero)
                throw new InvalidOperationException("网络玩家生成函数尚未定位。");
            var packetPtr = IntPtr.Zero;
            try
            {
                packetPtr = Memory.AllocateMemory(sizeof(SpawnPlayerPacket));
                Memory.Write(packetPtr, packet);
                HandleSpawnPlayerPacketFuncPtr.Call(targetId, packetPtr);
            }
            finally
            {
                if (packetPtr != IntPtr.Zero) Memory.FreeMemory(packetPtr);
            }
        }

        // 收包函数 SpawnNpc 分支调用的函数
        [SigPattern(
            "48 89 5C 24 ? 57 48 81 EC ? ? ? ? 48 8B DA 8B F9 E8 ? ? ? ? 3C ? 75 ? E8 ? ? ? ? 3C ? 75 ? 80 BB ? ? ? ? ? 75 ? 8B 05 ? ? ? ? 39 43 ? 0F 85 ? ? ? ? 0F B6 53 ? 48 8D 0D ? ? ? ? E8 ? ? ? ? 0F B6 53 ? 48 8D 0D ? ? ? ? E8 ? ? ? ? 48 8D 44 24 ? C7 44 24 ? ? ? ? ? BA ? ? ? ? 66 90 48 8D 80 ? ? ? ? ? ? ? 0F 10 4B ? 48 8D 9B ? ? ? ? 0F 11 40 ? 0F 10 43 ? 0F 11 48 ? 0F 10 4B ? 0F 11 40 ? 0F 10 43 ? 0F 11 48 ? 0F 10 4B ? 0F 11 40 ? 0F 10 43 ? 0F 11 48 ? 0F 10 4B ? 0F 11 40 ? 0F 11 48 ? 48 83 EA ? 75 ? ? ? ? 4C 8D 44 24",
            "83 E8 ? 3D ? ? ? ? 0F 87 ? ? ? ? 48 8D 15 ? ? ? ? 48 98 0F B6 84 02 ? ? ? ? 8B 8C 82 ? ? ? ? 48 03 CA FF E1 8B 4F ? 48 8B D3 E8 * * * *")]
        public static IntPtr HandleSpawnNpcPacketFuncPtr { get; set; }
        public static unsafe void HandleSpawnNpcPacket(uint targetId, SpawnNpcPacket packet)
        {
            if (HandleSpawnNpcPacketFuncPtr == IntPtr.Zero)
                throw new InvalidOperationException("网络角色生成函数尚未定位。");
            IntPtr packetPtr = default;
            try
            {
                packetPtr = Memory.AllocateMemory(sizeof(SpawnNpcPacket));
                Memory.Write(packetPtr, packet);
                HandleSpawnNpcPacketFuncPtr.Call(targetId, packetPtr);
            }
            finally
            {
                if (packetPtr != default)
                    Memory.FreeMemory(packetPtr);
            }
        }

        // AnoMech 1646a706：角色删除包包含 CharacterManager 的索引，也能取消尚未消费的生成包。
        [SigPattern("48 89 5C 24 ?? 57 48 83 EC 40 0F B6 1A")]
        public static IntPtr HandleDespawnCharacterPacketFuncPtr { get; set; }

        public static void HandleDespawnCharacterPacket(byte index)
        {
            if (index == 0 || index >= 100)
                throw new ArgumentOutOfRangeException(nameof(index), "模拟角色的网络槽位必须为 1-99。");
            if (HandleDespawnCharacterPacketFuncPtr == IntPtr.Zero)
                throw new InvalidOperationException("网络角色回收函数尚未定位。");
            var packetPtr = IntPtr.Zero;
            try
            {
                packetPtr = Memory.AllocateMemory(1);
                Memory.Write(packetPtr, new DespawnCharacterPacket { Index = index });
                HandleDespawnCharacterPacketFuncPtr.Call(0ul, packetPtr);
            }
            finally
            {
                if (packetPtr != IntPtr.Zero) Memory.FreeMemory(packetPtr);
            }
        }

        // 收包函数 SpawnObject 分支调用的函数
        [SigPattern(
            "40 53 57 48 83 EC ? F6 42",
            "E8 * * * * E9 ? ? ? ? 4C 8D 47 10 41 8B D6 48 8D 0D ? ? ? ? E8 ? ? ? ? F6 05 ? ? ? ? 04 0F 85 ? ? ? ? 48 8D 57 10 41 8B CE E8 ? ? ? ? E9 ? ? ? ? 4C 8D 47 10 41 8B D6 48 8D 0D ? ? ? ? E8 ? ? ? ? F6 05 ? ? ? ? 04 0F 85 ? ? ? ? 48 8D 57 10 41 8B CE E8 ? ? ? ? E9 ? ? ? ? 48 8D 57 10 41 8B CE E8 ? ? ? ? E9 ? ? ? ? 4C 8D 47 10")]
        public static IntPtr HandleSpawnObjectPacketFuncPtr { get; set; }
        public static unsafe IntPtr HandleSpawnObjectPacket(uint targetId, SpawnObjectPacket packet)
        {
            IntPtr packetPtr = default;
            try
            {
                packetPtr = Memory.AllocateMemory(sizeof(SpawnObjectPacket));
                Memory.Write(packetPtr, packet);
                return HandleSpawnObjectPacketFuncPtr.Call<IntPtr>(targetId, packetPtr);
            }
            finally
            {
                if (packetPtr != default)
                    Memory.FreeMemory(packetPtr);
            }
        }

        // 收包函数 ActorMove 分支调用的函数，
        // 将 ActorMove 包的 payload 整体注册到全局实体移动管理器中，
        // 由其他消费函数统一插值处理移动
        // 仅对网络实体有效，也许以后找到方法将实体注册进网络实体列表后，可以换用这个控制假玩家的移动
        [SigPattern("48 89 5C 24 ? 57 48 83 EC ? 4C 63 05 ? ? ? ?")]
        public static IntPtr HandleActorMovePacketFuncPtr { get; set; }
        public static unsafe IntPtr HandleActorMovePacket(uint objectId, ActorMovePacket actorMovePayload)
        {
            if (HandleActorMovePacketFuncPtr == IntPtr.Zero)
                throw new InvalidOperationException("网络角色移动函数尚未定位。");
            var memory = NamazuInterop.Plugin.Memory;
            IntPtr actorMovePayloadPtr = default;

            try
            {
                actorMovePayloadPtr = memory.AllocateMemory(sizeof(ActorMovePacket));
                memory.Write(actorMovePayloadPtr, actorMovePayload);
                return HandleActorMovePacketFuncPtr.Call<IntPtr>(objectId, actorMovePayloadPtr);
            }
            finally
            {
                if (actorMovePayloadPtr != default)
                    memory.FreeMemory(actorMovePayloadPtr);
            }
        }

        // 收包函数 ActorControl 等分支调用的函数
        // ActorControl 中 a5 后传参均固定为 0 (targetId 为 0xE0000000)
        // original "40 55 53 57 41 54 41 56 48 8D AC 24 ? ? ? ? B8 ? ? ? ?"
        [SigPattern("44 8B 4F ? 44 8B 47 ? 89 44 24 ? 8B 47 ? 89 44 24 ? E8 * * * *")]
        public static IntPtr HandleActorControlFuncPtr { get; set; }
        public static IntPtr HandleActorControl(uint objectId, ushort category, uint a1, uint a2, uint a3, uint a4, uint a5, uint a6, uint a7, uint a8, uint targetId, uint unk)
        {
            if (HandleActorControlFuncPtr == IntPtr.Zero)
                throw new InvalidOperationException("网络角色控制函数尚未定位。");
            return HandleActorControlFuncPtr.Call<IntPtr>(objectId, category, a1, a2, a3, a4, a5, a6, a7, a8, targetId, unk);
        }

    }
}
