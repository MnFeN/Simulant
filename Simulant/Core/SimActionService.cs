using Simulant.ACT;
using Simulant.Core.Entity;
using Simulant.Game;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Runtime.PlayerActions;
using Simulant.Simulation.Timeline;
using System;
using System.Numerics;
using System.Threading;
using ActionRow = Lumina.Excel.Sheets.Action;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Core
{
    /// <summary>防火墙期间的本机模拟技能，独立于副本的启动、停止和判定。</summary>
    internal sealed class SimActionService
    {
        private readonly PluginHost _host;
        private readonly object _gate = new object();
        private SimScheduler _scheduler;
        private SimEntityManager _entities;
        private SimEntityManager _encounterEntities;
        private ActionRequest _lastActionRequest;
        private (EntityIdentity Player, uint Sequence, Vector2 Position)? _castOrigin;
        private (EntityIdentity Player, ActionRequest Request, bool Observed)? _pendingCast;

        internal SimActionService(PluginHost host) => _host = host;

        internal SimEntityManager Entities => _entities;
        internal SimEntityManager EncounterEntities => Volatile.Read(ref _encounterEntities);

        private bool Enabled => _host.FirewallEnabled && _host.PluginReady && !_host.Disposed && !_host.IsSwitchingTerritory;

        internal void BeginSimulation(SimEntityManager encounterEntities = null)
        {
            if (Enabled)
                Volatile.Write(ref _encounterEntities, encounterEntities);
        }

        internal void EndSimulation(SimEntityManager encounterEntities)
        {
            Interlocked.CompareExchange(ref _encounterEntities, null, encounterEntities);
        }

        internal void ResetCooldowns()
        {
            if (!_host.FirewallEnabled)
                return;

            try
            {
                var connection = GameInterop.Connection;
                connection?.RunInFrame(() => ClearCooldownGroups(ActionManager.Instance.ThrowIfNull(), connection));
            }
            catch (Exception error)
            {
                _host.LogWarning("重置技能冷却失败：" + error.Message);
            }
        }

        private static void ClearCooldownGroups(ActionManager manager, GameConnection connection)
        {
            var cooldowns = manager._cooldowns;
            var emptyCooldowns = new byte[cooldowns.Length * RecastDetail.Size];
            if (connection.Memory.WriteBytes(cooldowns.Ptr, emptyCooldowns) != emptyCooldowns.Length)
                throw new InvalidOperationException("未能完整重置技能冷却。");
        }

        private void EnsureScheduler()
        {
            if (_scheduler != null)
                return;
            _scheduler = new SimScheduler((error, node) => _host.LogWarning("模拟技能失败：" + error.Message));
            _entities = new SimEntityManager(_host.EntitySpawner, error => _host.LogWarning("清理模拟技能状态失败：" + error.Message))
            {
                Scheduler = _scheduler,
                CombatLog = new SimCombatLog(() => _host.Configuration.GenerateActLogs, XivNetworkLog.WriteLine, _host.LogWarning)
            };
            _scheduler.Start();
        }

        /// <summary>按发送顺序处理本机技能请求，并检查本机读条期间的位移。</summary>
        internal void HandleActionRequests(ActionRequest[] requests, uint dropped)
        {
            ushort cancelSequence = 0;
            foreach (var request in requests)
            {
                _host.LogVerbose(request.ToString());
                if (request.Kind == ActionRequestKind.CancelCast)
                    cancelSequence = _lastActionRequest?.SourceSequence ?? 0;
                else
                    _lastActionRequest = request;
            }

            // 主动取消包没有请求序号。丢包时无法确定它前面的技能请求，跳过这批主动取消。
            if (dropped != 0)
            {
                _lastActionRequest = null;
                cancelSequence = 0;
            }

            var player = Enabled ? _host.EntityProvider.GetMyself() : null;
            if (player == null)
            {
                ClearCastTracking();
                return;
            }

            var previousPending = _pendingCast?.Request;
            QueueCapturedActions(player, requests);
            var cast = player.Native.CastInfo;
            var sequence = cast.SourceSequence.Get();
            var actionId = cast.ActionId.Get();
            var actionType = cast.ActionType.Get();
            var requestForCast = _lastActionRequest;
            var casting = (bool)cast.IsCasting;
            if (_pendingCast.HasValue)
            {
                var pending = _pendingCast.Value;
                var request = pending.Request;
                if (dropped != 0 || pending.Player != player.Identity || cancelSequence == request.SourceSequence ||
                    (ushort)cast.ResponseSourceSequence.Get() == request.SourceSequence)
                {
                    _pendingCast = null;
                }
                else if (casting && sequence == request.SourceSequence && actionId == request.ActionId &&
                    actionType == (byte)request.ActionType)
                {
                    if (RejectLockedAction(player, request))
                    {
                        _pendingCast = null;
                        _castOrigin = null;
                        return;
                    }

                    _pendingCast = (pending.Player, request, true);
                    requestForCast = request;
                }
                else if (ReferenceEquals(request, previousPending))
                {
                    _pendingCast = null;
                    // 请求被采集时原生函数可能还没有写入 CastInfo，瞬发在下一次轮询确认。
                    if (!casting && (!pending.Observed || sequence == request.SourceSequence && actionId == request.ActionId &&
                        cast.CurrentCastTime.Get() >= cast.TotalCastTime.Get()))
                        QueueCapturedResult(player, request);
                }
            }
            if (!casting || sequence == 0 || (ushort)cast.ResponseSourceSequence.Get() == sequence ||
                requestForCast == null || requestForCast.SourceSequence != sequence ||
                requestForCast.ActionId != actionId || (byte)requestForCast.ActionType != actionType)
            {
                _castOrigin = null;
                return;
            }

            var position = player.Pos;
            var origin = _castOrigin;
            if (!origin.HasValue || origin.Value.Player != player.Identity || origin.Value.Sequence != sequence)
            {
                origin = (player.Identity, sequence, position);
                _castOrigin = origin;
            }

            var byRequest = cancelSequence == sequence;
            var slidecastWindow = _host.Configuration.SlidecastWindow;
            var remaining = cast.TotalCastTime.Get() - cast.CurrentCastTime.Get();
            // 用施法期间的水平位置变化判定，转向和亚厘米的坐标抖动不计入。
            var byMovement = remaining > slidecastWindow &&
                Vector2.DistanceSquared(position, origin.Value.Position) > 0.0001f;
            if (!Enabled)
                return;
            if (!byRequest && !byMovement)
            {
                if (remaining <= slidecastWindow && _pendingCast?.Request.SourceSequence == sequence)
                {
                    var request = _pendingCast.Value.Request;
                    _pendingCast = null;
                    QueueCapturedResult(player, request);
                }
                return;
            }

            var cancelled = false;
            GameInterop.Connection?.RunInFrame(() =>
            {
                // 同一帧核对并取消，避免期间开始新读条、确认技能或走进滑步窗口。
                if (!cast.IsCasting || cast.SourceSequence.Get() != sequence || cast.ActionId.Get() != actionId ||
                    (ushort)cast.ResponseSourceSequence.Get() == sequence ||
                    (!byRequest && cast.TotalCastTime.Get() - cast.CurrentCastTime.Get() <= slidecastWindow))
                    return;

                player.ActorControl(ActorControlCategory.CancelCast, a2: actionType, a3: actionId, a4: 1);
                cancelled = true;
            });
            if (cancelled)
            {
                if (_pendingCast?.Request.SourceSequence == sequence)
                    _pendingCast = null;
                _host.LogVerbose($"[咏唱取消] 技能={actionId} 序号={sequence} 原因={(byRequest ? "主动取消" : "移动")}");
            }
        }

        internal void ClearCastTracking()
        {
            _lastActionRequest = null;
            _castOrigin = null;
            _pendingCast = null;
        }

        private void QueueCapturedActions(Character player, ActionRequest[] requests)
        {
            foreach (var request in requests)
            {
                if (request.Kind == ActionRequestKind.CancelCast || request.SourceSequence == 0 ||
                    (request.ActionType != ActionType.Action && request.ActionType != ActionType.GeneralAction))
                    continue;

                if (RejectLockedAction(player, request))
                    continue;

                var hasBaseCastTime = request.ActionType == ActionType.Action
                    ? GameDataService.Instance.GetSheet<ActionRow>().TryGetRow(request.ActionId, out var action) && action.Cast100ms > 0
                    : EncounterEntities?.MayCastLimitBreak(request) == true;
                if (hasBaseCastTime && !request.HasInstantCastProc)
                    _pendingCast = (player.Identity, request, false);
                else
                    QueueCapturedResult(player, request);
            }
        }

        private void QueueCapturedResult(Character player, ActionRequest request)
        {
            lock (_gate)
            {
                if (!Enabled)
                    return;
                EnsureScheduler();
                _scheduler.ScheduleAfter(0, () => ExecuteCapturedAction(player, request), "结算本机技能请求");
            }
        }

        private void ExecuteCapturedAction(Character player, ActionRequest request)
        {
            if (!Enabled || !player.IsAlive || _host.EntityProvider.GetMyself()?.Identity != player.Identity || player.HP == 0)
                return;

            // 技能请求入队后可能受到眩晕，结算前再核对一次。
            if (RejectLockedAction(player, request))
                return;

            var encounter = EncounterEntities;
            if (encounter?.TryUseLimitBreak(player, request) == true)
                return;
            if (request.Kind != ActionRequestKind.Normal)
                return;

            if (ReferenceEquals(PluginHost.Instance, _host) && GeneralActions.TryGetResult(request))
                return;
            if (request.ActionType != ActionType.Action)
                return;
            if (player.Level == 100 && TryGetJobResult(player.Job, request))
                return;

            if (request.ActionId == SimEntityManager.GetInvulnerability(player.Job).Action &&
                !_entities.UseInvulnerability(_entities.Attach(player), request))
                player.RejectActionResult(request, 582);
        }

        private static bool RejectLockedAction(Character player, ActionRequest request)
        {
            if (!player.AreActionsLocked())
                return false;

            player.RejectActionResult(request, 572);
            return true;
        }

        private bool TryGetJobResult(Job job, ActionRequest request)
        {
            switch (job)
            {
                case Job.PLD:
                    return ReferenceEquals(PluginHost.Instance, _host) && PaladinActions.TryGetResult(request);
                default:
                    return false;
            }
        }

        internal void Reset()
        {
            lock (_gate)
            {
                // 回调不获取此锁，先等待在途调用完成，再清除自己施加的状态。
                try
                {
                    _scheduler?.Dispose();
                    if (ReferenceEquals(PluginHost.Instance, _host))
                        PaladinActions.CancelSpellComboExpiry();
                    _entities?.Clear();
                }
                finally
                {
                    _scheduler = null;
                    _entities = null;
                    Volatile.Write(ref _encounterEntities, null);
                    ClearCastTracking();
                }
            }
        }
    }
}
