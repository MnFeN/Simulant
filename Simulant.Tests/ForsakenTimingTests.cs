using Simulant.Content.U7b.Presets;
using System;
using System.Linq;
using System.Numerics;
using static Simulant.Content.U7b.Presets.ForsakenTiming;

namespace Simulant.Tests
{
    internal static class ForsakenTimingTests
    {
        internal static void MovementDeadlinesFollowSnapshotsWithoutLeavingThePreviousMechanicEarly()
        {
            for (var seed = 0; seed < 512; seed++)
            {
                var plan = ForsakenPlan.Create(new Random(seed));
                var previous = Enumerable.Range(0, 8).Select(slot => ForsakenPlan.InitialPosition(slot, slot < 2 || slot == 4 || slot == 5).Midpoint()).ToArray();
                for (var index = 0; index < 8; index++)
                {
                    var round = plan.Rounds[index];
                    var arrival = TowerArrival(index);
                    Assert(arrival <= TowerPositionSnapshot(index),
                        "塔位的目标时间不得晚于本轮首次位置判定，实体自行决定提前量。");
                    for (var slot = 0; slot < 8; slot++)
                        Assert(Departure(previous[slot], round.Positions[slot], arrival) >= TowerMovementAvailable(index),
                            "正常跑速下不能提前离开上一判定位置，也不能压缩后续移动时间。");
                    previous = round.Positions;

                    if (index % 2 == 0)
                    {
                        if (index > 0) Assert(arrival <= RowMin(CleaveHit, index / 2 - 1), "下一组塔位必须同时避开最早的半场刀。");
                        continue;
                    }

                    var pair = index / 2;
                    Assert(arrival == RowMin(JumpHit, pair), "偶数轮的目标时间必须取更早的近点圆快照。");
                    var baitArrival = GatheringArrival(pair);
                    Assert(baitArrival == RowMin(CleaveLock, pair), "集合只指定首次锁面向时间，不指定最早到达或提前出发时间。");
                    for (var slot = 0; slot < 8; slot++)
                        Assert(Departure(round.Positions[slot], round.BaitPosition, baitArrival) >= SpellHit[index] + 0.15,
                            "集合出发不得抢在钢铁扇形判定之前。");
                    previous = Enumerable.Repeat(round.BaitPosition, 8).ToArray();
                    if (index == 7 && round.FutureEnd)
                    {
                        var finalArrival = RowMin(CleaveHit, pair);
                        Assert(Departure(round.BaitPosition, round.FinalDodgePosition, finalArrival) >= RowMax(CleaveLock, pair) + 0.1,
                            "最终穿背必须等待全部施法者锁定后，仍能在判定前到达。");
                    }
                }
            }
        }

        // 仅用于验证正常跑速下路线有足够时间，不参与副本或实体的实际调度。
        private static double Departure(Vector2 from, Vector2 to, double arrival) => arrival - Vector2.Distance(from, to) / 6;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
