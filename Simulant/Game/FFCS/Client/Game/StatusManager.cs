using Simulant.Game.FFCS.Client.Game.Object;
using System;

namespace Simulant.Game.FFCS.Client.Game
{
    public struct StatusManager : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // [FieldOffset(0x8), FixedSizeArray] internal FixedSizeArray60<Status> _status;
        public Status GetStatus(int index) => Ptr.As<Status>(0x8 + index * 0x10); // temp until we implement the array struct

        [SigPattern("E8 * * * * C6 43 2D 00")]
        public static IntPtr HasStatusFuncPtr { get; set; }
        public bool HasStatus(uint statusId, uint sourceId = 0xE0000000)
            => HasStatusFuncPtr.Call<bool>(Ptr, statusId, sourceId);

        [SigPattern("E8 * * * * 85 C0 79 ? 4C 8B 15")]
        public static IntPtr GetStatusIndexFuncPtr { get; set; }
        public int GetStatusIndex(uint statusId, uint sourceId = 0xE0000000)
            => GetStatusIndexFuncPtr.Call<int>(Ptr, statusId, sourceId);

        [SigPattern("83 FA 3C 72 04 0F 57 C0")]
        public static IntPtr GetRemainingTimeFuncPtr { get; set; }
        public float GetRemainingTime(int statusIndex)
            => GetRemainingTimeFuncPtr.Call<float>(Ptr, statusIndex);

        [SigPattern("E8 * * * * 85 C0 75 ? FE C3 EB")]
        public static IntPtr GetStatusIdFuncPtr { get; set; }
        public uint GetStatusId(int statusIndex)
            => GetStatusIdFuncPtr.Call<uint>(Ptr, statusIndex);

        [SigPattern("E8 * * * * 3B 44 24 28")]
        public static IntPtr GetSourceIdFuncPtr { get; set; }
        public uint GetSourceId(int statusIndex)
            => GetSourceIdFuncPtr.Call<uint>(Ptr, statusIndex);

        [SigPattern("66 85 D2 0F 84 ? ? ? ? 48 89 5C 24 ? 48 89 6C 24 ?")]
        public static IntPtr AddStatusFuncPtr { get; set; }
        public void AddStatus(ushort statusId, ushort param = 0, IntPtr u3 = default)
            => AddStatusFuncPtr.Call(Ptr, statusId, param, u3);

        [SigPattern("83 FA 3C 73 ? 53 48 83 EC 30 48 8B D9")] // INLINED
        public static IntPtr RemoveStatusFuncPtr { get; set; }
        public void RemoveStatus(int statusIndex, byte u2 = 0)
            => RemoveStatusFuncPtr.Call(Ptr, statusIndex, u2); // u2 always appears to be 0

        [SigPattern("E8 * * * * 40 0A F0 48 8D 5B")]
        public static IntPtr SetStatusFuncPtr { get; set; }
        /// <summary>设置状态；返回编号或来源是否改变，仅刷新时间、参数时返回 false，并非设置失败。</summary>
        public bool SetStatus(int statusIndex, ushort statusId, float remaining, ushort param, GameObjectId sourceObject, bool refreshFlags)
            // 原生按值接收完整 64 位身份；object[] 不会触发 GameObjectId 到 ulong 的隐式转换。
            => SetStatusFuncPtr.Call<bool>(Ptr, statusIndex, statusId, remaining, param, sourceObject.Id, refreshFlags);
    }

    public struct Status : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        public MemoryField<ushort> StatusId => Ptr.Field<ushort>(0x0);
        // this contains different information depending on the type of status
        // debuffs - stack count
        // food/potions - ID of the food/potion in the ItemFood sheet
        public MemoryField<ushort> Param => Ptr.Field<ushort>(0x2);
        public MemoryField<float> RemainingTime => Ptr.Field<float>(0x4);
        // ObjectId matching the entity that cast the effect - regens will be from the white mage ID etc
        public MemoryField<GameObjectId> SourceObject => Ptr.Field<GameObjectId>(0x8);
    }
}
