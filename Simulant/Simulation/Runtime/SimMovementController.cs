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
            public bool Network;
            public byte SpeedParam;
            public bool NetworkStarted;
            public Vector2 NetworkOrigin;
            public Vector2 NetworkLastSent;
            public double NetworkDuration;
            public bool NetworkDestinationSent;
            public double NextPollAt;
            public bool Stopping;
        }

        private const double TickIntervalSeconds = 0.016;
        private const double NetworkPollIntervalSeconds = 0.1;
        private const float NetworkArrivalTolerance = 0.1f;
        private readonly object _gate = new object();
        private readonly SimScheduler _scheduler;
        private readonly Action _onTick;
        private readonly List<Movement> _moving = new List<Movement>();
        private SimScheduledAction _tick;
        private double _tickDueAt;
        private readonly double _networkTimeoutSeconds;
        private bool _disposed;

        internal bool HasPendingCleanup
        {
            get
            {
                lock (_gate)
                {
                    foreach (var movement in _moving)
                        if (_disposed || movement.Stopping) return true;
                    return false;
                }
            }
        }

        public SimMovementController(SimScheduler scheduler, double networkTimeoutSeconds = 30)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            if (double.IsNaN(networkTimeoutSeconds) || double.IsInfinity(networkTimeoutSeconds) || networkTimeoutSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(networkTimeoutSeconds));
            _networkTimeoutSeconds = networkTimeoutSeconds;
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
            ValidateCallback(onArrived);
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

        public void MoveNetworkTo(SimActor actor, Vector2 target, byte speedParam = 30, Action onArrived = null)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            ValidatePosition(target, nameof(target));
            if (Math.Abs(target.X) > 1000 || Math.Abs(target.Y) > 1000)
                throw new ArgumentOutOfRangeException(nameof(target), "网络移动目标必须位于移动包可表示的坐标范围内（-1000 至 1000）。");
            if (speedParam == 0)
                throw new ArgumentOutOfRangeException(nameof(speedParam), "网络移动速度参数不能为零；停止移动请调用 StopMoving。");
            ValidateCallback(onArrived);
            var slot = actor.Entity.Identity.Slot;
            if (slot < 2 || slot >= 200 || (slot & 1) != 0)
                throw new InvalidOperationException("网络移动只支持网络角色表中的角色，不能用于自身玩家或本地备用实体。");
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(SimMovementController));
                if (!actor.IsActive) throw new InvalidOperationException("不能移动已经失效或解除关联的角色。");
                StopCore(actor);
                var movement = new Movement
                {
                    Actor = actor,
                    Target = target,
                    Network = true,
                    Speed = speedParam / 10f,
                    SpeedParam = speedParam,
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
            movement.OnArrived = null;
            movement.Stopping = true;
            StopNetworkMovement(movement);
            FinishAnimation(movement);
            // 原生停止失败时保留记录；后续 Stop 或 Dispose 仍能重试，不能丢失回收责任。
            _moving.Remove(movement);
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
                    // 混用本地插值时，网络角色仍只每 100 ms 读取一次状态和位置。
                    if (movement.Network && movement.NetworkStarted && now < movement.NextPollAt && !movement.Stopping) continue;
                    if (movement.Stopping || !movement.Actor.IsActive)
                    {
                        StopCore(movement.Actor);
                        continue;
                    }
                    try
                    {
                        var entity = movement.Actor.Entity;
                        bool arrived;
                        if (movement.Network)
                        {
                            arrived = UpdateNetworkMovement(movement, now);
                        }
                        else
                        {
                            UpdateAnimation(movement);
                            var current = entity.Pos;
                            var next = Advance(current, movement.Target, movement.Speed,
                                Math.Max(0, now - movement.UpdatedAt), out arrived);
                            movement.UpdatedAt = now;
                            if (next != current)
                            {
                                if (movement.FaceMovement)
                                    entity.Heading = (float)Math.Atan2((double)movement.Target.X - current.X,
                                        (double)movement.Target.Y - current.Y);
                                entity.Pos = next;
                            }
                        }
                        if (arrived && _moving.Contains(movement))
                        {
                            movement.Arrived = true;
                            FinishAnimation(movement);
                            if (completed == null) completed = new List<Movement>();
                            completed.Add(movement);
                        }
                    }
                    catch (InvalidOperationException) when (!movement.Actor.Entity.IsAlive)
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

        private bool UpdateNetworkMovement(Movement movement, double now)
        {
            var entity = movement.Actor.Entity;
            var current = entity.Pos3D;
            ValidatePosition(new Vector2(current.X, current.Y), "当前位置");
            if (float.IsNaN(current.Z) || float.IsInfinity(current.Z))
                throw new InvalidOperationException("网络角色当前高度不是有限数，无法发送移动包。");
            var dx = (double)movement.Target.X - current.X;
            var dy = (double)movement.Target.Y - current.Y;
            var withinTolerance = dx * dx + dy * dy <= NetworkArrivalTolerance * NetworkArrivalTolerance;
            // 原生位置可能已经追上前一个包；路程未按时间走完时，不能提前宣布到达。
            if (withinTolerance && (!movement.NetworkStarted || movement.NetworkDestinationSent)) return true;
            if (!movement.NetworkStarted)
            {
                // 首包也在共享调度线程发送；此前取消无需发送停止包。
                // 先记录已尝试发送，确保原生调用部分成功后抛错仍能被清理。
                movement.NetworkStarted = true;
                movement.UpdatedAt = now;
                movement.NetworkOrigin = movement.NetworkLastSent = new Vector2(current.X, current.Y);
                movement.NetworkDuration = Math.Sqrt(dx * dx + dy * dy) / movement.Speed;
                entity.ActorMove(current,
                    (float)Math.Atan2(dx, dy), movement.SpeedParam);
            }
            else if (now - movement.UpdatedAt >= _networkTimeoutSeconds)
            {
                var timeout = new TimeoutException($"网络角色 #{entity.Id:X8} 在 {_networkTimeoutSeconds} 秒内未到达目标位置。");
                try { StopCore(movement.Actor); }
                catch (Exception cleanupError) { throw new AggregateException("网络移动超时且停止失败。", timeout, cleanupError); }
                throw timeout;
            }
            else if (!movement.NetworkDestinationSent)
            {
                // ActorMove 报告的是服务器当前位置，不是导航终点；远端终点会让客户端追赶加速。
                // 按真实经过时间发沿途位置，丢帧时跳过过时中间包，仍由客户端平滑移动模型。
                // https://github.com/anomek/AnoMech/blob/1646a7068c2071a3d1fd9e3c84550dfd3f1b6ade/AnoMech/Core/Game/Motion/ModelSync.cs
                var next = Advance(movement.NetworkOrigin, movement.Target, movement.Speed,
                    Math.Max(0, now - movement.UpdatedAt), out var final);
                if (next != movement.NetworkLastSent || final)
                {
                    var direction = movement.Target - movement.NetworkOrigin;
                    entity.ActorMove(new Vector3(next.X, next.Y, current.Z),
                        (float)Math.Atan2(direction.X, direction.Y), movement.SpeedParam);
                    movement.NetworkLastSent = next;
                    movement.NetworkDestinationSent = final;
                }
            }
            // 最后一个不足轮询间隔的分段按截止时间补发；终点包只发送一次，随后只等待实际到达。
            movement.NextPollAt = movement.NetworkDestinationSent
                ? now + NetworkPollIntervalSeconds
                : Math.Min(now + NetworkPollIntervalSeconds, movement.UpdatedAt + movement.NetworkDuration);
            return false;
        }

        private static void StopNetworkMovement(Movement movement)
        {
            if (!movement.Network || !movement.NetworkStarted || movement.Arrived) return;
            var entity = movement.Actor.Entity;
            try
            {
                // Detach 已将 actor 标记为不活动，但仍应停止本次拥有的原生移动。
                if (entity.IsAlive) entity.ActorMove(entity.Pos3D, entity.Heading, movement.SpeedParam);
                movement.NetworkStarted = false;
            }
            catch (InvalidOperationException) when (!entity.IsAlive)
            {
                // 实体失效后不能向复用该槽位的新对象发送停止包。
                movement.NetworkStarted = false;
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

        private static void ValidateCallback(Action onArrived)
        {
            if (onArrived == null) return;
            foreach (var callback in onArrived.GetInvocationList())
                if (callback.Method.IsDefined(typeof(AsyncStateMachineAttribute), false))
                    throw new ArgumentException("到达回调必须同步执行，延迟动作请添加到调度器。", nameof(onArrived));
        }

        private int Find(SimActor actor)
        {
            for (var i = 0; i < _moving.Count; ++i)
                if (ReferenceEquals(_moving[i].Actor, actor)) return i;
            return -1;
        }

        private void QueueTick()
        {
            if (_tick != null && _tick.IsCompleted) _tick = null;
            var now = _scheduler.ElapsedSeconds;
            var dueAt = double.PositiveInfinity;
            foreach (var movement in _moving)
            {
                if (movement.Arrived) continue;
                var next = !movement.Network ? now + TickIntervalSeconds :
                    movement.NetworkStarted ? movement.NextPollAt : now;
                if (movement.Stopping) next = now;
                dueAt = Math.Min(dueAt, next);
            }
            if (double.IsPositiveInfinity(dueAt)) return;
            // 新的本地插值或网络首包无需等待已有的低频网络轮询。
            if (_tick != null && dueAt < _tickDueAt && _tick.Cancel()) _tick = null;
            if (_tick == null)
            {
                _tickDueAt = dueAt;
                _tick = _scheduler.ScheduleAfter(Math.Max(0, dueAt - now), _onTick, "更新实体移动");
            }
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
                _disposed = true;
                var movements = _moving.ToArray();
                if (_tick != null && _tick.Cancel()) _tick = null;
                List<Exception> errors = null;
                foreach (var movement in movements)
                {
                    movement.OnArrived = null;
                    try { StopCore(movement.Actor); }
                    catch (Exception error)
                    {
                        if (errors == null) errors = new List<Exception>();
                        errors.Add(error);
                    }
                }
                if (errors != null) throw new AggregateException("清理模拟移动失败。", errors);
            }
        }
    }
}
