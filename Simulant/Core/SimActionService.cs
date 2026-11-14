using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;

namespace Simulant.Core
{
    /// <summary>防火墙期间的本机模拟技能，独立于副本的启动、停止和判定。</summary>
    internal sealed class SimActionService
    {
        private readonly PluginHost _host;
        private readonly object _gate = new object();
        private SimScheduler _scheduler;
        private SimEntityManager _entities;

        internal SimActionService(PluginHost host) => _host = host;

        private bool Enabled => _host.FirewallEnabled && _host.PluginReady && !_host.Disposed && !_host.IsSwitchingTerritory;

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

                if (_scheduler == null)
                {
                    _scheduler = new SimScheduler((error, node) => _host.LogWarning("模拟技能失败：" + error.Message));
                    _entities = new SimEntityManager(_host.EntitySpawner, error => _host.LogWarning("清理模拟技能状态失败：" + error.Message));
                    _entities.Scheduler = _scheduler;
                    _scheduler.Start();
                }

                var entities = _entities;
                _scheduler.ScheduleAfter(0, () =>
                {
                    try
                    {
                        if (!Enabled)
                            return;

                        var player = _host.EntityProvider.GetMyself();
                        if (player != null && player.IsAlive)
                            entities.UseLocalAction(entities.Attach(player), action);
                    }
                    catch (Exception error)
                    {
                        _host.LogWarning("执行模拟技能失败：" + error.Message);
                    }
                }, "执行本机模拟技能");
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
                    _entities?.Clear();
                }
                finally
                {
                    _scheduler = null;
                    _entities = null;
                }
            }
        }
    }
}
