using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.Local
{
    // Script slots are shared by the party plan, selectors and the frozen resolution.
    internal sealed class LocalTargets
    {
        internal static readonly int[] AllSlots = Enumerable.Range(0, 8).ToArray();
        private readonly Func<int, Vector2?> _position;
        private readonly Func<string, Vector2?> _origin;
        private readonly Func<string, bool, IEnumerable<int>> _enmity;
        private readonly Action<LocalEvent, int> _draw;
        private readonly Dictionary<string, int> _holders = new Dictionary<string, int>();
        private readonly Dictionary<string, LocalEvent> _active = new Dictionary<string, LocalEvent>();
        internal bool Tracking => _active.Count != 0;

        internal LocalTargets(Func<int, Vector2?> position, Func<string, Vector2?> origin,
            Func<string, bool, IEnumerable<int>> enmity, Action<LocalEvent, int> draw)
        {
            _position = position;
            _origin = origin;
            _enmity = enmity;
            _draw = draw;
        }

        internal int Holder(string key) => _holders.TryGetValue(key, out var slot) ? slot : -1;

        internal int Current(string key)
        {
            if (_active.TryGetValue(key, out var line)) Refresh(line, false);
            return Holder(key);
        }

        internal void Start(LocalEvent e)
        {
            if (e.Op == "trackTether")
            {
                CancelActor(e.Actor);
                _active[e.Key] = e;
            }
            _holders[e.Key] = -1;
            Refresh(e, true);
        }

        internal void Refresh()
        {
            foreach (var e in _active.Values) Refresh(e, false);
        }

        private void Refresh(LocalEvent e, bool initial)
        {
            var old = Holder(e.Key);
            var center = _origin(e.Actor);
            var next = -1;
            if (center.HasValue)
            {
                var candidates = (e.Slots ?? AllSlots).OrderBy(s => s).Select(s => (Slot: s, Point: _position(s)))
                    .Where(p => p.Point.HasValue).ToArray();
                if (e.InterceptRadius > 0 && old >= 0 && candidates.Any(p => p.Slot == old))
                {
                    var end = candidates.First(p => p.Slot == old).Point.Value;
                    var segment = end - center.Value;
                    var length = segment.LengthSquared();
                    next = candidates.Where(p => p.Slot != old).Select(p =>
                    {
                        var t = length == 0 ? 0 : Vector2.Dot(p.Point.Value - center.Value, segment) / length;
                        var distance = Vector2.DistanceSquared(p.Point.Value, center.Value + t * segment);
                        return (p.Slot, T: t, Distance: distance, FromSource: Vector2.DistanceSquared(p.Point.Value, center.Value));
                    }).Where(p => p.T >= 0 && p.T < 1 && p.Distance <= e.InterceptRadius * e.InterceptRadius && p.FromSource < length)
                        .OrderBy(p => p.T).Select(p => p.Slot).DefaultIfEmpty(old).First();
                }
                else if (initial && e.Role >= 0 && candidates.Any(p => p.Slot == e.Role)) next = e.Role;
                else
                {
                    IEnumerable<int> order;
                    if (e.By == "enmity" || e.By == "firstEnmity")
                        order = _enmity(e.Actor, e.By == "firstEnmity").Where(s => candidates.Any(p => p.Slot == s));
                    else
                        order = candidates.OrderBy(p => (e.By == "nearest" ? 1 : -1) *
                            Vector2.DistanceSquared(p.Point.Value, center.Value)).Select(p => p.Slot);
                    next = order.Skip(e.Rank - 1).DefaultIfEmpty(-1).First();
                }
            }
            _holders[e.Key] = next;
            if (next != old && e.Op == "trackTether") _draw(e, next);
        }

        internal void Resolve(string key)
        {
            if (!_active.TryGetValue(key, out var e)) return;
            Refresh(e, false);
            _active.Remove(key);
            if (Holder(key) >= 0) _draw(e, -1);
        }

        internal void CancelActor(string actor)
        {
            foreach (var e in _active.Values.Where(line => line.Actor == actor).ToArray())
            {
                if (Holder(e.Key) >= 0) _draw(e, -1);
                _holders[e.Key] = -1;
                _active.Remove(e.Key);
            }
        }

        internal void Clear()
        {
            foreach (var line in _active.Values)
                if (Holder(line.Key) >= 0) _draw(line, -1);
            _active.Clear();
            _holders.Clear();
        }
    }
}
