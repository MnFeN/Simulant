using System;

namespace Simulant.Simulation.Runtime
{
    /// <summary>假玩家的可选行为；每名角色持有自己的副本，不接管真实玩家。</summary>
    public sealed class SimActorBehaviorOptions
    {
        public bool AttackTarget { get; set; } = true;
        public double IdleRadius { get; set; } = 3;
        /// <summary>相对直达方向的初始偏角上限，单位为度。</summary>
        public double RouteAngleDegrees { get; set; } = 30;
        /// <summary>接近目标时间再完成站位；关闭时放弃滑步、贪刀与绕路，立即直达，紧急移动同理。</summary>
        public bool DelayArrival { get; set; } = true;

        internal SimActorBehaviorOptions Copy()
        {
            if (double.IsNaN(IdleRadius) || double.IsInfinity(IdleRadius) || IdleRadius < 0 || IdleRadius > 3)
                throw new ArgumentOutOfRangeException(nameof(IdleRadius), "空闲走动半径必须在 0 至 3 米之间。");
            if (double.IsNaN(RouteAngleDegrees) || double.IsInfinity(RouteAngleDegrees) || RouteAngleDegrees < 0 || RouteAngleDegrees > 90)
                throw new ArgumentOutOfRangeException(nameof(RouteAngleDegrees), "绕路角度范围必须在 0 至 90 度之间。");
            return (SimActorBehaviorOptions)MemberwiseClone();
        }
    }
}
