using Simulant.Game.FFCS.Client.Game.Object;
using System;
using System.Numerics;

namespace Simulant.Game.FFCS.Client.Game.Character
{
    public struct CastInfo : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        public MemoryField<byte> Flags => Ptr.Field<byte>(0x00);
        public MemoryBitField IsCasting => Ptr.BitField(0x00, 0);
        public MemoryBitField Interruptible => Ptr.BitField(0x00, 1);

        /// <summary> See <see cref="Simulant.Game.FFCS.Client.Game.ActionType" /> </summary>
        public MemoryField<byte> ActionType => Ptr.Field<byte>(0x01);
        public MemoryField<uint> ActionId => Ptr.Field<uint>(0x04);
        public MemoryField<uint> SourceSequence => Ptr.Field<uint>(0x08); // for player-initiated casts - monotonically increasing id of the cast
        public MemoryField<GameObjectId> TargetId => Ptr.Field<GameObjectId>(0x10);
        public MemoryField<Vector3> TargetLocation => Ptr.Field<Vector3>(0x20);
        public MemoryField<float> Rotation => Ptr.Field<float>(0x30);
        public MemoryField<float> CurrentCastTime => Ptr.Field<float>(0x34);
        public MemoryField<float> BaseCastTime => Ptr.Field<float>(0x38);
        public MemoryField<float> TotalCastTime => Ptr.Field<float>(0x3C);
        /* 模拟器不需要 Response 相关字段
        
        // fields below (Response*) are set when ActionEffect is received - at this point cast can't be cancelled - this is the start of the slidecast window
        [FieldOffset(0x40)] public uint ResponseSpellId;
        [FieldOffset(0x44)] public ActionType ResponseActionType;
        [FieldOffset(0x48)] public uint ResponseActionId;
        [FieldOffset(0x4C)] public uint ResponseGlobalSequence;
        [FieldOffset(0x50)] public uint ResponseSourceSequence;
        [FieldOffset(0x58), FixedSizeArray] internal FixedSizeArray32<GameObjectId> _responseTargetIds;
        [FieldOffset(0x158)] public byte ResponseTargetCount;
        [FieldOffset(0x159)] public byte ResponseFlags; // see ActionEffectHandler.Header.Flags

        */
    }
}
