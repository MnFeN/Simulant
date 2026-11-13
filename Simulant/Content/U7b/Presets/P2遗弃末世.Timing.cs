using System;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U7b.Presets
{
    /// <summary>以 BABC 开始读条为原点的秒数。机制时序取自参考日志，人员分工由解法规划器决定。</summary>
    internal static class ForsakenTiming
    {
        // ActorMove 速度字节为每秒距离乘以 10；正常跑速为 6。
        // 网络移动控制器按经过时间发送沿途位置，不能把远端终点当作原生定速导航指令。
        // 换算依据：https://github.com/anomek/AnoMech/blob/1646a7068c2071a3d1fd9e3c84550dfd3f1b6ade/AnoMech/Core/Native/Implementations/BattleCharaProxy.cs
        internal const byte MoveSpeedParam = 60;
        internal const double MoveSpeed = MoveSpeedParam / 10.0;
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
        internal static double TowerArrival(int index) => TowerPositionSnapshot(index) - (index % 2 == 0 ? 0.85 : 0.90);
        internal static double TowerMovementAvailable(int index) => index == 0 ? 8.7 : index % 2 == 0
            ? RowMax(CleaveLock, index / 2 - 1) + 0.1 : SpellHit[index - 1] + 0.15;
        internal static double Departure(Vector2 from, Vector2 to, double arrival) => arrival - Vector2.Distance(from, to) / MoveSpeed;
        internal static double RowMin(double[,] values, int row) => Enumerable.Range(0, 4).Min(i => values[row, i]);
        internal static double RowMax(double[,] values, int row) => Enumerable.Range(0, 4).Max(i => values[row, i]);
    }
}
