using System;
using Simulant.Game.FFCS.Client.Game.Object;

namespace Simulant.Core.Entity
{
    /// <summary>一次世界生命周期中的一个实体实例。槽位或地址复用不会复用此身份。</summary>
    public readonly struct EntityIdentity : IEquatable<EntityIdentity>
    {
        public long WorldEpoch { get; }
        public long Generation { get; }
        public ushort Slot { get; }

        public EntityIdentity(long worldEpoch, long generation, ushort slot)
        {
            WorldEpoch = worldEpoch;
            Generation = generation;
            Slot = slot;
        }

        public bool Equals(EntityIdentity other)
            => WorldEpoch == other.WorldEpoch && Generation == other.Generation && Slot == other.Slot;
        public override bool Equals(object obj) => obj is EntityIdentity other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { return ((WorldEpoch.GetHashCode() * 397) ^ Generation.GetHashCode()) * 397 ^ Slot; }
        }
        public static bool operator ==(EntityIdentity left, EntityIdentity right) => left.Equals(right);
        public static bool operator !=(EntityIdentity left, EntityIdentity right) => !left.Equals(right);
        public override string ToString() => $"{WorldEpoch}:{Slot}:{Generation}";
    }

    /// <summary>对象表中一次观测到的身份数据，不含行为或模拟状态。</summary>
    public readonly struct EntityObservation : IEquatable<EntityObservation>
    {
        public IntPtr Address { get; }
        public uint EntityId { get; }
        public ObjectKind ObjectKind { get; }

        public EntityObservation(IntPtr address, uint entityId, ObjectKind objectKind)
        {
            Address = address;
            EntityId = entityId;
            ObjectKind = objectKind;
        }

        public bool Equals(EntityObservation other)
            => Address == other.Address && EntityId == other.EntityId && ObjectKind == other.ObjectKind;
        public override bool Equals(object obj) => obj is EntityObservation other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { return ((Address.GetHashCode() * 397) ^ (int)EntityId) * 397 ^ (int)ObjectKind; }
        }
    }
}
