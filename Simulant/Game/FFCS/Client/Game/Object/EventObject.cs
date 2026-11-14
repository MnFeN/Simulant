using Simulant.Game.FFCS.Client.Graphics.Scene;
using System;
using System.Numerics;

namespace Simulant.Game.FFCS.Client.Game.Object
{
    public struct EventObject : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // Inherits<GameObject>
        private GameObject GameObject => Ptr.As<GameObject>();
        #region GameObject
        public MemoryField<Vector3> DefaultPosition => GameObject.DefaultPosition;
        public MemoryField<float> DefaultRotation => GameObject.DefaultRotation;
        public MemoryField<byte> EventState => GameObject.EventState;
        public MemoryField<uint> EntityId => GameObject.EntityId;
        public MemoryField<uint> LayoutId => GameObject.LayoutId;
        public MemoryField<uint> GimmickId => GameObject.GimmickId;
        public MemoryField<uint> BaseId => GameObject.BaseId;
        public MemoryField<uint> OwnerId => GameObject.OwnerId;
        public MemoryField<ushort> ObjectIndex => GameObject.ObjectIndex;
        public MemoryField<ObjectKind> ObjectKind => GameObject.ObjectKind;
        public MemoryField<byte> YalmDistanceFromPlayerX => GameObject.YalmDistanceFromPlayerX;
        public MemoryField<byte> TargetStatus => GameObject.TargetStatus;
        public MemoryField<byte> YalmDistanceFromPlayerZ => GameObject.YalmDistanceFromPlayerZ;
        public MemoryField<ObjectTargetableFlags> TargetableStatus => GameObject.TargetableStatus;
        public MemoryField<Vector3> Position => GameObject.Position;
        public MemoryField<float> Rotation => GameObject.Rotation;
        public MemoryField<float> Scale => GameObject.Scale;
        public MemoryField<float> Height => GameObject.Height;
        public MemoryField<float> VfxScale => GameObject.VfxScale;
        public MemoryField<float> HitboxRadius => GameObject.HitboxRadius;
        public MemoryField<Vector3> DrawOffset => GameObject.DrawOffset;
        public DrawObject DrawObject => GameObject.DrawObject;
        public MemoryField<VisibilityFlags> RenderFlags => GameObject.RenderFlags;

        public bool GetIsTargetable() => GameObject.GetIsTargetable();
        public IntPtr GetName() => GameObject.GetName();
        public float GetRadius(bool adjustByTransformation = true) => GameObject.GetRadius(adjustByTransformation);
        public void EnableDraw() => GameObject.EnableDraw();
        public void DisableDraw() => GameObject.DisableDraw();
        public void SetReadyToDraw() => GameObject.SetReadyToDraw();
        public void PositionModified() => GameObject.PositionModified();
        public void RotationModified() => GameObject.RotationModified();
        public bool IsNotMounted() => GameObject.IsNotMounted();

        #endregion
        public MemoryField<IntPtr> EObjRowPtr => Ptr.Field<IntPtr>(0x1A0);
        public MemoryField<IntPtr> ExportedSGRowPtr => Ptr.Field<IntPtr>(0x1A8);

        // 以下是从 PlayAnimation 函数看出的
        public MemoryField<ushort> SharedTimelineState => Ptr.Field<ushort>(0x1B2); // 从 payload 0x2C 写入的
        public MemoryField<uint> SharedGroupState => Ptr.Field<uint>(0x1B4); // 从 payload 0x30 写入的
        public MemoryField<byte> PendingOrRefreshState => Ptr.Field<byte>(0x1B8);

        [SigPattern("E8 * * * * E9 ? ? ? ? 4D 85 F6 0F 84 ? ? ? ? 8B 44 24 70 BE ? ? ? ?")]
        public static IntPtr PlayAnimationFuncPtr { get; set; }

        // a2: sharedTimelineState, 似乎可以随意传参，实际值未观察到影响动画行为
        // a3: bitMask, 相当于 MapEffect 的 slotMask (flags)，目前同样仅观察到单比特值，如 0x1 0x80 0x200
        public void PlayAnimation(ushort sharedTimelineState, ushort bitMask, ulong context = 0)
            => PlayAnimationFuncPtr.Call(Ptr, sharedTimelineState, bitMask, context);
    }
}
