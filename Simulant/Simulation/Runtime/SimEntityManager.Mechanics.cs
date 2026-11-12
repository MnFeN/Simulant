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
