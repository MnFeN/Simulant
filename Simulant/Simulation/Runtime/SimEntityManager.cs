using Simulant.Core.Entity;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Simulation.Runtime
{
    /// <summary>本会话的参与者及创建记录。Attach 只借用，只有 Spawn 才取得回收责任。</summary>
    public sealed partial class SimEntityManager
    {
        private readonly EntitySpawner _spawner;
        private readonly Action<Exception> _onCleanupError;
        private readonly object _gate = new object();
        private readonly Dictionary<EntityIdentity, SimActor> _actors = new Dictionary<EntityIdentity, SimActor>();
        private readonly HashSet<EntityBase> _owned = new HashSet<EntityBase>();
        private SimScheduler _scheduler;
        private SimCastController _casts;
        private SimMovementController _movement;
        private bool _acceptingEntities;
        private bool _isClearing;

        internal List<SimActor> Party { get; } = new List<SimActor>();

        // 清理期间或仍持有资源时为真；Clear 返回后可用来判断是否需要重试。
        internal bool HasPendingCleanup
        {
            get
            {
                lock (_gate)
                    return _isClearing || _owned.Count != 0 || _networkSpawns.Count != 0 ||
                        _spawner.HasPendingCreationCleanup || _movement?.HasPendingCleanup == true;
            }
        }

        internal SimScheduler Scheduler
        {
            get => _scheduler;
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                lock (_gate)
                {
                    if (_isClearing)
                        throw new InvalidOperationException("正在清理本次模拟，不能更换调度器。");
                    if (ReferenceEquals(_scheduler, value)) return;
                    if (_spawner.HasPendingCreationCleanup)
                        throw new InvalidOperationException("宿主仍有未交付的失败创建，请先重试清理后开始模拟。");
                    if (_movement?.HasPendingCleanup == true)
                        throw new InvalidOperationException("上次模拟的移动尚未停止，请先重试清理后开始模拟。");
                    if (_actors.Count != 0 || _owned.Count != 0 || _networkSpawns.Count != 0)
                        throw new InvalidOperationException("更换调度器前必须清理本次模拟。");
                    _casts?.Dispose();
                    _movement?.Dispose();
                    _scheduler = value;
                    _networkGeneration++;
                    _casts = new SimCastController(value);
                    _movement = new SimMovementController(value);
                    _acceptingEntities = true;
                }
            }
        }

        public SimEntityManager(EntitySpawner spawner, Action<Exception> onCleanupError = null)
        {
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _onCleanupError = onCleanupError;
        }

        public SimActor SpawnPlayer(Job job)
        {
            lock (_gate)
            {
                EnsureAcceptingEntities();
                return Own(_spawner.SpawnPlayer(job));
            }
        }

        public SimActor SpawnBNpc(uint baseId, uint nameId = 0, byte level = 0)
            => SpawnBNpc(new CharacterSpawnSpec { BNpcBaseId = baseId, BNpcNameId = nameId, Level = level });

        public SimActor SpawnBNpc(CharacterSpawnSpec spec)
        {
            lock (_gate)
            {
                EnsureAcceptingEntities();
                return Own(_spawner.SpawnBNpc(spec));
            }
        }

        private SimActor Own(Character entity)
        {
            lock (_gate)
            {
                _owned.Add(entity);
                return Attach(entity);
            }
        }

        public EventObject SpawnEObj(ushort index, uint baseId, Vector3 pos, float heading = 0)
            => SpawnEObj(new EventObjectSpawnSpec { Index = checked((byte)index), BaseId = baseId, Pos = pos, Heading = heading });

        public EventObject SpawnEObj(EventObjectSpawnSpec spec)
        {
            lock (_gate)
            {
                EnsureAcceptingEntities();
                var entity = _spawner.SpawnEObj(spec);
                _owned.Add(entity);
                return entity;
            }
        }

        public SimActor Attach(Character entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (!entity.IsAlive) throw new InvalidOperationException("不能关联已经失效的实体。");
            lock (_gate)
            {
                EnsureAcceptingEntities();
                if (_actors.TryGetValue(entity.Identity, out var actor) && actor.IsActive)
                    return actor;
                actor = new SimActor(entity, _casts, _movement);
                _actors[entity.Identity] = actor;
                return actor;
            }
        }

        public void Detach(SimActor actor)
        {
            if (actor == null) return;
            lock (_gate)
            {
                if (!_actors.TryGetValue(actor.Entity.Identity, out var current) || !ReferenceEquals(current, actor))
                    return;
                try { actor.Dispose(); }
                finally
                {
                    _actors.Remove(actor.Entity.Identity);
                    Party.Remove(actor);
                }
            }
        }

        public void Delete(SimActor actor)
        {
            if (actor == null) return;
            Delete(actor.Entity);
        }

        public void Delete(EntityBase entity)
        {
            if (entity == null) return;
            lock (_gate)
            {
                // 延迟回收可能发生在 Clear 之后；不再拥有就不接触原生对象。
                if (!_owned.Contains(entity)) return;
                try
                {
                    if (_actors.TryGetValue(entity.Identity, out var actor)) Detach(actor);
                }
                finally
                {
                    _spawner.Delete(entity);
                    _owned.Remove(entity);
                }
            }
        }

        public void Clear()
        {
            SimActor[] actors;
            EntityBase[] owned;
            PendingNetworkSpawn[] networkSpawns;
            SimCastController casts;
            SimMovementController movement;
            lock (_gate)
            {
                // 回调或日志可能重入清理；由正在执行的这一轮继续持有回收责任。
                if (_isClearing) return;
                networkSpawns = _networkSpawns.ToArray();
                actors = _actors.Values.ToArray();
                owned = _owned.ToArray();
                casts = _casts;
                movement = _movement;
                _isClearing = true;
                _acceptingEntities = false;
                _networkGeneration++;
                _networkPoll?.Cancel();
                _networkPoll = null;
                foreach (var pending in networkSpawns)
                {
                    pending.OnReady = null;
                    // 已就绪实体的回收责任已经转入 _owned。
                    if (pending.Actor != null) _networkSpawns.Remove(pending);
                }
                _actors.Clear();
                Party.Clear();
            }
            try
            {
                foreach (var pending in networkSpawns)
                    if (pending.Actor == null && TryCleanup(() => _spawner.CancelNetworkCharacter(pending.Request)))
                        lock (_gate) _networkSpawns.Remove(pending);
                // 先停止所有参与者的行为，再回收本会话生成的实体。借用玩家只解除关联。
                foreach (var actor in actors)
                    TryCleanup(actor.Dispose);
                foreach (var entity in owned)
                    if (TryCleanup(() => _spawner.Delete(entity)))
                        lock (_gate) _owned.Remove(entity);
                if (casts != null) TryCleanup(casts.Dispose);
                if (movement != null) TryCleanup(movement.Dispose);
                // 仅接管共享生成器中从未交付的失败分配，不触及其他会话已拥有的实体。
                TryCleanup(_spawner.RetryFailedCreations);
            }
            finally
            {
                lock (_gate) _isClearing = false;
            }
        }

        private bool TryCleanup(Action cleanup)
        {
            try
            {
                cleanup();
                return true;
            }
            catch (Exception ex)
            {
                try { _onCleanupError?.Invoke(ex); }
                catch { /* 日志失败也不能中断其余资源的清理。 */ }
                return false;
            }
        }

        private void EnsureAcceptingEntities()
        {
            if (!_acceptingEntities) throw new InvalidOperationException("尚未开始模拟，或模拟已停止。");
        }

        internal SimActor GetPartyMember(int partyIndex)
        {
            if (partyIndex < 1 || partyIndex > Party.Count)
                throw new ArgumentOutOfRangeException(nameof(partyIndex), $"小队成员索引必须在范围内（1-{Party.Count}）。");
            return Party[partyIndex - 1];
        }
    }
}
