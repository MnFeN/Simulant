using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Simulation.Runtime
{
    public enum EnmityFollowMode
    {
        Disabled,
        FacingOnly,
        MoveAndFace
    }

    internal sealed partial class SimActorBehaviorController
    {
        private sealed class BossState
        {
            internal EnmityFollowMode Mode;
            internal SimActor[] EnmityOrder = Array.Empty<SimActor>();
            internal SimActor Target;
            internal double SuspendedUntil;
        }

        private const byte BossRunSpeed = 90;
        private const float BossStopOffset = 2.5f;
        private readonly Dictionary<SimActor, BossState> _bossStates = new Dictionary<SimActor, BossState>();

        private BossState GetBossState(SimActor boss)
        {
            if (!_bossStates.TryGetValue(boss, out var state))
            {
                state = new BossState();
                _bossStates.Add(boss, state);
            }

            return state;
        }

        internal EnmityFollowMode GetEnmityFollowMode(SimActor boss)
        {
            lock (_gate)
                return _bossStates.TryGetValue(boss, out var state) ? state.Mode : EnmityFollowMode.Disabled;
        }

        internal void SetEnmityFollowMode(SimActor boss, EnmityFollowMode mode)
        {
            if (!Enum.IsDefined(typeof(EnmityFollowMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));

            lock (_gate)
            {
                if (_disposed)
                    return;

                var state = GetBossState(boss);
                if (state.Mode == mode)
                    return;

                if (mode != EnmityFollowMode.MoveAndFace)
                    _movement.StopEnmityPursuit(boss);

                state.Mode = mode;
                if (mode == EnmityFollowMode.Disabled)
                    state.Target = null;
                else
                    QueueTick(immediate: true);
            }
        }

        internal void SetBossEnmity(SimActor boss, IReadOnlyList<SimActor> party, IReadOnlyList<int> enmity)
        {
            lock (_gate)
            {
                if (_disposed)
                    return;

                var state = GetBossState(boss);
                state.EnmityOrder = party.Zip(enmity, (actor, value) => (Actor: actor, Value: value))
                    .Where(entry => entry.Value > 0).OrderByDescending(entry => entry.Value).Select(entry => entry.Actor).ToArray();

                if (state.Mode != EnmityFollowMode.Disabled)
                    QueueTick(immediate: true);
            }
        }

        internal void RemoveBoss(SimActor boss)
        {
            lock (_gate)
                _bossStates.Remove(boss);
        }

        internal void SuspendBoss(SimActor boss, double seconds)
        {
            lock (_gate)
                if (_bossStates.TryGetValue(boss, out var state))
                    state.SuspendedUntil = Math.Max(state.SuspendedUntil, _scheduler.ElapsedSeconds + seconds);
        }

        private void UpdateBossFollow(SimActor boss, BossState state, double now)
        {
            if (state.Mode == EnmityFollowMode.Disabled || !boss.IsActive)
                return;

            if (boss.MovementLocked || now < state.SuspendedUntil || boss.Entity.HP == 0 ||
                boss.Entity.CastingActionId != 0 || !boss.Entity.IsTargetable || !_movement.CanPursueEnmity(boss))
            {
                _movement.StopEnmityPursuit(boss);
                return;
            }

            var target = state.EnmityOrder.FirstOrDefault(actor => actor.IsActive && actor.Entity.HP > 0);
            if (state.Target != target)
            {
                boss.Entity.SetNetworkTarget(target?.Entity);
                state.Target = target;
            }

            if (target == null)
            {
                _movement.StopEnmityPursuit(boss);
                return;
            }

            var position = boss.Pos;
            var direction = target.Pos - position;
            var distance = direction.Length();
            var stopDistance = boss.Entity.HitboxRadius + BossStopOffset;
            if (state.Mode == EnmityFollowMode.MoveAndFace && distance > stopDistance + SimMovementController.NetworkArrivalTolerance)
            {
                var destination = position + direction * ((distance - stopDistance) / distance);
                _movement.PursueEnmity(boss, destination, BossRunSpeed);
                return;
            }

            _movement.StopEnmityPursuit(boss);
            FaceDirection(boss, direction, BossRunSpeed);
        }
    }
}
