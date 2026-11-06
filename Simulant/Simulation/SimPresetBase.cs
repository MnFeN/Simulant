using Simulant.Core;
using Simulant.Core.Environment;
using Simulant.Simulation.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simulant.Simulation
{
    /// <summary> 模拟副本预设的元数据。</summary>
    public abstract class SimPresetBase
    {
        public abstract int TerritoryId { get; } // to-do：这个和 Preset 的 Attribute 重复了
        public abstract string Name { get; }
        public abstract string Author { get; }
        public abstract DateTime LastUpdated { get; }
        public abstract PhaseData Phase { get; }
        public abstract string Description { get; }
        /// <summary>占位预设设为 false，保留展示信息但禁止创建模拟会话。</summary>
        public virtual bool IsImplemented => true;

        /// <summary> 全部 Option，包括纯 Label 这种仅用于显示 UI 的假 Option。</summary>
        public abstract List<SimOptionBase> Options { get; }
        public abstract int Level { get; }
        public abstract Type SimLogicType { get; }

        public override string ToString() => $"[模拟] {Name}";

        public SimSession CreateSimSession(PluginHost host)
        {
            if (!IsImplemented)
                throw new InvalidOperationException($"预设「{Name}」尚未实现，暂不能开始模拟。");
            return new SimSession(host, this);
        }

    }

}
