using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using ActionEffect = Simulant.Game.FFCS.Client.Game.Character.ActionEffectHandler.Effect;
using GeneralActionId = Simulant.Simulation.Runtime.PlayerActions.GeneralActions.ActionId;
using GeneralStatusId = Simulant.Simulation.Runtime.PlayerActions.GeneralActions.StatusId;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Simulation.Runtime
{
    public sealed partial class SimEntityManager
    {
        private sealed class OwnedStatus
        {
            internal Character Actor;
            internal Character Source;
            internal int Slot;
            internal ushort StatusId;
            internal SimActor Participant;
            internal bool LockMovement;
            internal double ExpiresAt;
            internal SimScheduledAction Expiry;
            internal ushort? LoggedParam;
        }

        private readonly List<OwnedStatus> _statuses = new List<OwnedStatus>();

        internal Character[] GetActivePartyCharacters()
        {
            lock (_gate)
            {
                return Party.Where(actor => actor.IsActive && actor.Entity.HP != 0)
                    .Select(actor => actor.Entity).ToArray();
            }
        }

        private static bool IsShirkTarget(SimActor actor)
            => actor != null && actor.Entity.Identity.Slot != 0 && actor.IsActive && actor.Entity.HP != 0;

        internal bool TryUseEnmityAction(Character player, ActionRequest request, bool provoke)
        {
            lock (_gate)
            {
                if (!_acceptingEntities || !_scheduler.IsRunning || GetInvulnerability(player.Job).Action == 0 ||
                    !_actors.TryGetValue(player.Identity, out var source) || !source.IsActive || source.Entity.HP == 0 ||
                    !Party.Contains(source))
                    return false;

                if (provoke && Party.Count > 1 && _partyService?.IsOwnedBy(this) != true)
                    return false;

                // 网络战斗角色使用 Type 0 的完整 GameObjectId，按请求当时的目标结算。
                var candidates = provoke
                    ? _partyEnmity.Keys.Where(actor => actor.IsActive && actor.Entity.HP != 0 && actor.Entity.IsTargetable)
                    : Party.Where(IsShirkTarget);
                var target = candidates.FirstOrDefault(actor => actor != source && request.TargetId == actor.Id);
                if (target == null)
                    return false;

                UseEnmityAction(source, provoke, target, request);
                return true;
            }
        }

        internal void UsePartyEnmityAction(SimActor source, bool provoke, SimActor target)
        {
            lock (_gate)
            {
                if (!_acceptingEntities || !source.IsActive || source.Entity.HP == 0 || !target.IsActive || target.Entity.HP == 0)
                    return;

                UseEnmityAction(source, provoke, target);
            }
        }

        private void UseEnmityAction(SimActor source, bool provoke, SimActor target, ActionRequest request = null)
        {
            var bosses = provoke ? new[] { target } : _partyEnmity.Keys.Where(boss => boss.IsActive && boss.Entity.HP != 0).ToArray();
            foreach (var boss in bosses)
            {
                var scores = new Dictionary<SimActor, float>(_partyEnmity[boss]);
                if (provoke)
                {
                    var first = OrderedPartyEnmity(scores).FirstOrDefault(actor => actor.Entity.HP != 0);
                    scores[source] = (first == null ? 0 : scores[first]) + 20;
                }
                else
                {
                    if (!scores.TryGetValue(source, out var before) || before == 0)
                        continue;
                    scores.TryGetValue(target, out var received);
                    var amount = before * 0.25f;
                    scores[source] = before - amount;
                    scores[target] = received + amount;
                }

                var leader = OrderedPartyEnmity(scores).First(actor => actor.Entity.HP != 0);
                var percentages = Party.Select(actor => actor == leader ? 100 : actor.IsActive && actor.Entity.HP != 0 &&
                    scores.TryGetValue(actor, out var value) ? Math.Min(99, (int)(100 * value / scores[leader])) : 0).ToArray();
                PublishPartyEnmity(boss, scores, percentages);
            }

            var action = request?.ActionId ?? (uint)(provoke ? GeneralActionId.挑衅 : GeneralActionId.退避);
            ResolveAction(source.Entity, request, target.Entity, action);
        }

        internal void ResolveAction<TActionEnum>(Character source, ActionRequest request, Character target, TActionEnum action,
            params ActionEffect[] requestEffects) where TActionEnum : struct, Enum
            => ResolveAction(source, request, target, Convert.ToUInt32(action), requestEffects);

        internal void ResolveAction(Character source, ActionRequest request, Character target, uint actionId,
            params ActionEffect[] requestEffects)
        {
            var animationLock = 0f;
            if (request == null)
                source.ExecuteActionEffect(actionId, target);
            else
            {
                var hasCastTime = !request.HasInstantCastProc &&
                    GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Action>().TryGetRow(actionId, out var actionRow) &&
                    actionRow.Cast100ms > 0;
                animationLock = hasCastTime ? 0.1f : 0.6f;
                source.ReceiveActionResult(request, actionId, target, target?.Pos3D ?? source.Pos3D, animationLock, requestEffects);
            }

            var missed = target == null && request?.Kind == ActionRequestKind.Normal;
            CombatLog?.Ability(source, actionId, missed ? source : target, null, request?.Facing, animationLock,
                missed ? Array.Empty<SimActor>() : null, null, request == null ? null : requestEffects);

            // 时间轴触发的本机技能需补播动作，网络假角色由结算演出驱动。
            if (request == null && source.Identity.Slot == 0 &&
                GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Action>().TryGetRow(actionId, out var action))
                source.PlayBlendTimeline((ushort)action.AnimationEnd.RowId);
        }

        internal static (uint Action, ushort Status) GetInvulnerability(Job job)
        {
            switch (job)
            {
                case Job.PLD:
                    return (30, 82);
                case Job.WAR:
                    return (43, 409);
                case Job.DRK:
                    return (3638, 810);
                case Job.GNB:
                    return (16152, 1836);
                default:
                    return default;
            }
        }

        /// <summary>只演出本职业无敌；伤害判定必须显式允许无敌，不能全局免除机制错误。</summary>
        public bool UseInvulnerability(SimActor actor) => UseInvulnerability(actor, null);

        internal bool UseInvulnerability(SimActor actor, ActionRequest request)
        {
            var action = GetInvulnerability(actor.Job);
            return action.Action != 0 && UseStatusAction(actor, action.Action, action.Status, 10, request);
        }

        /// <summary>四人水分摊、五人神圣等人数不足时，所有实际参与者获得死亡刻印。</summary>
        public void MarkUnderfilledStack(SimActor[] participants, int required, SimActor source)
        {
            if (participants.Length >= required)
                return;
            lock (_gate)
            {
                foreach (var actor in participants)
                {
                    var status = _statuses.FirstOrDefault(s => s.Actor.Identity == actor.Entity.Identity && s.StatusId == 4372);
                    var stacks = status == null ? 0 : actor.Entity.Native.StatusManager.GetStatus(status.Slot).Param.Get();
                    SetStatus(actor, 4372, 600, (ushort)(stacks + 1), source);
                }
            }
        }

        /// <summary>模拟职能防击退：施加六秒状态和技能演出，不向服务器使用技能。</summary>
        public void UseKnockbackImmunity(SimActor actor, bool? surecast = null)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));

            if (surecast ?? (SimJobActions.For(actor.Job)?.Caster == true))
                UseStatusAction(actor, GeneralActionId.沉稳咏唱, GeneralStatusId.沉稳咏唱, 6);
            else
                UseStatusAction(actor, GeneralActionId.亲疏自行, GeneralStatusId.亲疏自行, 6);
        }

        private bool UseStatusAction<TActionEnum, TStatusEnum>(SimActor actor, TActionEnum action, TStatusEnum status, float seconds)
            where TActionEnum : struct, Enum
            where TStatusEnum : struct, Enum
            => UseStatusAction(actor, Convert.ToUInt32(action), Convert.ToUInt16(status), seconds);

        internal bool UseStatusAction(SimActor actor, uint action, ushort status, float seconds, ActionRequest request = null,
            byte param = 0)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));

            lock (_gate)
            {
                EnsureAcceptingEntities();
                EnsureStatusActor(actor);
                if (request == null && actor.Entity.AreActionsLocked())
                    return false;

                var effect = SetActionStatus(actor, actor, status, seconds, param);
                ResolveAction(actor.Entity, request, actor.Entity, action, request == null ? null : new[] { effect });
                return true;
            }
        }

        internal ActionEffect SetActionStatus(SimActor source, SimActor target, ushort status, float seconds,
            byte param, byte effectParam = 0)
        {
            SetStatus(target, status, seconds, param, source);
            return new ActionEffect
            {
                Type = 0x0E,
                Param0 = effectParam,
                Param2 = param,
                Value = status
            };
        }

        private void SetSprintEffect(SimActor actor, bool enabled)
        {
            const ushort sprint = (ushort)GeneralStatusId.冲刺;

            lock (_gate)
            {
                if (enabled)
                {
                    if (_acceptingEntities && actor.IsActive)
                        SetStatus(actor, sprint, 10, 0, actor);
                }
                else
                    TryCleanup(() => RemoveStatus(actor, sprint));
            }
        }

        /// <summary>施加本会话的状态，到期自动移除；传入 0 秒表示常驻。</summary>
        public void SetStatus(SimActor actor, ushort statusId, float seconds, ushort param, SimActor source)
            => SetStatus(actor, statusId, seconds, param, source, lockMovement: null);

        internal void SetStatus(SimActor actor, ushort statusId, float seconds, ushort param, SimActor source, bool? lockMovement)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));

            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (statusId == 0)
                throw new ArgumentOutOfRangeException(nameof(statusId), "状态编号不能为零。");

            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds), "状态持续时间必须为非负有限秒数，0 表示常驻。");

            lock (_gate)
            {
                EnsureAcceptingEntities();
                EnsureStatusActor(actor);
                EnsureStatusActor(source);

                if (StatusManager.SetStatusFuncPtr == IntPtr.Zero || StatusManager.RemoveStatusFuncPtr == IntPtr.Zero)
                    throw new InvalidOperationException("状态设置或移除函数尚未定位，请重新初始化。");

                var owned = _statuses.FirstOrDefault(s => s.Actor.Identity == actor.Entity.Identity && s.StatusId == statusId);
                var statuses = actor.Entity.Native.StatusManager;

                if (owned != null)
                {
                    var current = statuses.GetStatus(owned.Slot);

                    if (current.StatusId.Get() == 0)
                    {
                        ForgetStatus(owned);
                        owned = null;
                    }
                    else if (!MatchesStatus(current, owned))
                    {
                        ForgetStatus(owned);
                        throw new InvalidOperationException("模拟状态槽已被其他状态替换，拒绝覆盖。");
                    }
                    else if (owned.Source.Identity != source.Entity.Identity)
                        throw new InvalidOperationException("同一模拟状态不能在仍生效时更换来源实体。");
                }

                if (owned == null)
                {
                    var slot = -1;

                    for (var i = 0; i < 30; i++)
                        if (statuses.GetStatus(i).StatusId.Get() == 0)
                        {
                            slot = i;
                            break;
                        }

                    if (slot < 0)
                        throw new InvalidOperationException("角色前 30 个状态槽已满，无法添加模拟点名。");

                    owned = new OwnedStatus
                    {
                        Actor = actor.Entity,
                        Source = source.Entity,
                        Slot = slot,
                        StatusId = statusId,
                        Participant = actor
                    };
                    // 原生调用可能部分成功后抛错；写入之前即登记恢复责任。
                    _statuses.Add(owned);
                }

                var found = GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Status>().TryGetRow(statusId, out var row);
                var expiresAt = seconds > 0 ? _scheduler.ElapsedSeconds + seconds : double.PositiveInfinity;

                // 返回值只表示编号或来源有变化；同一状态刷新时间和参数会正常返回 false。
                var identityChanged = statuses.SetStatus(owned.Slot, statusId, seconds, param,
                    new GameObjectId
                    {
                        ObjectId = source.Id
                    }, true);
                var actual = actor.Entity.Native.StatusManager.GetStatus(owned.Slot);
                var actualId = actual.StatusId.Get();
                var actualSource = actual.SourceObject.Get().Id;
                var actualParam = actual.Param.Get();

                if (actualId != statusId || actualSource != source.Id || actualParam != param)
                    throw new InvalidOperationException("原生状态设置后未读到预期点名，已保留清理责任。" +
                        $"目标 0x{actor.Id:X8}，槽 {owned.Slot}；" +
                        FormattableString.Invariant($"期望编号 0x{statusId:X4}、参数 {param}、来源 0x{source.Id:X16}、时间 {seconds:F3} 秒；") +
                        FormattableString.Invariant($"实际编号 0x{actualId:X4}、参数 {actualParam}、来源 0x{actualSource:X16}、剩余 {actual.RemainingTime.Get():F3} 秒；") +
                        $"原生编号或来源变化标志：{(identityChanged ? "是" : "否")}。");

                owned.LockMovement = lockMovement ?? (found && row.LockMovement);
                owned.ExpiresAt = expiresAt;
                owned.Expiry?.Cancel();
                owned.Expiry = seconds > 0 ? ScheduleStatusExpiry(owned, expiresAt) : null;
                owned.LoggedParam = null;
                if (CombatLog?.Enabled == true)
                {
                    CombatLog.Status(true, statusId, seconds, param, source.Entity, actor.Entity);
                    owned.LoggedParam = param;
                }
                RefreshMovementLocks();
            }
        }

        private SimScheduledAction ScheduleStatusExpiry(OwnedStatus owned, double expiresAt)
            => _scheduler.Schedule(expiresAt, () =>
            {
                lock (_gate)
                {
                    if (owned.Expiry == null || owned.ExpiresAt != expiresAt)
                        return;

                    RemoveOwnedStatus(owned);
                    RefreshMovementLocks();
                }
            }, "移除到期模拟状态");

        private void ForgetStatus(OwnedStatus owned)
        {
            _statuses.Remove(owned);
            owned.Expiry?.Cancel();
            owned.Expiry = null;
            if (owned.LoggedParam.HasValue && owned.Actor.IsAlive)
                CombatLog?.Status(false, owned.StatusId, 0, owned.LoggedParam.Value, owned.Source, owned.Actor);
            owned.LoggedParam = null;
        }

        /// <summary>只移除本会话施加的同种状态，不触碰原有或后来替换的状态。</summary>
        public void RemoveStatus(SimActor actor, ushort statusId)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));

            lock (_gate)
            {
                foreach (var owned in _statuses.Where(s => s.Actor.Identity == actor.Entity.Identity && s.StatusId == statusId).ToArray())
                    RemoveOwnedStatus(owned);

                RefreshMovementLocks();
            }
        }

        /// <summary>查询仍由本会话持有的状态层数和剩余时间。</summary>
        internal bool TryGetOwnedStatus(SimActor actor, ushort statusId, out ushort param, out float seconds)
        {
            lock (_gate)
            {
                var owned = _statuses.FirstOrDefault(status => status.Actor.Identity == actor.Entity.Identity &&
                    status.StatusId == statusId);
                if (owned != null && actor.Entity.IsAlive)
                {
                    var current = actor.Entity.Native.StatusManager.GetStatus(owned.Slot);
                    var remaining = owned.ExpiresAt - _scheduler.ElapsedSeconds;
                    if (remaining > 0 && MatchesStatus(current, owned))
                    {
                        param = current.Param.Get();
                        seconds = double.IsPositiveInfinity(remaining) ? 0 : (float)remaining;
                        return true;
                    }
                }
                param = 0;
                seconds = 0;
                return false;
            }
        }

        private void EnsureStatusActor(SimActor actor)
        {
            if (!actor.IsActive || !_actors.TryGetValue(actor.Entity.Identity, out var current) || !ReferenceEquals(current, actor))
                throw new InvalidOperationException("状态的目标和来源必须是本会话的活动角色。");
        }

        private static bool MatchesStatus(Status status, OwnedStatus owned)
            => status.StatusId.Get() == owned.StatusId && status.SourceObject.Get().Id == owned.Source.Id;

        private void RemoveOwnedStatus(OwnedStatus owned)
        {
            // 实体身份包含世界与生命周期；失效后绝不触碰被复用的地址。
            if (!owned.Actor.IsAlive)
            {
                ForgetStatus(owned);
                return;
            }

            var statuses = owned.Actor.Native.StatusManager;
            var current = statuses.GetStatus(owned.Slot);

            if (!MatchesStatus(current, owned))
            {
                ForgetStatus(owned);
                return;
            }

            if (StatusManager.RemoveStatusFuncPtr == IntPtr.Zero)
                throw new InvalidOperationException("状态移除函数尚未定位，已保留模拟点名以便重试清理。");

            statuses.RemoveStatus(owned.Slot);

            if (!owned.Actor.IsAlive)
            {
                ForgetStatus(owned);
                return;
            }

            if (MatchesStatus(owned.Actor.Native.StatusManager.GetStatus(owned.Slot), owned))
                throw new InvalidOperationException("原生状态移除后模拟点名仍存在，请重试停止。");

            ForgetStatus(owned);
        }

        private void ClearStatuses(EntityBase dependency = null)
        {
            lock (_gate)
            {
                List<Exception> errors = null;

                foreach (var owned in _statuses.Where(s => dependency == null ||
                    s.Actor.Identity == dependency.Identity || s.Source.Identity == dependency.Identity).ToArray())
                {
                    try
                    {
                        RemoveOwnedStatus(owned);
                    }
                    catch (Exception error)
                    {
                        if (errors == null)
                            errors = new List<Exception>();

                        errors.Add(error);
                    }
                }

                RefreshMovementLocks();

                if (errors != null)
                    throw new AggregateException("部分模拟点名清理失败，已保留实体与清理责任。", errors);
            }
        }

        private bool HasStatusDependency(EntityBase entity)
            => _statuses.Any(s => s.Actor.Identity == entity.Identity || s.Source.Identity == entity.Identity);
    }
}
