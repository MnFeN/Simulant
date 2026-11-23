using Simulant.Game.Data;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Numerics;
using ActionRow = Lumina.Excel.Sheets.Action;

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
            public SimScheduledAction CastCompletion;
            public SimScheduledAction DisplayCompletion;
            public bool CastFinished;
            public SimScheduledAction PendingOmen;
            public SimActor Target;
            public Vector3? OmenPosition;
            public float? OmenHeading;
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

        public void Cast(SimActor actor, uint actionId, float? omenDelay = 0, SimActor target = null,
            Vector3? omenPosition = null, float? omenHeading = null, float? castTime = null, float? effectDelay = null,
            bool executeOnFinish = true, int? packetTargetCapacity = null)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));

            if (omenDelay.HasValue && (float.IsNaN(omenDelay.Value) || float.IsInfinity(omenDelay.Value)))
                throw new ArgumentOutOfRangeException(nameof(omenDelay));

            if (castTime.HasValue)
                SimTimeline.ValidateSeconds(castTime.Value, nameof(castTime));

            lock (_gate)
            {
                if (_disposed || !actor.IsActive)
                    return;

                if (!GameDataService.Instance.GetSheet<ActionRow>().TryGetRow(actionId, out var action))
                    return;

                var duration = castTime ?? CastDuration(action.Cast100ms);
                var extraCastTime = action.ExtraCastTime100ms / 10f;
                var delayAfterCast = effectDelay ?? (duration > 0 ? 0.3f : 0);
                SimTimeline.ValidateSeconds(delayAfterCast, nameof(effectDelay));
                CancelCore(actor);

                // 移除预兆会调用外部代码；它可能已清理角色或开始更新的读条。
                if (_disposed || !actor.IsActive || _casts.ContainsKey(actor))
                    return;

                var state = new CastState
                {
                    ActionId = actionId,
                    EndsAt = _scheduler.ElapsedSeconds + duration,
                    OmenTag = _tagPrefix + (++_castSequence),
                    Target = target,
                    OmenPosition = omenPosition,
                    OmenHeading = omenHeading
                };
                _casts.Add(actor, state);

                try
                {
                    actor.Entity.SetCastInfo(actionId, duration, extraCastTime: extraCastTime);

                    if (target != null)
                        actor.Entity.SetCastTarget(target.Entity);

                    if (duration > 0 && executeOnFinish)
                        state.CastCompletion = _scheduler.ScheduleAfter(duration,
                            () => FinishCast(actor, state), "技能 " + actionId + " 读条完成");
                    state.Completion = _scheduler.ScheduleAfter(duration + (executeOnFinish ? delayAfterCast : 0),
                        () => Complete(actor, state, action, executeOnFinish, packetTargetCapacity), "技能 " + actionId + " 结算演出");
                    if (extraCastTime > 0)
                        state.DisplayCompletion = _scheduler.ScheduleAfter(duration + extraCastTime,
                            () => FinishExtendedDisplay(actor, state), "技能 " + actionId + " 读条显示结束");

                    if (action.Omen.RowId > 0 && omenDelay.HasValue)
                    {
                        var delay = Math.Max(0, omenDelay.Value);

                        if (delay == 0)
                            ShowOmen(actor, state, action);
                        else if (delay < duration)
                            state.PendingOmen = _scheduler.ScheduleAfter(delay,
                                () => ShowOmen(actor, state, action), "技能 " + actionId + " 预兆");
                    }

                    if (duration > 0)
                        actor.CombatLog?.Cast(actor.Entity, action, target?.Entity, duration, omenPosition, omenHeading);

                    _log?.Invoke(FormattableString.Invariant($"开始读条：技能 {actionId}（{duration:0.0} 秒）"));
                }
                catch
                {
                    if (IsCurrent(actor, state))
                        CancelCore(actor);

                    throw;
                }
            }
        }

        public void Execute(SimActor actor, uint actionId, SimActor target = null, IEnumerable<SimActor> hitTargets = null,
            int? packetTargetCapacity = null)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));

            lock (_gate)
            {
                if (_disposed || !actor.IsActive)
                    return;

                if (!GameDataService.Instance.GetSheet<ActionRow>().TryGetRow(actionId, out var action))
                    return;

                CancelCore(actor);

                if (_disposed || !actor.IsActive || _casts.ContainsKey(actor))
                    return;

                ExecuteCore(actor, action, target, hitTargets: hitTargets, packetTargetCapacity: packetTargetCapacity);
            }
        }

        internal bool HasPendingAction(SimActor actor)
        {
            lock (_gate)
                return _casts.ContainsKey(actor);
        }

        internal double Remaining(SimActor actor)
        {
            lock (_gate)
                return _casts.TryGetValue(actor, out var state) ? Math.Max(0, state.EndsAt - _scheduler.ElapsedSeconds) : 0;
        }

        /// <summary>只取消自动战斗持有的带目标读条，保留副本显式开始的普通读条。</summary>
        internal void CancelVisual(SimActor actor)
        {
            lock (_gate)
                if (_casts.TryGetValue(actor, out var state) && state.Target != null && !state.CastFinished)
                    CancelCore(actor);
        }

        public void Cancel(SimActor actor)
        {
            if (actor == null)
                return;

            lock (_gate)
                CancelCore(actor);
        }

        private void ShowOmen(SimActor actor, CastState state, ActionRow action)
        {
            lock (_gate)
            {
                if (!IsCurrent(actor, state))
                    return;

                state.PendingOmen = null;

                if (!actor.IsActive)
                {
                    CancelCore(actor);
                    return;
                }

                // 外部回调接收预兆后即使抛出异常，取消时仍会移除对应的唯一标签。
                state.OmenVisible = true;
                state.OmenVisible = _omens.Show(actor, action, state.EndsAt - _scheduler.ElapsedSeconds,
                    state.OmenTag, state.OmenPosition, state.OmenHeading);
            }
        }

        /// <summary>技能表包含结算前的 0.3 秒间隔；显式传入的网络咏唱时长不再扣除。</summary>
        internal static float CastDuration(ushort cast100ms) => Math.Max(0, cast100ms / 10f - 0.3f);

        private void FinishCast(SimActor actor, CastState state)
        {
            lock (_gate)
            {
                if (!IsCurrent(actor, state))
                    return;

                try
                {
                    FinishCastCore(actor, state);
                }
                catch
                {
                    if (IsCurrent(actor, state))
                    {
                        _casts.Remove(actor);
                        CancelState(actor, state);
                    }
                    throw;
                }
            }
        }

        private void Complete(SimActor actor, CastState state, ActionRow action, bool executeOnFinish, int? packetTargetCapacity)
        {
            lock (_gate)
            {
                if (!IsCurrent(actor, state))
                    return;

                var keepDisplay = state.DisplayCompletion != null;
                state.Completion = null;
                if (!keepDisplay)
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
                finally
                {
                    if (keepDisplay)
                        FinishCastCore(actor, state);
                    else
                        CancelState(actor, state);
                }

                if (!executeOnFinish || !execute || _disposed || !actor.IsActive ||
                    (keepDisplay ? !IsCurrent(actor, state) : _casts.ContainsKey(actor)))
                    return;

                // 清理预兆期间也可能有外部原生读条接管动画，必须重新读取当前技能。
                var actionAfterCleanup = actor.Entity.CastingActionId;

                if (actionAfterCleanup == 0 || actionAfterCleanup == state.ActionId)
                    ExecuteCore(actor, action, state.Target, state.OmenPosition, state.OmenHeading, packetTargetCapacity: packetTargetCapacity);
            }
        }

        private void FinishExtendedDisplay(SimActor actor, CastState state)
        {
            lock (_gate)
            {
                if (!IsCurrent(actor, state))
                    return;

                if (state.Completion == null)
                    _casts.Remove(actor);
                FinishCastCore(actor, state, stopDisplay: true);
            }
        }

        private bool IsCurrent(SimActor actor, CastState state)
            => !_disposed && _casts.TryGetValue(actor, out var current) && ReferenceEquals(current, state);

        private void ExecuteCore(SimActor actor, ActionRow action, SimActor target,
            Vector3? effectOrigin = null, float? effectHeading = null, IEnumerable<SimActor> hitTargets = null,
            int? packetTargetCapacity = null)
        {
            if (target != null)
            {
                if (target.IsActive && target.Entity.IsTargetable && target.Entity.HP != 0)
                {
                    actor.Entity.ExecuteActionEffect(action.RowId, target.Entity);
                    actor.CombatLog?.Ability(actor.Entity, action.RowId, target.Entity, effectOrigin, effectHeading, 0, hitTargets,
                        packetTargetCapacity);
                }

                return;
            }

            var timeline = unchecked((int)action.AnimationEnd.RowId);

            if (timeline >= 0)
            {
                actor.Entity.ResetBaseTimeline();
                actor.Entity.PlayBlendTimeline((ushort)timeline);
            }

            actor.CombatLog?.Ability(actor.Entity, action.RowId, null, effectOrigin, effectHeading, 0, hitTargets, packetTargetCapacity);
            _log?.Invoke($"已执行技能：{action.RowId}；结束动画：{timeline}");
        }

        private void CancelCore(SimActor actor)
        {
            if (!_casts.TryGetValue(actor, out var state))
                return;

            _casts.Remove(actor);
            var interrupted = actor.IsActive && !state.CastFinished && _scheduler.ElapsedSeconds < state.EndsAt;
            CancelState(actor, state);
            if (interrupted)
                actor.CombatLog?.Cancel(actor.Entity, state.ActionId);
        }

        private void CancelState(SimActor actor, CastState state)
        {
            state.Completion?.Cancel();
            state.CastCompletion?.Cancel();
            FinishCastCore(actor, state, stopDisplay: true);
        }

        private void FinishCastCore(SimActor actor, CastState state, bool stopDisplay = false)
        {
            var removeOmen = !state.CastFinished && state.OmenVisible;
            var finalizeDisplay = !state.CastFinished && state.DisplayCompletion == null;
            state.CastFinished = true;
            state.PendingOmen?.Cancel();
            if (stopDisplay && state.DisplayCompletion != null)
            {
                state.DisplayCompletion.Cancel();
                state.DisplayCompletion = null;
                finalizeDisplay = true;
            }

            try
            {
                // 不清除借用角色上无关的读条，也不访问已失效的原生实体。
                if (finalizeDisplay && actor.Entity.IsAlive)
                    actor.Entity.FinalizeCast(state.ActionId);
            }
            finally
            {
                if (removeOmen)
                    _omens.Remove(state.OmenTag);
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                var states = new List<KeyValuePair<SimActor, CastState>>(_casts);
                _casts.Clear();
                List<Exception> errors = null;

                foreach (var entry in states)
                {
                    try
                    {
                        CancelState(entry.Key, entry.Value);
                    }
                    catch (Exception ex)
                    {
                        if (errors == null)
                            errors = new List<Exception>();

                        errors.Add(ex);
                    }
                }

                if (errors != null)
                    throw new AggregateException("清理模拟读条失败。", errors);
            }
        }
    }
}
