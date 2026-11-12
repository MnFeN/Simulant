using Simulant.Simulation.Timeline;
using System;
using System.Linq;
using System.Numerics;
using static Simulant.Content.U7b.Presets.ForsakenTiming;

namespace Simulant.Content.U7b.Presets
{
    internal sealed partial class P2遗弃末世Logic
    {
        private void StartMechanic()
        {
            _origin = Scheduler.ElapsedSeconds;
            var timeline = new SimTimeline();
            timeline.At(0, () =>
            {
                MapEffect(0x11, 8);
                _casters[0].Heading = (float)Math.PI;
                _casters[0].Cast(0xBABC);
            }, "遗弃末世读条");
            timeline.At(6.961, () => MapEffect(0x11, 2), "遗弃末世全屏效果");
            timeline.At(7.9, () => _host.EnvironmentService.SetWeather(89, 2), "遗弃末世天气变化");
            timeline.At(8.568, () =>
            {
                MapEffect(0x12, 4);
                for (var slot = 0; slot < 8; slot++) { SetTrouble(slot); ApplyMarker(slot, _markers[slot]); }
            }, "四层咏唱危机与初始钢扇分摊点名");

            for (var round = 0; round < 8; round++)
                AddTowerRound(timeline, round);
            for (var pair = 0; pair < 4; pair++)
                AddEndAttacks(timeline, pair);

            timeline.At(108.043, () => _casters[0].Cast(0xBABD), "制裁之光读条");
            timeline.At(113.043, () =>
            {
                var remaining = Enumerable.Range(0, 8).Where(slot => _stacks[slot] != 0).ToArray();
                if (remaining.Length != 0) Fail("制裁之光结算时仍有咏唱危机层数", remaining);
                CompleteMechanic();
            }, "制裁之光检查剩余层数");
            timeline.At(116.043, () =>
            {
                OnStop();
                EntityManager.Clear();
                Scheduler.Cancel();
            }, "回收遗弃末世实体与状态");
            // 使用显式原点，使移动的绝对截止时刻与整条机制轴一致。
            Scheduler.Schedule(_origin, () => Play(timeline), "播放遗弃末世时间轴");
        }

        private void AddTowerRound(SimTimeline timeline, int index)
        {
            var round = _plan.Rounds[index];
            timeline.At(TowerShow[index], Mechanic(CreateTowers(index)));

            // 奇数轮（第一轮除外）必须等前后刀全部锁面向；偶数轮则等待上一轮点名伤害结束。
            var earliest = TowerMovementAvailable(index);
            var arrival = TowerArrival(index);
            timeline.At(earliest, () => PlanPartyMovement(round.Positions, arrival, TowerHit[index], $"第 {index + 1} 轮站位"),
                $"倒排第 {index + 1} 轮队员出发时间");
            timeline.At(SpellHit[index], () => ResolveSpells(index), $"第 {index + 1} 轮钢铁、扇形与分摊判定");
        }

        private void AddEndAttacks(SimTimeline timeline, int pair)
        {
            var round = _plan.Rounds[pair * 2 + 1];
            timeline.At(EndCast[pair], () =>
            {
                _casters[0].Pos = ForsakenPlan.Center;
                _casters[0].Cast(round.FutureEnd ? 0xBAD2u : 0xBAD3u);
            }, $"第 {pair + 1} 次{(round.FutureEnd ? "未来" : "过去")}终结读条");
            timeline.At(RowMin(JumpHit, pair) - 0.09, () =>
            {
                for (var caster = 1; caster < 4; caster++)
                {
                    _casters[caster].Pos = ForsakenPlan.Center;
                    _casters[caster].EnableDraw();
                    _casters[caster].SetReadyToDraw();
                }
            }, $"第 {pair + 1} 次复制体出现");
            timeline.At(RowMin(JumpHit, pair), SelectJumpTargets, $"第 {pair + 1} 次选取最近四人");
            foreach (var moment in Enumerable.Range(0, 4).GroupBy(source => JumpHit[pair, source]))
            {
                var sources = moment.ToArray();
                timeline.At(moment.Key, () => ResolveJumps(round, sources), $"第 {pair + 1} 次近点圆判定");
            }
            foreach (var moment in Enumerable.Range(0, 4).GroupBy(source => CleaveHit[pair, source]))
            {
                var sources = moment.ToArray();
                timeline.At(moment.Key, () => ResolveCleaves(round, sources), $"第 {pair + 1} 次消灭之脚判定");
            }
            for (var caster = 0; caster < 4; caster++)
            {
                var source = caster;
                timeline.At(CleaveLock[pair, caster], () => LockCleave(round, source), $"第 {pair + 1} 次消灭之脚锁朝向 {caster + 1}");
                if (caster != 0)
                {
                    timeline.At(CleaveHit[pair, caster] + 3, () => _casters[source].Entity.PlayBlendTimeline(0x1E39),
                        $"第 {pair + 1} 次复制体 {caster} 退场动画");
                    timeline.At(CleaveHit[pair, caster] + 4.5, () => _casters[source].Entity.DisableDraw(),
                        $"第 {pair + 1} 次隐藏复制体 {caster}");
                }
            }
            var firstLock = RowMin(CleaveLock, pair);
            timeline.At(SpellHit[pair * 2 + 1] + 0.15, () =>
                PlanPartyMovement(Enumerable.Repeat(round.BaitPosition, 8).ToArray(), firstLock - 0.7, firstLock, "前后刀引导集合"),
                $"倒排第 {pair + 1} 次前后刀集合");
            if (pair == 3 && round.FutureEnd)
            {
                var firstHit = RowMin(CleaveHit, pair);
                timeline.At(RowMax(CleaveLock, pair) + 0.1, () =>
                    PlanPartyMovement(Enumerable.Repeat(round.FinalDodgePosition, 8).ToArray(), firstHit - 0.8, firstHit, "最后一次前刀穿背"),
                    "四名施法者锁定后安排最终穿背");
            }
        }

        private void PlanPartyMovement(Vector2[] destinations, double arriveAt, double snapshotAt, string name)
        {
            for (var slot = 0; slot < 8; slot++)
            {
                if (slot == _selfSlot) continue;
                var actor = Party[slot];
                var destination = destinations[slot];
                var distance = Vector2.Distance(actor.Pos, destination);
                if (distance <= 0.1f) continue;
                var departure = _origin + Departure(actor.Pos, destination, arriveAt);
                if (departure < Scheduler.ElapsedSeconds - 0.1)
                    throw new InvalidOperationException($"{name}的移动窗口不足，{Roles[slot]}无法按正常跑速在最后一秒到达；请查看前一节点是否延迟。");
                var member = slot;
                Scheduler.Schedule(Math.Max(departure, Scheduler.ElapsedSeconds), () =>
                    actor.MoveNetworkTo(destination, MoveSpeedParam, () =>
                    {
                        actor.Heading = Heading(actor.Pos, ForsakenPlan.Center);
                        var remaining = _origin + snapshotAt - Scheduler.ElapsedSeconds;
                        // 轮询只用于记录实际到位；判定始终在固定节点读取所有人的真实位置。
                        if (remaining > 1.05 || remaining < 0)
                            _host.LogWarning($"[遗弃末世] {Roles[member]}「{name}」到位偏差：距判定 {remaining:0.000} 秒。");
                    }), Roles[slot] + "最后一秒就位：" + name);
            }
        }

        private static float Heading(Vector2 origin, Vector2 target) => (float)Math.Atan2(target.X - origin.X, target.Y - origin.Y);
    }
}
