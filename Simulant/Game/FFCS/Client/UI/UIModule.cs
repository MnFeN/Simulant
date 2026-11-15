using Simulant.Game.FFCS.Client.UI.Agent;
using System;

namespace Simulant.Game.FFCS.Client.UI
{
    public struct UIModule : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        public AgentModule AgentModule
            => Ptr.CallVFunc<IntPtr>(37).As<AgentModule>();

        public AtkModule AtkModule => Ptr.CallVFunc<IntPtr>(7).As<AtkModule>();
        public RaptureHotbarModule HotbarModule => Ptr.CallVFunc<IntPtr>(13).As<RaptureHotbarModule>();
        public UIInputData InputData => Ptr.CallVFunc<IntPtr>(81).As<UIInputData>();
    }
}
