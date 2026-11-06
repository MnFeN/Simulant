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
    public sealed partial class EntitySpawner : IDisposable
    {
        private sealed class OwnedEntity
        {
            public EntityBase Entity;
            public ushort LocalIndex;
            public NetworkCharacterSpawnRequest NetworkRequest;
        }

        private const ushort EventObjectSlotStart = 449;
        private readonly object _gate = new object();
        private readonly EntityRegistry _registry;
        private readonly EntityProvider _provider;
        private readonly Action<string> _logError;
        private readonly Func<bool> _networkSpawnAllowed;
        private readonly Dictionary<EntityIdentity, OwnedEntity> _owned =
            new Dictionary<EntityIdentity, OwnedEntity>();
        private bool _disposed;
        private bool _worldReady;
        private static readonly object IdGate = new object();
        private static ushort _playerIdIndex;
        private static ushort _nonPlayerIdIndex;

        public EntitySpawner(EntityRegistry registry, EntityProvider provider, Action<string> logError = null,
            Func<bool> networkSpawnAllowed = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _logError = logError;
            _networkSpawnAllowed = networkSpawnAllowed;
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
                return CreateCharacter(true, (native, entityId) =>
                {
                    native.CharacterSetup.CopyFromCharacter(source.Native, copyFlags);
                    native.ObjectKind.Set(ObjectKind.Pc);
                    native.EntityId.Set(entityId);
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
                return CreateCharacter(false, (native, entityId) =>
                {
                    native.CharacterSetup.SetupBNpc(spec.BNpcBaseId, spec.BNpcNameId);
                    native.ObjectKind.Set(ObjectKind.BattleNpc);
                    native.EntityId.Set(entityId);
                    native.Level.Set(spec.Level);
                    native.Battalion.Set(4);
                    native.IsHostile.Set(true);
                    native.InCombat.Set(true);
                    native.Position.Set(new Vector3(spec.Pos.X, spec.Pos.Z, spec.Pos.Y));
                    native.Rotation.Set(spec.Heading);
                });
            }
        }

        private Character CreateCharacter(bool player, Action<NativeCharacter, uint> setup)
        {
            if (_failedLocalCreations.Count != 0)
                throw new InvalidOperationException("上次本地角色创建失败后尚未完成回收，请先重试清理。");
            var manager = ClientObjectManager.Instance;
            var creation = CaptureLocalCreationContext(manager);
            var entityId = NextAvailableId(CharacterManager.Instance(),
                GameObjectManager.Instance().Objects.IndexSorted, player);
            EnsureLocalCreationContext(creation);
            var index = manager.CreateBattleCharacter();
            if (index >= ClientObjectManager.BattleCharaList.Count)
                throw new InvalidOperationException($"生成实体失败：无效的本地实体索引 #{index}。");
            var localIndex = (ushort)index;
            creation.LocalIndex = localIndex;
            creation.ObjectSlot = (ushort)(LocalCharacterSlotStart + localIndex);
            Character entity = null;
            var native = default(NativeCharacter);
            try
            {
                native = manager.GetObjectByIndex(localIndex);
                creation.NativeAddress = native.Ptr;
                if (native.IsNull())
                    throw new InvalidOperationException($"生成实体失败：本地实体索引 #{index} 对应空指针。");
                setup(native, entityId);
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
                creation.Entity = entity;
                CaptureFailedLocalCreationIdentity(creation);
                _registry.Invalidate(entity);
                try
                {
                    // 首次回滚仍立即删除刚分配的槽，但不能跨进程或世界调用旧地址。
                    EnsureLocalCreationContext(creation);
                    if (creation.OwnershipConflict)
                        throw new InvalidOperationException($"本地失败分配 #{localIndex} 的槽位归属已发生冲突，不能按索引回收。");
                    manager.DeleteObjectByIndex(localIndex, 0);
                }
                catch (Exception cleanupError)
                {
                    // 未交付的失败分配单独保留，不能依赖调用方已取得实体包装。
                    if (creation.WorldGeneration == _localCreationGeneration)
                        _failedLocalCreations.Add(creation);
                    LogCleanupError("角色初始化失败后回收失败：", cleanupError);
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
                // 构包校验失败时保留原槽位及其回收责任。
                var packet = spec.ToPacket(0);
                packet.EntityId = NextAvailableId(CharacterManager.Instance(),
                    GameObjectManager.Instance().Objects.IndexSorted, false);
                var slot = (ushort)(EventObjectSlotStart + spec.Index);
                // 包生成操作会覆盖指定槽位。先撤销旧实例，防止旧节点继续操作新对象。
                foreach (var entry in _owned.Values.Where(e => e.Entity.Identity.Slot == slot).ToArray())
                    DeleteCore(entry.Entity);
                _registry.InvalidateSlot(slot);
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
                            LogCleanupError("EObj 初始化失败后隐藏失败：", cleanupError);
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
            if (entry.NetworkRequest != null)
            {
                CancelNetworkCore(entry.NetworkRequest);
                return;
            }
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
            foreach (var request in _networkSpawns.Values.ToArray())
            {
                try { CancelNetworkCore(request); }
                catch (Exception ex)
                {
                    (failures ?? (failures = new List<Exception>())).Add(ex);
                    LogCleanupError("回收网络实体失败：", ex);
                }
            }
            foreach (var entity in _owned.Values.Select(e => e.Entity).ToArray())
            {
                // 网络资源已在上面尝试过；失败记录保留到下次清理，不在同一轮重复发包。
                if (_owned.TryGetValue(entity.Identity, out var entry) && entry.NetworkRequest != null) continue;
                try { DeleteCore(entity); }
                catch (Exception ex)
                {
                    (failures ?? (failures = new List<Exception>())).Add(ex);
                    LogCleanupError("回收实体失败：", ex);
                }
            }
            var creationFailures = RetryFailedCreationsCore();
            if (creationFailures != null)
                (failures ?? (failures = new List<Exception>())).AddRange(creationFailures);
            if (failures != null) throw new AggregateException("部分本地实体回收失败。", failures);
        }

        private void LogCleanupError(string message, Exception error)
        {
            try { _logError?.Invoke(message + error); }
            catch { /* 日志失败不能中断其余资源的清理或掩盖原始异常。 */ }
        }

        /// <summary>进程/世界已经切换时，只丢弃旧引用；不能访问上一世界的原生内存。</summary>
        public void ResetWorld()
        {
            lock (_gate)
            {
                _worldReady = false;
                _owned.Clear();
                _localCreationGeneration++;
                _failedLocalCreations.Clear();
                _networkWorldGeneration++;
                _networkSpawns.Clear();
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
                if (_disposed && _owned.Count == 0 && _networkSpawns.Count == 0 && _failedLocalCreations.Count == 0) return;
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

        private uint NextAvailableId(CharacterManager manager, GameObjectPtrs objects, bool player)
        {
            var prefix = player ? 0x10FF0000u : 0x40FF0000u;
            ushort maxIndex = 0;
            void ObserveId(uint id)
            {
                if ((id & 0xFFFF0000u) == prefix && (ushort)id > maxIndex)
                    maxIndex = (ushort)id;
            }

            foreach (var request in _networkSpawns.Values) ObserveId(request.EntityId);
            foreach (var owned in _owned.Values) ObserveId(owned.Entity.Id);
            foreach (var creation in _failedLocalCreations)
            {
                if (creation.Observation.HasValue) ObserveId(creation.Observation.Value.EntityId);
                if (creation.Entity != null) ObserveId(creation.Entity.Id);
            }

            // 一次读取全部 819 个指针；隐藏但仍在表中的 EObj 也继续占用原 ID。
            var pointers = new HashSet<IntPtr>();
            foreach (var native in objects.GameObjects)
            {
                if (!native.IsNull() && pointers.Add(native.Ptr)) ObserveId(native.EntityId);
            }
            // 本地创建不依赖网络角色管理器；存在时补查尚未进入全局表的角色。
            if (manager.Ptr != IntPtr.Zero)
            {
                for (var index = 0; index < NetworkCharacterCapacity; index++)
                {
                    var native = manager.GetBattleChara(index);
                    if (!native.IsNull() && pointers.Add(native.Ptr)) ObserveId(native.EntityId);
                }
            }
            // 接续旧插件残留的最大编号；当前进程已发出的编号也不会因实体被回收而倒退。
            lock (IdGate)
            {
                var current = player ? _playerIdIndex : _nonPlayerIdIndex;
                if (current < maxIndex) current = maxIndex;
                if (current == ushort.MaxValue)
                    throw new InvalidOperationException("模拟实体的 ID 区间已用尽，不能继续分配。");
                current++;
                if (player) _playerIdIndex = current;
                else _nonPlayerIdIndex = current;
                return prefix + current;
            }
        }
    }
}
