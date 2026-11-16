using System;

namespace Simulant.Utils.Maths
{
    /// <summary>角度与弧度的统一换算；内部计算保持双精度，原生接口需要时再转为 float。</summary>
    public static class Angles
    {
        /// <summary>将角度转为弧度，例如 Deg(90) 表示四分之一圈。</summary>
        public static double Deg(double degree) => degree * (Math.PI / 180);

        /// <summary>将弧度转为角度，用于显示角度或与角度配置比较。</summary>
        public static double ToDegrees(double radian) => radian / (Math.PI / 180);
    }
}
