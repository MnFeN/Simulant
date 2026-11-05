using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Game.FFCS.Client.Network;
using System;
using System.Numerics;

namespace Simulant.Core.Entity
{
    /// <summary>游戏实体的即时访问入口；不持有调度器、模拟状态或资源所有权。</summary>
    public abstract class EntityBase
    {
        private readonly EntityRegistry _registry;
        public EntityIdentity Identity { get; }
        public IntPtr Address { get; }
        public uint Id { get; }
        public ObjectKind ObjectKind { get; }
        public bool IsAlive => _registry.IsAlive(this);

        internal EntityBase(EntityRegistry registry, EntityIdentity identity, EntityObservation observation)
        {
            _registry = registry;
            Identity = identity;
            Address = observation.Address;
            Id = observation.EntityId;
            ObjectKind = observation.ObjectKind;
        }

        internal GameObject Native
        {
            get
            {
                if (!IsAlive) throw new InvalidOperationException($"实体 {Identity} 已失效。");
                return Address.As<GameObject>();
            }
        }

        public float X
        {
            get => Native.Position.Ptr.Read<float>(0);
            set
            {
                var native = Native;
                Vector3 position = native.Position;
                position.X = value;
                SetPosition(native, position);
            }
        }

        // 对外坐标遵循 ACT：X/Y 为水平坐标，Z 为高度。
        public float Y
        {
            get => Native.Position.Ptr.Read<float>(0x8);
            set
            {
                var native = Native;
                Vector3 position = native.Position;
                position.Z = value;
                SetPosition(native, position);
            }
        }

        public float Z
        {
            get => Native.Position.Ptr.Read<float>(0x4);
            set
            {
                var native = Native;
                Vector3 position = native.Position;
                position.Y = value;
                SetPosition(native, position);
            }
        }

        public Vector2 Pos
        {
            get
            {
                Vector3 position = Native.Position;
                return new Vector2(position.X, position.Z);
            }
            set
            {
                var native = Native;
                Vector3 position = native.Position;
                SetPosition(native, new Vector3(value.X, position.Y, value.Y));
            }
        }

        public Vector3 Pos3D
        {
            get
            {
                Vector3 position = Native.Position;
                return new Vector3(position.X, position.Z, position.Y);
            }
            set
            {
                var native = Native;
                SetPosition(native, new Vector3(value.X, value.Z, value.Y));
            }
        }

        private static void SetPosition(GameObject native, Vector3 position)
        {
            native.Position.Set(position);
            native.PositionModified();
            // 本地角色不会收到服务器移动包，需同步已经存在的显示对象。
            var drawObject = native.DrawObject;
            if (!drawObject.IsNull()) drawObject.Position.Set(position);
        }

        public float Heading
        {
            get => Native.Rotation;
            set
            {
                var native = Native;
                native.Rotation.Set(value);
                native.RotationModified();
            }
        }
        public float Scale
        {
            get => Native.Scale;
            set => Native.Scale.Set(value);
        }

        public float HitboxRadius => Native.HitboxRadius;
        public void EnableDraw() => Native.EnableDraw();
        public void DisableDraw() => Native.DisableDraw();
        public void SetReadyToDraw() => Native.SetReadyToDraw();
        public void Redraw()
        {
            var native = Native;
            native.DisableDraw();
            native.EnableDraw();
        }

        public void SetEventState(byte eventState) => ActorControl(0x006A, eventState);
        public IntPtr ActorControl(ushort category, uint a1 = 0, uint a2 = 0, uint a3 = 0, uint a4 = 0)
            => PacketDispatcher.HandleActorControl(Native.EntityId, category, a1, a2, a3, a4,
                a5: 0, a6: 0, a7: 0, a8: 0, 0xE0000000, 0);
    }
}
