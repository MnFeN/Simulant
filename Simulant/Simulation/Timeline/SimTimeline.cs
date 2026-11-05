using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Simulant.Simulation.Timeline
{
    /// <summary>
    /// A reusable timeline definition. Times are seconds from the beginning of this timeline.
    /// Nodes at the same time run in the order in which they were added.
    /// </summary>
    public sealed class SimTimeline
    {
        private readonly object _gate = new object();
        private readonly List<Node> _nodes = new List<Node>();
        private double _cursor;

        public SimTimeline At(double seconds, Action action, string name = null)
        {
            ValidateSeconds(seconds, nameof(seconds));
            ValidateAction(action, nameof(action));

            lock (_gate)
                Add(seconds, action, name);
            return this;
        }

        /// <summary>Adds a node relative to the last node added, even if it was not the latest in time.</summary>
        public SimTimeline After(double seconds, Action action, string name = null)
        {
            ValidateSeconds(seconds, nameof(seconds));
            ValidateAction(action, nameof(action));

            lock (_gate)
            {
                var time = _cursor + seconds;
                ValidateSeconds(time, nameof(seconds));
                Add(time, action, name);
            }
            return this;
        }

        /// <summary>
        /// Copies the child's current nodes at the given offset. Later edits to the child have no effect.
        /// The cursor becomes the time of the last copied node; an empty child leaves it unchanged.
        /// </summary>
        public SimTimeline At(double seconds, SimTimeline child)
        {
            ValidateSeconds(seconds, nameof(seconds));
            if (child == null) throw new ArgumentNullException(nameof(child));

            var nodes = child.Snapshot();
            foreach (var node in nodes)
                ValidateSeconds(seconds + node.Seconds, nameof(seconds));

            lock (_gate)
                foreach (var node in nodes)
                    Add(seconds + node.Seconds, node.Action, node.Name);
            return this;
        }

        /// <summary>Adds count nodes at firstSeconds + index * intervalSeconds, with a zero-based index.</summary>
        public SimTimeline Repeat(double firstSeconds, double intervalSeconds, int count, Action<int> action, string name = null)
        {
            ValidateSeconds(firstSeconds, nameof(firstSeconds));
            ValidateSeconds(intervalSeconds, nameof(intervalSeconds));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            ValidateAction(action, nameof(action));
            if (count == 0) return this;
            ValidateSeconds(firstSeconds + (count - 1) * intervalSeconds, nameof(intervalSeconds));

            lock (_gate)
            {
                for (var i = 0; i < count; i++)
                {
                    var index = i;
                    Add(firstSeconds + i * intervalSeconds, () => action(index), name);
                }
            }
            return this;
        }

        internal Node[] Snapshot()
        {
            lock (_gate)
                return _nodes.ToArray();
        }

        internal static void ValidateSeconds(double seconds, string parameterName)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(parameterName, "Time must be finite and non-negative.");
        }

        // This detects direct async void delegates, including multicast entries. A synchronous wrapper
        // can still start asynchronous work; callers must not let such work escape the node's lifetime.
        internal static void ValidateAction(Delegate action, string parameterName)
        {
            if (action == null) throw new ArgumentNullException(parameterName);
            foreach (var callback in action.GetInvocationList())
            {
                if (callback.Method.IsDefined(typeof(AsyncStateMachineAttribute), false))
                    throw new ArgumentException("Timeline nodes must use synchronous callbacks; async void callbacks cannot be scheduled. Add another timeline node for delayed work.", parameterName);
            }
        }

        private void Add(double seconds, Action action, string name)
        {
            _nodes.Add(new Node(seconds, action, name));
            _cursor = seconds;
        }

        internal sealed class Node
        {
            internal readonly double Seconds;
            internal readonly Action Action;
            internal readonly string Name;

            internal Node(double seconds, Action action, string name)
            {
                Seconds = seconds;
                Action = action;
                Name = name;
            }
        }
    }
}
