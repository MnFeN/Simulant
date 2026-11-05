using Simulant.Game.ExtractedCsv;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using ActionRow = Simulant.Game.ExtractedCsv.Rows.Action;

namespace Simulant.Simulation.Runtime
{
    /// <summary>由会话管理的读条状态。Character 只负责即时原生操作。</summary>
    internal sealed class SimCastController : IDisposable
    {
        private sealed class CastState
        {
            public uint ActionId;
            public double EndsAt;
            public string OmenTag;
            public bool OmenVisible;
            public SimScheduledAction Completion;
            public SimScheduledAction PendingOmen;
        }

        private readonly object _gate = new object();
        private readonly SimScheduler _scheduler;
        private readonly Action<string> _log;
        private readonly SimOmenRenderer _omens = new SimOmenRenderer();
        private readonly Dictionary<SimActor, CastState> _casts = new Dictionary<SimActor, CastState>();
        private readonly string _tagPrefix = "Simulant-" + Guid.NewGuid().ToString("N") + "-";
        private long _castSequence;
        private bool _disposed;

        public SimCastController(SimScheduler scheduler, Action<string> log = null)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _log = log;
        }

        public void Cast(SimActor actor, uint actionId, float? omenDelay = 0)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (omenDelay.HasValue && (float.IsNaN(omenDelay.Value) || float.IsInfinity(omenDelay.Value)))
                throw new ArgumentOutOfRangeException(nameof(omenDelay));
            lock (_gate)
            {
                if (_disposed || !actor.IsActive) return;
                var action = ResolveAction(actionId);
                if (action == null) return;
                CancelCore(actor);
                var state = new CastState
                {
                    ActionId = actionId,
                    EndsAt = _scheduler.ElapsedSeconds + action.CastTime,
                    OmenTag = _tagPrefix + (++_castSequence)
                };
                _casts.Add(actor, state);
                try
                {
                    actor.Entity.SetCastInfo(actionId, action.CastTime);
                    state.Completion = _scheduler.ScheduleAfter(action.CastTime,
                        () => Complete(actor, state, action), "技能 " + actionId + " 读条完成");
                    if (action.OmenId > 0 && omenDelay.HasValue)
                    {
                        var delay = Math.Max(0, omenDelay.Value);
                        if (delay == 0)
                            ShowOmen(actor, state, action);
                        else if (delay < action.CastTime - 0.3)
                            state.PendingOmen = _scheduler.ScheduleAfter(delay,
                                () => ShowOmen(actor, state, action), "技能 " + actionId + " 预兆");
                    }
                    _log?.Invoke($"开始读条：技能 {actionId}（{action.CastTime:0.0} 秒）");
                }
                catch
                {
                    if (IsCurrent(actor, state)) CancelCore(actor);
                    throw;
                }
            }
        }

        public void Execute(SimActor actor, uint actionId)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            lock (_gate)
            {
                if (_disposed || !actor.IsActive) return;
                var action = ResolveAction(actionId);
                if (action == null) return;
                CancelCore(actor);
                ExecuteCore(actor, action);
            }
        }

        public void Cancel(SimActor actor)
        {
            if (actor == null) return;
            lock (_gate) CancelCore(actor);
        }

        private void ShowOmen(SimActor actor, CastState state, ActionRow action)
        {
            lock (_gate)
            {
                if (!IsCurrent(actor, state)) return;
                state.PendingOmen = null;
                if (!actor.IsActive)
                {
                    CancelCore(actor);
                    return;
                }
                // 外部回调接收预兆后即使抛出异常，取消时仍会移除对应的唯一标签。
                state.OmenVisible = true;
                state.OmenVisible = _omens.Show(actor, action, state.EndsAt - _scheduler.ElapsedSeconds - 0.3, state.OmenTag);
            }
        }

        private void Complete(SimActor actor, CastState state, ActionRow action)
        {
            lock (_gate)
            {
                if (!IsCurrent(actor, state)) return;
                _casts.Remove(actor);
                var execute = false;
                try
                {
                    if (actor.IsActive)
                    {
                        var currentActionId = actor.Entity.CastingActionId;
                        // 自然完成时 CastInfo 可能已被清空；如果正在读另一个技能，则交由游戏管理。
                        execute = currentActionId == 0 || currentActionId == state.ActionId;
                    }
                }
                finally { CancelState(actor, state); }
                if (execute && actor.IsActive && !_casts.ContainsKey(actor)) ExecuteCore(actor, action);
            }
        }

        private bool IsCurrent(SimActor actor, CastState state)
            => !_disposed && _casts.TryGetValue(actor, out var current) && ReferenceEquals(current, state);

        private void ExecuteCore(SimActor actor, ActionRow action)
        {
            if (action.AnimationEndId >= 0)
            {
                actor.Entity.ResetBaseTimeline();
                actor.Entity.PlayBlendTimeline((ushort)action.AnimationEndId);
            }
            _log?.Invoke($"已执行技能：{action.Index}；结束动画：{action.AnimationEndId}");
        }

        private void CancelCore(SimActor actor)
        {
            if (!_casts.TryGetValue(actor, out var state)) return;
            _casts.Remove(actor);
            CancelState(actor, state);
        }

        private void CancelState(SimActor actor, CastState state)
        {
            state.Completion?.Cancel();
            state.PendingOmen?.Cancel();
            try
            {
                // 不清除借用角色上无关的读条，也不访问已失效的原生实体。
                if (actor.Entity.IsAlive) actor.Entity.FinalizeCast(state.ActionId);
            }
            finally
            {
                if (state.OmenVisible) _omens.Remove(state.OmenTag);
            }
        }

        private static ActionRow ResolveAction(uint actionId)
            => CsvManager.Instance.Get<ActionRow>().TryGetValue((int)actionId, out var action) ? action : null;

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                var states = new List<KeyValuePair<SimActor, CastState>>(_casts);
                _casts.Clear();
                List<Exception> errors = null;
                foreach (var entry in states)
                {
                    try { CancelState(entry.Key, entry.Value); }
                    catch (Exception ex)
                    {
                        if (errors == null) errors = new List<Exception>();
                        errors.Add(ex);
                    }
                }
                if (errors != null) throw new AggregateException("清理模拟读条失败。", errors);
            }
        }
    }
}
