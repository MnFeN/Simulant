using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Game.FFCS.Client.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NativeCharacter = Simulant.Game.FFCS.Client.Game.Character.Character;

namespace Simulant.Core.Entity
{
    /// <summary>只负责本地资源的创建和释放。行为与延迟动作由模拟会话管理。</summary>
    public sealed class EntitySpawner : IDisposable
    {
        private sealed class OwnedEntity
        {
            public EntityBase Entity;
            public ushort LocalIndex;
        }

        private const ushort EventObjectSlotStart = 449;
        private readonly object _gate = new object();
        private readonly EntityRegistry _registry;
        private readonly EntityProvider _provider;
        private readonly Action<string> _logError;
        private readonly Dictionary<EntityIdentity, OwnedEntity> _owned =
            new Dictionary<EntityIdentity, OwnedEntity>();
        private bool _disposed;
        private bool _worldReady;
        private static readonly object IdGate = new object();
        private static ushort _playerIdIndex;
        private static ushort _nonPlayerIdIndex;

        public EntitySpawner(EntityRegistry registry, EntityProvider provider, Action<string> logError = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _logError = logError;
        }

        private const CharacterSetupContainer.CopyFlags DefaultPlayerCopyFlags =
            CharacterSetupContainer.CopyFlags.ClassJob |
            CharacterSetupContainer.CopyFlags.Position |
            CharacterSetupContainer.CopyFlags.Name;

        public Character SpawnPlayer(Job job, Character source = null,
            CharacterSetupContainer.CopyFlags copyFlags = DefaultPlayerCopyFlags)
        {
            lock (_gate)
            {
                EnsureWorldReady();
                source = source ?? _provider.GetMyself()
                    ?? throw new ArgumentException("未找到自身实体", nameof(source));
                if (source.ObjectKind != ObjectKind.Pc)
                    throw new ArgumentException("实体源必须为玩家角色", nameof(source));
                return CreateCharacter(native =>
                {
                    native.CharacterSetup.CopyFromCharacter(source.Native, copyFlags);
                    native.ObjectKind.Set(ObjectKind.Pc);
                    native.EntityId.Set(NextId(true));
                    native.ClassJob.Set(job);
                });
            }
        }

        public Character SpawnBNpc(uint bNpcBaseId, uint bNpcNameId, byte level)
            => SpawnBNpc(new CharacterSpawnSpec
            {
                BNpcBaseId = bNpcBaseId,
                BNpcNameId = bNpcNameId,
                Level = level
            });

        public Character SpawnBNpc(CharacterSpawnSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            lock (_gate)
            {
                EnsureWorldReady();
                return CreateCharacter(native =>
                {
                    native.CharacterSetup.SetupBNpc(spec.BNpcBaseId, spec.BNpcNameId);
                    native.ObjectKind.Set(ObjectKind.BattleNpc);
                    native.EntityId.Set(NextId(false));
                    native.Level.Set(spec.Level);
                    native.Battalion.Set(4);
                    native.IsHostile.Set(true);
                    native.InCombat.Set(true);
                    native.Position.Set(new Vector3(spec.Pos.X, spec.Pos.Z, spec.Pos.Y));
                    native.Rotation.Set(spec.Heading);
                });
            }
        }

        private Character CreateCharacter(Action<NativeCharacter> setup)
        {
            var manager = ClientObjectManager.Instance;
            var index = manager.CreateBattleCharacter();
            if (index >= ClientObjectManager.BattleCharaList.Count)
                throw new InvalidOperationException($"生成实体失败：无效的本地实体索引 #{index}。");
            var localIndex = (ushort)index;
            Character entity = null;
            var native = default(NativeCharacter);
            try
            {
                native = manager.GetObjectByIndex(localIndex);
                if (native.IsNull())
                    throw new InvalidOperationException($"生成实体失败：本地实体索引 #{index} 对应空指针。");
                setup(native);
                var registered = _registry.RegisterCreated(native.ObjectIndex) as Character
                    ?? throw new InvalidOperationException("生成实体失败：对象表中的实体不是角色。");
                if (registered.Address != native.Ptr || registered.Id != (uint)native.EntityId)
                    throw new InvalidOperationException("生成实体失败：对象表中的角色与新建资源不匹配。");
                entity = registered;
                _owned.Add(entity.Identity, new OwnedEntity { Entity = entity, LocalIndex = localIndex });
                return entity;
            }
            catch (Exception setupError)
            {
                _registry.Invalidate(entity);
                try { manager.DeleteObjectByIndex(localIndex, 0); }
                catch (Exception cleanupError)
                {
                    // 已成功登记的资源保留清理记录，后续 Clear/Dispose 可重试。
                    if (entity != null && entity.Address == native.Ptr && _registry.MatchesCurrent(entity))
                        _owned[entity.Identity] = new OwnedEntity { Entity = entity, LocalIndex = localIndex };
                    _logError?.Invoke("角色初始化失败后回收失败：" + cleanupError);
                    throw new AggregateException("角色初始化及回收均失败。", setupError, cleanupError);
                }
                throw;
            }
        }

        public EventObject SpawnEObj(int index, uint baseId, Vector3 pos, float heading, ushort animation = 0)
        {
            if (index < 0 || index >= 40) throw new ArgumentOutOfRangeException(nameof(index));
            return SpawnEObj(new EventObjectSpawnSpec
            {
                Index = (byte)index,
                BaseId = baseId,
                Pos = pos,
                Heading = heading,
                SharedTimelineState = animation
            });
        }

        public EventObject SpawnEObj(EventObjectSpawnSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (spec.Index >= 40) throw new ArgumentOutOfRangeException(nameof(spec), "EventObject 索引必须为 0-39。");
            lock (_gate)
            {
                EnsureWorldReady();
                var slot = (ushort)(EventObjectSlotStart + spec.Index);
                // 包生成操作会覆盖指定槽位。先撤销旧实例，防止旧节点继续操作新对象。
                foreach (var entry in _owned.Values.Where(e => e.Entity.Identity.Slot == slot).ToArray())
                    DeleteCore(entry.Entity);
                _registry.InvalidateSlot(slot);
                var packet = spec.ToPacket(NextId(false));
                EventObject entity = null;
                try
                {
                    PacketDispatcher.HandleSpawnObjectPacket(packet.EntityId, packet);
                    entity = _registry.RegisterCreated(slot) as EventObject
                        ?? throw new InvalidOperationException($"生成实体失败：槽位 {slot} 中不是 EObj。");
                    if (entity.Id != packet.EntityId)
                        throw new InvalidOperationException($"生成实体失败：槽位 {slot} 的 EObj ID 不匹配。");
                    _owned.Add(entity.Identity, new OwnedEntity { Entity = entity, LocalIndex = spec.Index });
                    return entity;
                }
                catch (Exception setupError)
                {
                    // 尚无 EObj 原生销毁接口：失败时也只隐藏本次生成的对象。
                    var current = _registry.Get(slot);
                    if (current is EventObject && current.Id == packet.EntityId)
                    {
                        _owned[current.Identity] = new OwnedEntity { Entity = current, LocalIndex = spec.Index };
                        try { DeleteCore(current); }
                        catch (Exception cleanupError)
                        {
                            _logError?.Invoke("EObj 初始化失败后隐藏失败：" + cleanupError);
                            throw new AggregateException("EObj 初始化及隐藏均失败。", setupError, cleanupError);
                        }
                    }
                    if (entity != null && entity.Id == packet.EntityId) _registry.Invalidate(entity);
                    throw;
                }
            }
        }

        public bool Owns(EntityBase entity)
        {
            if (entity == null) return false;
            lock (_gate)
                return _owned.TryGetValue(entity.Identity, out var entry) && ReferenceEquals(entry.Entity, entity);
        }

        public void Delete(EntityBase entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            lock (_gate) DeleteCore(entity);
        }

        private void DeleteCore(EntityBase entity)
        {
            if (!_owned.TryGetValue(entity.Identity, out var entry) || !ReferenceEquals(entry.Entity, entity))
                return;
            if (!_registry.MatchesCurrent(entity))
            {
                _owned.Remove(entity.Identity);
                return; // 槽位已由游戏复用；绝不能删除当前的新对象。
            }
            var native = entity.Address.As<GameObject>();
            _registry.Invalidate(entity);
            if (entity is EventObject)
                native.DisableDraw(); // 只隐藏并释放引用，不能使用 BattleChara 的删除方式回收 EObj。
            else
                ClientObjectManager.Instance.DeleteObjectByIndex(entry.LocalIndex, 0);
            _owned.Remove(entity.Identity);
        }

        /// <summary>回收仍属于当前世界的所有本地资源；可继续生成。</summary>
        public void Clear()
        {
            lock (_gate) ClearCore();
        }

        private void ClearCore()
        {
            List<Exception> failures = null;
            foreach (var entity in _owned.Values.Select(e => e.Entity).ToArray())
            {
                try { DeleteCore(entity); }
                catch (Exception ex)
                {
                    _logError?.Invoke("回收实体失败：" + ex);
                    (failures ?? (failures = new List<Exception>())).Add(ex);
                }
            }
            if (failures != null) throw new AggregateException("部分本地实体回收失败。", failures);
        }

        /// <summary>进程/世界已经切换时，只丢弃旧引用；不能访问上一世界的原生内存。</summary>
        public void ResetWorld()
        {
            lock (_gate)
            {
                _worldReady = false;
                _owned.Clear();
                _registry.Reset();
            }
        }

        /// <summary>宿主停止旧会话并完成当前世界初始化后，才重新开放创建。</summary>
        internal void ResumeWorld()
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                _worldReady = true;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed && _owned.Count == 0) return;
                _disposed = true;
                ClearCore();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EntitySpawner));
        }

        private void EnsureWorldReady()
        {
            ThrowIfDisposed();
            if (!_worldReady) throw new InvalidOperationException("实体世界尚未准备完成，暂时不能生成实体。");
        }

        private static uint NextId(bool player)
        {
            lock (IdGate)
                return player
                    ? 0x10FF0000u + unchecked(++_playerIdIndex)
                    : 0x40FF0000u + unchecked(++_nonPlayerIdIndex);
        }
    }
}
