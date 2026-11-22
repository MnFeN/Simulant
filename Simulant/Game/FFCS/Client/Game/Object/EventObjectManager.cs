using System;

namespace Simulant.Game.FFCS.Client.Game.Object
{
    // Client::Game::Object::EventObjectManager
    // For example, used for HousingEventObject, HousingCombinedObject, ...
    // https://github.com/aers/FFXIVClientStructs/blob/7f509ce13d0e29a64cd24ec35ccc1452c1b1b7d2/FFXIVClientStructs/FFXIV/Client/Game/Object/EventObjectManager.cs
    public struct EventObjectManager : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        [SigPattern("48 8D 0D * * * * E8 ? ? ? ? 48 85 C0 74 ? 48 39 B0 ? ? ? ? 74 ? FF C3")]
        public static IntPtr InstancePtr { get; set; }
        public static EventObjectManager Instance
            => InstancePtr.As<EventObjectManager>();

        // FFCS 未声明，已在国服 2026.11.03.0000.0000 核对。
        // 从 EventFramework.SetTerritoryTypeId 的区域初始化调用方，向前找集中清理各对象管理器的调用。
        // 在该函数内，找到以 EventObjectManager.Instance 为 this 的调用。
        // 该入口逐个回收 EventObjects 槽位中的对象，解除场景组绑定并刷新 GameObjectManager 对象表。
        [SigPattern("E8 * * * * 48 8D 0D ? ? ? ? E8 ? ? ? ? E8 ? ? ? ? 48 85 C0 74 ? E8 ? ? ? ? 48 83 B8")]
        public static IntPtr ClearFuncPtr { get; set; }

        public void Clear()
            => ClearFuncPtr.Call(Ptr);
    }
}
