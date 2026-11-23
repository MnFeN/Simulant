using Simulant.Content.U7a.Abilities;
using Simulant.Content.U7a.Entities;
using Simulant.Content.U7a.Presets;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace Simulant.Tests
{
    internal static class CrystallizeTimeTests
    {
        public static void FirstWaveCrossingHandlesToleranceEdgesAndDelayedPositionUpdates()
        {
            Exception failure = null;
            using (var done = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) =>
            {
                failure = error;
                done.Set();
            }))
            using (var movement = new SimMovementController(scheduler))
            using (var casts = new SimCastController(scheduler))
            using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
            {
                foreach (var wave in new[] { CrystalWave.LeftUp, CrystalWave.RightDown })
                foreach (var purple in new[] { CrystalPurple.MainDiagonal, CrystalPurple.AntiDiagonal })
                {
                    var plan = new P42时间结晶Plan(0, CrystalDebuff.RedIce, wave, purple, new Random(1553));
                    foreach (var kind in new[] { CrystalDebuff.RedIce, CrystalDebuff.RedWind, CrystalDebuff.BlueEarth })
                    foreach (var startAngle in Enumerable.Range(0, 8))
                    foreach (var endAngle in Enumerable.Range(0, 4))
                    {
                        var slot = Array.IndexOf(plan.Debuffs, kind);
                        var waiting = kind == CrystalDebuff.RedIce ? plan.RedIceNorth : kind == CrystalDebuff.RedWind ? plan.SouthRest : plan.NorthRest;
                        var actor = new SimActor(new EntityTestWorld().CreateCharacter(2), casts, movement);
                        actor.Pos = waiting.Sample(1, startAngle / 8.0, Math.PI);
                        var start = actor.Pos;
                        actor.Entity.OnActorMove = (position, _, __) =>
                        {
                            if (!done.IsSet)
                                scheduler.ScheduleAfter(0.25, () => actor.Entity.Pos3D = position);
                        };
                        behavior.Add(actor, new SimActorBehaviorOptions
                        {
                            AttackTarget = false,
                            IdleRadius = 0
                        });
                        var route = plan.RewindRoute(slot, start, Enumerable.Range(1, 4).Select(plan.PuddleDefault).ToArray()).First();
                        var target = route.Area.Sample(1, endAngle / 4.0, Math.PI);
                        scheduler.Schedule(P42时间结晶Plan.SandHits[2] - 32, () => Assert(
                            Enumerable.Range(0, 6).Where(i => plan.HourglassWave(i) == 2)
                                .All(i => Vector2.Distance(actor.Pos, plan.Hourglass(i)) > 12.1),
                            "跨中线前的完整等待区必须避开最后一组沙漏，至少留出 0.1 米余量。"));
                        scheduler.Schedule(route.Start - 32, () => actor.MoveForMechanic(
                            SimPosition.Ellipse(target, 0), route.Arrival - 32, 2, urgent: true));
                        scheduler.Schedule(P42时间结晶Plan.WaveHits[0][1] - 32, () => Assert(
                            (actor.Pos.X - 100) * -plan.FirstSide > 0.1,
                            FormattableString.Invariant($"跨中线未留足光波余量：{wave} {purple} {kind}，起点 {start}，终点 {target}，实际位置 {actor.Pos}。")));
                    }
                }

                scheduler.Schedule(1.5, () => done.Set());
                scheduler.Start();
                Assert(done.Wait(4000) && failure == null, "光波跨线极值检查失败：" + failure);
            }
        }

        public static void WindAndStackAreasKeepTheirFullToleranceSafe()
        {
            foreach (var purple in new[] { CrystalPurple.MainDiagonal, CrystalPurple.AntiDiagonal })
            {
                var plan = new P42时间结晶Plan(0, CrystalDebuff.BlueEarth, CrystalWave.LeftUp, purple, new Random(1552));
                foreach (var side in new[] { -1, 1 })
                {
                    var wind = plan.Wind(side);
                    var ice = plan.Initial(plan.Red(CrystalDebuff.RedIce, side));
                    Assert(Vector2.Distance(wind.Midpoint(), P42时间结晶Plan.Polar(19, 30 * side)) < 0.001,
                        "红风就位应沿场中 30 度方向；35 度只用于蓝组相对红风的借风方向。");
                    Assert(Vector2.Distance(wind.Midpoint(), ice.Midpoint()) - wind.DeviationRadius - ice.DeviationRadius > 15.1,
                        "红风与红冰随机区最靠近时，也必须避开 15 米击退范围。");
                }

                Assert(Vector2.Distance(plan.WindApproach.Midpoint(), P42时间结晶Plan.Polar(17.5f, -30 * plan.NorthSlowSide)) < 0.001,
                    "蓝组穿入点也应沿场中 30 度方向。");
                var targetArea = plan.StackFor(plan.Slot(CrystalDebuff.BlueEarth));
                foreach (var radius in new[] { 0.0, 1.0 })
                foreach (var angle in new[] { 0.0, 1.0 })
                {
                    var target = targetArea.Sample(radius, angle, Math.PI);
                    Assert(Vector2.Distance(target, 未来的碎片.Position) > 11.14,
                        "分摊点名者的完整随机区必须避开水晶，并保留至少 1.64 米余量。");
                    foreach (var participantRadius in new[] { 0.0, 1.0 })
                    foreach (var participantAngle in new[] { 0.0, 1.0 })
                        Assert(Vector2.Distance(target, plan.Stack.Sample(participantRadius, participantAngle, Math.PI)) < 4.97,
                            "参与者保留较大容差，两个随机区最远的边缘组合也必须能参与分摊。");
                }
            }
        }

        public static void SandCornersUseSafeForwardAnnularSectors()
        {
            var random = new Random(7362);
            foreach (var purple in new[]
            {
                CrystalPurple.MainDiagonal,
                CrystalPurple.AntiDiagonal
            })
            {
                var plan = new P42时间结晶Plan(0, CrystalDebuff.RedIce, CrystalWave.LeftUp, purple, random);
                var side = -plan.NorthSlowSide;
                var corners = Enumerable.Range(0, 8).Where(slot => plan.Debuffs[slot] != CrystalDebuff.RedIce)
                    .Select(slot => (Area: plan.Initial(slot), Sand: plan.Debuffs[slot] == CrystalDebuff.BlueDark ? 3 : 0))
                    .Concat(new[]
                    {
                        (Area: plan.RedAfterIce(side), Sand: side > 0 ? 2 : 4),
                        (Area: plan.WindWait, Sand: plan.NorthSlowSide > 0 ? 1 : 5)
                    });
                foreach (var (area, sandIndex) in corners)
                {
                    var sand = plan.Hourglass(sandIndex);
                    var center = P42时间结晶Plan.Center;
                    var radial = Vector2.Normalize(sand - center);
                    var lateral = new Vector2(radial.Y, -radial.X);
                    var opening = Math.Sign(Vector2.Dot(area.Pos - center, lateral));
                    var intersection = center + radial * 17.136364f + lateral * (opening * 10.312373f);
                    Assert(area.Contains(area.Pos) && Math.Abs(Vector2.Distance(intersection, area.Pos) - 0.3f) < 0.001f,
                        "扇形包含圆心，圆心位于场边与沙漏交点前方约 0.3 米。");

                    var inner = area.Sample(0, 0.5, Math.PI) - area.Pos;
                    var outer = area.Sample(1, 0.5, Math.PI) - area.Pos;
                    Assert(inner.LengthSquared() < 0.000001f && Math.Abs(outer.Length() - 2.7f) < 0.001f,
                        "所有场边扇形的内外半径应为 0 和 2.7 米。");
                    var angularEdge = area.Sample(1, 0, Math.PI) - area.Pos;
                    Assert(Math.Abs(Vector2.Dot(Vector2.Normalize(outer), Vector2.Normalize(angularEdge)) - Math.Cos(13 * Math.PI / 180)) < 0.001,
                        "扇形的角度半宽应为 13°。");

                    for (var radialStep = 0; radialStep <= 24; radialStep++)
                    for (var angleStep = 0; angleStep <= 24; angleStep++)
                    {
                        var point = area.Sample(radialStep / 24.0, angleStep / 24.0, Math.PI);
                        Assert(Vector2.Distance(point, center) <= 19.937f && Vector2.Distance(point, sand) >= 12.048f,
                            "扇形的边界和内部应至少离场边 0.063 米、离沙漏 0.048 米。");
                    }
                }

                for (var sample = 0; sample < 500; sample++)
                {
                    var north = plan.RedIceNorth.Sample(random);
                    Assert(new[]
                    {
                        2,
                        4
                    }.All(i => Vector2.Distance(north, plan.Hourglass(i)) > 12),
                        "北侧等候区必须处于左右两座北侧沙漏的范围之外。");
                }
            }
        }

        public static void RedIceLeavesImmediatelyAndAvoidsSecondHeadExplosion()
        {
            Exception failure = null;
            const double origin = 24.5;
            var random = new Random(7363);
            using (var done = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) =>
            {
                failure = error;
                done.Set();
            }))
            using (var movement = new SimMovementController(scheduler))
            using (var casts = new SimCastController(scheduler))
            using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
            {
                foreach (var purple in new[]
                {
                    CrystalPurple.MainDiagonal,
                    CrystalPurple.AntiDiagonal
                })
                    foreach (var side in new[]
                    {
                        -1,
                        1
                    })
                    {
                        var plan = new P42时间结晶Plan(0, CrystalDebuff.RedIce, CrystalWave.LeftUp, purple, random);
                        var actor = new SimActor(new EntityTestWorld().CreateCharacter(2), casts, movement);
                        actor.Pos = plan.Initial(plan.Red(CrystalDebuff.RedIce, side)).Sample(random);
                        var initial = actor.Pos;
                        actor.Entity.OnActorMove = (pos, _, __) =>
                            scheduler.ScheduleAfter(0.2, () => actor.Entity.Pos3D = pos);
                        behavior.Add(actor, new SimActorBehaviorOptions
                        {
                            AttackTarget = false,
                            IdleRadius = 0
                        });
                        foreach (var move in plan.RedIceRoute(side, origin))
                            scheduler.Schedule(move.Start - origin, () => actor.MoveForMechanic(move.Area, move.Arrival - origin, 20, urgent: true));

                        scheduler.Schedule(0.7, () => Assert(Vector2.Distance(actor.Pos, initial) > 1,
                            "第一撞结束后应立即撤离，不能等到固定时间才开始移动。"));
                        scheduler.Schedule(P42时间结晶Plan.SandHits[1] - origin, () =>
                        {
                            Assert(Enumerable.Range(0, 6).Where(i => plan.HourglassWave(i) == 1)
                                .All(i => Vector2.Distance(actor.Pos, plan.Hourglass(i)) > 12), "红冰撤离不能吃到第二轮沙漏。");
                        });
                        scheduler.Schedule(27.885 - origin, () =>
                        {
                            if (side == plan.NorthSlowSide)
                                Assert(plan.Stack.Contains(actor.Pos), "南侧先炸的红冰应先完成五人分摊。");
                        });
                        foreach (var time in new[]
                        {
                            29.1,
                            29.3,
                            29.6,
                            30.0
                        })
                            scheduler.Schedule(time - origin, () => Assert(Vector2.Distance(actor.Pos, plan.HeadPosition(side, time)) > 12,
                                $"{purple} 的红冰在 {time} 秒仍被第二次龙头爆炸覆盖：{actor.Pos}。"));
                        scheduler.Schedule(31.324 - origin, () => Assert(plan.RedIceNorth.Contains(actor.Pos),
                            "第一道光波前红冰应进入两座北侧沙漏上方。"));
                        scheduler.Schedule(P42时间结晶Plan.SandHits[2] - origin, () =>
                            Assert(Enumerable.Range(0, 6).Where(i => plan.HourglassWave(i) == 2)
                                .All(i => Vector2.Distance(actor.Pos, plan.Hourglass(i)) > 12), "北侧等候不能吃到最后一组沙漏。"));
                    }
                scheduler.Schedule(8.1, () => done.Set());
                scheduler.Start();
                Assert(done.Wait(11000) && failure == null, "红冰实际撤离路线失败：" + failure);
            }
        }

        public static void WindRidersTrackLateMovementAndYieldToKnockback()
        {
            Exception failure = null;
            var random = new Random(7361);

            using (var done = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) =>
            {
                failure = error;
                done.Set();
            }))
            using (var movement = new SimMovementController(scheduler))
            using (var casts = new SimCastController(scheduler))
            using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
            {
                foreach (var purple in new[]
                {
                    CrystalPurple.MainDiagonal, CrystalPurple.AntiDiagonal
                })
                {
                    var plan = new P42时间结晶Plan(0, CrystalDebuff.RedWind, CrystalWave.LeftUp, purple, random);
                    var wind = plan.Wind(-plan.NorthSlowSide).Sample(random);
                    var actualWind = wind + new Vector2(2, 0);
                    var samples = 0;
                    var samplesAtHit = 0;
                    var actors = plan.WaterGroup.Where(slot => !plan.IsRed(slot)).Select(slot =>
                    {
                        var actor = new SimActor(new EntityTestWorld().CreateCharacter(2), casts, movement);
                        actor.Pos = plan.Initial(slot).Sample(random);
                        actor.Entity.OnActorMove = (pos, _, __) => actor.Entity.Pos3D = pos;
                        behavior.Add(actor, new SimActorBehaviorOptions
                        {
                            AttackTarget = false,
                            IdleRadius = 0
                        });
                        actor.MoveForMechanic(plan.WindApproach, 1, 2.5, urgent: true);
                        scheduler.Schedule(1, () => actor.FollowForMechanic(() =>
                        {
                            samples++;
                            return plan.WindRider(actualWind);
                        }, 2.5));
                        return actor;
                    }).ToArray();

                    scheduler.Schedule(0.9, () => Assert(samples == 0 && actors.All(actor => plan.WindApproach.Contains(actor.Pos)),
                        "蓝组应先穿入 17.5 米站位，一秒后才开始跟随红风。"));
                    scheduler.Schedule(1.8, () => actualWind = wind);
                    scheduler.Schedule(2.4, () => Assert(actors.All(actor => plan.WindRider(actualWind).Contains(actor.Pos)),
                        "红风晚就位后，三名蓝色队员应按它的最新位置对准借风区域。"));
                    scheduler.Schedule(2.5, () =>
                    {
                        foreach (var actor in actors)
                        {
                            actor.StopMoving();
                            var direction = Vector2.Normalize(actor.Pos - actualWind);
                            var landing = actor.Pos + direction * 30;
                            SimKnockback.Resolve(scheduler, new[] { actor }, KnockbackEffect.Radial(actualWind, 30, 0.8f));
                            scheduler.Schedule(3.45, () => Assert(Vector2.Distance(actor.Pos, landing) < 0.025,
                                "结束跟随后，击退仍应完整到达落点。"));
                            scheduler.Schedule(2.603, () => actor.MoveForMechanic(plan.Stack, 5.263, 6, urgent: true));
                        }

                        samplesAtHit = samples;
                        actualWind += new Vector2(5, 0);
                    });
                    scheduler.Schedule(5.55, () =>
                    {
                        Assert(samples == samplesAtHit, "击退之后不能恢复旧的红风跟随。");
                        Assert(actors.All(actor => plan.Stack.Contains(actor.Pos)), "借风后仍应赶上五人分摊。");
                    });
                }

                scheduler.Schedule(5.7, () => done.Set());
                scheduler.Start();
                Assert(done.Wait(7500) && failure == null, "红风动态借风检查失败：" + failure);
            }
        }

        public static void WindKnockbackFlowsIntoFivePlayerStack()
        {
            Exception failure = null;
            var random = new Random(7360);

            using (var done = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) =>
            {
                failure = error;
                done.Set();
            }))
            using (var movement = new SimMovementController(scheduler))
            using (var casts = new SimCastController(scheduler))
            using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
            {
                foreach (var purple in new[]
                {
                    CrystalPurple.MainDiagonal, CrystalPurple.AntiDiagonal
                })
                {
                    var plan = new P42时间结晶Plan(0, CrystalDebuff.RedIce, CrystalWave.LeftUp, purple, random);
                    var wind = plan.Wind(-plan.NorthSlowSide).Sample(random);
                    var actors = plan.EarthGroup.Select(slot =>
                    {
                        var actor = new SimActor(new EntityTestWorld().CreateCharacter(2), casts, movement);
                        var riding = plan.WaterGroup.Contains(slot);
                        actor.Pos = (riding ? plan.WindRider(wind) : plan.Initial(slot)).Sample(random);

                        // 模拟包送出后原生位置稍晚更新，不能把空中位置当成跑步起点。
                        actor.Entity.OnActorMove = (pos, _, __) =>
                            scheduler.ScheduleAfter(0.08, () => actor.Entity.Pos3D = pos);
                        behavior.Add(actor, new SimActorBehaviorOptions
                        {
                            AttackTarget = false,
                            IdleRadius = 0
                        });

                        if (riding)
                        {
                            var direction = Vector2.Normalize(actor.Pos - wind);
                            var landing = actor.Pos + direction * 30;
                            Assert(Vector2.Distance(landing, P42时间结晶Plan.Center) < 20,
                                "借风击退的完整落点不能出界。");
                            SimKnockback.Resolve(scheduler, new[] { actor }, KnockbackEffect.Radial(wind, 30, 0.8f));
                            scheduler.Schedule(0.99, () => Assert(Vector2.Distance(actor.Pos, landing) < 0.025,
                                "后续分摊指令不能截断击退，必须先到达完整落点。"));
                        }

                        scheduler.Schedule(0.103, () => actor.MoveForMechanic(plan.StackFor(slot), 2.763, 4, urgent: true));
                        return actor;
                    }).ToArray();

                    scheduler.Schedule(3.048, () =>
                    {
                        Assert(actors.All(actor => plan.Stack.Contains(actor.Pos)), "五人应在黑暗神圣判定前到达分摊区域。");
                    });
                }

                scheduler.Schedule(3.1, () => done.Set());
                scheduler.Start();
                Assert(done.Wait(5000), "击退衔接分摊检查超时。");
                scheduler.Cancel();
                Assert(failure == null, "击退衔接分摊失败：" + failure);
            }
        }

        public static void NetworkRoutesCollectPuddlesAndDodgeBothWaves()
            => CheckNetworkRoutes(0);

        public static void WindKnockbackExtremesReachStackWithoutHittingCrystal()
        {
            Exception failure = null;
            var random = new Random(1551);

            using (var done = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) =>
            {
                failure = error;
                done.Set();
            }))
            using (var movement = new SimMovementController(scheduler))
            using (var casts = new SimCastController(scheduler))
            using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
            {
                foreach (var purple in new[] { CrystalPurple.MainDiagonal, CrystalPurple.AntiDiagonal })
                {
                    var plan = new P42时间结晶Plan(0, CrystalDebuff.BlueEarth, CrystalWave.LeftUp, purple, random);
                    foreach (var windAngle in new[] { 0.0, 0.25, 0.5, 0.75 })
                    foreach (var riderAngle in new[] { 0.0, 0.5, 1.0 })
                    foreach (var riderRadius in new[] { 0.0, 1.0 })
                    foreach (var stackAngle in new[] { 0.0, 1.0 })
                    foreach (var stackRadius in new[] { 0.0, 1.0 })
                    {
                        var wind = new SimActor(new EntityTestWorld().CreateCharacter(2), casts, movement);
                        var windDestination = plan.Wind(-plan.NorthSlowSide).Sample(1, windAngle, Math.PI);
                        wind.Pos = plan.Initial(plan.Red(CrystalDebuff.RedWind, -plan.NorthSlowSide)).Sample(random);
                        wind.Entity.OnActorMove = (pos, _, __) => scheduler.ScheduleAfter(0.2, () => wind.Entity.Pos3D = pos);
                        behavior.Add(wind, new SimActorBehaviorOptions { AttackTarget = false, IdleRadius = 0 });
                        var actor = new SimActor(new EntityTestWorld().CreateCharacter(2), casts, movement);
                        actor.Pos = plan.Initial(plan.Slot(CrystalDebuff.BlueEarth)).Sample(random);
                        var start = actor.Pos;
                        var destination = plan.StackFor(plan.Slot(CrystalDebuff.BlueEarth)).Sample(stackRadius, stackAngle, Math.PI);
                        actor.Entity.OnActorMove = (pos, _, __) => scheduler.ScheduleAfter(0.2, () => actor.Entity.Pos3D = pos);
                        behavior.Add(actor, new SimActorBehaviorOptions { AttackTarget = false, IdleRadius = 0 });
                        scheduler.Schedule(1, () =>
                        {
                            wind.MoveForMechanic(SimPosition.Ellipse(windDestination, 0), 2, 6, urgent: true);
                            actor.MoveForMechanic(plan.WindApproach, 2, 6, urgent: true);
                        });
                        scheduler.Schedule(2, () => actor.FollowForMechanic(() => SimPosition.Ellipse(
                            plan.WindRider(wind.Pos).Sample(riderRadius, riderAngle, Math.PI), 0), 2.882));
                        scheduler.Schedule(2.882, () =>
                        {
                            actor.StopMoving();
                            start = actor.Pos;
                            SimKnockback.Resolve(scheduler, new[] { actor }, KnockbackEffect.Radial(wind.Pos, 30, 0.8f));
                        });
                        scheduler.Schedule(2.985, () => actor.MoveForMechanic(SimPosition.Ellipse(destination, 0), 5.645, 7, urgent: true));
                        scheduler.Schedule(5.93, () =>
                        {
                            Assert(!ActionArea.Circle(6).IntersectsCircle(未来的碎片.Position, 3.5f, actor.Pos),
                                $"分摊判定时仍在水晶附近：起点 {start}，终点 {destination}，当前位置 {actor.Pos}。");
                            Assert(plan.Stack.Contains(actor.Pos),
                                $"极限借风角度仍须赶上分摊：起点 {start}，终点 {destination}，当前位置 {actor.Pos}。");
                        });
                    }
                }

                scheduler.Schedule(6, () => done.Set());
                scheduler.Start();
                Assert(done.Wait(8000), "击退极值检查超时。");
                scheduler.Cancel();
                Assert(failure == null, "击退极值衔接分摊失败：" + failure);
            }
        }

        public static void DelayedNetworkRoutesCollectPuddlesAndDodgeBothWaves()
            => CheckNetworkRoutes(0.2);

        public static void YRoutesCollectPuddlesAndDodgeBothWaves()
            => CheckNetworkRoutes(0.2, CrystalSolution.YAntiKnockback);

        private static void CheckNetworkRoutes(double updateDelay, CrystalSolution solution = CrystalSolution.Knockback)
        {
            Exception failure = null;
            const double origin = 32;
            var random = new Random(7359);

            using (var done = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) =>
            {
                failure = error;
                done.Set();
            }))
            using (var movement = new SimMovementController(scheduler))
            using (var casts = new SimCastController(scheduler))
            using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
            {
                foreach (var wave in new[]
                {
                    CrystalWave.LeftUp,
                    CrystalWave.LeftDown,
                    CrystalWave.RightUp,
                    CrystalWave.RightDown
                }

                )
                    foreach (var purple in new[]
                    {
                        CrystalPurple.MainDiagonal,
                        CrystalPurple.AntiDiagonal
                    }

                    )
                    {
                        var plan = new P42时间结晶Plan(0, CrystalDebuff.RedIce, wave, purple, random, solution: solution);
                        var pools = Enumerable.Range(1, 4).Select(n => P42时间结晶Plan.Circle(plan.PuddleDefault(n), 0.3f).Sample(random)).ToArray();
                        var taken = new bool[4];
                        var actors = Enumerable.Range(0, 8).Select(slot =>
                        {
                            var actor = new SimActor(new EntityTestWorld().CreateCharacter(2), casts, movement);
                            actor.Pos = (plan.Debuffs[slot] == CrystalDebuff.RedWind ? plan.SouthRest : plan.Debuffs[slot] == CrystalDebuff.RedIce ? plan.RedIceNorth : plan.NorthRest).Sample(random);
                            actor.Entity.OnActorMove = (pos, _, __) =>
                            {
                                if (!done.IsSet)
                                    scheduler.ScheduleAfter(updateDelay, () => actor.Entity.Pos3D = pos);
                            };
                            behavior.Add(actor, new SimActorBehaviorOptions
                            {
                                AttackTarget = false,
                                IdleRadius = 0
                            });

                            foreach (var move in plan.RewindRoute(slot, actor.Pos, pools))
                                scheduler.Schedule(move.Start - origin, () => actor.MoveForMechanic(move.Area, move.Arrival - origin, 20, urgent: true));

                            return actor;
                        }).ToArray();

                        void CheckPools()
                        {
                            for (var index = 0; index < 4; index++)
                            {
                                if (taken[index])
                                    continue;

                                for (var member = 0; member < 4; member++)
                                {
                                    var slot = plan.BlueOrder[member];

                                    if (Vector2.Distance(actors[slot].Pos, pools[index]) > 1)
                                        continue;

                                    Assert(index == member, $"{wave} {purple} 蓝色队员误吃了其他白圈。");
                                    taken[index] = true;
                                    var leave = plan.LeavePuddle(actors[slot].Pos, scheduler.ElapsedSeconds + origin);
                                    scheduler.Schedule(leave.Start - origin, () => actors[slot].MoveForMechanic(leave.Area, leave.Arrival - origin, 20, urgent: true));
                                    break;
                                }
                            }

                            if (scheduler.ElapsedSeconds < 11.5)
                                scheduler.ScheduleAfter(0.1, CheckPools);
                        }

                        scheduler.Schedule(0.5, CheckPools);

                        for (var w = 0; w < 2; w++)
                            for (var s = 0; s < 4; s++)
                            {
                                var t = P42时间结晶Plan.WaveHits[w][s] - origin;

                                if (t < 0)
                                    continue;

                                var position = plan.WavePosition(w, s);
                                var direction = plan.WaveDirection(w);
                                var heading = (float)Math.Atan2(direction.X, direction.Y);

                                scheduler.Schedule(t, () =>
                                {
                                    for (var slot = 0; slot < 8; slot++)
                                        Assert(!ActionArea.Rectangle(10, 40).Contains(actors[slot].Pos, position, heading), $"{wave} {purple} 在 {t + origin:0.000} 秒光波击中 {P42时间结晶Plan.Roles[slot]}：{actors[slot].Pos}。");
                                });
                            }

                        scheduler.Schedule(P42时间结晶Plan.RewindTime - origin, () =>
                        {
                            Assert(taken.All(t => t), $"{wave} {purple} 未及时吃完白圈。");

                            for (var slot = 0; slot < 8; slot++)
                            {
                                Assert(plan.Rewind(slot).Contains(actors[slot].Pos), $"{wave} {purple} 未在回返记录前完成排队。");
                                actors[slot].MoveForMechanic(plan.Spread(slot), P42时间结晶Plan.SpreadArrival - origin, 20, urgent: true);
                            }
                        });

                        scheduler.Schedule(47.375 - origin, () =>
                        {
                            for (var slot = 0; slot < 8; slot++)
                                Assert(plan.Spread(slot).Contains(actors[slot].Pos),
                                    $"{solution} {wave} {purple} 的 {P42时间结晶Plan.Roles[slot]} 未在碎灵前完成分散。");
                        });
                    }

                scheduler.Schedule(15.5, () => done.Set());
                scheduler.Start();
                Assert(done.Wait(20000), "光波与白圈实际移动检查超时。");
                scheduler.Cancel();
                Assert(failure == null, "光波与白圈实际移动检查失败：" + failure);
            }
        }

        public static void AssignmentsAndRandomAreasPreserveMechanics()
        {
            var random = new Random(7358);

            foreach (var priority in new[]
            {
                "12345678",
                "34125678",
                "43215678",
                "23184567"
            }

            )
                foreach (var wave in new[]
                {
                    CrystalWave.LeftUp,
                    CrystalWave.LeftDown,
                    CrystalWave.RightUp,
                    CrystalWave.RightDown
                }

                )
                    foreach (var purple in new[]
                    {
                        CrystalPurple.MainDiagonal,
                        CrystalPurple.AntiDiagonal
                    }

                    )
                        foreach (var selected in new[]
                        {
                            CrystalDebuff.RedIce,
                            CrystalDebuff.RedWind,
                            CrystalDebuff.BlueWater,
                            CrystalDebuff.BlueEarth,
                            CrystalDebuff.BlueDark,
                            CrystalDebuff.BlueIce
                        }

                        )
                            for (var self = 0; self < 8; self++)
                            {
                                var plan = new P42时间结晶Plan(self, selected, wave, purple, random, priority);
                                Assert(plan.Debuffs[self] == selected, "自身点名必须服从选项。");
                                var stackDirection = 135 * plan.NorthSlowSide;
                                Assert(Vector2.Distance(plan.Stack.Midpoint(), P42时间结晶Plan.Polar(16, stackDirection)) < 0.001f &&
                                    plan.Stack.Contains(P42时间结晶Plan.Polar(13.01f, stackDirection - 6.99)) &&
                                    plan.Stack.Contains(P42时间结晶Plan.Polar(18.99f, stackDirection + 6.99)) &&
                                    !plan.Stack.Contains(P42时间结晶Plan.Polar(19.01f, stackDirection)) &&
                                    !plan.Stack.Contains(P42时间结晶Plan.Polar(16, stackDirection + 7.01)),
                                    "分摊参与者必须使用斜方向 r=16±3、角度 ±7° 的扇环，不能回退到场边点。");
                                Assert(plan.Debuffs.Count(d => d == CrystalDebuff.RedIce) == 2 && plan.Debuffs.Count(d => d == CrystalDebuff.RedWind) == 2, "红冰与红风应各两人。");
                                Assert(plan.BlueOrder.Distinct().Count() == 4 && plan.BlueOrder.All(i => !plan.IsRed(i)), "四个蓝色头标应互不重复。");
                                Assert(plan.BlueOrder.Select(i => plan.Debuffs[i]).SequenceEqual(new[]
                                {
                    CrystalDebuff.BlueEarth, CrystalDebuff.BlueWater, CrystalDebuff.BlueIce, CrystalDebuff.BlueDark
                                }),
                    "红组优先级不能改变蓝色点名的头标和吃圈顺序。");

                                foreach (var kind in new[]
                                {
                                    CrystalDebuff.RedIce,
                                    CrystalDebuff.RedWind
                                }

                                )
                                {
                                    var left = plan.Red(kind, -1);
                                    var right = plan.Red(kind, 1);
                                    var order = priority == "12345678" ? new[]
                                    {
                                        0,
                                        1,
                                        2,
                                        3,
                                        4,
                                        5,
                                        6,
                                        7
                                    }

                                    :
                        priority == "34125678" ? new[]
                                    {
                                        2,
                                        3,
                                        0,
                                        1,
                                        4,
                                        5,
                                        6,
                                        7
                                    }

                                    :
                        priority == "43215678" ? new[]
                                    {
                                        3,
                                        2,
                                        1,
                                        0,
                                        4,
                                        5,
                                        6,
                                        7
                                    }

                                    : new[]
                                    {
                                        2,
                                        0,
                                        1,
                                        4,
                                        5,
                                        6,
                                        7,
                                        3
                                    };
                                    Assert(Array.IndexOf(order, left) < Array.IndexOf(order, right), "红色同类必须按所选职能顺序高左低右。");
                                }

                                Assert(plan.WaterGroup.Length == 4 && plan.EarthGroup.Length == 5, "水分摊四人、土分摊五人。");

                                for (var sample = 0; sample < 50; sample++)
                                {
                                    var initial = Enumerable.Range(0, 8).Select(i => plan.Initial(i).Sample(random)).ToArray();

                                    foreach (var sand in new[]
                                    {
                                        plan.Hourglass(0),
                                        plan.Hourglass(3)
                                    }

                                    )
                                        Assert(initial.All(p => Vector2.Distance(p, sand) > 12), "初始随机区域应避开第一组沙漏。");

                                    var water = initial[plan.Slot(CrystalDebuff.BlueWater)];
                                    Assert(Enumerable.Range(0, 8).Where(i => Vector2.Distance(initial[i], water) <= 6).OrderBy(i => i).SequenceEqual(plan.WaterGroup.OrderBy(i => i)), "初始随机区域不能改变水分摊人数。");
                                    Assert(!ActionArea.Circle(6).IntersectsCircle(未来的碎片.Position, 3.5f, water) &&
                                        !ActionArea.Circle(6).IntersectsCircle(未来的碎片.Position, 3.5f, plan.StackFor(plan.Slot(CrystalDebuff.BlueEarth)).Sample(random)),
                                        "水、土与暗的安全点必须连同水晶碰撞半径一起避开。");

                                    foreach (var side in new[]
                                    {
                                        -1,
                                        1
                                    })
                                    {
                                        var ice = initial[plan.Red(CrystalDebuff.RedIce, side)];
                                        Assert(!ActionArea.Donut(3, 12).IntersectsCircle(未来的碎片.Position, 3.5f, ice) &&
                                            !ActionArea.Circle(12).IntersectsCircle(未来的碎片.Position, 3.5f, ice) &&
                                            !ActionArea.Circle(12).IntersectsCircle(未来的碎片.Position, 3.5f, plan.RedHead(side).Sample(random)) &&
                                            !ActionArea.Circle(15).IntersectsCircle(未来的碎片.Position, 3.5f, plan.Wind(side).Sample(random)),
                                            "红冰、龙头接触与红风不能波及水晶。");
                                    }

                                    var wind = plan.Wind(-plan.NorthSlowSide).Sample(random);
                                    var riders = Enumerable.Range(0, 3).Select(_ => plan.WindRider(wind).Sample(random)).ToArray();

                                    foreach (var rider in riders)
                                    {
                                        Assert(Vector2.Distance(plan.WindApproach.Midpoint(), rider) / 7.8 + 0.2 < 24.837 - (P42时间结晶Plan.SandHits[0] + 2),
                                            "最晚穿入一秒后开始跟随，仍应来得及到达借风区域。");
                                        Assert(Vector2.Distance(rider, wind) < 3, "蓝冰和红风之间应保持在冰封内圈内。");
                                        Assert(!ActionArea.Donut(3, 12).IntersectsCircle(未来的碎片.Position, 3.5f, rider),
                                            "蓝冰借风站位不能波及水晶。");
                                        var landing = rider + Vector2.Normalize(rider - wind) * 30;
                                        Assert(Vector2.Distance(landing, P42时间结晶Plan.Center) < 19, "随机击退角度不能把假队员送到场外。");
                                        Assert((Vector2.Distance(landing, plan.Stack.Midpoint()) - plan.Stack.DeviationRadius) / 7.8 + 0.4 < 27.885 - 24.837 - 0.9,
                                            "击退落地后应能在冲刺上限内进入五人分摊区域。");
                                    }

                                    Assert(riders.All(p => riders.All(q => Vector2.Distance(p, q) < 3)), "蓝色三人的独立随机不能踩出冰封内圈。");
                                    var rewinds = Enumerable.Range(0, 8).Select(i => plan.Rewind(i).Sample(random)).ToArray();
                                    var x = P42时间结晶Plan.FrontFour(rewinds, 0, plan.FirstSide);
                                    var y = P42时间结晶Plan.FrontFour(rewinds, 1, plan.SecondSide);
                                    Assert(x[0] < 2 && y[0] < 2 && x[0] != y[0], "两次圣翼都应由不同坦克挡在最前方。");
                                    Assert(!x.Take(4).Intersect(y.Take(4)).Any(), "两次圣翼的前四人必须互补。");

                                    foreach (var pos in rewinds)
                                    {
                                        var first = pos + plan.WaveDirection(0) * 20;
                                        var second = first + plan.WaveDirection(1) * 20;
                                        Assert(Vector2.Distance(pos, P42时间结晶Plan.Center) < 20 && Vector2.Distance(first, P42时间结晶Plan.Center) < 20 && Vector2.Distance(second, P42时间结晶Plan.Center) < 20, "回返与连续击退的三个位置都必须在场内。");
                                    }

                                    var spreads = Enumerable.Range(0, 8).Select(i => plan.Spread(i).Sample(random)).ToArray();

                                    for (var i = 0; i < 8; i++)
                                    {
                                        Assert(Vector2.Distance(spreads[i], P42时间结晶Plan.Center) < 20 &&
                                            !ActionArea.Circle(5).IntersectsCircle(未来的碎片.Position, 3.5f, spreads[i]),
                                            "分散不能出界，技能边缘也不能接触水晶碰撞体。");

                                        for (var j = i + 1; j < 8; j++)
                                            Assert(Vector2.Distance(spreads[i], spreads[j]) > 5, "碎灵一击随机分散不得重叠。");

                                        Assert(Vector2.Distance(rewinds[i], spreads[i]) / 7.8 + 0.1 < 47.375 - P42时间结晶Plan.RewindTime,
                                            "记录回返后必须有时间通过正常移动或冲刺展开分散。");
                                    }
                                }
                            }
        }

        public static void SpiritTakerPrioritizesCrystalThenOverlapThenRandom()
        {
            var random = new Random(7360);
            var area = ActionArea.Circle(5);
            var positions = Enumerable.Range(0, 8).Select(i => new Vector2(100 + 10 * i, 110)).ToArray();
            var crystal = 未来的碎片.Position;
            positions[0] = crystal + new Vector2(8.5f, 0);
            positions[1] = positions[0] + Vector2.UnitX;
            Assert(碎灵一击.SelectTarget(positions, 0, area, crystal, 3.5f, random) == 0,
                "即使旁边有人，能够打到水晶时也必须优先点自己。");

            positions[0] = new Vector2(100, 110);
            positions[1] = positions[0] + new Vector2(5, 0);
            Assert(碎灵一击.SelectTarget(positions, 0, area, crystal, 3.5f, random) == 1,
                "自己进入其他人的碎灵范围时，应点那个人而非自己，保留受波及击退。");

            positions[1] = new Vector2(110, 110);
            var selected = Enumerable.Range(0, 256)
                .Select(_ => 碎灵一击.SelectTarget(positions, 0, area, crystal, 3.5f, random)).Distinct().ToArray();
            Assert(selected.Length == 8, "自身既不威胁水晶也不重叠时，应保留八人随机点名。");
        }

        public static void SpiritTakerWithoutCrystalStillPrioritizesOverlap()
        {
            var random = new Random(40289);
            var area = ActionArea.Circle(5);
            var positions = Enumerable.Range(0, 8).Select(i => new Vector2(100 + 10 * i, 110)).ToArray();
            positions[1] = positions[0] + new Vector2(5, 0);
            for (var sample = 0; sample < 32; sample++)
                Assert(碎灵一击.SelectTarget(positions, 0, area, null, 0, random) == 1,
                    "没有水晶时，自身靠近队友仍应必定点队友，让自身受到波及击退。");

            positions[1] = new Vector2(110, 110);
            var selected = Enumerable.Range(0, 256)
                .Select(_ => 碎灵一击.SelectTarget(positions, 0, area, null, 0, random)).Distinct().ToArray();
            Assert(selected.Length == 8, "没有水晶且自身未与队友重叠时，应保留八人随机点名。");
        }

        public static void BothSolutionsKeepFullSpreadAreasSafeAndYTankOrderCorrect()
        {
            var random = new Random(7362);
            foreach (var wave in new[] { CrystalWave.LeftUp, CrystalWave.LeftDown, CrystalWave.RightUp, CrystalWave.RightDown })
                foreach (var solution in new[] { CrystalSolution.Knockback, CrystalSolution.YAntiKnockback })
                {
                    var plan = new P42时间结晶Plan(0, CrystalDebuff.RedIce, wave, CrystalPurple.MainDiagonal, random, solution: solution);
                    var spread = Enumerable.Range(0, 8).Select(plan.Spread).ToArray();
                    for (var i = 0; i < 8; i++)
                    {
                        var center = spread[i].Midpoint();
                        var tolerance = spread[i].DeviationRadius;
                        Assert(Vector2.Distance(center, P42时间结晶Plan.Center) + tolerance < 20,
                            "整个分散容差范围都不能越过电网。");
                        Assert(Vector2.Distance(center, 未来的碎片.Position) - tolerance > 8.5,
                            "整个分散容差范围都必须避开 5 米碎灵伤害与 3.5 米水晶。");
                        Assert(Vector2.Distance(center, plan.Rewind(i).Midpoint()) + tolerance + plan.Rewind(i).DeviationRadius <
                            (47.375 - P42时间结晶Plan.RewindTime - 0.1) * 7.8,
                            "最远随机分散落点也要在碎灵判定前能够跑到。");
                        for (var j = 0; j < i; j++)
                            Assert(Vector2.Distance(center, spread[j].Midpoint()) - tolerance - spread[j].DeviationRadius > 5,
                                "任意两人的完整容差范围都不能造成碎灵重叠。");
                    }

                    if (!plan.IsY)
                        continue;

                    for (var sample = 0; sample < 500; sample++)
                    {
                        var positions = Enumerable.Range(0, 8).Select(i => plan.Rewind(i).Sample(random)).ToArray();
                        for (var wing = 0; wing < 2; wing++)
                        {
                            var order = P42时间结晶Plan.FrontFour(positions, wing, wing == 0 ? plan.FirstSide : plan.SecondSide);
                            Assert(plan.WingTanksCorrect(order, wing), "Y 字随机站位应保证第一次首位、第二次前两位是坦克。");
                        }

                        Assert(positions.All(p => Vector2.Distance(p + plan.WaveDirection(1) * 20, P42时间结晶Plan.Center) < 20),
                            "Y 字仅承受第二次击退，随机落点也应全部在场内。");
                    }

                    Assert(!plan.WingTanksCorrect(new[] { 0, 2, 1, 3, 4, 5, 6, 7 }, 1),
                        "第二次只有首位是坦克时必须判错。");
                }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }
    }
}
