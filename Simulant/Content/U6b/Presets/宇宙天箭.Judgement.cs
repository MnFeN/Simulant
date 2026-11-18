using Simulant.Simulation.Geometry;
using Simulant.Simulation.Mechanics;
using System;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U6b.Presets
{
    internal partial class U6b6_宇宙天箭Logic
    {
        private void ResolveArrows(int wave, CosmoArrowPlan.Shot[] shots)
        {
            EntityManager.RunMechanic(new MechanicGroup(shots.Select((shot, index) => new AreaMechanic($"第 {wave + 1} 次天箭",
                ActionArea.FromAction(shot.Action), shot.Position)
            {
                Heading = shot.Heading,
                MaxPlayers = 0,
                FailureSeverity = MechanicFailureSeverity.Warning, // 宽直条由读条结束自动演出，窄直条在本次判定统一播放。
                Visual = result =>
                {
                    if (shot.Action == 0x7BA4)
                        PlaceHelper(12 + index, shot.Position, shot.Heading).Execute(shot.Action, result.HitActors);
                },
            }))
            {
                Name = $"第 {wave + 1} 次天箭",
                Failed = result =>
                {
                    var hit = result.Children.Where(child => !child.Success).SelectMany(child => child.Participants).Select(member => member.Slot).Distinct().ToArray();
                    Report(MechanicError.Fixed($"第 {wave + 1} 次天箭", Array.Empty<int>(), hit)
                        .WithAttribution(hit, "自身进入天箭伤害范围"));
                },
            });
        }

        private void ResolveDive()
        {
            var tankArea = ActionArea.FromAction(0x7BA7);
            var stackArea = ActionArea.FromAction(0x7BA8);
            Vector2[] actualPositions = null;
            int[] targets = null;

            EntityManager.RunMechanic(new MechanicGroup(Enumerable.Range(0, 3).Select(index => new AreaMechanic(index < 2 ? "宇宙龙炎近点死刑" : "宇宙龙炎远点分摊",
                index < 2 ? tankArea : stackArea)
            {
                RequiredPlayers = index < 2 ? 1 : 6,
                FailureSeverity = MechanicFailureSeverity.Warning,
                CenterAtResolve = snapshot =>
                {
                    actualPositions = actualPositions ?? Positions(snapshot);
                    targets = targets ?? CosmoArrowPlan.DiveTargets(actualPositions);
                    return snapshot.Get(targets[index]).Position;
                },
                Check = members => index < 2
                    ? (targets[index] < 2 && members.All(member => member.Slot < 2) ? null : "死刑必须由双 T 在近点单独引导。")
                    : (targets[index] >= 2 && members.All(member => member.Slot >= 2) ? null : "分摊必须由非坦克在远点引导，由六名非坦克共同承担。"), // 龙炎在被引导者身上播放，必须提供目标，单播施法者动作不会产生完整特效。
                Visual = result => PlaceHelper(12 + index, _boss.Pos, Heading(Party[targets[index]].Pos - _boss.Pos))
                        .ExecuteActionEffect(index < 2 ? 0x7BA7u : 0x7BA8u, Party[targets[index]], hitTargets: result.HitActors),
            }))
            {
                Name = "宇宙龙炎",
                Failed = result =>
                {
                    var offenders = FindOffenders(actualPositions, CosmoArrowPlan.FollowupHit(Occurrence),
                        positions => CosmoArrowPlan.ValidDive(positions, tankArea, stackArea));

                    for (var index = 0; index < 3; index++)
                    {
                        var child = result.Children[index];

                        if (child.Success)
                        continue;

                        var target = targets[index];
                        var actual = child.Participants.Select(member => member.Slot).ToArray();
                        var error = index == 2
                            ? MechanicError.Targeted($"{CosmoArrowPlan.Roles[target]} 引导的宇宙龙炎分摊", Enumerable.Range(2, 6), actual, target, child.Reason)
                            : target < 2
                                ? MechanicError.Targeted($"{CosmoArrowPlan.Roles[target]} 引导的宇宙龙炎死刑", new[]
                        {
                            target
                        }, actual, target, child.Reason)
                                : MechanicError.Rule($"{CosmoArrowPlan.Roles[target]} 误引导宇宙龙炎死刑", actual, child.Reason);
                        Report(error.WithAttribution(offenders, SelfSlot < 2 ? "自身未正确引导近点死刑，或与队友距离不足" : "自身未正确参与远点分摊，或误入近点死刑范围"));
                    }
                },
            });
        }

        private void ResolveBeams(int wave)
        {
            var targets = _beamOrder.Skip(wave * 4).Take(4).ToArray();
            var area = ActionArea.FromAction(0x7BAB);
            Vector2[] actualPositions = null;

            EntityManager.RunMechanic(new MechanicGroup(targets.Select((target, index) => new AreaMechanic($"第 {wave + 1} 组 {CosmoArrowPlan.Roles[target]} 的波动炮",
                area, CosmoArrowPlan.Center)
            {
                HeadingAtResolve = snapshot =>
                {
                    actualPositions = actualPositions ?? Positions(snapshot);
                    return CosmoArrowPlan.TargetHeading(actualPositions, target);
                },
                RequiredPlayers = 1,
                FailureSeverity = MechanicFailureSeverity.Warning,
                Visual = result => PlaceHelper(12 + index, CosmoArrowPlan.Center,
                    Heading(Party[target].Pos - CosmoArrowPlan.Center)).Execute(0x7BAB, result.HitActors),
            }))
            {
                Name = $"第 {wave + 1} 组波动炮",
                Failed = result =>
                {
                    var offenders = FindOffenders(actualPositions, CosmoArrowPlan.BeamHit(wave), positions => CosmoArrowPlan.ValidBeams(positions, targets, area));

                    for (var index = 0; index < targets.Length; index++)
                    {
                        var child = result.Children[index];

                        if (child.Success)
                        continue;

                        var target = targets[index];
                        Report(child.DescribeFailure($"第 {wave + 1} 组 {CosmoArrowPlan.Roles[target]} 的波动炮分散", new[]
                        {
                            target
                        }, target)
                            .WithAttribution(offenders, "自身引导的直线波及队友，或自身进入队友的直线范围"));
                    }
                },
            });
        }

        private void ResolveCharge()
        {
            var area = ActionArea.FromAction(0x7BAA);
            Vector2[] actualPositions = null;
            int[] front = null;

            EntityManager.RunMechanic(new AreaMechanic("波动炮八人分摊", area, CosmoArrowPlan.Center)
            {
                RequiredPlayers = 8,
                FailureSeverity = MechanicFailureSeverity.Warning,
                HeadingAtResolve = snapshot =>
                {
                    actualPositions = Positions(snapshot);
                    var heading = CosmoArrowPlan.TargetHeading(actualPositions, _chargeTarget);
                    front = CosmoArrowPlan.ChargeFront(actualPositions, heading);
                    return heading;
                },
                Check = members => front.All(slot => slot < 2) ? null : "双 T 必须站在分摊队列最前方挡枪。",
                Visual = result =>
                {
                    _boss.FaceNetwork(Heading(Party[_chargeTarget].Pos - CosmoArrowPlan.Center));
                    _boss.ExecuteActionEffect(0x7BAA, Party[_chargeTarget], hitTargets: result.HitActors);
                },
                Failed = result => Report(result.DescribeFailure("波动炮八人分摊", Enumerable.Range(0, 8), _chargeTarget)
                    .WithDetail("最前两人：" + string.Join(" ", front.Select(slot => CosmoArrowPlan.Roles[slot])))
                    .WithAttribution(FindOffenders(actualPositions, CosmoArrowPlan.FollowupHit(Occurrence),
                        positions => CosmoArrowPlan.ValidCharge(positions, _chargeTarget, area)),
                        SelfSlot < 2 ? "自身未在分摊前排挡枪" : "自身未进入分摊直线，或越过坦克站到前排")),
            });
        }

        private int[] FindOffenders(Vector2[] actualPositions, double time, Func<Vector2[], bool> valid)
            => CosmoArrowPlan.FindOffenders(actualPositions, CosmoArrowPlan.PositionsAt(ArrowMode, Occurrence, time), valid);

        private static Vector2[] Positions(PartySnapshot snapshot)
            => Enumerable.Range(0, 8).Select(slot => snapshot.Get(slot).Position).ToArray();
    }
}
