using System;

namespace Simulant.Game.FFCS.Client.Game.Group
{
    /// <summary>客户端小队数据；布局来自 FFCS aad0de66，原生界面从主小队读取成员。</summary>
    public struct GroupManager : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/Game/Group/GroupManager.cs
        [SigPattern("33 D2 48 8D 0D * * * * 33 DB")]
        public static IntPtr InstancePtr { get; set; }

        public static GroupManager Instance() => InstancePtr.As<GroupManager>();

        public Group MainGroup => Ptr.As<Group>(0x20);

        public struct Group : IMemoryObject
        {
            public const int Capacity = 8;
            public const int Size = 0x7FF0;

            public IntPtr Ptr { get; set; }

            public MemoryField<long> PartyId => Ptr.Field<long>(0x7FC8);
            public MemoryField<long> PartyId_2 => Ptr.Field<long>(0x7FD0);
            public MemoryField<uint> PartyLeaderIndex => Ptr.Field<uint>(0x7FD8);
            public MemoryField<byte> MemberCount => Ptr.Field<byte>(0x7FDC);
            public MemoryField<byte> AllianceFlags => Ptr.Field<byte>(0x7FE1);

            public PartyMember GetPartyMember(int index)
            {
                if ((uint)index >= Capacity)
                    throw new ArgumentOutOfRangeException(nameof(index), "小队成员索引必须为 0-7。");
                return Ptr.As<PartyMember>(index * PartyMember.Size);
            }
        }
    }
}
