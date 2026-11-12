using Simulant.Simulation.Geometry;
using Simulant.Simulation.Mechanics;
using System;
using System.Linq;
using System.Numerics;
using static Simulant.Content.U7b.Presets.ForsakenTiming;

namespace Simulant.Content.U7b.Presets
{
    internal sealed partial class P2遗弃末世Logic
    {
        private readonly double[] _lastMagicHit = Enumerable.Repeat(double.NegativeInfinity, 8).ToArray();

        private SimMechanic CreateTowers(int index)
        {
            var round = _plan.Rounds[index];
            var towers = Enumerable.Range(0, 2).Select(tower => new AreaMechanic
            {
                Name = $"第 {index + 1} 轮第 {tower + 1} 座塔",
                Area = ActionArea.FromAction(0xBABE), Center = round.TowerCenters[tower], RequiredPlayers = 2,
                Check = players => players.Any(player => _stacks[player.Slot] == 0) ? "踩塔者已没有咏唱危机层数。" : null,
                Started = context =>
                {
                    MapEffect(round.TowerMapIndices[tower], 2);
                    context.ScheduleAfter(3.03, () => MapEffect(round.TowerMapIndices[tower], 8), "塔动画转换");
                },
                Succeeded = result => Effect(8 + tower, result.Center, 0, 0xBABE),
                Failed = result =>
                {
                    Effect(8 + tower, result.Center, 0, 0xBABF);
                    Fail($"第 {index + 1} 轮第 {tower + 1} 座塔失败：{result.Reason}", Enumerable.Range(0, 8).ToArray());
                },
                Resolved = result =>
                {
                    foreach (var player in result.Participants)
                    {
                        if (!_triggered[index].Contains(player.Slot)) _triggered[index].Add(player.Slot);
                        if (round.TowerBySlot[player.Slot] != tower)
                            _host.LogSim($"[遗弃末世] {Roles[player.Slot]} 的踩塔分工与约定解法不同，已按实际入塔者结算。");
                    }
                },
                Cleanup = context =>
                {
                    if (!_host.EnvironmentService.PlayMapEffect(round.TowerMapIndices[tower], 4))
                        throw new InvalidOperationException("遗弃末世的塔特效清理失败。");
                },
            }).ToArray();
            return new MechanicGroup(towers) { Name = $"第 {index + 1} 轮双人塔", ResolveAfter = TowerHit[index] - TowerShow[index] };
        }

        private void ResolveSpells(int index)
        {
            var hits = new int[8];
            var spells = _triggered[index].Where(slot => _markers[slot] != ForsakenMarker.None).Select(source =>
            {
                var marker = _markers[source];
                var fan = marker == ForsakenMarker.Fan;
                var action = marker == ForsakenMarker.Stack ? 0xBAC0u : fan ? 0xBAC2u : 0xBAC1u;
                return new AreaMechanic
                {
                    Name = $"第 {index + 1} 轮 {Roles[source]} 的点名",
                    Area = ActionArea.FromAction(action, coneAngleDegrees: fan ? 90f : (float?)null),
                    CenterAtResolve = snapshot => snapshot.Get(source).Position,
                    HeadingAtResolve = snapshot => fan ? Heading(snapshot.Get(source).Position,
                        (snapshot.Nearest(snapshot.Get(source).Position, Party[source]) ?? throw new InvalidOperationException("扇形没有可引导的队员。")).Position) : 0,
                    Exclude = fan ? Party[source] : null,
                    RequiredPlayers = marker == ForsakenMarker.Stack ? 3 : 1,
                    Failed = result => Fail($"第 {index + 1} 轮 {Roles[source]} 的点名处理失败：{result.Reason}",
                        result.Participants.Select(player => player.Slot).Concat(new[] { source }).Distinct().ToArray()),
                    Resolved = result =>
                    {
                        Effect(source, result.Center, result.Heading, action);
                        foreach (var player in result.Participants) hits[player.Slot]++;
                    },
                };
            }).ToArray();
            if (spells.Length == 0) { FinishSpells(index, hits); return; }
            EntityManager.RunMechanic(new MechanicGroup(spells)
            {
                Name = $"第 {index + 1} 轮钢铁、扇形与分摊",
                Resolved = result => FinishSpells(index, hits),
            });
        }

        private void FinishSpells(int index, int[] hits)
        {
            ApplyMagicHits(hits, $"第 {index + 1} 轮钢扇分摊");
            // 失败后继续整段练习；实际没进塔的玩家保留层数，不假装完成消层。
            foreach (var slot in _triggered[index])
            {
                if (_stacks[slot] > 0) _stacks[slot]--;
                _markers[slot] = _stacks[slot] == 0 ? ForsakenMarker.None : _plan.Rounds[index].MarkersAfter[slot];
                ApplyMarker(slot, _markers[slot]);
            }
            for (var slot = 0; slot < 8; slot++) SetTrouble(slot);
            _host.LogSim($"[遗弃末世] 第 {index + 1}/8 轮完成；剩余层数 {string.Join("、", Enumerable.Range(0, 8).Select(slot => Roles[slot] + ":" + _stacks[slot]))}。");
        }

        private void SelectJumpTargets()
            => _jumpTargets = PartySnapshot.Capture(Party).Nearest(ForsakenPlan.Center, 4).Select(player => player.Slot).ToArray();

        private void ResolveJumps(ForsakenRoundPlan round, int[] sources)
        {
            var hits = new int[8];
            var jumps = sources.Select(source =>
            {
                var target = _jumpTargets[source];
                var actor = _casters[source];
                var action = source == 0 ? (round.FutureEnd ? 0xBAD6u : 0xBAD7u) : (round.FutureEnd ? 0xBAD8u : 0xBAD9u);
                return new AreaMechanic
                {
                    Name = $"第 {round.Number} 轮近点圆", Area = ActionArea.FromAction(action), RequiredPlayers = 1,
                    CenterAtResolve = snapshot => snapshot.Get(target).Position,
                    Failed = result => Fail("近点圆重叠：" + result.Reason, result.Participants.Select(player => player.Slot).ToArray()),
                    Resolved = result =>
                    {
                        foreach (var player in result.Participants) hits[player.Slot]++;
                        actor.Heading = Heading(actor.Pos, result.Center);
                        actor.StopCasting();
                        actor.Entity.ResetBaseTimeline();
                        // 网络位置仍在中心，跳跃的表现位移交给带目标的原生动作效果。
                        actor.Entity.ExecuteActionEffect(action, Party[target].Entity);
                    },
                };
            }).ToArray();
            EntityManager.RunMechanic(new MechanicGroup(jumps)
            {
                Name = $"第 {round.Number} 轮近点圆",
                Resolved = result => ApplyMagicHits(hits, $"第 {round.Number} 轮近点圆"),
            });
        }

        private void LockCleave(ForsakenRoundPlan round, int source)
        {
            var actor = _casters[source];
            var snapshot = PartySnapshot.Capture(Party);
            var origin = actor.Pos;
            var target = snapshot.Nearest(origin) ?? throw new InvalidOperationException("前后刀没有可引导的队员。");
            var direction = target.Position - origin;
            if (direction.LengthSquared() < 0.0001f) direction = new Vector2(0, -1);
            actor.Heading = Heading(origin, origin + direction);
            _cleaveOrigins[source] = origin;
            _cleaveHeadings[source] = actor.Heading + (round.FutureEnd ? 0 : (float)Math.PI);
            actor.Cast(round.FutureEnd ? 0xBADCu : 0xBADDu);
        }

        private void ResolveCleaves(ForsakenRoundPlan round, int[] sources)
        {
            var action = round.FutureEnd ? 0xBADCu : 0xBADDu;
            EntityManager.RunMechanic(new MechanicGroup(sources.Select(source => new AreaMechanic
            {
                Name = $"第 {round.Number / 2} 次{(round.FutureEnd ? "前刀" : "后刀")}",
                Area = ActionArea.FromAction(action, coneAngleDegrees: 180),
                Center = _cleaveOrigins[source], Heading = _cleaveHeadings[source], RequiredPlayers = 0,
                Failed = result => Fail($"第 {round.Number / 2} 次{(round.FutureEnd ? "前刀" : "后刀")}命中",
                    result.Participants.Select(player => player.Slot).ToArray()),
                Resolved = result => _casters[source].Execute(action),
            }).ToArray()));
        }

        private void ApplyMagicHits(int[] hits, string description)
        {
            var now = Scheduler.ElapsedSeconds;
            for (var slot = 0; slot < 8; slot++)
            {
                if (hits[slot] == 0) continue;
                if (hits[slot] > 1 || now - _lastMagicHit[slot] < 1.96) Fail(description + "期间受到重复魔法伤害", slot);
                _lastMagicHit[slot] = now;
                EntityManager.SetStatus(Party[slot], 0xB7D, 1.96f, 0, _casters[0]);
            }
        }

        private void Fail(string reason, params int[] slots)
        {
            _mistakes++;
            foreach (var slot in slots) _failedPlayers.Add(slot);
            var names = string.Join("、", slots.Distinct().Select(slot => Roles[slot]));
            _host.LogWarning($"[遗弃末世] {Scheduler.ElapsedSeconds - _origin:0.000} 秒：{reason}（{names}）。");
            if (slots.Contains(_selfSlot)) ShowResultMarker("m0489trg_b0c");
        }
    }
}
