using System;

// BattleChara has extra definitions than Character (StatusManager, Cast Info, etc.),
// but we combined them together in Character for simplicity,
// since we do not need to differentiate them in raid simulations.
using BattleChara = Simulant.Game.FFCS.Client.Game.Character.Character;

namespace Simulant.Game.FFCS.Client.Game.Object
{
    // Client::Game::Object::ClientObjectManager
    public struct ClientObjectManager : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        [SigPattern("48 8D 0D * * * * E8 ? ? ? ? C7 43 60 FF FF FF FF")]
        public static IntPtr InstancePtr { get; set; }
        public static ClientObjectManager Instance
            => InstancePtr.As<ClientObjectManager>();

        public BattleChara BattleCharaMemory => Ptr.ReadPtr(0x00).As<BattleChara>(); // ?
        public MemoryField<uint> BattleCharaSize => Ptr.Field<uint>(0x08);

        // 仅本地实体，遍历实体不要找这里
        // [FieldOffset(0x10), FixedSizeArray] internal FixedSizeArray249<BattleCharaEntry> _battleCharas;
        public BattleCharaList BattleCharas => Ptr.As<BattleCharaList>(0x10); // temp until we implement the array struct

        public struct BattleCharaEntry : IMemoryObject
        {
            public IntPtr Ptr { get; set; }

            public BattleChara BattleChara => Ptr.ReadPtr(0x00).As<BattleChara>();
            public MemoryField<ObjectKind> ObjectKind => Ptr.Field<ObjectKind>(0x08);

            /// <remarks> Index in <see cref="BattleCharas"/>. </remarks>
            public MemoryField<byte> Index => Ptr.Field<byte>(0x0C);

            /// <remarks> Index in <see cref="BattleCharaMemory"/>. </remarks>
            public MemoryField<ushort> MemoryIndex => Ptr.Field<ushort>(0x0E);
        }

        // temp until we implement the array struct
        public struct BattleCharaList : IMemoryObject
        {
            public IntPtr Ptr { get; set; }

            public const int Count = 249;

            public BattleCharaEntry this[int index]
            {
                get
                {
                    if ((uint)index >= Count)
                        throw new ArgumentOutOfRangeException(nameof(index));

                    return Ptr.As<BattleCharaEntry>(index * BattleCharaEntrySize);
                }
            }

            private const int BattleCharaEntrySize = 0x10;
        }

        // 下述传入和返回的 index 均为本地实体 ClientObject 的 index，
        // 与全部实体的 ObjectIndex 相差 BattleCharacter 容量 * 2 （目前为 200）。
        // 如 index = 2 对应的 ObjectIndex 为 202。
        // 详见 GameObjectManager。
        [SigPattern("E8 * * * * 41 89 44 FC ?")]
        public static IntPtr CreateBattleCharacterFuncPtr { get; set; }

        public uint CreateBattleCharacter(int index = -1, byte param = 0)
            => CreateBattleCharacterFuncPtr.Call<uint>(Ptr, index, param);

        [SigPattern("E8 * * * * 4C 8B C0 4D 85 C0")]
        public static IntPtr GetObjectByIndexFuncPtr { get; set; }

        public BattleChara GetObjectByIndex(ushort idx)
            => GetObjectByIndexFuncPtr.Call<IntPtr>(Ptr, idx).As<BattleChara>();

        [SigPattern("E8 * * * * 8B E8 4C 8D 35")]
        public static IntPtr GetIndexByObjectFuncPtr { get; set; }

        public uint GetIndexByObject(GameObject character)
            => GetIndexByObjectFuncPtr.Call<uint>(Ptr, character.Ptr);

        [SigPattern("E8 * * * * C6 43 49 00")]
        public static IntPtr DeleteObjectByIndexFuncPtr { get; set; }

        public void DeleteObjectByIndex(ushort idx, byte param = 0)
            => DeleteObjectByIndexFuncPtr.Call(Ptr, idx, param);

        [SigPattern("E8 * * * * 8B F8 48 8B CB 83 F8 FF")]
        public static IntPtr CalculateNextAvailableIndexFuncPtr { get; set; }

        public uint CalculateNextAvailableIndex()
            => CalculateNextAvailableIndexFuncPtr.Call<uint>(Ptr);

    }
}
