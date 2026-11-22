using System;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.Game.UI
{
    // https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/Hater.cs
    /// <summary>原生敌对列表，包含名称、实体编号和本机玩家的仇恨百分比。</summary>
    public struct Hater : IMemoryObject
    {
        public const int Size = 0x908;
        public const int Capacity = 32;
        private const int CountOffset = 0x900;

        public IntPtr Ptr { get; set; }
        public MemoryField<int> HaterCount => Ptr.Field<int>(CountOffset);

        internal HaterInfo[] ReadEntries()
        {
            var data = Ptr.ReadBytes(Size);
            var count = BitConverter.ToInt32(data, CountOffset);
            if ((uint)count > Capacity)
                throw new InvalidOperationException("原生敌对列表的条目数超出范围。");

            return MemoryMarshal.Cast<byte, HaterInfo>(data.AsSpan(0, count * HaterInfo.Size)).ToArray();
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public unsafe struct HaterInfo
    {
        public const int Size = 0x48;

        [FieldOffset(0x00)]
        public fixed byte Name[0x40];
        [FieldOffset(0x40)]
        public uint EntityId;
        [FieldOffset(0x44)]
        public int Enmity;
    }
}
