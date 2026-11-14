using System;

namespace Simulant.Simulation.Mechanics
{
    /// <summary>机制判定失败；会话中断后保留现场，等待手动停止。</summary>
    public sealed class SimMechanicFailedException : InvalidOperationException
    {
        public bool AlreadyReported { get; }

        public SimMechanicFailedException(string reason) : base(reason)
        {
        }

        public SimMechanicFailedException(string mechanicName, string reason, bool alreadyReported = false)
            : base(mechanicName + "：" + reason)
        {
            AlreadyReported = alreadyReported;
        }
    }
}
