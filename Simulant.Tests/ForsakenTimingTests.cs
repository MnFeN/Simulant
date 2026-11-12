using Simulant.Content.U7b.Presets;
using System;
using System.Linq;
using System.Numerics;
using static Simulant.Content.U7b.Presets.ForsakenTiming;

namespace Simulant.Tests
{
    internal static class ForsakenTimingTests
    {
        internal static void EveryRouteArrivesInTheLastSecondWithoutLeavingBeforeThePreviousSnapshot()
        {
            for (var seed = 0; seed < 512; seed++)
            {
                var plan = ForsakenPlan.Create(new Random(seed));
                var previous = Enumerable.Range(0, 8).Select(ForsakenPlan.InitialPosition).ToArray();
                for (var index = 0; index < 8; index++)
                {
                    var round = plan.Rounds[index];
                    var arrival = TowerArrival(index);
                    Assert(TowerHit[index] - arrival > 0 && TowerHit[index] - arrival < 1, "塔位必须最后一秒到达。");
                    for (var slot = 0; slot < 8; slot++)
                        Assert(Departure(previous[slot], round.Positions[slot], arrival) >= TowerMovementAvailable(index),
                            "正常跑速下不能提前离开上一判定位置，也不能压缩后续移动时间。");
                    previous = round.Positions;

                    if (index % 2 == 0)
                    {
                        if (index > 0) Assert(arrival < RowMin(CleaveHit, index / 2 - 1), "下一组塔位必须同时避开最早的半场刀。");
                        continue;
                    }

                    var pair = index / 2;
                    Assert(arrival + 0.1 < RowMin(JumpHit, pair), "偶数轮发出最终位置后，再留一个网络轮询间隔仍须早于最早的近点圆快照。");
                    var baitArrival = RowMin(CleaveLock, pair) - 0.7;
                    for (var slot = 0; slot < 8; slot++)
                        Assert(Departure(round.Positions[slot], round.BaitPosition, baitArrival) >= SpellHit[index] + 0.15,
                            "集合出发不得抢在钢铁扇形判定之前。");
                    previous = Enumerable.Repeat(round.BaitPosition, 8).ToArray();
                    if (index == 7 && round.FutureEnd)
                    {
                        var finalArrival = RowMin(CleaveHit, pair) - 0.8;
                        Assert(Departure(round.BaitPosition, round.FinalDodgePosition, finalArrival) >= RowMax(CleaveLock, pair) + 0.1,
                            "最终穿背必须等待全部施法者锁定后，仍能在最后一秒到达。");
                    }
                }
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
