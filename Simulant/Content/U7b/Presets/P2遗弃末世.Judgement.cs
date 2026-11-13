using Simulant.ACT;
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
                Visual = result => Effect(8 + tower, result.Center, 0, result.Success ? 0xBABEu : 0xBABFu),
                Failed = result => ReportTowerFailure(index, tower, result),
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
                var area = ActionArea.FromAction(action, coneAngleDegrees: fan ? 90f : (float?)null);
                return new AreaMechanic
                {
                    Name = $"第 {index + 1} 轮 {Roles[source]} 的{MarkerName(marker)}点名",
                    Area = area,
                    CenterAtResolve = snapshot => snapshot.Get(source).Position,
                    HeadingAtResolve = snapshot => fan ? Heading(snapshot.Get(source).Position,
                        (snapshot.Nearest(snapshot.Get(source).Position, Party[source]) ?? throw new InvalidOperationException("扇形没有可引导的队员。")).Position) : 0,
                    Exclude = fan ? Party[source] : null,
                    RequiredPlayers = marker == ForsakenMarker.Stack ? 3 : 1,
                    Visual = result =>
                    {
                        Effect(source, result.Center, result.Heading, action);
                        if (fan) CheckFanDirection(index, source, area, result);
                    },
                    Failed = result => Fail(DescribeMarkerFailure(index, source, marker, area, result)),
                    Resolved = result =>
                    {
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

        private void CheckFanDirection(int index, int source, ActionArea area, MechanicResult result)
        {
            var positions = _plan.Rounds[index].Positions;
            var expectedTarget = Enumerable.Range(0, positions.Length).Where(slot => slot != source)
                .OrderBy(slot => Vector2.DistanceSquared(positions[source], positions[slot])).ThenBy(slot => slot).First();
            var expectedHeading = Heading(positions[source], positions[expectedTarget]);
            // 比较同一轮世界坐标中的方向，取跨越正负 π 后的最小夹角。
            var difference = (double)result.Heading - expectedHeading;
            var degrees = Math.Abs(Math.Atan2(Math.Sin(difference), Math.Cos(difference))) * 180 / Math.PI;
            if (degrees <= 30) return;

            // 沿用伤害判定快照，不再次读取移动中的玩家，也不改变实际扇形或人数判定。
            var actualTarget = result.Snapshot.Nearest(result.Center, Party[source])
                ?? throw new InvalidOperationException("扇形没有可引导的队员。");
            var error = DescribeMarkerFailure(index, source, ForsakenMarker.Fan, area, result)
                .WithDetail($"扇形偏离解法方向 {degrees:0.0}°，超过 30°；" +
                    $"实际由 {Roles[actualTarget.Slot]} 引导，约定由 {Roles[expectedTarget]} 引导");
            var message = RecordMistake(error);
            _host.LogWarning(message);
            try { TriggernometryInterop.InvokeNamedCallback("command", "/e <se.11>\n" + message); }
            catch (Exception chatError) { _host.LogWarning("[遗弃末世] 扇形引导偏差的默语提示发送失败：" + chatError.Message); }
        }

        private void FinishSpells(int index, int[] hits)
        {
            ApplyMagicHits(hits, $"第 {index + 1} 轮钢扇分摊");
            // 只有本轮判定全部通过，才更新实际入塔者的层数与后续点名。
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
            // 本体取最近者，三个分身依次取第二至第四近者，不按动作包的先后重新选人。
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
                    Name = $"第 {round.Number} 轮 {Roles[target]} 的跳跃圆形伤害", Area = ActionArea.FromAction(action), RequiredPlayers = 1,
                    CenterAtResolve = snapshot => snapshot.Get(target).Position,
                    Failed = result => ReportJumpFailure(round, target, result),
                    Visual = result =>
                    {
                        var direction = result.Center - ForsakenPlan.Center;
                        var distance = direction.Length();
                        var landing = ForsakenPlan.Center + (distance > 4 ? direction * (1 - 4 / distance) : Vector2.Zero);
                        var position = new Vector3(landing, actor.Pos3D.Z);
                        var heading = distance > 0.0001f ? Heading(ForsakenPlan.Center, result.Center) : (float)Math.PI;
                        actor.StopCasting();
                        actor.Entity.ResetBaseTimeline();
                        actor.FaceNetwork(heading);
                        // 圆形伤害仍以目标为中心；施法者沿场中心到目标的方向，停在目标前四米。
                        actor.Entity.ExecuteActionEffect(action, Party[target].Entity);
                        // 回放在动作效果后约 0.65 秒以速度参数 90 更新落点，后续半场刀读取该实体位置。
                        Scheduler.ScheduleAfter(0.65, () =>
                        {
                            if (actor.IsActive) actor.Entity.ActorMove(position, heading, 90);
                        }, $"第 {round.Number} 轮跳跃落地 {source + 1}");
                    },
                    Resolved = result =>
                    {
                        foreach (var player in result.Participants) hits[player.Slot]++;
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
            var heading = Heading(origin, origin + direction);
            actor.FaceNetwork(heading);
            _cleaveOrigins[source] = origin;
            // 判定保存锁定的目标角度，不读取客户端尚在插值的当前朝向。
            _cleaveHeadings[source] = heading + (round.FutureEnd ? 0 : (float)Math.PI);
            actor.Cast(round.FutureEnd ? 0xBADCu : 0xBADDu);
        }

        private void ResolveCleaves(ForsakenRoundPlan round, int[] sources)
        {
            var action = round.FutureEnd ? 0xBADCu : 0xBADDu;
            EntityManager.RunMechanic(new MechanicGroup(sources.Select(source => new AreaMechanic
            {
                Name = $"第 {round.Number} 轮{(round.FutureEnd ? "前刀" : "后刀")}",
                Area = ActionArea.FromAction(action, coneAngleDegrees: 180),
                Center = _cleaveOrigins[source], Heading = _cleaveHeadings[source], RequiredPlayers = 0,
                FailureSeverity = MechanicFailureSeverity.Warning,
                Failed = result => Warn(result.DescribeFailure($"第 {round.Number} 轮{(round.FutureEnd ? "前刀" : "后刀")}", Array.Empty<int>())
                    .WithAttribution(result.Participants.Select(player => player.Slot),
                        $"自身未避开{(round.FutureEnd ? "前刀" : "后刀")}范围")),
                Visual = result => _casters[source].Execute(action),
            }).ToArray()));
        }

        private void ApplyMagicHits(int[] hits, string description)
        {
            var now = Scheduler.ElapsedSeconds;
            for (var slot = 0; slot < 8; slot++)
            {
                if (hits[slot] == 0) continue;
                if (hits[slot] > 1 || now - _lastMagicHit[slot] < 1.96)
                    Fail(MechanicError.Rule(description + "重复魔法伤害", new[] { slot }, "短时间内受到多次魔法伤害。")
                        .WithDetail("仅凭受伤次数无法确定责任人，需结合各次伤害的位置与点名检查"));
                _lastMagicHit[slot] = now;
                EntityManager.SetStatus(Party[slot], 0xB7D, 1.96f, 0, _casters[0]);
            }
        }

    }
}
