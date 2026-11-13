using Simulant.Core.Entity;
using Simulant.Core.Party;
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
        private SimPartyService _partyService;
        private SimScheduledAction _partyEnmityPoll;

        internal List<SimActor> Party { get; } = new List<SimActor>();

        // 清理期间或仍持有资源时为真；Clear 返回后可用来判断是否需要重试。
        internal bool HasPendingCleanup
        {
            get
            {
                lock (_gate)
                    return _isClearing || _owned.Count != 0 || _networkSpawns.Count != 0 || _statuses.Count != 0 || _mechanics.Count != 0 || _names.Count != 0 ||
                        _spawner.HasPendingCreationCleanup || _movement?.HasPendingCleanup == true ||
                        _partyService?.IsOwnedBy(this) == true;
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
                    if (_partyService?.IsOwnedBy(this) == true)
                        throw new InvalidOperationException("上次模拟的小队列表尚未恢复，请先重试清理。");
                    if (_statuses.Count != 0)
                        throw new InvalidOperationException("上次模拟的点名状态尚未清理，请先重试停止。");
                    if (_names.Count != 0)
                        throw new InvalidOperationException("上次模拟的玩家名字尚未恢复，请先重试停止。");
                    if (_mechanics.Count != 0)
                        throw new InvalidOperationException("上次模拟的机制尚未清理，请先重试停止。");
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

        internal void ShowParty(SimPartyService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            lock (_gate)
            {
                EnsureAcceptingEntities();
                if (_partyService != null && !ReferenceEquals(_partyService, service) && _partyService.IsOwnedBy(this))
                    throw new InvalidOperationException("本会话已借用另一个小队列表，必须先恢复。");
                _partyService = service;
                service.Show(this, Party);
            }
        }

        internal void SetPartyEnmity(SimActor target, params int[] enmity)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            lock (_gate)
            {
                EnsureAcceptingEntities();
                if (!target.IsActive || _partyService?.IsOwnedBy(this) != true)
                    throw new InvalidOperationException("请先显示模拟小队并指定有效目标。");
                _partyService.SetEnmity(this, target.Id, Party, enmity);
                SchedulePartyEnmity();
            }
        }

        private void SchedulePartyEnmity()
        {
            if (_partyEnmityPoll != null) return;
            _partyEnmityPoll = _scheduler.ScheduleAfter(0.25, () =>
            {
                lock (_gate)
                {
                    _partyEnmityPoll = null;
                    if (!_acceptingEntities || _isClearing || _partyService?.IsOwnedBy(this) != true) return;
                    _partyService.RefreshEnmity(this);
                    SchedulePartyEnmity();
                }
            }, "检查模拟目标的仇恨显示");
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
                if (!_isClearing && _mechanics.Any(mechanic => mechanic.HasFailedCleanup))
                    throw new InvalidOperationException("仍有机制尚未清理，不能提前解除角色关联，请重试停止模拟。");
                ClearStatuses(actor.Entity);
                RestoreNames(actor.Entity);
                _partyService?.RemoveMember(this, actor.Id);
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
                if (!_isClearing && _mechanics.Any(mechanic => mechanic.HasFailedCleanup))
                    throw new InvalidOperationException("仍有机制尚未清理，不能提前回收实体，请重试停止模拟。");
                ClearStatuses(entity);
                RestoreNames(entity);
                // 列表移除失败时保留实体，避免界面仍引用已回收的队员。
                _partyService?.RemoveMember(this, entity.Id);
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
                if (_mechanics.Any(mechanic => mechanic.IsCleaning))
                    throw new InvalidOperationException("不能在机制清理回调中再次清理整个会话；请在回调返回后停止。");
                networkSpawns = _networkSpawns.ToArray();
                actors = _actors.Values.ToArray();
                owned = _owned.ToArray();
                casts = _casts;
                movement = _movement;
                _isClearing = true;
                _acceptingEntities = false;
                _partyEnmityPoll?.Cancel();
                _partyEnmityPoll = null;
                _networkGeneration++;
                _networkPoll?.Cancel();
                _networkPoll = null;
                foreach (var pending in networkSpawns)
                {
                    pending.OnReady = null;
                    // 已就绪实体的回收责任已经转入 _owned。
                    if (pending.Actor != null) _networkSpawns.Remove(pending);
                }
            }
            try
            {
                foreach (var pending in networkSpawns)
                    if (pending.Actor == null && TryCleanup(() => _spawner.CancelNetworkCharacter(pending.Request)))
                        lock (_gate) _networkSpawns.Remove(pending);
                // 机制清理闭包仍可能读取小队和角色；失败时保留整批资源供下一次停止重试。
                if (!TryCleanup(ClearMechanics))
                {
                    foreach (var actor in actors)
                    {
                        TryCleanup(actor.StopCasting);
                        TryCleanup(actor.StopMoving);
                    }
                    return;
                }
                lock (_gate)
                {
                    _actors.Clear();
                    Party.Clear();
                }
                // 先停止所有参与者的行为，再回收本会话生成的实体。借用玩家只解除关联。
                foreach (var actor in actors)
                    TryCleanup(actor.Dispose);
                TryCleanup(() => ClearStatuses());
                TryCleanup(() => RestoreNames());
                var partyRestored = _partyService == null || TryCleanup(() => _partyService.Restore(this));
                if (partyRestored)
                    foreach (var entity in owned)
                    {
                        lock (_gate)
                            if (HasStatusDependency(entity) || _names.Any(name => name.Actor.Identity == entity.Identity)) continue;
                        if (TryCleanup(() => _spawner.Delete(entity)))
                            lock (_gate) _owned.Remove(entity);
                    }
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
