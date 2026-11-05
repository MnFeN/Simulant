using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Simulant.Simulation.Timeline
{
    /// <summary>
    /// Runs timeline nodes serially on one dedicated background thread, without per-node tasks or timers.
    /// Callbacks should do short synchronous work; a blocking callback delays all following nodes.
    /// </summary>
    public sealed class SimScheduler : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Stopwatch _clock = new Stopwatch();
        private readonly List<SimScheduledAction> _heap = new List<SimScheduledAction>();
        private readonly Action<Exception, string> _errorHandler;
        private readonly Thread _worker;
        private long _sequence;
        private bool _started;
        private bool _cancelled;
        private bool _disposed;
        private bool _executing;
        private double _currentNodeSeconds;

        public SimScheduler(Action<Exception, string> errorHandler = null)
        {
            _errorHandler = errorHandler;
            _worker = new Thread(Run) { IsBackground = true, Name = "Simulant timeline" };
        }

        public double ElapsedSeconds
        {
            get { lock (_gate) return _clock.Elapsed.TotalSeconds; }
        }

        /// <summary>The number of queued nodes; excludes the currently running node.</summary>
        public int PendingCount
        {
            get { lock (_gate) return _heap.Count; }
        }

        public bool IsRunning
        {
            get { lock (_gate) return _started && !_cancelled; }
        }

        public bool IsSchedulerThread => Thread.CurrentThread == _worker;

        /// <summary>Starts the clock and worker. Nodes may be queued before this call. Repeated calls are harmless.</summary>
        public void Start()
        {
            lock (_gate)
            {
                ThrowIfStopped();
                if (_started) return;
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
        {
            SimTimeline.ValidateSeconds(seconds, nameof(seconds));
            SimTimeline.ValidateAction(action, nameof(action));
            lock (_gate)
            {
                ThrowIfStopped();
                var node = Enqueue(seconds, action, name);
                Monitor.Pulse(_gate);
                return node;
            }
        }

        /// <summary>Queues a node relative to the actual current clock time, including when called by a node.</summary>
        public SimScheduledAction ScheduleAfter(double seconds, Action action, string name = null)
        {
            SimTimeline.ValidateSeconds(seconds, nameof(seconds));
            SimTimeline.ValidateAction(action, nameof(action));
            lock (_gate)
            {
                ThrowIfStopped();
                var time = _clock.Elapsed.TotalSeconds + seconds;
                SimTimeline.ValidateSeconds(time, nameof(seconds));
                var node = Enqueue(time, action, name);
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
            if (timeline == null) throw new ArgumentNullException(nameof(timeline));
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
                join = _started && !IsSchedulerThread;
            }
            // No callback, error handler, or thread join runs while holding the queue lock.
            if (join) _worker.Join();
        }

        internal bool Cancel(SimScheduledAction node)
        {
            lock (_gate)
            {
                if (node.State != SimScheduledAction.Queued) return false;
                RemoveAt(node.HeapIndex);
                node.Action = null;
                Volatile.Write(ref node.State, SimScheduledAction.Cancelled);
                Monitor.Pulse(_gate);
                return true;
            }
        }

        private void CancelCore()
        {
            if (_cancelled) return;
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
            if (_disposed) throw new ObjectDisposedException(nameof(SimScheduler));
            if (_cancelled) throw new InvalidOperationException("The scheduler has been cancelled and cannot be restarted.");
        }

        private void Run()
        {
            while (true)
            {
                SimScheduledAction node;
                Action action;
                lock (_gate)
                {
                    while (true)
                    {
                        if (_cancelled) return;
                        if (_heap.Count == 0)
                        {
                            Monitor.Wait(_gate);
                            continue;
                        }

                        node = _heap[0];
                        var remaining = node.Seconds - _clock.Elapsed.TotalSeconds;
                        if (remaining > 0)
                        {
                            // Clamp before converting so very distant deadlines cannot overflow the wait.
                            var milliseconds = remaining >= int.MaxValue / 1000.0
                                ? int.MaxValue : Math.Max(1, (int)Math.Ceiling(remaining * 1000));
                            Monitor.Wait(_gate, milliseconds);
                            continue;
                        }

                        RemoveAt(0);
                        Volatile.Write(ref node.State, SimScheduledAction.Running);
                        action = node.Action;
                        node.Action = null;
                        _currentNodeSeconds = node.Seconds;
                        _executing = true;
                        break;
                    }
                }

                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    bool report;
                    lock (_gate)
                    {
                        // A callback may try to enqueue more work after another thread has stopped us.
                        // That is normal shutdown, not a second simulation failure.
                        report = !_cancelled;
                        if (report) CancelCore();
                    }
                    if (report)
                    {
                        // Freeze scheduling before reporting: the handler may tear down simulation state.
                        try { _errorHandler?.Invoke(exception, node.Name); }
                        catch { /* An error reporter must not bring down the host process. */ }
                    }
                }
                finally
                {
                    lock (_gate)
                    {
                        _executing = false;
                        Volatile.Write(ref node.State, SimScheduledAction.Completed);
                    }
                }
            }
        }

        private SimScheduledAction Enqueue(double seconds, Action action, string name)
        {
            var node = new SimScheduledAction(this, seconds, _sequence++, action, name);
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
            if (index == lastIndex) return;

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
                if (!ComesBefore(_heap[index], _heap[parent])) return;
                Swap(index, parent);
                index = parent;
            }
        }

        private void SiftDown(int index)
        {
            while (index < _heap.Count / 2)
            {
                var child = index * 2 + 1;
                if (child + 1 < _heap.Count && ComesBefore(_heap[child + 1], _heap[child])) child++;
                if (!ComesBefore(_heap[child], _heap[index])) return;
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
