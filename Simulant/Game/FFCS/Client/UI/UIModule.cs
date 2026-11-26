using Simulant.Game.FFCS.Client.UI.Agent;
using Simulant.Game.FFCS.Client.System.Input;
using Simulant.Game.FFCS.Client.UI.Misc;
using Simulant.Game.FFCS.Component.GUI;
using System;

namespace Simulant.Game.FFCS.Client.UI
{
    public struct UIModule : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        public AgentModule AgentModule
            => Ptr.CallVFunc<IntPtr>(37).As<AgentModule>();

        internal RaptureShellModule ShellModule => Ptr.CallVFunc<IntPtr>(9).As<RaptureShellModule>();

        public AtkModule AtkModule => Ptr.CallVFunc<IntPtr>(7).As<AtkModule>();
        public RaptureHotbarModule HotbarModule => Ptr.CallVFunc<IntPtr>(13).As<RaptureHotbarModule>();
        // GetUIInputData 返回的对象以 InputData 为基类。
        public InputData InputData => Ptr.CallVFunc<IntPtr>(81).As<InputData>();

        /// <summary>使用区域切换的 FadeMiddle 层，帧数按 59.94 帧/秒计算。</summary>
        internal void FadeMiddle(bool toBlack, uint frames)
            => Ptr.CallVFunc(toBlack ? 149 : 148, 0u, frames, 1u, (byte)0, 0u);
    }
}
