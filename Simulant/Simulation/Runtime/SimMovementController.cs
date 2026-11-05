using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

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
            public ushort? AnimationId;
            public long AnimationVersion;
            public bool AnimationFinished;
            public bool Arrived;
            public Action OnArrived;
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

        public void MoveTo(SimActor actor, Vector2 target, float speed = 6, bool faceMovement = true,
            ushort? animationId = null, Action onArrived = null)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            ValidatePosition(target, nameof(target));
            ValidateSpeed(speed);
            if (animationId == 0)
                throw new ArgumentOutOfRangeException(nameof(animationId), "移动动画编号必须为正数；不控制动画时请省略该参数。");
            if (onArrived != null)
            {
                foreach (var callback in onArrived.GetInvocationList())
                    if (callback.Method.IsDefined(typeof(AsyncStateMachineAttribute), false))
                        throw new ArgumentException("到达回调必须同步执行，延迟动作请添加到调度器。", nameof(onArrived));
            }
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(SimMovementController));
                StopCore(actor);
                if (!actor.IsActive)
                {
                    CancelIdleTick();
                    return;
                }
                var movement = new Movement
                {
                    Actor = actor,
                    Target = target,
                    Speed = speed,
                    FaceMovement = faceMovement,
                    UpdatedAt = _scheduler.ElapsedSeconds,
                    AnimationId = animationId,
                    OnArrived = onArrived
                };
                _moving.Add(movement);
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
            lock (_gate)
            {
                var index = Find(actor);
                return !_disposed && actor.IsActive && index >= 0 && !_moving[index].Arrived;
            }
        }

        public void Stop(SimActor actor)
        {
            if (actor == null) return;
            lock (_gate)
            {
                try { StopCore(actor); }
                finally { CancelIdleTick(); }
            }
        }

        private void StopCore(SimActor actor)
        {
            var index = Find(actor);
            if (index < 0) return;
            var movement = _moving[index];
            _moving.RemoveAt(index);
            movement.OnArrived = null;
            FinishAnimation(movement);
        }

        private void Tick()
        {
            List<Movement> completed = null;
            lock (_gate)
            {
                _tick = null;
                if (_disposed) return;
                var now = _scheduler.ElapsedSeconds;
                for (var i = _moving.Count - 1; i >= 0; --i)
                {
                    var movement = _moving[i];
                    if (movement.Arrived) continue;
                    if (!movement.Actor.IsActive)
                    {
                        StopCore(movement.Actor);
                        continue;
                    }
                    try
                    {
                        var entity = movement.Actor.Entity;
                        UpdateAnimation(movement);
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
                        if (arrived && _moving.Contains(movement))
                        {
                            movement.Arrived = true;
                            FinishAnimation(movement);
                            if (completed == null) completed = new List<Movement>();
                            completed.Add(movement);
                        }
                    }
                    catch (InvalidOperationException) when (!movement.Actor.IsActive)
                    {
                        // 首次检查有效性后、执行原生操作前，实体仍可能消失。
                        _moving.Remove(movement);
                        movement.OnArrived = null;
                        FinishAnimation(movement);
                    }
                }
                // 以实际时间为基准：更新迟到时按真实经过的时长推进，不补排追赶帧。
                if (HasMovingActors()) QueueTick();
            }
            if (completed == null) return;
            foreach (var movement in completed)
            {
                Action callback;
                lock (_gate)
                {
                    // 保留完成候选的身份直到领取回调；前一个回调可以取消、换目标或删除其他角色。
                    if (!_moving.Remove(movement)) continue;
                    callback = !_disposed && movement.Actor.IsActive && _scheduler.IsRunning
                        ? movement.OnArrived : null;
                    movement.OnArrived = null;
                }
                // 用户代码可继续移动、读条或回收实体，不能持有移动锁调用。
                callback?.Invoke();
            }
        }

        private static void UpdateAnimation(Movement movement)
        {
            if (!movement.AnimationId.HasValue || movement.AnimationFinished) return;
            if (movement.Actor.Entity.CastingActionId != 0)
            {
                // 读条接管动画后，本次移动不再抢回覆盖；下一次 MoveTo 可重新指定移动动画。
                FinishAnimation(movement);
                return;
            }
            if (movement.AnimationVersion == 0)
                movement.AnimationVersion = movement.Actor.Entity.PlayOwnedBaseTimeline(movement.AnimationId.Value);
        }

        private static void FinishAnimation(Movement movement)
        {
            movement.AnimationFinished = true;
            if (!movement.AnimationId.HasValue || movement.AnimationVersion == 0) return;
            var version = movement.AnimationVersion;
            movement.AnimationVersion = 0;
            var entity = movement.Actor.Entity;
            try
            {
                if (entity.IsAlive) entity.ResetOwnedBaseTimeline(movement.AnimationId.Value, version);
            }
            catch (InvalidOperationException) when (!entity.IsAlive)
            {
                // 世界切换或实体回收可能发生在有效性检查之后。
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
            if (HasMovingActors()) return;
            // 已取出的回调可能正在等待此锁，保留其句柄直到回调进入 Tick，
            // 防止并发调用 MoveTo 在该回调执行前再安排一个更新节点。
            if (_tick != null && _tick.Cancel()) _tick = null;
        }

        private bool HasMovingActors()
        {
            foreach (var movement in _moving)
                if (!movement.Arrived) return true;
            return false;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                var movements = _moving.ToArray();
                _moving.Clear();
                CancelIdleTick();
                List<Exception> errors = null;
                foreach (var movement in movements)
                {
                    movement.OnArrived = null;
                    try { FinishAnimation(movement); }
                    catch (Exception error)
                    {
                        if (errors == null) errors = new List<Exception>();
                        errors.Add(error);
                    }
                }
                if (errors != null) throw new AggregateException("清理模拟移动动画失败。", errors);
            }
        }
    }
}
