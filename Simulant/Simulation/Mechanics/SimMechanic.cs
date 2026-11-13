using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Simulant.Simulation.Mechanics
{
    /// <summary>单次机制的开始、判定、结果与清理。实例只能使用一次，重复播放应创建新实例。</summary>
    public abstract class SimMechanic
    {
        private readonly object _gate = new object();
        private readonly List<SimScheduledAction> _nodes = new List<SimScheduledAction>();
        private bool _claimed;
        private bool _begun;
        private int _claimAttempted;
        private volatile bool _stopping;
        private volatile bool _cleaning;
        private volatile bool _completed;
        private bool _onCleanupFinished;
        private bool _cleanupCallbackFinished;
        private Action<SimMechanic> _released;

        public string Name { get; set; } = "机制";
        public double ResolveAfter { get; set; }
        public Action<MechanicContext> Started { get; set; }
        public Action<MechanicContext> Cleanup { get; set; }
        /// <summary>判定后先播放演出，无论成功失败；机制组会先播放全部子机制演出，再处理结果。</summary>
        public Action<MechanicResult> Visual { get; set; }
        public Action<MechanicResult> Succeeded { get; set; }
        public Action<MechanicResult> Failed { get; set; }
        public Action<MechanicResult> Resolved { get; set; }
        public MechanicResult Result { get; private set; }
        protected MechanicContext Context { get; private set; }
        protected virtual IReadOnlyList<SimMechanic> Children => Array.Empty<SimMechanic>();
        internal bool IsCleaning => _cleaning;
        internal bool HasFailedCleanup => _stopping && !_cleaning && !_completed;

        internal void Claim(MechanicContext context, Action<SimMechanic> released = null)
        {
            if (Interlocked.CompareExchange(ref _claimAttempted, 1, 0) != 0)
                throw new InvalidOperationException("机制实例只能运行一次；重复播放请创建新实例。");
            lock (_gate)
            {
                SimTimeline.ValidateSeconds(ResolveAfter, nameof(ResolveAfter));
                ValidateCallbacks();
                Validate();
                _claimed = true;
                Context = context.Bind(ScheduleAfter);
                _released = released;
            }
        }

        internal void Begin(bool scheduleResolution = true)
        {
            lock (_gate)
            {
                if (_stopping || _completed) return;
                _begun = true;
                try
                {
                    if (!Context.IsWorldValid) { Stop(); return; }
                    OnStart();
                    if (_stopping) return;
                    if (!Context.IsWorldValid) { Stop(); return; }
                    Started?.Invoke(Context);
                    if (!_stopping && scheduleResolution)
                        ScheduleAfter(ResolveAfter, Resolve, Name + "判定");
                }
                catch { Interrupt(); throw; }
            }
        }

        protected virtual void Validate() { }
        protected virtual void OnStart() { }
        protected abstract MechanicResult Evaluate(PartySnapshot snapshot);
        protected virtual void OnCleanup() { }

        /// <summary>子类可增加同步节点；机制结束或停止时，尚未执行的节点统一取消。</summary>
        protected void ScheduleAfter(double seconds, Action action, string name = null)
        {
            SimTimeline.ValidateAction(action, nameof(action));
            lock (_gate)
            {
                if (!_claimed || _stopping || _completed)
                    throw new InvalidOperationException("机制尚未开始或正在停止，不能继续添加节点。");
                var node = Context.Scheduler.ScheduleRelativeToNode(seconds, () =>
                {
                    lock (_gate)
                    {
                        if (_stopping || _completed) return;
                        try
                        {
                            if (!Context.IsWorldValid) { Stop(); return; }
                            action();
                        }
                        catch { Interrupt(); throw; }
                    }
                }, name ?? Name);
                _nodes.Add(node);
            }
        }

        private void Resolve()
        {
            var snapshot = PartySnapshot.Capture(Context.Candidates);
            var result = EvaluateForGroup(snapshot);
            PresentForGroup(result);
            PublishForGroup(result);
            Stop();
        }

        internal MechanicResult EvaluateForGroup(PartySnapshot snapshot)
        {
            lock (_gate)
            {
                if (!_claimed || _stopping) throw new InvalidOperationException("已停止的机制不能继续判定。");
                return Evaluate(snapshot) ?? throw new InvalidOperationException("机制没有返回判定结果。");
            }
        }

        internal virtual void PresentForGroup(MechanicResult result)
        {
            lock (_gate)
            {
                if (_stopping || _completed) return;
                if (!Context.IsWorldValid) { Stop(); return; }
                Visual?.Invoke(result);
            }
        }

        internal virtual void PublishForGroup(MechanicResult result)
        {
            lock (_gate)
            {
                if (_stopping || _completed) return;
                if (!Context.IsWorldValid) { Stop(); return; }
                Result = result;
                if (result.Success) Succeeded?.Invoke(result);
                else
                {
                    Failed?.Invoke(result);
                    if (result.FailureSeverity == MechanicFailureSeverity.Fatal)
                        throw new SimMechanicFailedException(Name, result.Reason);
                }
                if (!_stopping && Context.IsWorldValid) Resolved?.Invoke(result);
            }
        }

        internal void Stop()
        {
            lock (_gate)
            {
                if (!_claimed || _completed || _cleaning) return;
                _stopping = true;
                _cleaning = true;
                try
                {
                    foreach (var node in _nodes) node.Cancel();
                    _nodes.Clear();
                    List<Exception> errors = null;
                    foreach (var child in Children)
                    {
                        try { child.Stop(); }
                        catch (Exception error) { (errors ?? (errors = new List<Exception>())).Add(error); }
                    }
                    if (errors != null) throw new AggregateException("部分子机制清理失败，请重试停止。", errors);
                    if (_begun && Context.IsWorldValid)
                    {
                        if (!_onCleanupFinished)
                        {
                            OnCleanup();
                            _onCleanupFinished = true;
                        }
                        if (!_cleanupCallbackFinished && Context.IsWorldValid)
                        {
                            Cleanup?.Invoke(Context);
                            _cleanupCallbackFinished = true;
                        }
                    }
                    _completed = true;
                    _released?.Invoke(this);
                }
                finally { _cleaning = false; }
            }
        }

        /// <summary>中断未执行节点，保留实体、演出及清理责任，直到显式停止会话。</summary>
        internal void Interrupt()
        {
            lock (_gate)
            {
                if (!_claimed || _completed) return;
                _stopping = true;
                foreach (var node in _nodes) node.Cancel();
                _nodes.Clear();
                foreach (var child in Children) child.Interrupt();
            }
        }

        private void ValidateCallbacks()
        {
            if (Started != null) SimTimeline.ValidateAction(Started, nameof(Started));
            if (Cleanup != null) SimTimeline.ValidateAction(Cleanup, nameof(Cleanup));
            if (Visual != null) SimTimeline.ValidateAction(Visual, nameof(Visual));
            if (Succeeded != null) SimTimeline.ValidateAction(Succeeded, nameof(Succeeded));
            if (Failed != null) SimTimeline.ValidateAction(Failed, nameof(Failed));
            if (Resolved != null) SimTimeline.ValidateAction(Resolved, nameof(Resolved));
        }
    }
}
