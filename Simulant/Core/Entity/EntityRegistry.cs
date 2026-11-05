using System;
using System.Collections.Generic;
using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Object;

namespace Simulant.Core.Entity
{
    /// <summary>
    /// Provider 和 Spawner 共用的实体目录。每次访问原生内存前检查对象表，
    /// 换图、切换进程时还必须由宿主 Reset；无法观测到的同值复用不能靠地址检查识别。
    /// </summary>
    public sealed class EntityRegistry
    {
        private sealed class Entry
        {
            public EntityIdentity Identity;
            public EntityObservation Observation;
            public EntityBase Entity;
        }

        private readonly object _gate = new object();
        private readonly Func<ushort, EntityObservation?> _observe;
        private readonly Entry[] _entries;
        private long _worldEpoch = 1;
        private long _generation;

        public EntityRegistry() : this(ObserveNative) { }

        public EntityRegistry(Func<ushort, EntityObservation?> observe, int capacity = GameObjectPtrs.Count)
        {
            _observe = observe ?? throw new ArgumentNullException(nameof(observe));
            if (capacity <= 0 || capacity > ushort.MaxValue + 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _entries = new Entry[capacity];
        }

        public EntityBase Get(ushort slot)
        {
            lock (_gate)
            {
                ValidateSlot(slot);
                var observed = _observe(slot);
                if (!IsSupported(observed))
                {
                    _entries[slot] = null;
                    return null;
                }
                var entry = _entries[slot];
                if (entry != null && entry.Observation.Equals(observed.Value))
                    return entry.Entity; // null 表示已显式释放，不应重新关联已经隐藏的 EObj。
                return Register(slot, observed.Value);
            }
        }

        public IEnumerable<EntityBase> GetEntities()
        {
            for (int slot = 0; slot < _entries.Length; slot++)
            {
                var entity = Get((ushort)slot);
                if (entity != null) yield return entity;
            }
        }

        /// <summary>本地创建必须登记新代次，即使游戏恰好复用了同一地址和 ID。</summary>
        public EntityBase RegisterCreated(ushort slot)
        {
            lock (_gate)
            {
                ValidateSlot(slot);
                var observed = _observe(slot);
                if (!IsSupported(observed))
                {
                    _entries[slot] = null;
                    throw new InvalidOperationException($"槽位 {slot} 中没有可登记的实体。");
                }
                return Register(slot, observed.Value);
            }
        }

        public bool IsAlive(EntityBase entity)
        {
            if (entity == null) return false;
            lock (_gate)
            {
                var identity = entity.Identity;
                if (identity.WorldEpoch != _worldEpoch || identity.Slot >= _entries.Length)
                    return false;
                var entry = _entries[identity.Slot];
                if (entry == null || !ReferenceEquals(entry.Entity, entity)) return false;
                var observed = _observe(identity.Slot);
                if (observed.HasValue && entry.Observation.Equals(observed.Value)) return true;
                _entries[identity.Slot] = null;
                return false;
            }
        }

        public void Invalidate(EntityBase entity)
        {
            if (entity == null) return;
            lock (_gate)
            {
                var identity = entity.Identity;
                if (identity.WorldEpoch != _worldEpoch || identity.Slot >= _entries.Length) return;
                var entry = _entries[identity.Slot];
                if (entry != null && ReferenceEquals(entry.Entity, entity))
                    entry.Entity = null;
            }
        }

        // 已释放的包装对象保持失效，但资源所有者仍可重试原生清理。
        // 检查保留的代次，避免重试时操作已替换的新实体。
        internal bool MatchesCurrent(EntityBase entity)
        {
            if (entity == null) return false;
            lock (_gate)
            {
                var identity = entity.Identity;
                if (identity.WorldEpoch != _worldEpoch || identity.Slot >= _entries.Length) return false;
                var entry = _entries[identity.Slot];
                if (entry == null || entry.Identity != identity) return false;
                var observed = _observe(identity.Slot);
                if (observed.HasValue && entry.Observation.Equals(observed.Value)) return true;
                _entries[identity.Slot] = null;
                return false;
            }
        }

        internal void InvalidateSlot(ushort slot)
        {
            lock (_gate)
            {
                ValidateSlot(slot);
                var entry = _entries[slot];
                if (entry != null) entry.Entity = null;
            }
        }

        public void Reset()
        {
            lock (_gate)
            {
                _worldEpoch++;
                Array.Clear(_entries, 0, _entries.Length);
            }
        }

        private EntityBase Register(ushort slot, EntityObservation observation)
        {
            var identity = new EntityIdentity(_worldEpoch, ++_generation, slot);
            EntityBase entity = observation.ObjectKind == ObjectKind.EventObj
                ? (EntityBase)new EventObject(this, identity, observation)
                : new Character(this, identity, observation);
            _entries[slot] = new Entry { Identity = identity, Observation = observation, Entity = entity };
            return entity;
        }

        private void ValidateSlot(ushort slot)
        {
            if (slot >= _entries.Length) throw new ArgumentOutOfRangeException(nameof(slot));
        }

        private static bool IsSupported(EntityObservation? observation)
        {
            if (!observation.HasValue) return false;
            var value = observation.Value;
            return value.Address != IntPtr.Zero
                && (value.ObjectKind == ObjectKind.Pc || value.ObjectKind == ObjectKind.BattleNpc
                    || value.ObjectKind == ObjectKind.EventObj);
        }

        private static EntityObservation? ObserveNative(ushort slot)
        {
            var manager = GameObjectManager.Instance();
            if (manager.IsNull()) return null;
            var native = manager.Objects.IndexSorted[slot];
            if (native.IsNull()) return null;
            return new EntityObservation(native.Ptr, native.EntityId, native.ObjectKind);
        }
    }
}
