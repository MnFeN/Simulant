using Simulant.Game.FFCS.Client.UI.Agent;
using System;

namespace Simulant.Game.FFCS.Client.UI
{
    public struct UIModule : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        public AgentModule AgentModule
            => Ptr.CallVFunc<IntPtr>(37).As<AgentModule>();
    }
}