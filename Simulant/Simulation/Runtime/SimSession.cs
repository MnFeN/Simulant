using Simulant.ACT;
using Simulant.Core;
using Simulant.Core.Environment;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Timeline;
using System;
using System.Threading;

namespace Simulant.Simulation.Runtime
{
    public sealed class SimSession : IDisposable
    {
        private readonly PluginHost _host;
        private readonly SimLogicBase _logic;
        private readonly SimEntityManager _entityManager;
        private readonly SimPresetBase _preset;
        private readonly object _lifecycleLock = new object();
        private SimScheduler _scheduler;
        private int _cleanupRequired;
        private int _failureRetained;
        private volatile bool _waymarksPendingCleanup;
        private volatile bool _disposed;

        public bool IsRunning => _scheduler?.IsRunning == true;
        internal bool HasPendingCleanup => _entityManager.HasPendingCleanup || _waymarksPendingCleanup || Volatile.Read(ref _failureRetained) != 0;

        internal SimSession(PluginHost host, SimPresetBase preset)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _preset = preset;
            _ = preset ?? throw new ArgumentNullException(nameof(preset));
            _ = preset.SimLogicType ?? throw new NullReferenceException($"{preset.GetType().Name} 预设类型未定义 SimLogicType，或 SimLogicType 为 null。");

            if (!typeof(SimLogicBase).IsAssignableFrom(preset.SimLogicType))
                throw new InvalidOperationException($"{preset.GetType().Name} 预设的逻辑类型 {preset.SimLogicType.Name} 不继承自 SimLogicBase。");

            _entityManager = new SimEntityManager(_host.EntitySpawner,
                ex => _host.LogError("清理模拟实体失败：" + ex));

            _logic = (SimLogicBase)Activator.CreateInstance(preset.SimLogicType, _host, preset);
            _logic.EntityManager = _entityManager;
        }

        public void Start()
        {
            if (Monitor.IsEntered(_lifecycleLock) || _scheduler?.IsSchedulerThread == true)
                throw new InvalidOperationException("不能在本会话的启动、停止或时间轴节点内重启会话，请等待当前调用退出后再开始。");
            lock (_lifecycleLock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(SimSession));
                if (IsRunning)
                    throw new InvalidOperationException("SimSession 已在运行中。");

                StopCore();
                if (_disposed)
                    throw new ObjectDisposedException(nameof(SimSession));
                if (HasPendingCleanup)
                    throw new InvalidOperationException("上次模拟的实体或场地标点尚未完全清理，请先停止并重试清理。");
                _scheduler = new SimScheduler(HandleFailure);
                try
                {
                    _entityManager.Scheduler = _scheduler;
                    _logic.Scheduler = _scheduler;
                    Interlocked.Exchange(ref _cleanupRequired, 1);
                    // 行为来自插件全局配置；每次模拟取独立快照，不受窗口编辑或预设切换影响。
                    _entityManager.PlayerBehavior = _host.Configuration.SimulatedParty.Copy();
                    _logic.Prepare();
                    var waymarks = _preset.Waymarks;
                    if (waymarks != null)
                    {
                        var payload = waymarks.ToPlaceJson();
                        // 回调可能只完成部分写入就抛错，因此在调用前承担清理责任。
                        _waymarksPendingCleanup = true;
                        TriggernometryInterop.InvokeNamedCallback("place", payload);
                    }
                    _scheduler.Schedule(0, _logic.Start, "初始化模拟");
                    _scheduler.Start();
                }
                catch
                {
                    StopCore();
                    throw;
                }
            }
        }

        private void HandleFailure(Exception error, string node)
        {
            Interlocked.Exchange(ref _failureRetained, 1);
            var reason = error is SimMechanicFailedException ? error.Message :
                $"节点「{node ?? "未命名"}」执行失败：{error.Message}";
            try
            {
                if (error is SimMechanicFailedException) _host.LogWarning(reason);
                else _host.LogError($"时间轴节点「{node ?? "未命名"}」执行失败：{error}");
            }
            finally
            {
                try
                {
                    try { TriggernometryInterop.InvokeNamedCallback("command", "/e <se.11>\n" + reason); }
                    catch (Exception chatError) { _host.LogWarning("模拟错误的聊天提示发送失败：" + chatError.Message); }
                }
                finally
                {
                    // 当前判定的演出已提交；取消后续机制后留出两秒，再停止角色行为。
                    // 普通执行异常仍立即中断，手动停止可提前结束等待。
                    if (error is SimMechanicFailedException) _scheduler.WaitForFailureDisplay(2);
                    try { _entityManager.Interrupt(); }
                    catch (Exception interruptError) { _host.LogError("中断模拟行为失败，已保留清理责任：" + interruptError); }
                }
            }
        }

        public void Stop()
        {
            // 外部停止可能持有生命周期锁并等待当前节点；节点不能再反向等待该锁。
            if (_scheduler?.IsSchedulerThread == true)
            {
                StopCore();
                return;
            }
            lock (_lifecycleLock)
                StopCore();
        }

        private void StopCore()
        {
            // Dispose 先取消并等待当前节点退出，再允许释放节点引用的原生实体。
            _scheduler?.Dispose();
            Cleanup();
        }

        private void Cleanup()
        {
            Interlocked.Exchange(ref _failureRetained, 0);
            try
            {
                if (Interlocked.Exchange(ref _cleanupRequired, 0) != 0)
                    _logic.Stop();
                else if (_entityManager.HasPendingCleanup)
                    _entityManager.Clear();
            }
            finally
            {
                if (_waymarksPendingCleanup)
                {
                    try
                    {
                        TriggernometryInterop.InvokeNamedCallback("place", WaymarkLayout.ClearJson);
                        _waymarksPendingCleanup = false;
                    }
                    catch (Exception ex)
                    {
                        _host.LogError("清理模拟场地标点失败，已保留清理责任，请再次停止模拟重试：" + ex);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_scheduler?.IsSchedulerThread == true)
            {
                _disposed = true;
                StopCore();
                return;
            }
            lock (_lifecycleLock)
            {
                _disposed = true;
                StopCore();
            }
        }
    }
}
