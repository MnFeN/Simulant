using System;

namespace Simulant.Game.FFCS.Client.Game.Object
{
    // Client::Game::Object::StandObjectManager
    // Lively Actors (Named/Unnamed ENPCs)
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Game/Object/StandObjectManager.cs
    internal struct StandObjectManager : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        [SigPattern("48 8D 0D * * * * E8 ? ? ? ? 48 8B F8 48 85 C0 74 11")]
        internal static IntPtr InstancePtr { get; set; }

        internal static StandObjectManager Instance() => InstancePtr.As<StandObjectManager>();

        // FFCS 未声明。国服 2026.11.03.0000.0000、国际服 2026.03.17.0000.0000：
        // 从 EventFramework.SetTerritoryTypeId 的区域初始化调用方回溯。集中清理函数在 EventObjectManager 之后
        // 传入 StandObjectManager，逐个回收 120 个角色槽位和 20 个事件对象槽位。
        [SigPattern("E8 ? ? ? ? 48 8D 0D ? ? ? ? E8 * * * * E8 ? ? ? ? 48 85 C0 74 ? E8 ? ? ? ? 48 83 B8")]
        internal static IntPtr ClearFuncPtr { get; set; }

        internal void Clear() => ClearFuncPtr.Call(Ptr);
    }
}
