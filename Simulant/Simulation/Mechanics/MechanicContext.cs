using Simulant.Core.Entity;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulant.Simulation.Mechanics
{
    /// <summary>本次机制所用的会话资源与候选队员；不创建额外线程或调度器。</summary>
    public sealed class MechanicContext
    {
        private readonly Character[] _worldAnchors;
        private readonly Action<double, Action, string> _schedule;
        public SimEntityManager Entities { get; }
        public SimScheduler Scheduler { get; }
        public IReadOnlyList<SimActor> Candidates { get; }

        /// <summary>世界失效后，旧实体身份全部无效，不再执行可能触碰新地图的回调。</summary>
        public bool IsWorldValid => _worldAnchors.Any(actor => actor.IsAlive);

        /// <summary>相对当前节点的计划时间排入同步动作；机制停止时一并取消。</summary>
        public void ScheduleAfter(double seconds, Action action, string name = null)
        {
            if (_schedule == null) throw new InvalidOperationException("机制尚未绑定生命周期，不能添加节点。");
            _schedule(seconds, action, name);
        }

        internal MechanicContext(SimEntityManager entities, SimScheduler scheduler, IEnumerable<SimActor> candidates)
        {
            Entities = entities;
            Scheduler = scheduler;
            var actors = candidates.ToArray();
            Candidates = Array.AsReadOnly(actors);
            _worldAnchors = actors.Where(actor => actor != null && actor.IsActive).Select(actor => actor.Entity).ToArray();
            if (_worldAnchors.Length == 0)
                throw new InvalidOperationException("机制至少需要一名有效候选角色作为世界身份依据。");
        }

        private MechanicContext(MechanicContext original, Action<double, Action, string> schedule)
        {
            Entities = original.Entities;
            Scheduler = original.Scheduler;
            Candidates = original.Candidates;
            _worldAnchors = original._worldAnchors;
            _schedule = schedule;
        }

        internal MechanicContext Bind(Action<double, Action, string> schedule) => new MechanicContext(this, schedule);
    }
}
