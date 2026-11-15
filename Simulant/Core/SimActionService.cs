using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Concurrent;

namespace Simulant.Core
{
    /// <summary>防火墙期间的本机模拟技能，独立于副本的启动、停止和判定。</summary>
    internal sealed class SimActionService
    {
        private readonly PluginHost _host;
        private readonly object _gate = new object();
        private SimScheduler _scheduler;
        private SimEntityManager _entities;
        private SimHotbarInput _input;
        private SimActionRecasts _recasts;
        private bool _polling;
        private readonly ConcurrentDictionary<uint, SimActionCooldown> _cooldowns = new ConcurrentDictionary<uint, SimActionCooldown>();

        internal SimActionService(PluginHost host) => _host = host;

        private bool Enabled => _host.FirewallEnabled && _host.PluginReady && !_host.Disposed && !_host.IsSwitchingTerritory;

        // 亲疏自行与沉稳咏唱共用一个冷却组。
        private static uint CooldownKey(uint action) => action == 7559 ? 7548u : action;
        internal SimActionCooldown Cooldown(uint action) => _cooldowns.TryGetValue(CooldownKey(action), out var value) ? value : default;

        internal void BeginSimulation()
        {
            lock (_gate)
            {
                if (!Enabled)
                    return;
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

        internal static uint ParseAction(string command)
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
                    return 7559;
                case "亲疏自行":
                    return 7548;
                case "冲刺":
                    return 3;
                default:
                    return 0;
            }
        }

        internal void Receive(string command)
        {
            var action = ParseAction(command);
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

        private void Execute(uint action)
        {
            try
            {
                if (!Enabled || Cooldown(action).Remaining > 0)
                    return;
                var player = _host.EntityProvider.GetMyself();
                if (player == null || !player.IsAlive)
                    return;
                if (_recasts == null)
                    _recasts = new SimActionRecasts(player);
                if (!_entities.UseLocalAction(_entities.Attach(player), action))
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
                    _input = null;
                    _recasts = null;
                    _polling = false;
                    _cooldowns.Clear();
                }
            }
        }
    }
}
