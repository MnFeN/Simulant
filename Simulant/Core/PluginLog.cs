using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Simulant.Core
{
    public enum LogType { Error, Warning, Runtime, Sim, Verbose }

    internal sealed class PluginLog
    {
        private const int MaxCountPerLevel = 30000;

        private readonly Dictionary<LogType, Queue<LogItem>> _log =
            Enum.GetValues(typeof(LogType))
                .Cast<LogType>()
                .ToDictionary(level => level, level => new Queue<LogItem>());

        internal void Clear()
        {
            foreach (var pair in _log)
            {
                lock (pair.Value)
                {
                    pair.Value.Clear();
                }
            }
        }

        internal void Add(LogType type, string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            var item = new LogItem
            {
                Timestamp = DateTime.Now,
                Type = type,
                Message = message
            };

            var queue = _log[type];
            lock (queue)
            {
                queue.Enqueue(item);
                if (queue.Count > MaxCountPerLevel)
                {
                    queue.Dequeue();
                }
            }
        }

        internal void Remove(IEnumerable<LogItem> items)
        {
            if (items == null)
                return;

            var set = new HashSet<LogItem>(items.Where(x => x != null));

            if (set.Count == 0)
                return;

            foreach (var pair in _log)
            {
                var queue = pair.Value;

                lock (queue)
                {
                    if (queue.Count == 0)
                        continue;

                    var kept = queue
                        .Where(x => !set.Contains(x))
                        .ToList();

                    queue.Clear();

                    foreach (var item in kept)
                        queue.Enqueue(item);
                }
            }
        }

        internal Dictionary<LogType, List<LogItem>> Snapshot()
        {
            var result = new Dictionary<LogType, List<LogItem>>();

            foreach (var pair in _log)
            {
                var queue = pair.Value;
                lock (queue)
                {
                    result[pair.Key] = queue.ToList();
                }
            }

            return result;
        }
    }

    internal sealed class LogItem
    {
        public DateTime Timestamp { get; set; }
        public LogType Type { get; set; }
        public string Message { get; set; }
        public string TypeDescription => _typeToDesc.TryGetValue(Type, out var desc) ? desc : Type.ToString();

        private static readonly Dictionary<LogType, string> _typeToDesc = new Dictionary<LogType, string>
        {
            { LogType.Error,    "错误" },
            { LogType.Warning,  "警告" },
            { LogType.Runtime,  "运行" },
            { LogType.Sim,      "模拟" },
            { LogType.Verbose,  "详细" },
        };
    }
}
