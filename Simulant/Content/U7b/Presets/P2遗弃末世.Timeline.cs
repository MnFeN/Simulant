using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Runtime;
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
                if (remaining.Length != 0)
                    Fail(MechanicError.Rule("制裁之光", remaining, "结算时仍有咏唱危机层数。")
                        .WithAttribution(remaining, "自身未完成规定的踩塔消层"));
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
            timeline.At(earliest, () => PlanPartyMovement(round.SafePositions,
                arrival, SpellHit[index] + 0.1, round.NorthHeading),
                $"安排第 {index + 1} 轮队员站位");
            timeline.At(SpellHit[index], () => ResolveSpells(index), $"第 {index + 1} 轮钢铁、扇形与分摊判定");
        }

        private void AddEndAttacks(SimTimeline timeline, int pair)
        {
            var round = _plan.Rounds[pair * 2 + 1];
            // 集合按下一轮塔的相对北布置；最后一轮集合与穿背使用场地正北。
            var northHeading = pair < 3 ? _plan.Rounds[pair * 2 + 2].NorthHeading : Math.PI;
            timeline.At(EndCast[pair], () =>
            {
                // 跳跃动作只改变表现位置；新一轮通过收包恢复中心与朝北面向。
                _casters[0].Entity.ActorMove(new Vector3(ForsakenPlan.Center, _casters[0].Pos3D.Z), (float)Math.PI);
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
            timeline.At(SpellHit[pair * 2 + 1] + 0.15, () =>
                PlanPartyMovement(Party.Select(actor => round.GatheringPosition(SimJobActions.For(actor.Job)?.Melee == true)).ToArray(),
                    GatheringArrival(pair), RowMax(CleaveLock, pair), northHeading),
                $"安排第 {pair + 1} 次前后刀集合");
            if (pair == 3 && round.FutureEnd)
            {
                var firstHit = RowMin(CleaveHit, pair);
                timeline.At(RowMax(CleaveLock, pair) + 0.1, () =>
                    PlanPartyMovement(Party.Select(actor => round.GatheringPosition(SimJobActions.For(actor.Job)?.Melee == true, finalDodge: true)).ToArray(),
                        firstHit, RowMax(CleaveHit, pair) + 0.2, northHeading, urgent: true),
                    "四名施法者锁定后安排最终穿背");
            }
        }

        private void PlanPartyMovement(SimPosition[] destinations, double arriveAt, double holdUntil, double northHeading, bool urgent = false)
        {
            for (var slot = 0; slot < 8; slot++)
            {
                if (slot == _selfSlot) continue;
                // 解法只提供安全目的地和时间约束，出发、绕路、滑步与近战贪刀均由角色决定。
                Party[slot].MoveForMechanic(destinations[slot], _origin + arriveAt, _origin + holdUntil, urgent, northHeading);
            }
        }

        private static float Heading(Vector2 origin, Vector2 target) => (float)Math.Atan2(target.X - origin.X, target.Y - origin.Y);
    }
}
