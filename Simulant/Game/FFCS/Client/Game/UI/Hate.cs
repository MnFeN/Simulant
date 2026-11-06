using System;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.Game.UI
{
    /// <summary>当前目标对各角色的仇恨，供小队栏显示；与敌对列表 Hater 不同。</summary>
    public struct Hate : IMemoryObject
    {
        // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/Game/UI/Hate.cs
        public const int Size = 0x108;
        public const int Capacity = 32;

        public IntPtr Ptr { get; set; }
        public MemoryField<int> HateArrayLength => Ptr.Field<int>(0x100);
        public MemoryField<uint> HateTargetId => Ptr.Field<uint>(0x104);

        public MemoryField<HateEntry> GetEntry(int index)
        {
            if ((uint)index >= Capacity)
                throw new ArgumentOutOfRangeException(nameof(index), "仇恨条目索引必须为 0-31。");
            return Ptr.Field<HateEntry>(index * HateEntry.Size);
        }

        /// <summary>只保存值型仇恨数据；恢复时应使用捕获快照时的进程实例。</summary>
        public byte[] ReadSnapshot() => Ptr.ReadBytes(Size);
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public struct HateEntry
    {
        public const int Size = 8;
        [FieldOffset(0)] public uint EntityId;
        [FieldOffset(4)] public int Enmity;
    }
}
