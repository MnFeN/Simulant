using Simulant.Content.U6b;
using Simulant.Core;
using Simulant.Core.Environment;
using Simulant.Simulation;
using Simulant.Simulation.Options;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Data;

namespace Simulant.Content.U6b.Presets
{
    internal class 屏幕探测 : SimPresetBase
    {
        public override int TerritoryId { get; } = 1122;
        public override string Name { get; } = "P3 屏幕探测";
        public override string Author { get; } = "Simulant";
        public override DateTime LastUpdated { get; } = new DateTime(2026, 4, 21);
        public override PhaseData Phase { get; } = U6b.P3;
        public override int Level => 90;
        public override Type SimLogicType { get; } = typeof(U6b3_屏幕探测Logic);
        public override string Description { get; } = "占位符";
        public override bool IsImplemented => false;

        public override List<SimOptionBase> Options { get; } = new List<SimOptionBase>
        {
        };
    }

    internal class U6b3_屏幕探测Logic : SimLogicBase
    {
        public U6b3_屏幕探测Logic(PluginHost host, SimPresetBase preset) : base(host, preset)
        {
        }

        protected override void BuildTimeline(SimTimeline timeline)
        {
            throw new NotImplementedException();
        }

        protected override void OnStop()
        {
            throw new NotImplementedException();
        }
    }
}
