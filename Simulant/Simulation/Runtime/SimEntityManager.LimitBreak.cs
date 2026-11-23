using Simulant.Core.Entity;
using Simulant.Core.Party;
using Simulant.Game;
using Simulant.Game.Data;
using Simulant.Simulation.Timeline;
using System;
using System.Linq;
using System.Numerics;
using ActionRow = Lumina.Excel.Sheets.Action;
using ClassJob = Lumina.Excel.Sheets.ClassJob;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Simulation.Runtime
{
    public sealed partial class SimEntityManager
    {
        private const uint LimitBreakBarUnits = 10000;
        private const uint LimitBreakHudTenths = 37;
        private const double LimitBreakChargeInterval = 3;
        private const uint LimitBreakChargeUnits = 220;
        // 两份潘多拉魔盒回放均由施放者约 1.35 秒先获得减伤，随后逐个确认队员效果。
        private const double TankLimitBreakFirstStatusDelay = 1.35;
        private const double TankLimitBreakStatusInterval = 0.137;
        private SimActor _limitBreakPlayer;
        private uint[] _limitBreakActions;
        private uint _limitBreakBars;
        private uint _limitBreakUnits;
        private bool _limitBreakPending;
        private uint _lastLimitBreakAction;
        private ushort _tankLimitBreakStatus;
        private SimScheduledAction _limitBreakChargeTick;

        /// <summary>战斗中默认自然充能。过场可暂时关闭，开关不影响机制直接补充量表。</summary>
        internal bool LimitBreakAutoChargeEnabled { get; set; } = true;

        internal static uint[] LimitBreakActions(ClassJob job)
            => new[] { job.LimitBreak1.RowId, job.LimitBreak2.RowId, job.LimitBreak3.RowId };

        internal static bool IsTankJob(Job job)
            => GameDataService.Instance.GetSheet<ClassJob>().TryGetRow((uint)job, out var row) && row.Role == 1;

        internal (uint Action, bool Ready) LimitBreakButton
        {
            get
            {
                lock (_gate)
                {
                    if (_limitBreakActions == null)
                        return default;
                    var level = (int)(_limitBreakUnits / LimitBreakBarUnits);
                    var action = _limitBreakActions[level == 0 ? (int)_limitBreakBars - 1 : level - 1];
                    return (action, action != 0 && level != 0 && !_limitBreakPending && _acceptingEntities && _scheduler.IsRunning);
                }
            }
        }

        internal bool TankLimitBreakActive
        {
            get
            {
                lock (_gate)
                {
                    if (_tankLimitBreakStatus == 0)
                        return false;

                    var now = _scheduler.ElapsedSeconds;
                    return Party.All(actor => _statuses.Any(status => status.Participant == actor &&
                        status.StatusId == _tankLimitBreakStatus && status.ExpiresAt > now));
                }
            }
        }

        internal void ShowLimitBreakGauge(SimActor player, uint bars, uint units)
        {
            if (bars < 1 || bars > 3)
                throw new ArgumentOutOfRangeException(nameof(bars), "极限技量表需有一至三格。");

            lock (_gate)
            {
                EnsureAcceptingEntities();
                _limitBreakPlayer = player;
                _limitBreakActions = LimitBreakActions(GameDataService.Instance.GetSheet<ClassJob>().GetRow((uint)player.Job));
                _limitBreakBars = bars;
                _limitBreakUnits = Math.Min(units, bars * LimitBreakBarUnits);
                PublishLimitBreakGauge();
                QueueLimitBreakCharge();
            }
        }

        private void QueueLimitBreakCharge()
        {
            if (_limitBreakChargeTick != null || _limitBreakPlayer?.IsActive != true || !_behaviors.InCombat)
                return;

            SimScheduledAction tick = null;
            tick = _scheduler.ScheduleRelativeToNode(LimitBreakChargeInterval, () => ChargeLimitBreak(tick), "极限技槽自然充能");
            _limitBreakChargeTick = tick;
        }

        private void ChargeLimitBreak(SimScheduledAction tick)
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_limitBreakChargeTick, tick))
                    return;

                _limitBreakChargeTick = null;
                if (!_acceptingEntities || !_scheduler.IsRunning || !_behaviors.InCombat)
                    return;

                if (LimitBreakAutoChargeEnabled)
                    GainLimitBreak(LimitBreakChargeUnits);
                QueueLimitBreakCharge();
            }
        }

        private void StopLimitBreakCharge()
        {
            _limitBreakChargeTick?.Cancel();
            _limitBreakChargeTick = null;
        }

        internal void GainLimitBreak(uint units, uint presentation = 0)
        {
            lock (_gate)
            {
                if (_limitBreakPlayer?.IsActive != true)
                    return;
                var gained = Math.Min(units, _limitBreakBars * LimitBreakBarUnits - _limitBreakUnits);
                if (gained == 0 && presentation == 0)
                    return;
                _limitBreakUnits += gained;
                PublishLimitBreakGauge(presentation);
            }
        }

        private void PublishLimitBreakGauge(uint presentation = 0)
            => _limitBreakPlayer.ActorControlSelf(ActorControlCategory.LimitBreakGauge,
                _limitBreakBars, _limitBreakUnits, LimitBreakBarUnits, presentation);

        internal bool TryUseLimitBreak(Character player, uint action)
        {
            lock (_gate)
                return _limitBreakPlayer?.Entity.Identity == player.Identity && QueueLimitBreak(_limitBreakPlayer, action, 1);
        }

        internal bool UsePartyLimitBreak(SimActor actor, int requiredLevel)
        {
            lock (_gate)
                return QueueLimitBreak(actor, 0, requiredLevel);
        }

        private bool QueueLimitBreak(SimActor source, uint requestedAction, int requiredLevel)
        {
            var level = (int)(_limitBreakUnits / LimitBreakBarUnits);
            if (!_acceptingEntities || !_scheduler.IsRunning || _limitBreakPending || level < requiredLevel ||
                source?.IsActive != true || source.Entity.HP == 0 || !Party.Contains(source))
                return false;

            var data = GameDataService.Instance;
            var job = data.GetSheet<ClassJob>().GetRow((uint)source.Job);
            var id = LimitBreakActions(job)[level - 1];
            if (id == 0 || requestedAction != 0 && requestedAction != id || ActionsLocked(source) ||
                source == _limitBreakPlayer && _casts.HasPendingAction(source))
                return false;

            var action = data.GetSheet<ActionRow>().GetRow(id);
            var hostile = action.CanTargetHostile || action.TargetArea;
            SimActor target = source;
            if (hostile)
            {
                var enemies = _partyEnmity.Keys.Where(actor => actor.IsActive && actor.Entity.HP != 0 && actor.Entity.IsTargetable).ToArray();
                var softTarget = SimPartyService.GetSelectedTarget(preferSoftTarget: true);
                var hardTarget = SimPartyService.GetSelectedTarget();
                target = enemies.FirstOrDefault(actor => actor.Address == softTarget) ??
                    enemies.FirstOrDefault(actor => actor.Address == hardTarget) ?? enemies.FirstOrDefault();
                if (target == null)
                    return false;
            }

            _scheduler.ScheduleAfter(0, () => BeginLimitBreak(source, target, action, level, hostile), "开始模拟极限技");
            _limitBreakPending = true;
            return true;
        }

        private void BeginLimitBreak(SimActor source, SimActor target, ActionRow action, int level, bool hostile)
        {
            lock (_gate)
            {
                if (!source.IsActive || !target.IsActive || source.Entity.HP == 0 ||
                    hostile && (target.Entity.HP == 0 || !target.Entity.IsTargetable))
                {
                    _limitBreakPending = false;
                    return;
                }

                var point = target.Pos3D;
                var facing = hostile ? (float)Math.Atan2(point.X - source.Pos.X, point.Y - source.Pos.Y) : source.Heading;
                var duration = SimCastController.CastDuration(action.Cast100ms);
                if (duration <= 0)
                {
                    FinishLimitBreak(source, target, action, level, point, facing, hostile);
                    return;
                }

                var player = _limitBreakPlayer;
                _lastLimitBreakAction = action.RowId;
                player.ActorControl(ActorControlCategory.LimitBreakStart, action.RowId, source == player ? 0u : 1u);
                source.Cast(action.RowId, target: target, castTime: duration, omenPosition: action.TargetArea ? (Vector3?)point : null,
                    omenHeading: facing, executeOnFinish: false);
                _scheduler.ScheduleRelativeToNode(duration + 0.3,
                    () => FinishLimitBreak(source, target, action, level, point, facing, hostile), "模拟极限技结算");
            }
        }

        private void FinishLimitBreak(SimActor source, SimActor target, ActionRow action, int level, Vector3 point, float heading, bool hostile)
        {
            lock (_gate)
            {
                _limitBreakPending = false;
                if (!source.IsActive || !target.IsActive || source.Entity.HP == 0 ||
                    hostile && (target.Entity.HP == 0 || !target.Entity.IsTargetable))
                {
                    _limitBreakPlayer.ActorControl(ActorControlCategory.EndLimitBreakHud, action.RowId);
                    return;
                }

                var tank = IsTankJob(source.Job);
                var animationLock = tank ? 3.86f : 0.6f;
                var hits = hostile ? new[] { target } : new[] { source }.Concat(Party.Where(member => member != source)).ToArray();
                if (action.TargetArea)
                    source.ExecuteActionEffectAt(action.RowId, point, heading, animationLock, hits, 8);
                else
                    source.ExecuteActionEffect(action.RowId, target, heading, hits, hostile ? 1 : 8, animationLock);
                if (tank)
                    ScheduleTankLimitBreakStatuses(source, hits, level, action.RowId);

                if (source == _limitBreakPlayer)
                    source.Entity.PlayBlendTimeline((ushort)action.AnimationEnd.RowId);

                _limitBreakUnits = 0;
                _lastLimitBreakAction = action.RowId;
                // 同帧扣槽并通知 HUD 开始闪烁，倒计时结束后由客户端显示最新量表。
                var player = _limitBreakPlayer;
                GameInterop.Connection.RunInFrame(() =>
                {
                    PublishLimitBreakGauge();
                    player.ActorControl(ActorControlCategory.LimitBreakPartyStart, action.RowId, player == source ? 0u : 1u, LimitBreakHudTenths);
                });
                foreach (var member in Party.Where(member => member != player && member.IsActive))
                    member.ActorControl(ActorControlCategory.LimitBreakPartyStart, action.RowId, member == source ? 0u : 1u, LimitBreakHudTenths);
            }
        }

        private void ScheduleTankLimitBreakStatuses(SimActor source, SimActor[] targets, int level, uint action)
        {
            var releaseTime = _scheduler.ElapsedSeconds;
            var status = level == 1 ? (ushort)194 : level == 2 ? (ushort)195 : TankLimitBreakStatus(action);
            var duration = level == 1 ? 10f : level == 2 ? 15f : 8f;
            if (level == 3)
                _tankLimitBreakStatus = status;

            for (var index = 0; index < targets.Length; index++)
            {
                var member = targets[index];
                _scheduler.Schedule(releaseTime + TankLimitBreakFirstStatusDelay + index * TankLimitBreakStatusInterval, () =>
                {
                    if (member.IsActive && source.IsActive)
                        SetStatus(member, status, duration, 0, source);
                }, "施加坦克极限技减伤");
            }
        }

        private static ushort TankLimitBreakStatus(uint action)
        {
            switch (action)
            {
                case 199: // 终极堡垒
                    return 196;
                case 4240: // 原初大地
                    return 863;
                case 4241: // 暗黑之力
                    return 864;
                case 17105: // 灵魂之青
                    return 1931;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), "技能没有对应的三级坦克极限技减伤。");
            }
        }

        private void ClearLimitBreakGauge()
        {
            lock (_gate)
            {
                StopLimitBreakCharge();
                if (_limitBreakPlayer?.IsActive == true)
                {
                    if (_lastLimitBreakAction != 0)
                        _limitBreakPlayer.ActorControl(ActorControlCategory.EndLimitBreakHud, _lastLimitBreakAction);
                    _limitBreakPlayer.ActorControlSelf(ActorControlCategory.LimitBreakGauge, 0, 0, LimitBreakBarUnits);
                }
                _lastLimitBreakAction = 0;
                _limitBreakPending = false;
                _limitBreakBars = 0;
                _limitBreakUnits = 0;
                _limitBreakPlayer = null;
                _limitBreakActions = null;
                _tankLimitBreakStatus = 0;
                LimitBreakAutoChargeEnabled = true;
            }
        }
    }
}
