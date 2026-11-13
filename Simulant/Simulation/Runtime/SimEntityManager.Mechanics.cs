using Simulant.Simulation.Mechanics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulant.Simulation.Runtime
{
    public sealed partial class SimEntityManager
    {
        private readonly List<SimMechanic> _mechanics = new List<SimMechanic>();

        /// <summary>使用本会话小队和调度器开始单次机制；创建成功前即持有其清理责任。</summary>
        public void RunMechanic(SimMechanic mechanic)
        {
            if (mechanic == null) throw new ArgumentNullException(nameof(mechanic));
            lock (_gate)
            {
                EnsureAcceptingEntities();
                mechanic.Claim(new MechanicContext(this, _scheduler, Party), ReleaseMechanic);
                _mechanics.Add(mechanic);
            }
            mechanic.Begin();
        }

        private void ReleaseMechanic(SimMechanic mechanic)
        {
            lock (_gate) _mechanics.Remove(mechanic);
        }

        /// <summary>中断机制和移动，保留角色、读条与已展示特效，交由显式停止时清理。</summary>
        internal void Interrupt()
        {
            SimMechanic[] mechanics;
            SimActor[] actors;
            lock (_gate)
            {
                _acceptingEntities = false;
                _partyEnmityPoll?.Cancel();
                _partyEnmityPoll = null;
                _networkGeneration++;
                _networkPoll?.Cancel();
                _networkPoll = null;
                foreach (var pending in _networkSpawns) pending.OnReady = null;
                mechanics = _mechanics.ToArray();
                actors = _actors.Values.ToArray();
            }
            List<Exception> errors = null;
            foreach (var mechanic in mechanics)
            {
                try { mechanic.Interrupt(); }
                catch (Exception error) { (errors ?? (errors = new List<Exception>())).Add(error); }
            }
            foreach (var actor in actors)
            {
                try { actor.StopMoving(); }
                catch (Exception error) { (errors ?? (errors = new List<Exception>())).Add(error); }
            }
            if (errors != null) throw new AggregateException("部分机制或移动中断失败，已保留现场及清理责任。", errors);
        }

        private void ClearMechanics()
        {
            SimMechanic[] mechanics;
            lock (_gate) mechanics = _mechanics.ToArray();
            List<Exception> errors = null;
            foreach (var mechanic in mechanics)
            {
                try { mechanic.Stop(); }
                catch (Exception error) { (errors ?? (errors = new List<Exception>())).Add(error); }
            }
            if (errors != null) throw new AggregateException("部分机制清理失败，已保留角色和清理责任。", errors);
        }
    }
}
