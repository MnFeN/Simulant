using Simulant.Game.FFCS.Client.Graphics.Scene;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.Game.Object
{
    public struct GameObject : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        public MemoryField<Vector3> DefaultPosition => Ptr.Field<Vector3>(0x10);
        public MemoryField<float> DefaultRotation => Ptr.Field<float>(0x20);
        // Name
        public MemoryField<byte> EventState => Ptr.Field<byte>(0x70);
        public MemoryField<uint> EntityId => Ptr.Field<uint>(0x78);
        public MemoryField<uint> LayoutId => Ptr.Field<uint>(0x7C);
        public MemoryField<uint> GimmickId => Ptr.Field<uint>(0x80);
        public MemoryField<uint> BaseId => Ptr.Field<uint>(0x84);
        public MemoryField<uint> OwnerId => Ptr.Field<uint>(0x88);
        public MemoryField<ushort> ObjectIndex => Ptr.Field<ushort>(0x8C);
        public MemoryField<ObjectKind> ObjectKind => Ptr.Field<ObjectKind>(0x90);
        public MemoryField<byte> YalmDistanceFromPlayerX => Ptr.Field<byte>(0x94);
        public MemoryField<byte> TargetStatus => Ptr.Field<byte>(0x95);
        public MemoryField<byte> YalmDistanceFromPlayerZ => Ptr.Field<byte>(0x96);
        public MemoryField<ObjectTargetableFlags> TargetableStatus => Ptr.Field<ObjectTargetableFlags>(0x9A);

        public MemoryField<Vector3> Position => Ptr.Field<Vector3>(0xB0);
        public MemoryField<float> Rotation => Ptr.Field<float>(0xC0);
        public MemoryField<float> Scale => Ptr.Field<float>(0xC4);
        public MemoryField<float> Height => Ptr.Field<float>(0xC8);
        public MemoryField<float> VfxScale => Ptr.Field<float>(0xCC);
        public MemoryField<float> HitboxRadius => Ptr.Field<float>(0xD0);
        public MemoryField<Vector3> DrawOffset => Ptr.Field<Vector3>(0xE0);
        public DrawObject DrawObject => Ptr.ReadPtr(0x100).As<DrawObject>();

        public MemoryField<VisibilityFlags> RenderFlags => Ptr.Field<VisibilityFlags>(0x118);

        // public GameObjectId GetGameObjectId()
        //    => Ptr.CallVFunc<GameObjectId>(1);

        public bool GetIsTargetable()
            => Ptr.CallVFunc<bool>(4);

        public IntPtr GetName()
            => Ptr.CallVFunc<IntPtr>(6);

        // 来源：FFXIVClientStructs 的 GameObject.SetName；原生入口同时通知名牌刷新。
        [SigPattern("E8 * * * * 48 8B 16 48 8B 4F")]
        public static IntPtr SetNameFuncPtr { get; set; }
        public void SetName(IntPtr name)
            => SetNameFuncPtr.Call(Ptr, name);

        public float GetRadius(bool adjustByTransformation = true)
            => Ptr.CallVFunc<float>(7, adjustByTransformation);

        public void EnableDraw()
            => Ptr.CallVFunc(12);

        public void DisableDraw()
            => Ptr.CallVFunc(13);

        public void SetReadyToDraw()
            => Ptr.CallVFunc(34);

        public void PositionModified()
            => Ptr.CallVFunc(54);

        public void RotationModified()
            => Ptr.CallVFunc(55);

        public bool IsNotMounted()
            => Ptr.CallVFunc<bool>(58);
    }

    public enum ObjectKind : byte
    {
        None = 0,
        Pc = 1,
        BattleNpc = 2,
        EventNpc = 3,
        Treasure = 4,
        Aetheryte = 5,
        GatheringPoint = 6,
        EventObj = 7,
        Mount = 8,
        Companion = 9,
        Retainer = 10,
        AreaObject = 11,
        HousingEventObject = 12,
        Cutscene = 13,
        ReactionEventObject = 14,
        Ornament = 15,
        CardStand = 16
    }

    // if (EntityId == 0xE0000000)
    //   if (Companion && Companion.HasOwner && Companion.EntityId == 0xE0000000) ObjectId = Parent.EntityId, Type = 4
    //   if (BaseId == 0 || (ObjectIndex >= 200 && ObjectIndex < 244)) ObjectId = ObjectIndex, Type = 2
    //   if (BaseId != 0) ObjectId = BaseId, Type = 1
    // else ObjectId = EntityId, Type = 0
    [StructLayout(LayoutKind.Explicit, Size = 0x08)]
    public struct GameObjectId : IEquatable<GameObjectId>, IComparable<GameObjectId>
    {
        [FieldOffset(0x00)] public ulong Id;
        [FieldOffset(0x00)] public uint ObjectId;
        [FieldOffset(0x04)] public byte Type;

        public static implicit operator ulong(GameObjectId id) => id.Id;

        public static implicit operator GameObjectId(ulong id)
        {
            return new GameObjectId { Id = id };
        }

        public bool Equals(GameObjectId other) => Id == other.Id;
        public override bool Equals(object obj) => obj is GameObjectId other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(GameObjectId left, GameObjectId right) => left.Id == right.Id;
        public static bool operator !=(GameObjectId left, GameObjectId right) => left.Id != right.Id;
        public int CompareTo(GameObjectId other) => Id.CompareTo(other.Id);
    }

    [Flags]
    public enum ObjectTargetableFlags : byte
    {
        IsTargetable = 1 << 1,
        Unk1 = 1 << 2, // This flag is used but purpose is unclear
        ReadyToDraw = 1 << 6
    }

    [Flags]
    public enum VisibilityFlags : ulong
    {
        None = 0,
        Model = 1ul << 1,
        Nameplate = 1ul << 11
    }
}
