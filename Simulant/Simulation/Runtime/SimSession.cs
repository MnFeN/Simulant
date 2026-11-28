using Simulant.ACT;
using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Core.Environment;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Timeline;
using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Simulant.Simulation.Runtime
{
    public sealed class SimSession : IDisposable
    {
        private enum DirectorCommand : uint
        {
            FadeOut = 0x40000005,
            Recommence = 0x40000006,
            Reset = 0x4000000F,
            Ready = 0x40000011,
            ClearBattleTalk = 0x40000013
        }

        private readonly PluginHost _host;
        private readonly SimLogicBase _logic;
        private readonly SimEntityManager _entityManager;
        private readonly SimPresetBase _preset;
        private readonly object _lifecycleLock = new object();
        private SimScheduler _scheduler;
        private int _cleanupRequired;
        private int _failureRetained;
        private volatile bool _waymarksPendingCleanup;
        private int _waymarkWorld;
        private volatile bool _disposed;
        private bool _roundStarted;
        private int _stopping;
        private uint _directorId;
        private long _wipeStartedAt;

        public int RandomSeed { get; }
        public bool IsRunning => _scheduler?.IsRunning == true;
        internal bool HasPendingCleanup => _entityManager.HasPendingCleanup || _waymarksPendingCleanup || Volatile.Read(ref _failureRetained) != 0;
        internal long RestartNotBefore => _wipeStartedAt == 0 ? 0 : _wipeStartedAt + Stopwatch.Frequency / 2;

        internal static int DelayMillisecondsUntil(long timestamp)
            => (int)Math.Ceiling(Math.Max(0, timestamp - Stopwatch.GetTimestamp()) * 1000d / Stopwatch.Frequency);

        internal SimSession(PluginHost host, SimPresetBase preset, int? randomSeed = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _preset = preset;
            _ = preset ?? throw new ArgumentNullException(nameof(preset));
            _ = preset.SimLogicType ?? throw new NullReferenceException($"{preset.GetType().Name} 预设类型未定义 SimLogicType，或 SimLogicType 为 null。");

            if (!typeof(SimLogicBase).IsAssignableFrom(preset.SimLogicType))
                throw new InvalidOperationException($"{preset.GetType().Name} 预设的逻辑类型 {preset.SimLogicType.Name} 不继承自 SimLogicBase。");

            _entityManager = new SimEntityManager(_host.EntitySpawner,
                ex => _host.LogError("清理模拟实体失败：" + ex))
            {
                CombatLog = new SimCombatLog(() => _host.Configuration.GenerateActLogs, XivNetworkLog.WriteLine, _host.LogWarning),
                EnemyList = new SimEnemyList(_host)
            };

            RandomSeed = randomSeed ?? (BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0) & int.MaxValue);
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

                WaitUntil(RestartNotBefore);
                _wipeStartedAt = 0;

                _scheduler = new SimScheduler(HandleFailure, () => Interrupt(cancelCasts: false));

                try
                {
                    // 假队员的轮询读盘与技能间隔不影响机制随机序列。
                    var random = new Random(RandomSeed);
                    _entityManager.BehaviorRandom = new Random(random.Next());
                    _logic.Random = random;
                    _entityManager.Scheduler = _scheduler;
                    _logic.Scheduler = _scheduler;
                    Interlocked.Exchange(ref _cleanupRequired, 1);
                    // 行为来自插件全局配置；每次模拟取独立快照，不受窗口编辑或预设切换影响。
                    _entityManager.PlayerBehavior = _host.Configuration.SimulatedParty.Copy();
                    _entityManager.SelfJudgementAdvance = _host.Configuration.SimLatency;
                    _logic.Prepare();
                    var waymarks = _preset.Waymarks;

                    if (waymarks != null)
                    {
                        // 写入中途失败也要清理标点。
                        _waymarkWorld = _host.WorldGeneration;
                        _waymarksPendingCleanup = true;
                        GameMarkers.PlaceWaymarks(waymarks);
                    }

                    _scheduler.Schedule(0, _logic.Start, "初始化模拟");
                    _directorId = _host.ZoneService?.ContentDirectorId ?? 0;
                    _host.PlayerActions.BeginSimulation(_entityManager, checked((byte)_preset.Level));
                    _host.PlayerActions.ResetCooldowns();
                    _roundStarted = true;
                    _scheduler.Start();
                }
                catch
                {
                    _roundStarted = false;
                    StopCore();
                    throw;
                }
            }
        }

        private void HandleFailure(Exception error, string node)
        {
            Interlocked.Exchange(ref _failureRetained, 1);
            var alreadyReported = error is SimMechanicFailedException failure && failure.AlreadyReported;
            var reason = error is SimMechanicFailedException ? error.Message :
                $"节点「{node ?? "未命名"}」执行失败：{error.Message}";

            try
            {
                if (error is SimMechanicFailedException)
                {
                    if (!alreadyReported)
                        _host.LogWarning(reason);
                }
                else
                    _host.LogError($"时间轴节点「{node ?? "未命名"}」执行失败：{error}");
            }
            finally
            {
                try
                {
                    if (!alreadyReported)
                        GameChat.SendEcho(reason, playSound: true);
                }
                catch (Exception chatError)
                {
                    _host.LogWarning("模拟错误的聊天提示发送失败：" + chatError.Message);
                }
            }
        }

        private void Interrupt(bool cancelCasts)
        {
            try
            {
                _entityManager.Interrupt(cancelCasts);
            }
            catch (Exception interruptError)
            {
                _host.LogError("中断模拟行为失败，已保留清理责任：" + interruptError);
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
            _host.PlayerActions.EndSimulation(_entityManager);
            // Dispose 先取消并等待当前节点退出，再允许释放节点引用的原生实体。
            _scheduler?.Dispose();
            if (Interlocked.Exchange(ref _stopping, 1) != 0)
                return;

            try
            {
                if (!_roundStarted)
                {
                    Cleanup();
                    return;
                }

                _roundStarted = false;
                StopRound();
            }
            finally
            {
                Volatile.Write(ref _stopping, 0);
            }
        }

        private void StopRound()
        {
            Interrupt(cancelCasts: true);
            _wipeStartedAt = Stopwatch.GetTimestamp();
            WriteDirectorLog(DirectorCommand.FadeOut);

            WaitUntil(_wipeStartedAt, 100);
            WriteDirectorLog(DirectorCommand.ClearBattleTalk);
            ExceptionDispatchInfo cleanupError = null;
            try
            {
                Cleanup();
            }
            catch (Exception error)
            {
                // 先写完触发器重置日志，再将清理异常交还调用者。
                cleanupError = ExceptionDispatchInfo.Capture(error);
            }

            WaitUntil(_wipeStartedAt, 200);
            WriteDirectorLog(DirectorCommand.Reset);
            WaitUntil(_wipeStartedAt, 300);
            WriteDirectorLog(DirectorCommand.Ready);
            WaitUntil(_wipeStartedAt, 400);
            WriteDirectorLog(DirectorCommand.Recommence);
            _host.PlayerActions.ResetCooldowns();

            cleanupError?.Throw();
        }

        private void WriteDirectorLog(DirectorCommand command)
            => _entityManager.CombatLog.Director(_directorId, (uint)command, 0, 0, 0, 0);

        private static void WaitUntil(long timestamp, int offsetMilliseconds = 0)
        {
            var deadline = timestamp + Stopwatch.Frequency * offsetMilliseconds / 1000;
            int delay;
            while ((delay = DelayMillisecondsUntil(deadline)) > 0)
                Thread.Sleep(delay);
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
                        if (_waymarkWorld == _host.WorldGeneration)
                            GameMarkers.ClearWaymarks();
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
