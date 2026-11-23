using System;
using System.Linq;

namespace Simulant.Content.U7b.Presets
{
    /// <summary>以 BABC 开始读条为原点的秒数。机制时序取自参考日志，人员分工由解法规划器决定。</summary>
    internal static class ForsakenTiming
    {
        // 每组包含两轮塔，强制移动前后两轮各有自己的起始时刻。
        private const double TowerPairInterval = 21.027;
        internal static double TowerShow(int index) => (index % 2 == 0 ? 10.047 : 20.072) + TowerPairInterval * (index / 2);
        internal static double TowerHit(int index) => (index % 2 == 0 ? 20.183 : 30.215) + TowerPairInterval * (index / 2);
        internal static double SpellHit(int index) => (index % 2 == 0 ? 20.851 : 30.860) + TowerPairInterval * (index / 2);
        internal static double EndCast(int pair) => 23.140 + 20.928 * pair;

        private const double FirstCleaveSpacing = 0.0447;
        internal static double JumpHit(int pair, int source) => JumpHits[pair, source];
        internal static double CleaveLock(int pair, int source) => pair == 0
            ? 36.079 - FirstCleaveSpacing * source : LaterCleaveLocks[pair - 1, source];
        internal static double CleaveHit(int pair, int source) => pair == 0
            ? 41.0373 - FirstCleaveSpacing * source : LaterCleaveHits[pair - 1, source];

        // 二维数组的来源下标始终为本体、复制体一、复制体二、复制体三。
        private static readonly double[,] JumpHits =
        {
            {
                29.835,
                29.789,
                29.879,
                29.923
            },
            {
                50.713,
                50.891,
                50.757,
                50.757
            },
            {
                71.626,
                71.671,
                71.805,
                71.671
            },
            {
                92.799,
                92.576,
                92.620,
                92.620
            },
        };
        private static readonly double[,] LaterCleaveLocks =
        {
            {
                56.955,
                56.999,
                56.999,
                56.779
            },
            {
                77.862,
                77.682,
                77.907,
                77.907
            },
            {
                99.015,
                98.656,
                98.611,
                98.836
            },
        };
        private static readonly double[,] LaterCleaveHits =
        {
            {
                61.912,
                61.955,
                61.955,
                61.777
            },
            {
                82.852,
                82.674,
                82.897,
                82.897
            },
            {
                103.976,
                103.618,
                103.573,
                103.797
            },
        };

        // 偶数轮跳跃早于踩塔；全队必须先就位，避免跑动中的踩塔者误接近点圆。
        internal static double TowerPositionSnapshot(int index) => index % 2 == 0
            ? TowerHit(index) : Math.Min(TowerHit(index), RowMin(JumpHit, index / 2));
        internal static double TowerArrival(int index) => index > 0 && index % 2 == 0
            ? Math.Min(TowerHit(index), RowMin(CleaveHit, index / 2 - 1))
            : TowerPositionSnapshot(index);
        // 这里只声明机制允许离开和要求到位的时刻，具体何时出发、提前多久到达由实体决定。
        internal static double TowerMovementAvailable(int index) => index == 0 ? 8.7 : index % 2 == 0
            ? RowMax(CleaveLock, index / 2 - 1) + 0.01 : SpellHit(index - 1) + 0.15;
        internal static double GatheringArrival(int pair) => RowMin(CleaveLock, pair);
        internal static double RowMin(Func<int, int, double> time, int pair) => Enumerable.Range(0, 4).Min(source => time(pair, source));
        internal static double RowMax(Func<int, int, double> time, int pair) => Enumerable.Range(0, 4).Max(source => time(pair, source));
    }
}
