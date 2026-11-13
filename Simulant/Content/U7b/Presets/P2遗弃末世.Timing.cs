using System;
using System.Linq;

namespace Simulant.Content.U7b.Presets
{
    /// <summary>以 BABC 开始读条为原点的秒数。机制时序取自参考日志，人员分工由解法规划器决定。</summary>
    internal static class ForsakenTiming
    {
        internal static readonly double[] TowerShow = { 10.040, 20.068, 31.082, 41.081, 52.093, 62.134, 73.136, 83.165 };
        internal static readonly double[] TowerHit = { 20.202, 30.191, 41.216, 51.247, 62.222, 72.294, 83.253, 93.290 };
        internal static readonly double[] SpellHit = { 20.871, 30.816, 41.883, 51.914, 62.891, 72.958, 83.922, 93.914 };
        internal static readonly double[] EndCast = { 23.138, 44.074, 64.988, 85.926 };
        // 二维数组的来源下标始终为本体、复制体一、复制体二、复制体三。
        internal static readonly double[,] JumpHit =
        {
            { 29.835, 29.789, 29.879, 29.923 }, { 50.713, 50.891, 50.757, 50.757 },
            { 71.626, 71.671, 71.805, 71.671 }, { 92.799, 92.576, 92.620, 92.620 },
        };
        internal static readonly double[,] CleaveLock =
        {
            { 36.078, 36.035, 35.990, 35.944 }, { 56.955, 56.999, 56.999, 56.779 },
            { 77.862, 77.682, 77.907, 77.907 }, { 99.015, 98.656, 98.611, 98.836 },
        };
        internal static readonly double[,] CleaveHit =
        {
            { 41.037, 40.993, 40.948, 40.903 }, { 61.912, 61.955, 61.955, 61.777 },
            { 82.852, 82.674, 82.897, 82.897 }, { 103.976, 103.618, 103.573, 103.797 },
        };

        // 偶数轮跳跃早于踩塔；全队必须先就位，避免跑动中的踩塔者误接近点圆。
        internal static double TowerPositionSnapshot(int index) => index % 2 == 0
            ? TowerHit[index] : Math.Min(TowerHit[index], RowMin(JumpHit, index / 2));
        internal static double TowerArrival(int index) => index > 0 && index % 2 == 0
            ? Math.Min(TowerHit[index], RowMin(CleaveHit, index / 2 - 1))
            : TowerPositionSnapshot(index);
        // 这里只声明机制允许离开和要求到位的时刻，具体何时出发、提前多久到达由实体决定。
        internal static double TowerMovementAvailable(int index) => index == 0 ? 8.7 : index % 2 == 0
            ? RowMax(CleaveLock, index / 2 - 1) + 0.01 : SpellHit[index - 1] + 0.15;
        internal static double GatheringArrival(int pair) => RowMin(CleaveLock, pair);
        internal static double RowMin(double[,] values, int row) => Enumerable.Range(0, 4).Min(i => values[row, i]);
        internal static double RowMax(double[,] values, int row) => Enumerable.Range(0, 4).Max(i => values[row, i]);
    }
}
