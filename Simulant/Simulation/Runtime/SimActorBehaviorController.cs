using Simulant.Game;
using Simulant.Game.Data;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Numerics;
using ActionRow = Lumina.Excel.Sheets.Action;

namespace Simulant.Simulation.Runtime
{
    /// <summary>会话内假玩家共用一个低频更新节点；机制意图优先，技能和走动只修饰表现。</summary>
    internal sealed partial class SimActorBehaviorController : IDisposable
    {
        private sealed class State
        {
            public SimActor Actor;
            public SimActorBehaviorOptions Options;
            public SimJobActions Actions;
            public Vector2 Anchor;
            public bool Initialized, ManualMovement, InCombat;
            public int ActionIndex;
            public uint OwnCast;
            public uint SelectedTarget = 0xE0000000;
            public double NextAction, NextIdle, NextRefresh, NextFacing;
            public MovePlan Move;
        }

        private readonly object _gate = new object();
        private readonly Dictionary<SimActor, State> _states = new Dictionary<SimActor, State>();
        private readonly SimScheduler _scheduler;
        private readonly SimMovementController _movement;
        private readonly SimCastController _casts;
        private readonly Random _random = new Random();
        private readonly Action _update;
        private SimScheduledAction _tick;
        private SimActor _target;
        private Vector2 _targetPosition;
        private float _targetRadius;
        private bool _disposed;
        private const double TickSeconds = 0.1;
        private const double SlideSeconds = 0.45;
        private const byte RunSpeed = 60;

        internal SimActorBehaviorController(SimScheduler scheduler, SimMovementController movement, SimCastController casts)
        { _scheduler = scheduler; _movement = movement; _casts = casts; _update = Tick; }

        internal void Add(SimActor actor, SimActorBehaviorOptions options)
        {
            lock (_gate)
            {
                if (_disposed) return;
                _states.Add(actor, new State { Actor = actor, Options = options.Copy(), Actions = SimJobActions.For(actor.Job),
                    NextAction = _scheduler.ElapsedSeconds + _random.NextDouble() * 2.5 });
                actor.BehaviorController = this;
                QueueTick();
            }
        }

        internal void Configure(SimActor actor, SimActorBehaviorOptions options)
        {
            var copy = (options ?? throw new ArgumentNullException(nameof(options))).Copy();
            lock (_gate)
            {
                if (_states.TryGetValue(actor, out var state))
                {
                    state.Options = copy;
                    state.NextRefresh = 0;
                    if (!copy.AttackTarget) CancelOwnCast(state);
                    QueueTick();
                }
                else throw new InvalidOperationException("只有本会话生成的网络假玩家可以配置拟人行为。");
            }
        }

        internal void SetTarget(SimActor target)
        {
            lock (_gate)
            {
                if (_disposed || ReferenceEquals(_target, target)) return;
                _target = target;
                foreach (var state in _states.Values)
                {
                    CancelOwnCast(state);
                    state.NextRefresh = 0;
                    if (target != null) state.NextAction = _scheduler.ElapsedSeconds + 0.2 + _random.NextDouble() * 1.8;
                    else if (state.Move == null && !state.ManualMovement) _movement.Stop(state.Actor);
                }
                QueueTick();
            }
        }

        internal void Remove(SimActor actor) { lock (_gate) _states.Remove(actor); }

        /// <summary>显式低层移动接管路线；完成后再以新的站位为中心空闲活动。</summary>
        internal void ManualMove(SimActor actor)
        {
            lock (_gate)
                if (_states.TryGetValue(actor, out var state))
                { state.Move = null; state.ManualMovement = true; state.NextIdle = _scheduler.ElapsedSeconds + 5; QueueTick(); }
        }

        internal void ManualAction(SimActor actor)
        {
            lock (_gate)
                if (_states.TryGetValue(actor, out var state))
                { state.OwnCast = 0; state.NextAction = _scheduler.ElapsedSeconds + 2.5; }
        }

        private void Tick()
        {
            lock (_gate)
            {
                _tick = null;
                if (_disposed) return;
                var now = _scheduler.ElapsedSeconds;
                var inCombat = _target != null && _target.IsActive;
                var targetable = inCombat && _target.Entity.IsTargetable && _target.Entity.HP != 0;
                if (inCombat) { _targetPosition = _target.Pos; _targetRadius = _target.Entity.HitboxRadius; }
                foreach (var state in _states.Values)
                {
                    var actor = state.Actor;
                    if (!actor.IsActive) continue;
                    if (!state.Initialized)
                    { state.Anchor = actor.Pos; state.Initialized = true; state.NextIdle = now + 3 + _random.NextDouble() * 3; }
                    if (actor.Entity.HP == 0) { CancelOwnCast(state); _movement.Stop(actor); continue; }
                    UpdateMovement(state, now);
                    RefreshCombat(state, now, inCombat, targetable);
                    if (!inCombat) { CancelOwnCast(state); continue; }
                    if (targetable && state.Options.AttackTarget)
                    {
                        UpdateMeleeReturn(state, now);
                        if (now >= state.NextFacing && !actor.IsMoving)
                        { FaceTarget(actor); state.NextFacing = now + 0.5; }
                        UpdateAction(state, now);
                    }
                    else CancelOwnCast(state);
                    UpdateIdle(state, now);
                }
                QueueTick();
            }
        }

        private void RefreshCombat(State state, double now, bool inCombat, bool targetable)
        {
            // 原生调用只在入战、目标或拔刀状态需要改变时发送，不重复刷相同的收包操作。
            var selected = inCombat && targetable && state.Options.AttackTarget ? _target.Id : 0xE0000000u;
            if (state.SelectedTarget != selected)
            {
                state.Actor.Entity.SetNetworkTarget(selected == 0xE0000000 ? null : _target.Entity);
                state.SelectedTarget = selected;
            }
            if (state.InCombat != inCombat)
            {
                state.Actor.Entity.ActorControl(ActorControlCategory.SetBattle, inCombat ? 1u : 0u);
                state.InCombat = inCombat;
            }
            if (now < state.NextRefresh) return;
            state.NextRefresh = now + 1;
            if (inCombat && !state.Actor.Entity.IsWeaponDrawn)
                // Sapphire Chara::setStance：Active=1，第二参数固定为 1。
                state.Actor.Entity.ActorControl(ActorControlCategory.ToggleWeapon, 1, 1);
        }

        private void UpdateAction(State state, double now)
        {
            var actor = state.Actor;
            var actions = state.Actions;
            if (actions == null || now < state.NextAction || actor.Entity.CastingActionId != 0 || _casts.Remaining(actor) > 0) return;
            var distance = Vector2.Distance(actor.Pos, _targetPosition) - _targetRadius;
            var moving = actor.IsMoving || MustRun(state, now);
            var step = actions.Loop[state.ActionIndex % actions.Loop.Length];
            var id = actions.Caster && moving ? actions.MovingAction :
                actions.Melee && distance > 3 ? actions.DistantAction : step.Id;
            if (id == 0 || !GameDataService.Instance.GetSheet<ActionRow>().TryGetRow(id, out var action)) return;
            var range = action.Range >= 0 ? action.Range : actions.Melee ? 3 : 25;
            if (distance > range) return;
            var castTime = id == step.Id && step.Instant ? 0 : (action.Cast100ms / 10f);
            if (castTime > 0 && (moving || !CanWait(state, now, castTime + 0.3)))
            {
                if (!actions.Caster) return;
                id = actions.MovingAction;
                if (!GameDataService.Instance.GetSheet<ActionRow>().TryGetRow(id, out action) || (action.Cast100ms / 10f) != 0) return;
                castTime = 0;
            }
            if (castTime > 0)
            {
                _casts.Cast(actor, id, null, _target);
                state.OwnCast = id;
            }
            else _casts.Execute(actor, id, _target);
            state.NextAction = now + Math.Max(2.5, castTime) + _random.NextDouble() * 0.12;
            if (id == step.Id) state.ActionIndex = (state.ActionIndex + 1) % actions.Loop.Length;
        }

        private void UpdateIdle(State state, double now)
        {
            if (state.Move != null || state.ManualMovement || state.Actor.IsMoving || now < state.NextIdle || state.Options.IdleRadius <= 0) return;
            if (_casts.Remaining(state.Actor) > 0 || state.Actor.Entity.CastingActionId != 0) return;
            state.NextIdle = now + 4 + _random.NextDouble() * 4;
            var angle = _random.NextDouble() * Math.PI * 2;
            var radius = (float)(state.Options.IdleRadius * (0.3 + _random.NextDouble() * 0.7));
            var position = state.Anchor + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
            // 以原站位为锚，不累计漂移；已经在近战范围内时也不因闲逛丢失攻击距离。
            if (state.Actions?.Melee == true && Vector2.Distance(state.Anchor, _targetPosition) <= _targetRadius + 3 &&
                Vector2.Distance(position, _targetPosition) > _targetRadius + 2.8) return;
            var plan = new MovePlan { Destination = position, HoldUntil = now };
            plan.Points.AddRange(CreateRoute(state.Actor.Pos, position, state.Options.RouteAngleDegrees, _random));
            // 空闲路线也使用角度绕路，但整条折线必须留在原站位的圆形范围内。
            if (plan.Points.Exists(point => Vector2.Distance(point, state.Anchor) > state.Options.IdleRadius))
            { plan.Points.Clear(); plan.Points.Add(position); }
            plan.Arrival = now + TravelTime(state.Actor.Pos, plan);
            plan.Latest = plan.Arrival + 1;
            state.Move = plan;
        }

        private void FaceTarget(SimActor actor)
        {
            if (actor.IsMoving) return;
            var direction = _targetPosition - actor.Pos;
            if (direction.LengthSquared() < 0.01f) return;
            var heading = (float)Math.Atan2(direction.X, direction.Y);
            var difference = Math.Atan2(Math.Sin(heading - actor.Heading), Math.Cos(heading - actor.Heading));
            if (Math.Abs(difference) > 0.05) actor.Entity.ActorMove(actor.Pos3D, heading, RunSpeed);
        }

        private void CancelOwnCast(State state)
        {
            if (state.OwnCast == 0) return;
            _casts.CancelVisual(state.Actor);
            state.OwnCast = 0;
        }

        private void QueueTick()
        {
            if (_disposed || _tick != null) return;
            foreach (var state in _states.Values)
                if (_target != null || state.Move != null || state.ManualMovement || state.InCombat || state.SelectedTarget != 0xE0000000)
                {
                    _tick = _scheduler.ScheduleAfter(TickSeconds, _update, "更新假玩家战斗与走位意图");
                    return;
                }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                _tick?.Cancel();
                _tick = null;
                // 角色的读条和原生移动由原有控制器收尾，避免重复清理或打断失败现场的特效。
                _states.Clear();
                _target = null;
            }
        }
    }
}
