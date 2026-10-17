using System;

namespace Simulant.Game.FFCS.Client.UI.Agent
{
    public struct AgentModule : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // +0x20 AgentInterface* Agents[509]

        public IntPtr GetAgentPtr(AgentId agentId)
            => Ptr.ReadPtr(0x20 + (int)agentId * IntPtr.Size);

        public AgentLookingForGroup LookingForGroup
            => GetAgentPtr(AgentId.LookingForGroup).As<AgentLookingForGroup>();
    }

    public enum AgentId : uint
    {
        LookingForGroup = 130
    }
}