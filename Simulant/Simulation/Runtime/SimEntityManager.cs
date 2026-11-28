using Simulant.Core.Entity;
using Simulant.Core.Environment;
using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.UI;
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
        private SimActorBehaviorController _behaviors;
        internal Random BehaviorRandom { get; set; }
        public double SelfJudgementAdvance { get; set; }
        public SimActorBehaviorOptions PlayerBehavior { get; set; } = new SimActorBehaviorOptions();

        private bool _acceptingEntities;
        private bool _isClearing;
        private SimPartyService _partyService;
        private SimScheduledAction _partyEnmityPoll;
        private SimScheduledAction _enemyListPoll;
        private readonly List<EnemyListEntry> _enemyEntries = new List<EnemyListEntry>();
        private readonly Dictionary<SimActor, Dictionary<SimActor, float>> _partyEnmity = new Dictionary<SimActor, Dictionary<SimActor, float>>();
        private static readonly int[] DefaultPartyEnmity = { 100, 50, 10, 10, 15, 15, 15, 15 };
        internal SimEnemyList EnemyList { get; set; }

        private sealed class EnemyListEntry
        {
            internal SimActor Actor;
            internal byte Enmity = 100;
            internal bool? Visible;
        }

        private readonly Dictionary<MarkerType, uint> _simMarkers = new Dictionary<MarkerType, uint>();

        internal void Mark(MarkerType marker, uint entityId)
        {
            lock (_gate)
            {
                GameMarkers.Mark(marker, entityId);

                var source = _actors.Values.FirstOrDefault(actor => actor.Entity.Identity.Slot == 0)?.Entity;
                if (_simMarkers.TryGetValue(marker, out var previous) && previous != entityId)
                    CombatLog?.Mark(false, (int)marker, source, previous, FindLogActor(previous));

                if (entityId == SimCombatLog.NoTarget)
                    _simMarkers.Remove(marker);
                else if (previous != entityId)
                {
                    _simMarkers[marker] = entityId;
                    CombatLog?.Mark(true, (int)marker, source, entityId, FindLogActor(entityId));
                }
            }
        }

        internal SimCombatLog CombatLog { get; set; }

        private Character FindLogActor(uint id)
        {
            lock (_gate)
                return _actors.Values.FirstOrDefault(actor => actor.Id == id && actor.IsActive)?.Entity;
        }

        internal List<SimActor> Party { get; } = new List<SimActor>();

        // 清理期间或仍持有资源时为真；Clear 返回后可用来判断是否需要重试。
        internal bool HasPendingCleanup
        {
            get
            {
                lock (_gate)
                    return _isClearing || _movementLockPlayer != null || _limitBreakPlayer != null || _owned.Count != 0 || _networkSpawns.Count != 0 || _groundEffects.Count != 0 || _statuses.Count != 0 || _mechanics.Count != 0 || _names.Count != 0 ||
                        _spawner.HasPendingCreationCleanup || _movement?.HasPendingCleanup == true ||
                        _partyService?.IsOwnedBy(this) == true || EnemyList?.HasPendingCleanup == true;
            }
        }

        internal SimScheduler Scheduler
        {
            get => _scheduler;
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                lock (_gate)
                {
                    if (_isClearing)
                        throw new InvalidOperationException("正在清理本次模拟，不能更换调度器。");

                    if (ReferenceEquals(_scheduler, value))
                        return;

                    if (_spawner.HasPendingCreationCleanup)
                        throw new InvalidOperationException("宿主仍有未交付的失败创建，请先重试清理后开始模拟。");

                    if (_movement?.HasPendingCleanup == true)
                        throw new InvalidOperationException("上次模拟的移动尚未停止，请先重试清理后开始模拟。");

                    if (_partyService?.IsOwnedBy(this) == true)
                        throw new InvalidOperationException("上次模拟的小队列表尚未恢复，请先重试清理。");

                    if (EnemyList?.HasPendingCleanup == true)
                        throw new InvalidOperationException("上次模拟的敌对列表尚未恢复，请先重试清理。");

                    if (_statuses.Count != 0 || _movementLockPlayer != null)
                        throw new InvalidOperationException("上次模拟的点名状态尚未清理，请先重试停止。");

                    if (_names.Count != 0)
                        throw new InvalidOperationException("上次模拟的玩家名字尚未恢复，请先重试停止。");

                    if (_mechanics.Count != 0)
                        throw new InvalidOperationException("上次模拟的机制尚未清理，请先重试停止。");

                    if (_actors.Count != 0 || _owned.Count != 0 || _networkSpawns.Count != 0 || _groundEffects.Count != 0)
                        throw new InvalidOperationException("更换调度器前必须清理本次模拟。");

                    _casts?.Dispose();
                    _movement?.Dispose();
                    _behaviors?.Dispose();
                    _scheduler = value;
                    _networkGeneration++;
                    _simMarkers.Clear();
                    _casts = new SimCastController(value);
                    _movement = new SimMovementController(value);
                    _behaviors = new SimActorBehaviorController(value, _movement, _casts, SetSprintEffect, BehaviorRandom);
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
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            lock (_gate)
            {
                EnsureAcceptingEntities();

                if (_partyService != null && !ReferenceEquals(_partyService, service) && _partyService.IsOwnedBy(this))
                    throw new InvalidOperationException("本会话已借用另一个小队列表，必须先恢复。");

                _partyService = service;
                service.Show(this, Party);
                CombatLog?.Party(Party);
            }
        }

        internal void SetPartyEnmity(SimActor target, params int[] enmity)
            => SetPartyEnmity(target, true, enmity);

        internal void SetSecondaryBossEnmity(SimActor target)
        {
            lock (_gate)
            {
                if (Party.Count < 2)
                    throw new InvalidOperationException("设置第二名 Boss 的仇恨前需要准备模拟小队。");

                var percentages = DefaultPartyEnmity.Take(Party.Count).ToArray();
                // 双 Boss 的第二张表由 ST 担任一仇。
                percentages[0] = DefaultPartyEnmity[1];
                percentages[1] = DefaultPartyEnmity[0];
                SetPartyEnmity(target, false, percentages);
            }
        }

        internal void SetPartyEnmity(SimActor target, bool alsoSetCombatTarget, params int[] enmity)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            lock (_gate)
            {
                EnsureAcceptingEntities();

                var partyShown = _partyService?.IsOwnedBy(this) == true;
                var solo = Party.Count == 1 && Party[0]?.IsActive == true && Party[0].Entity.Identity.Slot == 0;
                if (!target.IsActive || (!partyShown && !solo))
                    throw new InvalidOperationException("请先准备模拟小队或单人练习者，并指定有效目标。");

                if (enmity?.Length != Party.Count || enmity.Any(value => value < 0 || value > 100))
                    throw new ArgumentException("仇恨值需与小队一一对应，范围为 0-100。", nameof(enmity));

                RegisterPartyEnmity(target, enmity);
                if (alsoSetCombatTarget)
                    SetCombatTarget(target);
            }
        }

        private void RegisterPartyEnmity(SimActor target, int[] percentages)
        {
            var scores = Party.Select((actor, index) => new { Actor = actor, Score = (float)percentages[index] })
                .ToDictionary(entry => entry.Actor, entry => entry.Score);
            PublishPartyEnmity(target, scores, percentages);
        }

        private void PublishPartyEnmity(SimActor target, Dictionary<SimActor, float> scores, int[] percentages)
        {
            var partyShown = _partyService?.IsOwnedBy(this) == true;
            if (partyShown)
                _partyService.SetEnmity(this, target.Id, Party, percentages);

            var entry = _enemyEntries.Find(item => item.Actor == target);
            var self = Party.FindIndex(actor => actor.Entity.Identity.Slot == 0);
            if (entry != null && self >= 0)
                entry.Enmity = (byte)percentages[self];
            _partyEnmity[target] = scores;
            _behaviors.SetBossEnmity(target, OrderedPartyEnmity(scores).ToArray());
            if (partyShown)
                SchedulePartyEnmity();
        }

        private IEnumerable<SimActor> OrderedPartyEnmity(Dictionary<SimActor, float> scores)
            => Party.Where(actor => actor.IsActive && scores.TryGetValue(actor, out var value) && value > 0)
                .OrderByDescending(actor => scores[actor]);

        internal SimActor[] GetPartyEnmityOrder(SimActor target)
        {
            lock (_gate)
                return _partyEnmity.TryGetValue(target, out var scores)
                    ? OrderedPartyEnmity(scores).Where(actor => actor.Entity.HP != 0).ToArray()
                    : Array.Empty<SimActor>();
        }

        /// <summary>设置假玩家的共同战斗目标；只允许本会话拥有的网络战斗 NPC，空值表示离开战斗。</summary>
        public void SetCombatTarget(SimActor target)
        {
            lock (_gate)
            {
                EnsureAcceptingEntities();

                if (target != null && (!target.IsActive || !_owned.Contains(target.Entity) ||
                    target.Entity.ObjectKind != Simulant.Game.FFCS.Client.Game.Object.ObjectKind.BattleNpc ||
                    target.Entity.Identity.Slot < 2 || target.Entity.Identity.Slot >= 200 || (target.Entity.Identity.Slot & 1) != 0))
                    throw new ArgumentException("战斗目标必须是本会话拥有的有效网络战斗 NPC。", nameof(target));

                if (target != null && Party.Count > 1 && _partyService?.IsOwnedBy(this) == true && !_partyEnmity.ContainsKey(target))
                    RegisterPartyEnmity(target, DefaultPartyEnmity.Take(Party.Count).ToArray());
                _behaviors.SetTarget(target);
                if (target == null)
                    StopLimitBreakCharge();
                else
                    QueueLimitBreakCharge();
            }
        }

        private void SchedulePartyEnmity()
        {
            if (_partyEnmityPoll != null)
                return;

            _partyEnmityPoll = _scheduler.ScheduleAfter(0.25, () =>
            {
                lock (_gate)
                {
                    _partyEnmityPoll = null;

                    if (!_acceptingEntities || _isClearing || _partyService?.IsOwnedBy(this) != true)
                        return;

                    _partyService.RefreshEnmity(this);
                    SchedulePartyEnmity();
                }
            }, "检查模拟目标的仇恨显示");
        }

        internal void StartEnemyListLettersAt(char letter) => EnemyList?.StartLettersAt(letter);

        /// <summary>设置本会话生成的敌人是否入列。目标可选中状态仍由原生实体控制。</summary>
        public void SetEnemyListVisibility(SimActor actor, bool visible)
        {
            lock (_gate)
            {
                EnsureAcceptingEntities();
                var entry = _enemyEntries.Find(item => item.Actor == actor);
                if (entry != null)
                    entry.Visible = visible;
            }
        }

        private void RegisterEnemy(SimActor actor, bool? visible = null)
        {
            if (EnemyList == null)
                return;

            _enemyEntries.Add(new EnemyListEntry { Actor = actor, Visible = visible });
            ScheduleEnemyList();
        }

        internal bool OwnsHostileTarget(Character target)
        {
            lock (_gate)
                return _acceptingEntities && !_isClearing &&
                    _enemyEntries.Any(entry => entry.Actor.IsActive && entry.Actor.Entity.Identity == target.Identity);
        }

        private void ScheduleEnemyList()
        {
            if (_enemyListPoll != null || (_enemyEntries.Count == 0 && !EnemyList.HasPendingCleanup))
                return;

            _enemyListPoll = _scheduler.ScheduleAfter(0.25, () =>
            {
                lock (_gate)
                {
                    _enemyListPoll = null;
                    if (!_acceptingEntities || _isClearing)
                        return;

                    var self = _actors.Values.FirstOrDefault(actor => actor.Entity.Identity.Slot == 0 && actor.IsActive);
                    if (self != null)
                    {
                        var enemies = new List<(uint EntityId, byte Enmity)>();
                        foreach (var entry in _enemyEntries)
                        {
                            var actor = entry.Actor;
                            if (!actor.IsActive || actor.Entity.HP == 0)
                                continue;

                            // 原生 HUD 独立判断可选中状态与行的亮度。辅助施法者的读条也不会使其入列。
                            if (entry.Visible == false || (entry.Visible == null && !actor.Entity.IsTargetable))
                                continue;

                            entry.Visible = true;
                            enemies.Add((actor.Id, entry.Enmity));
                        }

                        EnemyList.Refresh(self.Id, enemies);
                    }

                    ScheduleEnemyList();
                }
            }, "同步模拟敌对列表");
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
            => SpawnBNpc(new CharacterSpawnSpec
            {
                BNpcBaseId = baseId,
                BNpcNameId = nameId,
                Level = level
            });

        public SimActor SpawnBNpc(CharacterSpawnSpec spec)
        {
            lock (_gate)
            {
                EnsureAcceptingEntities();
                var actor = Own(_spawner.SpawnBNpc(spec));
                RegisterEnemy(actor);
                return actor;
            }
        }

        private SimActor Own(Character entity)
        {
            lock (_gate)
            {
                _owned.Add(entity);
                var actor = Attach(entity);
                var slot = entity.Identity.Slot;

                if (slot >= 2 && slot < 200 && (slot & 1) == 0)
                {
                    if (entity.ObjectKind == Simulant.Game.FFCS.Client.Game.Object.ObjectKind.Pc)
                        _behaviors.Add(actor, PlayerBehavior);
                    else if (entity.ObjectKind == Simulant.Game.FFCS.Client.Game.Object.ObjectKind.BattleNpc)
                        actor.BossBehaviorController = _behaviors;
                }

                return actor;
            }
        }

        public EventObject SpawnEObj(ushort index, uint baseId, Vector3 pos, float heading = 0)
            => SpawnEObj(new EventObjectSpawnSpec
            {
                Index = checked((byte)index),
                BaseId = baseId,
                Pos = pos,
                Heading = heading
            });

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

        internal void PlayEObjAnimation(EventObject entity, ushort sharedTimelineState, ushort bitMask)
        {
            const ActorControlCategory category = ActorControlCategory.EObjAnimation;
            entity.ActorControl(category, sharedTimelineState, bitMask);
            CombatLog?.ActorControl(entity.Id, category, sharedTimelineState, bitMask, 0, 0);
        }

        public SimActor Attach(Character entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (!entity.IsAlive)
                throw new InvalidOperationException("不能关联已经失效的实体。");

            lock (_gate)
            {
                EnsureAcceptingEntities();

                if (_actors.TryGetValue(entity.Identity, out var actor) && actor.IsActive)
                    return actor;

                actor = new SimActor(entity, _casts, _movement)
                {
                    Manager = this,
                    CombatLog = CombatLog,
                    FindLogActor = FindLogActor
                };
                _actors[entity.Identity] = actor;
                if (entity.Identity.Slot == 0 && SelfJudgementAdvance > 0)
                    actor.StartJudgementSampling(_scheduler, SelfJudgementAdvance);
                return actor;
            }
        }

        public void Detach(SimActor actor)
        {
            if (actor == null)
                return;

            lock (_gate)
            {
                if (!_actors.TryGetValue(actor.Entity.Identity, out var current) || !ReferenceEquals(current, actor))
                    return;

                if (!_isClearing && _mechanics.Any(mechanic => mechanic.HasFailedCleanup))
                    throw new InvalidOperationException("仍有机制尚未清理，不能提前解除角色关联，请重试停止模拟。");

                ClearStatuses(actor.Entity);
                RestoreNames(actor.Entity);
                _partyService?.RemoveMember(this, actor.Id);

                try
                {
                    actor.Dispose();
                }
                finally
                {
                    _actors.Remove(actor.Entity.Identity);
                    _enemyEntries.RemoveAll(entry => entry.Actor == actor);
                    _partyEnmity.Remove(actor);
                    foreach (var scores in _partyEnmity.Values)
                        scores.Remove(actor);
                    if (Party.Remove(actor) && _partyService?.IsOwnedBy(this) == true)
                        CombatLog?.Party(Party);
                }
            }
        }

        public void Delete(SimActor actor)
        {
            if (actor == null)
                return;

            Delete(actor.Entity);
        }

        public void Delete(EntityBase entity)
        {
            if (entity == null)
                return;

            lock (_gate)
            {
                // 延迟回收可能发生在 Clear 之后；不再拥有就不接触原生对象。
                if (!_owned.Contains(entity))
                    return;

                if (!_isClearing && _mechanics.Any(mechanic => mechanic.HasFailedCleanup))
                    throw new InvalidOperationException("仍有机制尚未清理，不能提前回收实体，请重试停止模拟。");

                ClearStatuses(entity);
                RestoreNames(entity);
                // 列表移除失败时保留实体，避免界面仍引用已回收的队员。
                _partyService?.RemoveMember(this, entity.Id);

                try
                {
                    if (_actors.TryGetValue(entity.Identity, out var actor))
                        Detach(actor);
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
                if (_isClearing)
                    return;

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
                _enemyListPoll?.Cancel();
                _enemyListPoll = null;
                _networkGeneration++;
                _networkPoll?.Cancel();
                _networkPoll = null;

                foreach (var pending in networkSpawns)
                {
                    pending.OnReady = null;

                    // 已就绪实体的回收责任已经转入 _owned。
                    if (pending.Actor != null)
                        _networkSpawns.Remove(pending);
                }
            }

            try
            {
                TryCleanup(ClearMovementLocks);
                var limitBreakCleared = TryCleanup(ClearLimitBreakGauge);
                var groundEffectsCleared = ClearGroundEffects();
                if (!limitBreakCleared || !groundEffectsCleared)
                    return;
                _behaviors?.Dispose();

                foreach (var pending in networkSpawns)
                    if (pending.Actor == null && TryCleanup(() => _spawner.CancelNetworkCharacter(pending.Request)))
                        lock (_gate)
                            _networkSpawns.Remove(pending);

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
                    _enemyEntries.Clear();
                    _partyEnmity.Clear();
                }

                // 先停止所有参与者的行为，再回收本会话生成的实体。借用玩家只解除关联。
                foreach (var actor in actors)
                    TryCleanup(actor.Dispose);

                TryCleanup(() => ClearStatuses());
                TryCleanup(() => RestoreNames());
                var partyRestored = _partyService == null || TryCleanup(() => _partyService.Restore(this));
                var enemyListRestored = EnemyList == null || TryCleanup(EnemyList.Restore);

                if (partyRestored && enemyListRestored)
                    foreach (var entity in owned)
                    {
                        lock (_gate)
                            if (HasStatusDependency(entity) || _names.Any(name => name.Actor.Identity == entity.Identity))
                                continue;

                        if (TryCleanup(() => _spawner.Delete(entity)))
                            lock (_gate)
                                _owned.Remove(entity);
                    }

                if (casts != null)
                    TryCleanup(casts.Dispose);

                if (movement != null)
                    TryCleanup(movement.Dispose);

                // 仅接管共享生成器中从未交付的失败分配，不触及其他会话已拥有的实体。
                TryCleanup(_spawner.RetryFailedCreations);
            }
            finally
            {
                lock (_gate)
                    _isClearing = false;
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
                try
                {
                    _onCleanupError?.Invoke(ex);
                }
                catch
                { /* 日志失败也不能中断其余资源的清理。 */
                }

                return false;
            }
        }

        private void EnsureAcceptingEntities()
        {
            if (!_acceptingEntities)
                throw new InvalidOperationException("尚未开始模拟，或模拟已停止。");
        }

        internal SimActor GetPartyMember(int partyIndex)
        {
            if (partyIndex < 1 || partyIndex > Party.Count)
                throw new ArgumentOutOfRangeException(nameof(partyIndex), $"小队成员索引必须在范围内（1-{Party.Count}）。");

            return Party[partyIndex - 1];
        }
    }
}
