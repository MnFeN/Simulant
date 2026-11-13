namespace Simulant.Simulation.Mechanics
{
    /// <summary>机制判定失败的处理级别，不改变成功或失败的判定结果。</summary>
    public enum MechanicFailureSeverity
    {
        /// <summary>记录并提示失误，继续后续结算和时间轴。</summary>
        Warning,
        /// <summary>播放当前演出后中断模拟，保留现场。</summary>
        Fatal,
    }
}
