using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Network;
using Simulant.Game.FFCS.Client.Game.Object;
using System.Numerics;

namespace Simulant.Core.Entity
{
    public sealed class CharacterSpawnSpec
    {
        public uint BNpcBaseId;
        public uint BNpcNameId;
        public byte Level = 1;
        public Vector3 Pos;
        public float Heading;
    }

    public sealed class EventObjectSpawnSpec
    {
        /// <summary>EventObject 只有 40 个槽位，范围为 0-39。</summary>
        public byte Index;
        public byte TargetableFlags = 0x1 | 0x4;
        public bool Hidden;
        public uint BaseId;
        public uint LayoutId;
        public uint EventId;
        public uint GimmickId;
        public float Scale = 1f;
        public ushort FateId;
        public byte EventState;
        public ushort SharedTimelineState;
        public uint SharedGroupState;
        public Vector3 Pos;
        public float Heading;

        internal SpawnObjectPacket ToPacket(uint entityId)
        {
            return new SpawnObjectPacket
            {
                EntityId = entityId,
                ObjectIndex = Index,
                ObjectKind = (byte)ObjectKind.EventObj,
                TargetableStatus = TargetableFlags,
                Visibility = (byte)(Hidden ? 1 : 0),
                BaseId = BaseId,
                LayoutId = LayoutId,
                EventId = EventId,
                OwnerId = 0xE0000000,
                GimmickId = GimmickId,
                Radius = Scale,
                Rotation = PacketCodec.EncodeUShortHeading(Heading),
                FateId = FateId,
                EventState = EventState,
                SharedTimelineState = SharedTimelineState,
                SharedGroupState = SharedGroupState,
                PositionX = Pos.X,
                PositionY = Pos.Z,
                PositionZ = Pos.Y
            };
        }
    }
}
