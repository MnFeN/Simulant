using Simulant.Core.Entity;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulant.Simulation.Runtime
{
    public sealed partial class SimEntityManager
    {
        private readonly HashSet<SimActor> _movementLockedActors = new HashSet<SimActor>();
        private SimScheduledAction _movementLockPoll;
        private Character _movementLockPlayer;
        private bool _originalMovementLock;

        private void RefreshMovementLocks()
        {
            var now = _scheduler?.ElapsedSeconds ?? 0;
            var locked = new HashSet<SimActor>();

            if (_acceptingEntities && !_isClearing)
                foreach (var status in _statuses)
                    if (status.LockMovement && now < status.ExpiresAt && status.Participant.IsActive &&
                        MatchesStatus(status.Actor.Native.StatusManager.GetStatus(status.Slot), status))
                        locked.Add(status.Participant);

            foreach (var actor in _movementLockedActors)
                if (!locked.Contains(actor))
                    actor.MovementLocked = false;

            _movementLockedActors.Clear();

            foreach (var actor in locked)
            {
                actor.MovementLocked = true;
                _movementLockedActors.Add(actor);
                _movement.StopVoluntary(actor);
            }

            var player = locked.FirstOrDefault(actor => actor.Entity.Identity.Slot == 0)?.Entity;

            if (_movementLockPlayer != null && player?.Identity != _movementLockPlayer.Identity)
                RestorePlayerMovement();

            if (player != null && _movementLockPlayer == null)
            {
                if (Conditions.InstancePtr == IntPtr.Zero || Conditions.SetFuncPtr == IntPtr.Zero)
                    throw new InvalidOperationException("本机移动限制函数尚未定位，请重新初始化。");

                _originalMovementLock = Conditions.Instance().SufferingStatusAffliction.Get();
                _movementLockPlayer = player;
                Conditions.Instance().SetMovementLocked(true);
            }

            if (locked.Count == 0)
            {
                _movementLockPoll?.Cancel();
                _movementLockPoll = null;
            }
            else if (_movementLockPoll == null)
                _movementLockPoll = _scheduler.ScheduleAfter(0.05, () =>
                {
                    lock (_gate)
                    {
                        _movementLockPoll = null;
                        RefreshMovementLocks();
                    }
                }, "更新状态造成的移动限制");
        }

        private void RestorePlayerMovement()
        {
            // 换图或退回标题后，客户端已重置 Conditions；不把旧玩家的限制恢复到新身份。
            if (_movementLockPlayer.IsAlive)
                Conditions.Instance().SetMovementLocked(_originalMovementLock);

            _movementLockPlayer = null;
        }

        private void ClearMovementLocks()
        {
            lock (_gate)
            {
                _movementLockPoll?.Cancel();
                _movementLockPoll = null;

                foreach (var actor in _movementLockedActors)
                    actor.MovementLocked = false;

                _movementLockedActors.Clear();

                if (_movementLockPlayer != null)
                    RestorePlayerMovement();
            }
        }
    }
}
