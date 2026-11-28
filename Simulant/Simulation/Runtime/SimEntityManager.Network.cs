using Simulant.Core.Entity;
using System.Linq;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Simulation.Runtime
{
    public sealed partial class SimEntityManager
    {
        private delegate void BeginNetworkSpawn(out NetworkCharacterSpawnRequest request);

        private sealed class PendingNetworkSpawn
        {
            internal NetworkCharacterSpawnRequest Request;
            internal Action<SimActor> OnReady;
            internal double Deadline;
            internal SimActor Actor;
            internal Action<SimActor> OnSpawned;
        }

        private readonly List<PendingNetworkSpawn> _networkSpawns = new List<PendingNetworkSpawn>();
        private SimScheduledAction _networkPoll;
        private long _networkGeneration;

        /// <summary>
        /// 通过网络包生成战斗实体。实体进入战斗列表后，在本会话调度线程执行一次回调。
        /// 停止会话会取消尚未领取的回调和原生生成队列；已领取的回调可以完成。
        /// </summary>
        public void SpawnNetworkBNpc(NetworkNpcSpawnSpec spec, Action<SimActor> onReady, double timeoutSeconds = 5)
        {
            if (spec == null)
                throw new ArgumentNullException(nameof(spec));

            // OverlayPlugin 的 272 行读取生成包 0x58、0x7C 和 0x95；这里对应连接对端、连线类型和动画状态。
            var parent = spec.TetherTargetId;
            var tether = spec.TetherId;
            var animation = spec.AnimationState;
            var enemyListVisibility = spec.EnemyListVisibility;
            QueueNetworkSpawn((out NetworkCharacterSpawnRequest request) => _spawner.BeginSpawnNetworkBNpc(spec, out request),
                onReady, timeoutSeconds, actor =>
                {
                    RegisterEnemy(actor, enemyListVisibility);
                    CombatLog?.SpawnNpc(actor.Id, parent, tether, animation);
                });
        }

        /// <summary>生成归属指定施法者的网络宠物。宠物持有独立实体 ID，不加入小队或敌对列表。</summary>
        internal void SpawnPet(Character owner, NetworkNpcSpawnSpec spec, Action<SimActor> onReady, double duration = 0)
        {
            SimTimeline.ValidateSeconds(duration, nameof(duration));
            QueueNetworkSpawn((out NetworkCharacterSpawnRequest request) => _spawner.BeginSpawnNetworkBNpc(spec, out request, owner.Id),
                actor =>
                {
                    if (!owner.IsAlive)
                    {
                        Delete(actor);
                        return;
                    }

                    if (duration > 0)
                        _scheduler.ScheduleAfter(duration, () => Delete(actor), "回收召唤物");
                    onReady?.Invoke(actor);
                }, timeoutSeconds: 5, onSpawned: actor =>
                {
                    actor.PetOwner = owner.Identity;
                    actor.BossBehaviorController = null;
                    actor.Entity.Native.IsHostile.Set(false);
                    CombatLog?.SpawnNpc(actor.Id, spec.TetherTargetId, spec.TetherId, spec.AnimationState);
                });
        }

        internal SimActor[] GetPets(Character owner)
        {
            lock (_gate)
                return _actors.Values.Where(actor => actor.IsActive && actor.PetOwner == owner.Identity).ToArray();
        }

        /// <summary>生成网络玩家，默认复制自身外貌并优先采用已知的职业装备；就绪后在调度线程交付。</summary>
        public void SpawnNetworkPlayer(Job job, Action<SimActor> onReady, double timeoutSeconds = 5)
            => SpawnNetworkPlayer(new NetworkPlayerSpawnSpec
            {
                Job = job
            }, onReady, timeoutSeconds);

        public void SpawnNetworkPlayer(NetworkPlayerSpawnSpec spec, Action<SimActor> onReady, double timeoutSeconds = 5)
        {
            if (spec == null)
                throw new ArgumentNullException(nameof(spec));

            QueueNetworkSpawn((out NetworkCharacterSpawnRequest request) => _spawner.BeginSpawnNetworkPlayer(spec, out request),
                onReady, timeoutSeconds);
        }

        /// <summary>先预留整队槽位和实际 ID，在发送第一个生成包之前交付名单。</summary>
        internal void SpawnNetworkParty(IReadOnlyList<NetworkPlayerSpawnSpec> specs, Action<uint[]> beforeDispatch,
            Action<int, SimActor> onReady)
        {
            lock (_gate)
            {
                EnsureAcceptingEntities();
                const double timeoutSeconds = 5;
                var ids = new uint[specs.Count];
                var prepared = new List<PendingNetworkSpawn>(specs.Count);

                try
                {
                    for (var i = 0; i < specs.Count; i++)
                    {
                        var spec = specs[i];
                        if (spec == null)
                            continue;

                        var slot = i;
                        var pending = QueueNetworkSpawn(
                            (out NetworkCharacterSpawnRequest request) => _spawner.BeginSpawnNetworkPlayer(spec, out request, deferDispatch: true),
                            actor => onReady(slot, actor), timeoutSeconds, deferDispatch: true);
                        prepared.Add(pending);
                        ids[slot] = pending.Request.EntityId;
                    }

                    beforeDispatch(ids);
                    foreach (var pending in prepared)
                    {
                        pending.Deadline = _scheduler.ElapsedSeconds + timeoutSeconds;
                        _spawner.DispatchNetworkCharacter(pending.Request);
                    }
                    ScheduleNetworkPoll();
                }
                catch
                {
                    foreach (var pending in prepared)
                    {
                        pending.OnReady = null;
                        if (TryCleanup(() => _spawner.CancelNetworkCharacter(pending.Request)))
                            _networkSpawns.Remove(pending);
                        else
                        {
                            // 发包可能部分成功，保留失败的请求供会话清理，禁止继续交付。
                            _acceptingEntities = false;
                            _networkPoll?.Cancel();
                            _networkPoll = null;
                        }
                    }
                    throw;
                }
            }
        }

        private PendingNetworkSpawn QueueNetworkSpawn(BeginNetworkSpawn begin, Action<SimActor> onReady, double timeoutSeconds,
            Action<SimActor> onSpawned = null, bool deferDispatch = false)
        {
            if (onReady == null)
                throw new ArgumentNullException(nameof(onReady));

            foreach (var callback in onReady.GetInvocationList())
                if (callback.Method.IsDefined(typeof(AsyncStateMachineAttribute), false))
                    throw new ArgumentException("生成完成回调必须同步执行；延迟操作请另排时间轴节点。", nameof(onReady));

            if (double.IsNaN(timeoutSeconds) || double.IsInfinity(timeoutSeconds) || timeoutSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "等待生成的超时必须是有限正数。");

            lock (_gate)
            {
                EnsureAcceptingEntities();
                var deadline = _scheduler.ElapsedSeconds + timeoutSeconds;

                if (double.IsInfinity(deadline))
                    throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "等待生成的截止时间超出范围。");

                var pending = new PendingNetworkSpawn
                {
                    OnReady = onReady,
                    OnSpawned = onSpawned,
                    Deadline = deadline
                };

                try
                {
                    begin(out pending.Request);
                }
                catch
                {
                    if (pending.Request != null && !pending.Request.Released)
                    {
                        // 发包内部回滚失败时仍保留原请求，只允许清理，不再轮询或交付结果。
                        pending.OnReady = null;
                        _networkSpawns.Add(pending);
                        _acceptingEntities = false;
                        _networkPoll?.Cancel();
                        _networkPoll = null;
                    }

                    throw;
                }

                _networkSpawns.Add(pending);
                if (deferDispatch)
                    return pending;

                try
                {
                    ScheduleNetworkPoll();
                }
                catch (Exception scheduleError)
                {
                    _acceptingEntities = false;
                    pending.OnReady = null;

                    try
                    {
                        _spawner.CancelNetworkCharacter(pending.Request);
                        _networkSpawns.Remove(pending);
                    }
                    catch (Exception cleanupError)
                    {
                        throw new AggregateException("等待生成节点入队失败，取消请求也失败；已保留请求供清理重试。",
                            scheduleError, cleanupError);
                    }

                    throw;
                }

                return pending;
            }
        }

        private void ScheduleNetworkPoll()
        {
            if (_networkPoll != null && _networkPoll.IsCompleted)
                _networkPoll = null;

            if (_networkPoll != null || _networkSpawns.Count == 0 || !_acceptingEntities)
                return;

            var generation = _networkGeneration;
            _networkPoll = _scheduler.ScheduleAfter(0.016, () => PollNetworkSpawns(generation), "等待网络实体就绪");
        }

        private void PollNetworkSpawns(long generation)
        {
            List<PendingNetworkSpawn> ready = null;

            lock (_gate)
            {
                if (!_acceptingEntities || generation != _networkGeneration)
                    return;

                _networkPoll = null;

                foreach (var pending in _networkSpawns)
                {
                    if (pending.Actor != null)
                        continue;

                    var entity = _spawner.TryCompleteNetworkCharacter(pending.Request);

                    if (entity != null)
                    {
                        pending.Actor = Own(entity);
                        pending.OnSpawned?.Invoke(pending.Actor);
                        CombatLog?.Targetable(entity.Id, entity, pending.Request.Targetable ? 1u : 0u);

                        if (ready == null)
                            ready = new List<PendingNetworkSpawn>();

                        ready.Add(pending);
                    }
                    else if (_scheduler.ElapsedSeconds >= pending.Deadline)
                    {
                        // 保留请求记录，异常处理停止会话时仍会重试取消原生队列。
                        _spawner.CancelNetworkCharacter(pending.Request);
                        throw new TimeoutException($"网络实体 {pending.Request.EntityId:X8} 未在期限内进入战斗列表。");
                    }
                }

                ScheduleNetworkPoll();
            }

            if (ready == null)
                return;

            foreach (var pending in ready)
            {
                Action<SimActor> callback;

                lock (_gate)
                {
                    if (!_acceptingEntities || !_scheduler.IsRunning || generation != _networkGeneration ||
                        !_networkSpawns.Remove(pending))
                        continue;

                    // 这里领取回调；调用方可以在回调中清理会话，取消其他尚未领取的结果。
                    callback = pending.OnReady;
                    pending.OnReady = null;

                    if (_networkSpawns.Count == 0)
                    {
                        _networkPoll?.Cancel();
                        _networkPoll = null;
                    }
                }

                callback(pending.Actor);
            }
        }
    }
}
