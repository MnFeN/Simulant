using Simulant.Core.Entity;
using Simulant.Game;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;

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
        private SimHotbarInput _input;
        private SimActionRecasts _recasts;
        private bool _polling;
        private ActionRequest _lastActionRequest;
        private (EntityIdentity Player, uint Sequence, Vector2 Position)? _castOrigin;
        private Func<uint, bool> _enmityAction;
        private readonly ConcurrentDictionary<uint, SimActionCooldown> _cooldowns = new ConcurrentDictionary<uint, SimActionCooldown>();

        internal SimActionService(PluginHost host) => _host = host;

        internal bool CanUseEnmityAction(uint action) => SimEntityManager.IsEnmityAction(action) &&
            (Volatile.Read(ref _enmityAction) != null || Volatile.Read(ref _encounterEntities)?.CanUseEnmityAction(action) == true);

        internal void SetEnmityActionHandler(Func<uint, bool> handler)
            => Volatile.Write(ref _enmityAction, handler);

        internal (uint Action, bool Ready) LimitBreakButton
            => Volatile.Read(ref _encounterEntities)?.LimitBreakButton ?? default;

        private bool Enabled => _host.FirewallEnabled && _host.PluginReady && !_host.Disposed && !_host.IsSwitchingTerritory;

        // 亲疏自行与沉稳咏唱共用一个冷却组。
        private static uint CooldownKey(uint action) => action == (uint)SimLocalAction.Surecast ? (uint)SimLocalAction.ArmsLength : action;
        internal SimActionCooldown Cooldown(uint action) => _cooldowns.TryGetValue(CooldownKey(action), out var value) ? value : default;

        internal void BeginSimulation(SimEntityManager encounterEntities = null)
        {
            lock (_gate)
            {
                if (!Enabled)
                    return;
                Volatile.Write(ref _encounterEntities, encounterEntities);
                EnsureScheduler();
                _scheduler.ScheduleAfter(0, () =>
                {
                    _input = null;
                    _cooldowns.Clear();
                    if (!Enabled)
                        return;
                    try
                    {
                        var player = _host.EntityProvider.GetMyself();
                        if (player == null || !player.IsAlive)
                            return;
                        if (_recasts == null)
                            _recasts = new SimActionRecasts(player);
                        _recasts.Reset();
                    }
                    catch (Exception error)
                    {
                        _host.LogWarning("重置模拟技能冷却失败：" + error.Message);
                    }
                    try
                    {
                        // 只在本轮启动时读取键位、当前栏位和宏图标，途中不刷新映射。
                        _input = new SimHotbarInput();
                        if (!_polling)
                        {
                            _polling = true;
                            PollInput();
                        }
                    }
                    catch (Exception error)
                    {
                        _host.LogWarning("读取模拟技能快捷键失败，可使用悬浮窗或默语指令：" + error.Message);
                    }
                }, "缓存模拟技能快捷键并重置冷却");
            }
        }

        internal void EndSimulation(SimEntityManager encounterEntities)
        {
            Interlocked.CompareExchange(ref _encounterEntities, null, encounterEntities);
        }

        private void EnsureScheduler()
        {
            if (_scheduler != null)
                return;
            _scheduler = new SimScheduler((error, node) => _host.LogWarning("模拟技能失败：" + error.Message));
            _entities = new SimEntityManager(_host.EntitySpawner, error => _host.LogWarning("清理模拟技能状态失败：" + error.Message));
            _entities.Scheduler = _scheduler;
            _scheduler.Start();
        }

        private void PollInput()
        {
            if (!Enabled || _input == null)
            {
                _polling = false;
                return;
            }
            try
            {
                _input.Poll(Execute);
                _scheduler.ScheduleAfter(0.02, PollInput, "监听模拟技能按键");
            }
            catch (Exception error)
            {
                _polling = false;
                _input = null;
                _host.LogWarning("监听模拟技能按键失败，可使用悬浮窗或默语指令：" + error.Message);
            }
        }

        internal static uint ParseAction(string command, Game.FFCS.Client.Game.Character.Job job = Game.FFCS.Client.Game.Character.Job.ADV)
        {
            var parts = (command ?? "").Split(new[]
            {
                ' '
            }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !parts[0].Equals("ac", StringComparison.OrdinalIgnoreCase))
                return 0;

            switch (parts[1].Trim().Trim('"'))
            {
                case "沉稳咏唱":
                    return (uint)SimLocalAction.Surecast;
                case "亲疏自行":
                    return (uint)SimLocalAction.ArmsLength;
                case "冲刺":
                    return (uint)SimLocalAction.Sprint;
                case "挑衅":
                    return (uint)SimLocalAction.Provoke;
                case "退避":
                    return (uint)SimLocalAction.Shirk;
                case "无敌":
                    return SimEntityManager.InvulnerabilityAction(job);
                case "神圣领域":
                    return (uint)SimLocalAction.HallowedGround;
                case "死斗":
                    return (uint)SimLocalAction.Holmgang;
                case "行尸走肉":
                    return (uint)SimLocalAction.LivingDead;
                case "超火流星":
                    return (uint)SimLocalAction.Superbolide;
                default:
                    return 0;
            }
        }

        internal void Receive(string command)
        {
            var action = ParseAction(command, _host.EntityProvider.GetMyself()?.Job ?? Game.FFCS.Client.Game.Character.Job.ADV);
            RequestAction(action);
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

            var cast = player.Native.CastInfo;
            var sequence = cast.SourceSequence.Get();
            var actionId = cast.ActionId.Get();
            var actionType = cast.ActionType.Get();
            var requestForCast = _lastActionRequest;
            if (!cast.IsCasting || sequence == 0 || (ushort)cast.ResponseSourceSequence.Get() == sequence ||
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
            if ((!byRequest && !byMovement) || !Enabled)
                return;

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
                _host.LogVerbose($"[咏唱取消] 技能={actionId} 序号={sequence} 原因={(byRequest ? "主动取消" : "移动")}");
        }

        internal void ClearCastTracking()
        {
            _lastActionRequest = null;
            _castOrigin = null;
        }

        internal void RequestAction(uint action)
        {
            if (action == 0)
                return;

            lock (_gate)
            {
                if (!Enabled)
                    return;

                EnsureScheduler();
                _scheduler.ScheduleAfter(0, () => Execute(action), "执行本机模拟技能");
            }
        }

        private void Execute(uint action, bool isLimitBreak = false)
        {
            try
            {
                if (!Enabled || !isLimitBreak && Cooldown(action).Remaining > 0)
                    return;
                var player = _host.EntityProvider.GetMyself();
                if (player == null || !player.IsAlive)
                    return;
                if (Volatile.Read(ref _encounterEntities)?.TryUseLimitBreak(player, isLimitBreak ? 0 : action) == true || isLimitBreak)
                    return;
                if (_recasts == null)
                    _recasts = new SimActionRecasts(player);
                if (!_recasts.Supports(action))
                    return;
                if (SimEntityManager.IsEnmityAction(action))
                {
                    var handler = Volatile.Read(ref _enmityAction);
                    var accepted = handler != null ? handler(action) :
                        Volatile.Read(ref _encounterEntities)?.TryUseEnmityAction(player, action) == true;
                    if (!accepted)
                        return;
                }
                else if (!_entities.UseLocalAction(_entities.Attach(player), action))
                    return;
                _cooldowns[CooldownKey(action)] = new SimActionCooldown(_recasts.Duration(action));
                _recasts.Start(action);
            }
            catch (Exception error)
            {
                _host.LogWarning("执行模拟技能失败：" + error.Message);
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
                    try
                    {
                        _recasts?.Dispose();
                    }
                    catch (Exception error)
                    {
                        _host.LogWarning("恢复模拟前技能冷却失败：" + error.Message);
                    }
                    _entities?.Clear();
                }
                finally
                {
                    _scheduler = null;
                    _entities = null;
                    Volatile.Write(ref _encounterEntities, null);
                    _input = null;
                    _recasts = null;
                    SetEnmityActionHandler(null);
                    _polling = false;
                    _cooldowns.Clear();
                    ClearCastTracking();
                }
            }
        }
    }
}
