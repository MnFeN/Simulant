using System;
using System.Threading;

namespace Simulant.Simulation.Timeline
{
    /// <summary>A cancellation handle for one queued node. Cancel does not interrupt a node already running.</summary>
    public sealed class SimScheduledAction : IDisposable
    {
        internal const int Queued = 0;
        internal const int Running = 1;
        internal const int Completed = 2;
        internal const int Cancelled = 3;

        private readonly SimScheduler _scheduler;
        internal readonly double Seconds;
        internal readonly long Sequence;
        internal readonly string Name;
        internal Action Action;
        internal int HeapIndex = -1;
        internal int State;

        internal SimScheduledAction(SimScheduler scheduler, double seconds, long sequence, Action action, string name)
        {
            _scheduler = scheduler;
            Seconds = seconds;
            Sequence = sequence;
            Action = action;
            Name = name;
        }

        public bool IsCancelled => Volatile.Read(ref State) == Cancelled;

        /// <summary>True once the node has finished or has been cancelled.</summary>
        public bool IsCompleted => Volatile.Read(ref State) >= Completed;

        /// <summary>Returns true if the node was removed before the worker claimed it.</summary>
        public bool Cancel() => _scheduler.Cancel(this);

        public void Dispose() => Cancel();
    }
}
