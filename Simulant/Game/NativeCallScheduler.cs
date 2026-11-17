using SimuMagic;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Simulant.Game
{
    /// <summary>串行调度游戏原生调用，让短时间内的多个调用共用一次帧锁。</summary>
    internal sealed class NativeCallScheduler : IDisposable
    {
        private sealed class ScheduledCall
        {
            public Action Action;
            public ManualResetEventSlim Done = new ManualResetEventSlim(false);
            public Exception Exception;
            internal ScheduledCall(Action action)
            {
                Action = action;
            }
        }

        private readonly ExternalProcessMemory _memory;
        private readonly object _gate = new object();
        private readonly Queue<ScheduledCall> _queue = new Queue<ScheduledCall>();

        private readonly int _frameLeaseMs;

        private bool _workerRunning;
        private bool _disposed;

        private readonly ManualResetEventSlim _workerFinished = new ManualResetEventSlim(true);

        [ThreadStatic]
        private static NativeCallScheduler _currentWorkerScheduler;

        internal NativeCallScheduler(ExternalProcessMemory memory, int frameLeaseMs)
        {
            if (frameLeaseMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(frameLeaseMs));

            _memory = memory ?? throw new ArgumentNullException(nameof(memory));
            _frameLeaseMs = frameLeaseMs;
        }

        /// <summary>在帧锁保护下执行操作，等待其完成并将执行异常传回调用方。</summary>
        public void Execute(Action action)
        {
            action = action ?? throw new ArgumentNullException(nameof(action));

            // 如果当前已经在调度器的工作线程中，说明这是 Action 内部的重入调用。
            // 这里必须直接执行，否则会把新调用重新入队，然后工作线程等待自己处理队列，造成死锁。
            if (_currentWorkerScheduler == this)
            {
                action();
                return;
            }

            var call = new ScheduledCall(action);
            EnqueueCall(call);
            WaitCall(call);
        }

        private void EnqueueCall(ScheduledCall call)
        {
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(NativeCallScheduler));

                // 将 ScheduledCall 交给工作线程，工作线程在其持有的 FrameLock 窗口内依次执行队列项
                _queue.Enqueue(call);

                // 如果当前没有工作线程，启动一轮新的 FrameLock 调度
                if (!_workerRunning)
                {
                    _workerRunning = true;
                    _workerFinished.Reset();
                    StartWorkerThread();
                }

                // 尝试唤醒已有工作线程在 TryTakeNextCall 中 Monitor.Wait 的等待
                Monitor.PulseAll(_gate);
            }
        }

        private void WaitCall(ScheduledCall call)
        {
            try
            {
                call.Done.Wait();

                if (call.Exception != null)
                    ExceptionDispatchInfo.Capture(call.Exception).Throw();
            }
            finally
            {
                call.Done.Dispose();
            }
        }

        private void StartWorkerThread()
        {
            var thread = new Thread(WorkerProc)
            {
                IsBackground = true,
                Name = "Simulant Game Call Scheduler"
            };

            thread.Start();
        }


        private void WorkerProc()
        {
            _currentWorkerScheduler = this;

            try
            {
                using (_memory.AcquireFrame(true))
                {
                    // 取得帧锁后开始计时。到期后停止接收下一项，当前调用执行完才释放帧锁。
                    var frameDeadline = DateTime.UtcNow.AddMilliseconds(_frameLeaseMs);

                    while (TryTakeNextCall(frameDeadline, out ScheduledCall call))
                    {
                        SafeExecuteCall(call);

                        if (DateTime.UtcNow >= frameDeadline)
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                FailQueuedCalls(ex);
            }
            finally
            {
                FinalizeWorkerAndRestartIfNeeded();
            }
        }

        private bool TryTakeNextCall(DateTime frameDeadline, out ScheduledCall call)
        {
            call = null;

            lock (_gate)
            {
                while (true)
                {
                    if (_disposed)
                        return false;

                    var remaining = frameDeadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                        return false;

                    if (_queue.Count > 0)
                    {
                        call = _queue.Dequeue();
                        return true;
                    }

                    remaining = frameDeadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                        return false;

                    Monitor.Wait(_gate, remaining);
                }
            }
        }

        private void SafeExecuteCall(ScheduledCall call)
        {
            try
            {
                call.Action();
            }
            catch (Exception ex)
            {
                call.Exception = ex;
            }
            finally
            {
                call.Done.Set();
            }
        }

        private void FailQueuedCalls(Exception ex)
        {
            List<ScheduledCall> calls;

            lock (_gate)
            {
                calls = DrainQueueNoLock();
            }

            foreach (var call in calls)
            {
                call.Exception = ex;
                call.Done.Set();
            }
        }

        private void FinalizeWorkerAndRestartIfNeeded()
        {
            // 当前 worker 已经终止调度任务，清除当前线程上的调度器标记，防止误判重入调用
            // 如果 Action 内部再次调用 Call，应直接执行而不是重新入队，避免 worker 等待自己
            _currentWorkerScheduler = null;

            var shouldRestart = false;

            lock (_gate)
            {
                _workerRunning = false;

                // 如果此时队列里还有请求，说明这些请求没有被当前 FrameLock 处理完，需要立刻重启 worker。
                // 可能是当前 frame 到期后退出，或 worker 退出后又有新的请求入队。
                if (!_disposed && _queue.Count > 0)
                {
                    _workerRunning = true;
                    _workerFinished.Reset();
                    shouldRestart = true;
                }
                else
                {
                    _workerFinished.Set();
                }
            }

            if (shouldRestart)
                StartWorkerThread();
        }

        private List<ScheduledCall> DrainQueueNoLock()
        {
            var calls = new List<ScheduledCall>();

            while (_queue.Count > 0)
            {
                calls.Add(_queue.Dequeue());
            }

            return calls;
        }

        /// <summary>让排队中的调用以异常结束。从其他线程释放时，等待活动的工作线程退出。</summary>
        public void Dispose()
        {
            List<ScheduledCall> calls;
            bool shouldWaitWorker;

            lock (_gate)
            {
                if (_disposed) return;

                _disposed = true;
                calls = DrainQueueNoLock();
                shouldWaitWorker = _workerRunning && _currentWorkerScheduler != this;

                Monitor.PulseAll(_gate);
            }

            var ex = new ObjectDisposedException(nameof(NativeCallScheduler));

            foreach (var call in calls)
            {
                call.Exception = ex;
                call.Done.Set();
            }

            // 如果有 worker 正在执行，并且当前不是 worker 线程，等待它自然退出。
            // 避免随后释放 Memory 时，worker 仍在使用 SimuMagic 资源。
            if (shouldWaitWorker)
                _workerFinished.Wait();
        }
    }
}
