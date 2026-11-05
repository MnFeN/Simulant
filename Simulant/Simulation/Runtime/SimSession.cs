using Simulant.Core;
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
        private readonly object _lifecycleLock = new object();
        private SimScheduler _scheduler;
        private int _cleanupRequired;
        private bool _disposed;

        public bool IsRunning => _scheduler?.IsRunning == true;

        internal SimSession(PluginHost host, SimPresetBase preset)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
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
            lock (_lifecycleLock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(SimSession));
                if (IsRunning)
                    throw new InvalidOperationException("SimSession 已在运行中。");

                StopCore();
                _scheduler = new SimScheduler((ex, node) =>
                {
                    _host.LogError($"时间轴节点「{node ?? "未命名"}」执行失败：{ex}");
                    Cleanup();
                });
                _logic.Scheduler = _scheduler;
                _entityManager.Scheduler = _scheduler;
                Interlocked.Exchange(ref _cleanupRequired, 1);
                try
                {
                    // UI 选项只在调用方读取；实体初始化与所有节点都在专用线程运行。
                    _logic.Prepare();
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

        public void Stop()
        {
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
            if (Interlocked.Exchange(ref _cleanupRequired, 0) != 0)
                _logic.Stop();
        }

        public void Dispose()
        {
            lock (_lifecycleLock)
            {
                if (_disposed) return;
                _disposed = true;
                StopCore();
            }
        }
    }
}
