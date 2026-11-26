using System;

// BattleChara has extra definitions than Character (StatusManager, Cast Info, etc.),
// but we combined them together in Character for simplicity,
// since we do not need to differentiate them in raid simulations.
using BattleChara = Simulant.Game.FFCS.Client.Game.Character.Character;

namespace Simulant.Game.FFCS.Client.Game.Character
{
    // Client::Game::Character::CharacterManager
    //   Client::Game::Character::CharacterManagerInterface
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Game/Character/CharacterManager.cs
    public struct CharacterManager : IMemoryObject
    {
        private const int BattleCharaCount = 100;

        public IntPtr Ptr { get; set; }

        [SigPattern("8B D0 48 8D 0D * * * * E8 ?? ?? ?? ?? 48 85 C0 0F 84 ?? ?? ?? ?? 48 8B D0")]
        public static IntPtr InstancePtr { get; set; }

        public static CharacterManager Instance()
            => InstancePtr.As<CharacterManager>();

        // Inherits<CharacterManagerInterface>
        // private CharacterManagerInterface CharacterManagerInterface => Ptr.As<CharacterManagerInterface>();
        public IntPtr GetBattleCharaPtr(int index)
        {
            if ((uint)index >= BattleCharaCount)
                throw new ArgumentOutOfRangeException(nameof(index), "CharacterManager only contains 100 BattleChara pointers.");

            return Ptr.ReadPtr(0x50 + index * IntPtr.Size);
        }

        public BattleChara GetBattleChara(int index)
            => GetBattleCharaPtr(index).As<BattleChara>();

        internal MemoryArray<IntPtr> BattleCharas => Ptr.ArrayField<IntPtr>(0x50, BattleCharaCount);

        // FFCS 未声明 C# 绑定，ida/data.yml 命名为 DeleteCharacterAtIndex。
        // 国服 2026.11.03.0000.0000、国际服 2026.03.17.0000.0000：
        // 从 PacketDispatcher.HandleDespawnCharacterPacket 的末尾跳转定位，入口从 CharacterManager 的指定槽位回收角色。
        [SigPattern("0F B6 17 48 8D 0D ? ? ? ? 48 8B 5C 24 ? 48 83 C4 ? 5F E9 * * * *")]
        internal static IntPtr DeleteCharacterAtIndexFuncPtr { get; set; }

        internal void DeleteCharacterAtIndex(int index)
            => DeleteCharacterAtIndexFuncPtr.Call(Ptr, index);

        // 自己找的：在收包函数 SpawnNpc 分支中的下层函数
        [SigPattern("40 53 48 83 EC ?? 33 DB 48 8D 41 ?? 44 8B C3 90")]
        public static IntPtr AddBattleCharaFuncPtr { get; set; }

        /// <summary> 返回实体的槽位，0-99（不是实际表中的 0 2 4 ... 198）。</summary>
        public int AddBattleChara(uint entityId)
            => AddBattleCharaFuncPtr.Call<int>(Ptr, entityId);
        // [FieldOffset(0x370)] public BattleChara* BattleCharaMemory;
        // [FieldOffset(0x378)] public Companion* CompanionMemory;
        // used to calculate the minion address in CompanionMemory when adding a BattleChara
        // [FieldOffset(0x380)] public int CompanionClassSize;
        // [FieldOffset(0x384)] public int UpdateIndex;
        /*
        [SigPattern("E8 * * * * 48 89 84 3D ?? ?? ?? ??")]
        public static IntPtr LookupBattleCharaByEntityIdFuncPtr { get; set; }
        public BattleChara LookupBattleCharaByEntityId(uint entityId)
            => LookupBattleCharaByEntityIdFuncPtr.Call<IntPtr>(Ptr, entityId).As<BattleChara>();

        [SigPattern("E8 * * * * 49 8D 4F 20 48 89 44 24")]
        public static IntPtr LookupBuddyByOwnerObjectFuncPtr { get; set; }
        public BattleChara LookupBuddyByOwnerObject(BattleChara owner)
            => LookupBuddyByOwnerObjectFuncPtr.Call<IntPtr>(Ptr, owner.Ptr).As<BattleChara>();

        public BattleChara LookupBuddyByOwnerObject(IntPtr ownerPtr)
            => LookupBuddyByOwnerObjectFuncPtr.Call<IntPtr>(Ptr, ownerPtr).As<BattleChara>();

        [SigPattern("E8 * * * * EB 41 83 FF 1C")]
        public static IntPtr LookupPetByOwnerObjectFuncPtr { get; set; }
        public BattleChara LookupPetByOwnerObject(BattleChara owner)
            => LookupPetByOwnerObjectFuncPtr.Call<IntPtr>(Ptr, owner.Ptr).As<BattleChara>();

        public BattleChara LookupPetByOwnerObject(IntPtr ownerPtr)
            => LookupPetByOwnerObjectFuncPtr.Call<IntPtr>(Ptr, ownerPtr).As<BattleChara>();
         */
    }
}
