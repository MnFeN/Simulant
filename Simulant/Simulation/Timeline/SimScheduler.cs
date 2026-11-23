using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Simulant.Simulation.Mechanics;

namespace Simulant.Simulation.Timeline
{
    /// <summary>
    /// Runs timeline nodes serially on one dedicated background thread, without per-node tasks or timers.
    /// Callbacks should do short synchronous work; a blocking callback delays all following nodes.
    /// </summary>
    public sealed class SimScheduler : IDisposable
    {
        private const double MechanicFailureDelay = 2;
        private readonly object _gate = new object();
        private readonly Stopwatch _clock = new Stopwatch();
        private readonly List<SimScheduledAction> _heap = new List<SimScheduledAction>();
        private readonly Action<Exception, string> _errorHandler;
        private readonly Action _interruptAfterFailure;
        private readonly Thread _worker;
        private long _sequence;
        private bool _started;
        private bool _cancelled;
        private bool _failed;
        private bool _drainingFailure;
        private bool _disposed;
        private bool _executing;
        private double _currentNodeSeconds;
        private double _nativePresentationUntil;

        public SimScheduler(Action<Exception, string> errorHandler = null)
            : this(errorHandler, null)
        {
        }

        internal SimScheduler(Action<Exception, string> errorHandler, Action interruptAfterFailure)
        {
            _errorHandler = errorHandler;
            _interruptAfterFailure = interruptAfterFailure;
            _worker = new Thread(Run)
            {
                IsBackground = true,
                Name = "Simulant timeline"
            };
        }

        public double ElapsedSeconds
        {
            get
            {
                lock (_gate)
                    return _clock.Elapsed.TotalSeconds;
            }
        }

        /// <summary>The number of queued nodes; excludes the currently running node.</summary>
        public int PendingCount
        {
            get
            {
                lock (_gate)
                    return _heap.Count;
            }
        }

        public bool IsRunning
        {
            get
            {
                lock (_gate)
                    return _started && !_cancelled && !_drainingFailure;
            }
        }

        public bool IsSchedulerThread => Thread.CurrentThread == _worker;

        internal bool IsDrainingFailure
        {
            get
            {
                lock (_gate)
                    return _drainingFailure;
            }
        }

        /// <summary>Starts the clock and worker. Nodes may be queued before this call. Repeated calls are harmless.</summary>
        public void Start()
        {
            lock (_gate)
            {
                ThrowIfStopped();

                if (_started)
                    return;

                _clock.Start();

                try
                {
                    _worker.Start();
                    _started = true;
                }
                catch
                {
                    CancelCore();
                    throw;
                }
            }
        }

        /// <summary>Queues a node at an absolute time from Start. Past deadlines run as soon as possible.</summary>
        public SimScheduledAction Schedule(double seconds, Action action, string name = null)
            => Schedule(seconds, action, name, finishOnMechanicFailure: false);

        internal SimScheduledAction Schedule(double seconds, Action action, string name, bool finishOnMechanicFailure)
        {
            SimTimeline.ValidateSeconds(seconds, nameof(seconds));
            SimTimeline.ValidateAction(action, nameof(action));

            lock (_gate)
            {
                ThrowIfStopped();
                var node = Enqueue(seconds, action, name, finishOnMechanicFailure);
                Monitor.Pulse(_gate);
                return node;
            }
        }

        /// <summary>Queues a node relative to the actual current clock time, including when called by a node.</summary>
        public SimScheduledAction ScheduleAfter(double seconds, Action action, string name = null)
            => ScheduleAfter(seconds, action, name, finishOnMechanicFailure: false);

        internal SimScheduledAction ScheduleAfter(double seconds, Action action, string name, bool finishOnMechanicFailure)
        {
            SimTimeline.ValidateSeconds(seconds, nameof(seconds));
            SimTimeline.ValidateAction(action, nameof(action));

            lock (_gate)
            {
                ThrowIfStopped();
                var time = _clock.Elapsed.TotalSeconds + seconds;
                SimTimeline.ValidateSeconds(time, nameof(seconds));
                var node = Enqueue(time, action, name, finishOnMechanicFailure);
                Monitor.Pulse(_gate);
                return node;
            }
        }

        /// <summary>供机制添加可取消节点；节点内部以计划时间为基准，避免开始演出的耗时推迟判定。</summary>
        internal SimScheduledAction ScheduleRelativeToNode(double seconds, Action action, string name = null,
            bool finishOnMechanicFailure = false)
        {
            SimTimeline.ValidateSeconds(seconds, nameof(seconds));
            SimTimeline.ValidateAction(action, nameof(action));

            lock (_gate)
            {
                ThrowIfStopped();
                var anchor = IsSchedulerThread && _executing ? _currentNodeSeconds : _clock.Elapsed.TotalSeconds;
                var time = anchor + seconds;
                SimTimeline.ValidateSeconds(time, nameof(seconds));
                var node = Enqueue(time, action, name, finishOnMechanicFailure);
                Monitor.Pulse(_gate);
                return node;
            }
        }

        /// <summary>
        /// Queues a snapshot of the timeline. Inside a node, the anchor is that node's planned time,
        /// preventing accumulated drift. From other threads, the anchor is the actual current time.
        /// </summary>
        public void Play(SimTimeline timeline, double delaySeconds = 0)
        {
            if (timeline == null)
                throw new ArgumentNullException(nameof(timeline));

            SimTimeline.ValidateSeconds(delaySeconds, nameof(delaySeconds));
            var nodes = timeline.Snapshot();

            lock (_gate)
            {
                ThrowIfStopped();
                var anchor = (IsSchedulerThread && _executing ? _currentNodeSeconds : _clock.Elapsed.TotalSeconds) + delaySeconds;
                SimTimeline.ValidateSeconds(anchor, nameof(delaySeconds));

                foreach (var node in nodes)
                    SimTimeline.ValidateSeconds(anchor + node.Seconds, nameof(timeline));

                foreach (var node in nodes)
                    Enqueue(anchor + node.Seconds, node.Action, node.Name);

                Monitor.Pulse(_gate);
            }
        }

        /// <summary>按指定原点排入时间轴；早于开场的节点在开场时按添加顺序执行。</summary>
        internal void PlayAt(SimTimeline timeline, double origin, double notBefore)
        {
            foreach (var node in timeline.Snapshot())
                Schedule(Math.Max(notBefore, origin + node.Seconds), node.Action, node.Name);
        }

        /// <summary>
        /// Permanently stops accepting nodes, clears the queue, and freezes the clock.
        /// A callback already claimed by the worker may finish; use Dispose to wait for it.
        /// </summary>
        public void Cancel()
        {
            lock (_gate)
                CancelCore();
        }

        /// <summary>
        /// Cancels all pending work and waits for the worker to exit. When called by the worker itself,
        /// cancellation is immediate and the worker exits after the current callback returns.
        /// </summary>
        public void Dispose()
        {
            bool join;

            lock (_gate)
            {
                _disposed = true;
                CancelCore();
                Monitor.PulseAll(_gate);
                join = _started && !IsSchedulerThread;
            }

            // No callback, error handler, or thread join runs while holding the queue lock.
            if (join)
                _worker.Join();
        }

        /// <summary>记录已经交给客户端执行的位移结束时间。</summary>
        internal void TrackNativePresentation(double duration)
        {
            lock (_gate)
            {
                _nativePresentationUntil = Math.Max(_nativePresentationUntil, _clock.Elapsed.TotalSeconds + duration);
                Monitor.Pulse(_gate);
            }
        }

        internal bool Cancel(SimScheduledAction node)
        {
            lock (_gate)
            {
                if (node.State != SimScheduledAction.Queued)
                    return false;

                RemoveAt(node.HeapIndex);
                node.Action = null;
                Volatile.Write(ref node.State, SimScheduledAction.Cancelled);
                Monitor.Pulse(_gate);
                return true;
            }
        }

        private void CancelCore()
        {
            if (_cancelled)
                return;

            _cancelled = true;
            _clock.Stop();

            foreach (var node in _heap)
            {
                node.HeapIndex = -1;
                node.Action = null;
                Volatile.Write(ref node.State, SimScheduledAction.Cancelled);
            }

            _heap.Clear();
            Monitor.PulseAll(_gate);
        }

        private void ThrowIfStopped()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SimScheduler));

            if (_cancelled)
                throw new InvalidOperationException("The scheduler has been cancelled and cannot be restarted.");
        }

        private void Run()
        {
            while (TryTakeNext(out var node, out var action))
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    HandleNodeFailure(exception, node.Name);
                }
                finally
                {
                    Complete(node);
                }
            }

            Cancel();
            if (_failed)
            {
                try
                {
                    _interruptAfterFailure?.Invoke();
                }
                catch
                { /* 清理回调失败后仍保留现场，等待显式停止。 */ }
            }
        }

        private bool TryTakeNext(out SimScheduledAction node, out Action action)
        {
            node = null;
            action = null;

            lock (_gate)
            {
                while (!_cancelled)
                {
                    if (_heap.Count == 0 && !_drainingFailure)
                    {
                        Monitor.Wait(_gate);
                        continue;
                    }

                    var dueAt = _heap.Count == 0 ? _nativePresentationUntil : _heap[0].Seconds;
                    var remaining = dueAt - _clock.Elapsed.TotalSeconds;
                    if (remaining > 0)
                    {
                        var milliseconds = remaining >= int.MaxValue / 1000.0
                            ? int.MaxValue : Math.Max(1, (int)Math.Ceiling(remaining * 1000));
                        Monitor.Wait(_gate, milliseconds);
                        continue;
                    }

                    if (_heap.Count == 0)
                        return false;

                    node = _heap[0];
                    RemoveAt(0);
                    Volatile.Write(ref node.State, SimScheduledAction.Running);
                    action = node.Action;
                    node.Action = null;
                    _currentNodeSeconds = node.Seconds;
                    _executing = true;
                    return true;
                }
            }

            return false;
        }

        private void Complete(SimScheduledAction node)
        {
            lock (_gate)
            {
                _executing = false;
                Volatile.Write(ref node.State, SimScheduledAction.Completed);
            }
        }

        private void HandleNodeFailure(Exception exception, string name)
        {
            lock (_gate)
            {
                // 其他线程已停止时，当前节点的退出不再报告为新错误。
                if (_cancelled)
                    return;

                if (exception is SimMechanicFailedException && _interruptAfterFailure != null)
                {
                    // 从第一次失败开始计时，期间后续判定仍按原时间运行并报告结果。
                    if (!_failed)
                        Enqueue(_clock.Elapsed.TotalSeconds + MechanicFailureDelay, BeginFailureDrain, "机制失败后中断时间轴");
                }
                else
                    CancelCore();

                _failed = true;
            }

            try
            {
                _errorHandler?.Invoke(exception, name);
            }
            catch
            { /* An error reporter must not bring down the host process. */
            }
        }

        private void BeginFailureDrain()
        {
            lock (_gate)
            {
                _drainingFailure = true;
                foreach (var node in _heap.ToArray())
                    if (!node.FinishOnMechanicFailure)
                        Cancel(node);
            }
        }

        private SimScheduledAction Enqueue(double seconds, Action action, string name, bool finishOnMechanicFailure = false)
        {
            var node = new SimScheduledAction(this, seconds, _sequence++, action, name, finishOnMechanicFailure);
            if (_drainingFailure && !finishOnMechanicFailure)
            {
                node.Action = null;
                Volatile.Write(ref node.State, SimScheduledAction.Cancelled);
                return node;
            }

            node.HeapIndex = _heap.Count;
            _heap.Add(node);
            SiftUp(node.HeapIndex);
            return node;
        }

        private static bool ComesBefore(SimScheduledAction left, SimScheduledAction right)
            => left.Seconds < right.Seconds || (left.Seconds == right.Seconds && left.Sequence < right.Sequence);

        private void RemoveAt(int index)
        {
            var removed = _heap[index];
            var lastIndex = _heap.Count - 1;
            var last = _heap[lastIndex];
            _heap.RemoveAt(lastIndex);
            removed.HeapIndex = -1;

            if (index == lastIndex)
                return;

            _heap[index] = last;
            last.HeapIndex = index;

            if (index > 0 && ComesBefore(last, _heap[(index - 1) / 2]))
                SiftUp(index);
            else
                SiftDown(index);
        }

        private void SiftUp(int index)
        {
            while (index > 0)
            {
                var parent = (index - 1) / 2;

                if (!ComesBefore(_heap[index], _heap[parent]))
                    return;

                Swap(index, parent);
                index = parent;
            }
        }

        private void SiftDown(int index)
        {
            while (index < _heap.Count / 2)
            {
                var child = index * 2 + 1;

                if (child + 1 < _heap.Count && ComesBefore(_heap[child + 1], _heap[child]))
                    child++;

                if (!ComesBefore(_heap[child], _heap[index]))
                    return;

                Swap(index, child);
                index = child;
            }
        }

        private void Swap(int first, int second)
        {
            var node = _heap[first];
            _heap[first] = _heap[second];
            _heap[second] = node;
            _heap[first].HeapIndex = first;
            _heap[second].HeapIndex = second;
        }
    }
}
