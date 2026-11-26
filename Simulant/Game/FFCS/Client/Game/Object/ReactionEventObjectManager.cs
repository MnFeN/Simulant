using System;

namespace Simulant.Game.FFCS.Client.Game.Object
{
    // Client::Game::Object::ReactionEventObjectManager
    // Gatherables/Farm in Island Sanctuary
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Game/Object/ReactionEventObjectManager.cs
    internal struct ReactionEventObjectManager : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        [SigPattern("48 8D 0D * * * * E8 ? ? ? ? 48 8D 0D ? ? ? ? E8 ? ? ? ? 48 8D 0D ? ? ? ? E8 ? ? ? ? 33 C9")]
        internal static IntPtr InstancePtr { get; set; }

        internal static ReactionEventObjectManager Instance() => InstancePtr.As<ReactionEventObjectManager>();

        // FFCS 未声明 C# 绑定，ida/data.yml 命名为 DeleteAllObjects。
        // 国服 2026.11.03.0000.0000、国际服 2026.03.17.0000.0000：
        // 从同表的 DeleteObjectByIndex 后找到相邻的批量入口。它遍历同一数组中的 100 个槽位，
        // 逐个解除场景绑定、销毁对象并更新 GameObjectManager 的对象登记。
        [SigPattern("48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 57 48 83 EC ? 33 ED 48 8D 59 08 8B F5 0F 1F 40 00 48 8B 3B 48 85 FF 74 ?")]
        internal static IntPtr DeleteAllObjectsFuncPtr { get; set; }

        internal void DeleteAllObjects() => DeleteAllObjectsFuncPtr.Call(Ptr);
    }
}
