using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Simulant.Simulation.Runtime
{
    /// <summary>通过一个调度节点更新所有移动中的角色。没有角色移动时不安排更新。</summary>
    internal sealed class SimMovementController : IDisposable
    {
        private sealed class Movement
        {
            public SimActor Actor;
            public Vector2 Target;
            public float Speed;
            public bool FaceMovement;
            public double UpdatedAt;
        }

        private const double TickIntervalSeconds = 0.016;
        private readonly object _gate = new object();
        private readonly SimScheduler _scheduler;
        private readonly Action _onTick;
        private readonly List<Movement> _moving = new List<Movement>();
        private SimScheduledAction _tick;
        private bool _disposed;

        public SimMovementController(SimScheduler scheduler)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _onTick = Tick;
        }

        public void MoveTo(SimActor actor, Vector2 target, float speed = 6, bool faceMovement = true)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            ValidatePosition(target, nameof(target));
            ValidateSpeed(speed);
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(SimMovementController));
                var index = Find(actor);
                if (!actor.IsActive)
                {
                    if (index >= 0) _moving.RemoveAt(index);
                    CancelIdleTick();
                    return;
                }
                var movement = index >= 0 ? _moving[index] : new Movement { Actor = actor };
                movement.Target = target;
                movement.Speed = speed;
                movement.FaceMovement = faceMovement;
                movement.UpdatedAt = _scheduler.ElapsedSeconds;
                if (index < 0) _moving.Add(movement);
                try { QueueTick(); }
                catch
                {
                    _moving.Remove(movement);
                    throw;
                }
            }
        }

        public bool IsMoving(SimActor actor)
        {
            if (actor == null) return false;
            lock (_gate) return !_disposed && actor.IsActive && Find(actor) >= 0;
        }

        public void Stop(SimActor actor)
        {
            if (actor == null) return;
            lock (_gate)
            {
                var index = Find(actor);
                if (index >= 0) _moving.RemoveAt(index);
                CancelIdleTick();
            }
        }

        private void Tick()
        {
            lock (_gate)
            {
                _tick = null;
                if (_disposed) return;
                var now = _scheduler.ElapsedSeconds;
                for (var i = _moving.Count - 1; i >= 0; --i)
                {
                    var movement = _moving[i];
                    if (!movement.Actor.IsActive)
                    {
                        _moving.RemoveAt(i);
                        continue;
                    }
                    try
                    {
                        var entity = movement.Actor.Entity;
                        var current = entity.Pos;
                        var next = Advance(current, movement.Target, movement.Speed,
                            Math.Max(0, now - movement.UpdatedAt), out var arrived);
                        movement.UpdatedAt = now;
                        if (next != current)
                        {
                            if (movement.FaceMovement)
                                entity.Heading = (float)Math.Atan2((double)movement.Target.X - current.X,
                                    (double)movement.Target.Y - current.Y);
                            entity.Pos = next;
                        }
                        if (arrived) _moving.RemoveAt(i);
                    }
                    catch (InvalidOperationException) when (!movement.Actor.IsActive)
                    {
                        // 首次检查有效性后、执行原生操作前，实体仍可能消失。
                        _moving.RemoveAt(i);
                    }
                }
                // 以实际时间为基准：更新迟到时按真实经过的时长推进，不补排追赶帧。
                if (_moving.Count != 0) QueueTick();
            }
        }

        internal static Vector2 Advance(Vector2 current, Vector2 target, float speed, double elapsed, out bool arrived)
        {
            ValidatePosition(current, nameof(current));
            ValidatePosition(target, nameof(target));
            ValidateSpeed(speed);
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsed));
            // 使用双精度，避免两个有限的单精度坐标相减时溢出。
            var dx = (double)target.X - current.X;
            var dy = (double)target.Y - current.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            var step = speed * elapsed;
            arrived = step >= distance;
            if (arrived) return target;
            var fraction = step / distance;
            return new Vector2((float)(current.X + dx * fraction), (float)(current.Y + dy * fraction));
        }

        private static void ValidatePosition(Vector2 position, string name)
        {
            if (float.IsNaN(position.X) || float.IsInfinity(position.X) ||
                float.IsNaN(position.Y) || float.IsInfinity(position.Y))
                throw new ArgumentOutOfRangeException(name, "位置的各坐标分量必须为有限数。");
        }

        private static void ValidateSpeed(float speed)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0)
                throw new ArgumentOutOfRangeException(nameof(speed), "速度必须为有限的正数。");
        }

        private int Find(SimActor actor)
        {
            for (var i = 0; i < _moving.Count; ++i)
                if (ReferenceEquals(_moving[i].Actor, actor)) return i;
            return -1;
        }

        private void QueueTick()
        {
            if (_tick == null) _tick = _scheduler.ScheduleAfter(TickIntervalSeconds, _onTick, "更新实体移动");
        }

        private void CancelIdleTick()
        {
            if (_moving.Count != 0) return;
            // 已取出的回调可能正在等待此锁，保留其句柄直到回调进入 Tick，
            // 防止并发调用 MoveTo 在该回调执行前再安排一个更新节点。
            if (_tick != null && _tick.Cancel()) _tick = null;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _moving.Clear();
                CancelIdleTick();
            }
        }
    }
}
