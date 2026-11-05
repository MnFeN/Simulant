using Simulant.Game;
using System;
using System.Collections.Generic;

namespace Simulant.Game.FFCS.Client.Game.Object
{
    // Client::Game::Object::GameObjectManager
    public struct GameObjectManager : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        [SigPattern("48 8D 35 * * * * 81 FA")]
        public static IntPtr InstancePtr { get; set; }
        public static GameObjectManager Instance()
            => InstancePtr.As<GameObjectManager>();

        public ObjectArrays Objects => Ptr.As<ObjectArrays>(0x20);
    }

    public struct ObjectArrays : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // sparse array containing all objects; some slots could be null
        // different ranges have different meaning:
        // 000-199: 200 objects from CharacterManager, 100 BattleCharas at index 2*i and their dependendent 100 objects (mounts, minions, etc) at index 2*i+1 (networked)
        // 200-448: 249 objects from ClientObjectManager (non-networked)
        // 449-488:  40 objects from EventObjectManager? (contains AreaObject, EventObject, GatheringPointObject, HousingObject, HousingCombinedObject, Treasure)
        // 489-628: 140 objects from StandObjectManager (Lively Actors (Named/Unnamed ENPCs))
        // 629-728: 100 objects from ReactionEventObjectManager (Gatherables/Farm in Island Sanctuary)
        // 729-818:  90 objects from MJIManager and WKSManager (more Lively Actors)

        /// <summary> Pointers to GameObjects, sorted by ObjectIndex. Contains null pointers for inactive indexes. </summary>
        public GameObjectPtrs IndexSorted 
            => Ptr.As<GameObjectPtrs>(0);

        /// <summary> Pointers to active GameObjects, sorted by GameObject.GetGameObjectId(). </summary>
        public GameObjectPtrs GameObjectIdSorted 
            => Ptr.As<GameObjectPtrs>(GameObjectPtrs.Size); // 0x1998

        /// <summary> Pointers to active GameObjects with a valid GameObject.EntityId (!= E0000000), sorted by EntityId.  </summary>
        public GameObjectPtrs EntityIdSorted 
            => Ptr.As<GameObjectPtrs>(GameObjectPtrs.Size * 2); // 0x3330

        public MemoryField<int> GameObjectIdSortedCount 
            => Ptr.Field<int>(GameObjectPtrs.Size * 3); // 0x4CC8
        public MemoryField<int> EntityIdSortedCount 
            => Ptr.Field<int>(GameObjectPtrs.Size * 3 + sizeof(int)); // 0x4CCC

        /*
        /// <summary>
        /// Binary search for an object by GameObjectId, using the GameObjectId sorted list.
        /// </summary>
        [SigPattern("E8 * * * * 48 8B D8 48 8B D3 48 8D 0D ? ? ? ? E8 ? ? ? ? 48 8D 8E")]
        public static IntPtr GetObjectByGameObjectIdFuncPtr { get; set; }

        public GameObject GetObjectByGameObjectId(GameObjectId objectId)
            => GetObjectByGameObjectIdFuncPtr.Call<IntPtr>(Ptr, objectId).As<GameObject>();

        /// <summary>
        /// Binary search for an object by EntityId, using the EntityId sorted list.
        /// </summary>
        [SigPattern("48 89 5C 24 ? 44 8B 89")]
        public static IntPtr GetObjectByEntityIdFuncPtr { get; set; }

        public GameObject GetObjectByEntityId(uint entityId)
            => GetObjectByEntityIdFuncPtr.Call<IntPtr>(Ptr, entityId).As<GameObject>();
        */
    }

    public struct GameObjectPtrs : IMemoryObject
    {
        public const int Count = 819;
        public const int Size = 0x1998; // 819 * 8

        public IntPtr Ptr { get; set; }

        public GameObject this[int index]
        {
            get
            {
                if ((uint)index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
                return Ptr.ReadPtr(index * IntPtr.Size).As<GameObject>();
            }
        }

        public IEnumerable<GameObject> GameObjects
        {
            get
            {
                var bytes = Ptr.ReadBytes(Size);

                for (int i = 0; i < Count; i++)
                {
                    var objectPtr = new IntPtr(BitConverter.ToInt64(bytes, i * IntPtr.Size));
                    yield return objectPtr.As<GameObject>();
                }
            }
        }
    }
}
