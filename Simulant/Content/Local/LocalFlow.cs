using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulant.Content.Local
{
    // One instance per round. All callbacks and observations run on the session scheduler.
    internal sealed class LocalFlow
    {
        private readonly SimScheduler _scheduler;
        private readonly Action<LocalEvent> _run;
        private readonly Func<LocalCondition, bool> _readWorld;
        private readonly IReadOnlyDictionary<string, string> _options;
        private readonly Dictionary<string, bool> _results = new Dictionary<string, bool>();
        private readonly List<Pending> _listeners = new List<Pending>();
        private bool _finished;
        internal int Member { get; private set; } = -1;
        private readonly Func<int, bool> _present;

        private sealed class Pending
        {
            internal LocalEvent Step;
            internal SimScheduledAction Deadline;
            internal int Member;
        }

        internal LocalFlow(SimScheduler scheduler, Action<LocalEvent> run, Func<LocalCondition, bool> readWorld,
            IReadOnlyDictionary<string, string> options, Func<int, bool> present = null)
        {
            _scheduler = scheduler;
            _run = run;
            _readWorld = readWorld;
            _options = options;
            _present = present;
        }

        internal void Play(IEnumerable<LocalEvent> steps, int member = -1)
        {
            if (steps == null || _finished) return;
            var timeline = new SimTimeline();
            foreach (var step in steps)
                timeline.At(step.At, () => Dispatch(step, member), step.Path);
            _scheduler.Play(timeline);
        }

        private void Dispatch(LocalEvent e, int member)
        {
            if (_finished) return;
            Member = member;
            switch (e.Op)
            {
                case "branch":
                    Play(Test(e.Condition) ? e.Then : e.Else, member);
                    return;
                case "forParty":
                    EachMember(e, _scheduler.ElapsedSeconds + e.Duration);
                    return;
                case "trigger":
                    if (e.On == null)
                        Watch(e, _scheduler.ElapsedSeconds + e.Timeout, member);
                    else
                    {
                        var pending = new Pending { Step = e, Member = member };
                        _listeners.Add(pending);
                        pending.Deadline = _scheduler.ScheduleAfter(e.Timeout, () =>
                        {
                            _listeners.Remove(pending);
                            Play(e.Else, member);
                        }, e.Path + " 超时");
                    }
                    return;
                case "finish":
                    _finished = true;
                    foreach (var listener in _listeners) listener.Deadline.Cancel();
                    _listeners.Clear();
                    break;
            }
            _run(e);
        }

        private void EachMember(LocalEvent e, double deadline)
        {
            if (_finished || (e.Interval > 0 && _scheduler.ElapsedSeconds >= deadline)) return;
            foreach (var slot in e.Slots ?? LocalTargets.AllSlots)
                if (_present == null || _present(slot)) Play(e.Then, slot);
            if (e.Interval > 0 && _scheduler.ElapsedSeconds + e.Interval < deadline)
                _scheduler.ScheduleAfter(e.Interval, () => EachMember(e, deadline), e.Path);
        }

        private void Watch(LocalEvent e, double deadline, int member)
        {
            if (_finished) return;
            Member = member;
            if (Test(e.Condition)) Play(e.Then, member);
            else if (_scheduler.ElapsedSeconds >= deadline) Play(e.Else, member);
            else _scheduler.Schedule(Math.Min(deadline, _scheduler.ElapsedSeconds + 0.1), () => Watch(e, deadline, member), e.Path);
        }

        internal bool Test(LocalCondition c)
        {
            switch (c.Type)
            {
                case "all": return c.Children.All(Test);
                case "any": return c.Children.Any(Test);
                case "not": return !Test(c.Children[0]);
                case "optionEquals": return _options[c.Key] == c.Value;
                case "checkResult": return _results.TryGetValue(c.Key, out var result) && result == c.Passed;
                default: return _readWorld(c);
            }
        }

        internal void Record(string id, bool passed)
        {
            if (id == null || _finished) return;
            _results[id] = passed;
            Emit(new LocalSignal { Type = "checkResolved", Check = id, Passed = passed });
        }

        internal void Emit(LocalSignal signal)
        {
            foreach (var listener in _listeners.Where(p => p.Step.On.Matches(signal)).ToArray())
            {
                listener.Deadline.Cancel();
                _listeners.Remove(listener);
                Play(listener.Step.Then, listener.Member);
            }
        }
    }
}
