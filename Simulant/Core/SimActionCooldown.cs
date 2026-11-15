using System;
using System.Diagnostics;

namespace Simulant.Core
{
    /// <summary>只读冷却快照，供快捷键、聊天指令与悬浮窗共用。</summary>
    internal readonly struct SimActionCooldown
    {
        private readonly long _started;
        internal readonly double Duration;
        internal double Remaining => Duration == 0 ? 0 :
            Math.Max(0, Duration - (Stopwatch.GetTimestamp() - _started) / (double)Stopwatch.Frequency);

        internal SimActionCooldown(double duration)
        {
            _started = Stopwatch.GetTimestamp();
            Duration = duration;
        }
    }
}
