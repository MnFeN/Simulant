using Simulant.Content.U7a.Presets;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.Game.Character;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using ActionRow = Lumina.Excel.Sheets.Action;
using static Simulant.Utils.Maths.Angles;

namespace Simulant.Tests
{
    internal static class BehaviorMovementTests
    {
        public static void ApocalypseHealersReturnBeforeTheThirdWaterStack()
        {
            GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Knockback>()[201] = new Lumina.Excel.Sheets.Knockback
            {
                Distance = 21,
                Speed = 40
            };
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
                var boss = new Vector2(99.992f, 89.036f);
                var towardCenter = Math.Atan2(100 - boss.X, 100 - boss.Y);
                var maxRadius = 0.8f * (Vector2.Distance(boss, new Vector2(100, 100)) - 2);
                var actors = new List<SimActor>();
                var destinations = new List<Vector2>();
                var sides = new List<int>();
                var startedAt = new List<double>();
                foreach (var side in new[] { -1, 1 })
                    foreach (var radius in new[] { maxRadius / 2, maxRadius })
                        foreach (var angle in new[] { 10, 20 })
                            foreach (var stackRadius in new[] { 13, 15 })
                                foreach (var stackAngle in new[] { 17, 33 })
                                {
                                    var heading = towardCenter + Deg(side * angle);
                                    var start = boss + radius * ApocalypsePlan.Direction(heading);
                                    var destination = boss + stackRadius * ApocalypsePlan.Direction(towardCenter + Deg(side * stackAngle));
                                    var actor = CreateActor(behavior, movement, casts, job: Job.SCH, position: start);
                                    var index = actors.Count;
                                    startedAt.Add(double.PositiveInfinity);
                                    actor.Entity.OnActorMove = (next, _, speed) =>
                                    {
                                        var packet = actor.Entity.NetworkMoves[actor.Entity.NetworkMoves.Count - 1];
                                        if (speed != 0 && packet.Flags == Simulant.Game.FFCS.Client.Game.Network.ActorMoveFlags.None)
                                            startedAt[index] = Math.Min(startedAt[index], scheduler.ElapsedSeconds);
                                        if (scheduler.IsRunning)
                                            scheduler.ScheduleAfter(0.08, () => actor.Entity.Pos3D = next);
                                    };
                                    SimKnockback.Resolve(scheduler, new[] { actor }, KnockbackEffect.DirectionalFromRow((float)heading, 201));
                                    scheduler.Schedule(0.15, () => actor.MoveForMechanic(destination, 2.25, 3.85, urgent: true, gapClose: true));
                                    actors.Add(actor);
                                    destinations.Add(destination);
                                    sides.Add(side);
                                }

                scheduler.Schedule(3.21, () =>
                {
                    for (var i = 0; i < actors.Count; i++)
                    {
                        Assert(startedAt[i] < 0.95, "击退落地后应及时回跑，不能继续使用旧的一秒锁定窗口。");
                        foreach (var near in new[] { true, false })
                            foreach (var radius in near ? new[] { 11, 13 } : new[] { 13, 15 })
                                foreach (var angle in new[] { 17, 33 })
                                {
                                    var target = boss + radius * ApocalypsePlan.Direction(towardCenter + Deg(sides[i] * angle));
                                    Assert(Vector2.Distance(actors[i].Pos, target) <= 6,
                                        FormattableString.Invariant($"治疗击退后必须参与第三轮分摊：实际 {actors[i].Pos}，终点 {destinations[i]}，与点名者相距 {Vector2.Distance(actors[i].Pos, target):F3} 米。"));
                                }
                    }
                    done.Set();
                });
                scheduler.Start();
                Assert(done.Wait(5000) && failure == null, "治疗击退回跑检查失败：" + failure);
            }
        }

        public static void ApocalypseTankReachesEighteenMetresBeforeTheBaitSnapshot()
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
                var actors = new List<SimActor>();
                var destinations = new List<Vector2>();
                var random = new Random(40182);
                foreach (var tank in new[] { BaitTank.MT, BaitTank.ST })
                    foreach (var group in new[] { 0, 1 })
                    {
                        ApocalypsePlan plan = null;
                        for (var attempt = 0; attempt < 64; attempt++)
                        {
                            plan = new ApocalypsePlan(0, ApocalypseDebuff.Random, ApocalypseSolution.DoubleCN,
                                ApocalypseReference.GroupSafe, ApocalypseHalf.NorthToSouthwest,
                                ExerciseDirection.N, ExerciseTurn.Clockwise, random, 5, tank);
                            if (plan.StackGroups[(int)tank] == group)
                                break;
                        }
                        Assert(plan.StackGroups[(int)tank] == group, "检查必须覆盖从两侧分摊位置引导。");
                        var departure = plan.SecondStack((int)tank);
                        var destination = plan.TankPosition;
                        var start = departure.Sample(0, 0.5, Math.PI);
                        var actor = CreateActor(behavior, movement, casts, job: Job.DRK, position: start);
                        actor.MoveForMechanic(destination, 36.35 - 34.55, 37.84 - 34.55, urgent: true);
                        actors.Add(actor);
                        destinations.Add(destination);
                    }

                scheduler.Schedule(36.81 - 34.55, () =>
                {
                    for (var index = 0; index < actors.Count; index++)
                    {
                        Assert(Vector2.Distance(actors[index].Pos, destinations[index]) <= 0.025f,
                            "坦克应通过实际移动控制器在死刑锁定前到达十八米引导点。");
                        Assert(actors[index].Entity.NetworkMoves.TrueForAll(move => move.SpeedParam <= 78),
                            "贴边引导不能超过一倍三移速上限。");
                    }
                    done.Set();
                });
                scheduler.Start();
                Assert(done.Wait(4000) && failure == null, "十八米贴边引导检查失败：" + failure);
            }
        }

        public static void GapClosingWaitsForKnockbackAndKeepsTheMechanicDestination()
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
                var boss = new SimActor(new EntityTestWorld().CreateCharacter(0), casts, movement);
                boss.Entity.HitboxRadius = 3;
                behavior.SetTarget(boss);
                var actors = new List<SimActor>();
                foreach (var job in new[] { Job.PLD, Job.WAR, Job.DRK, Job.GNB, Job.MNK, Job.DRG, Job.NIN, Job.SAM, Job.RPR, Job.VPR, Job.BRD })
                {
                    var actor = CreateActor(behavior, movement, casts, job: job, position: new Vector2(20, 0));
                    actor.Entity.OnActorMove = (next, _, __) =>
                    {
                        if (scheduler.IsRunning)
                            scheduler.ScheduleAfter(0.25, () => actor.Entity.Pos3D = next);
                    };
                    SimKnockback.Resolve(scheduler, new[] { actor }, KnockbackEffect.Directional((float)Deg(90), 6, 0.2f));
                    actors.Add(actor);
                }
                var destination = new Vector2(9, 5);
                scheduler.Schedule(0.1, () =>
                {
                    foreach (var actor in actors)
                        actor.MoveForMechanic(destination, 2.2, 3.5, urgent: true, gapClose: true);
                });
                scheduler.Schedule(0.3, () =>
                {
                    foreach (var actor in actors)
                        actor.MoveForMechanic(destination, 2.2, 3.5, urgent: true, gapClose: true);
                });
                scheduler.Schedule(0.43, () =>
                {
                    foreach (var actor in actors)
                        Assert(actor.Entity.ActionEffects.Count == 0, "击退未结束时不能提前突进、截断击退轨迹。");
                });
                scheduler.Schedule(0.95, () =>
                {
                    foreach (var actor in actors)
                    {
                        var action = SimJobActions.For(actor.Job).GapCloser;
                        Assert(action == 0 ? actor.Entity.ActionEffects.Count == 0 : actor.Entity.ActionEffects.Contains(action),
                            "T 和近战应使用本职业突进，远程保持普通回跑。");
                        if (action != 0)
                            Assert(actor.Entity.NetworkMoves.Exists(move => Vector2.Distance(new Vector2(move.Position.X, move.Position.Y), new Vector2(3.5f, 0)) < 0.025f),
                                "突进必须沿原连线落在目标圈外半米，不能把普通跑步或站位终点当作突进落点。");
                    }
                });
                scheduler.Schedule(3.35, () =>
                {
                    foreach (var actor in actors)
                    {
                        Assert(Vector2.Distance(actor.Pos, destination) < 0.025f && !actor.IsMoving,
                            FormattableString.Invariant($"突进结束后仍须完成原机制走位，不能停在 Boss 身边或丢失目的地：{actor.Job} 位于 {actor.Pos}。"));
                        if (SimJobActions.For(actor.Job).GapCloser != 0)
                            Assert(actor.Entity.ActionEffects.Count == 1, "同一条走位意图只能突进一次。");
                    }
                    done.Set();
                });
                scheduler.Start();
                Assert(done.Wait(4500) && failure == null, "击退后突进并继续走位检查失败：" + failure);
            }
        }

        public static void GapClosingFallsBackWhenBossCannotBeTargeted()
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
                var boss = new SimActor(new EntityTestWorld().CreateCharacter(0), casts, movement);
                boss.Entity.IsTargetable = false;
                behavior.SetTarget(boss);
                var actor = CreateActor(behavior, movement, casts, job: Job.DRK, position: new Vector2(20, 0));
                actor.MoveForMechanic(new Vector2(20, 2), 1, 1.5, urgent: true, gapClose: true);
                scheduler.Schedule(1.1, () =>
                {
                    Assert(actor.Entity.ActionEffects.Count == 0 && Vector2.Distance(actor.Pos, new Vector2(20, 2)) < 0.025f,
                        "Boss 不可选中时应直接完成普通机制移动，不得突进或中断流程。");
                    done.Set();
                });
                scheduler.Start();
                Assert(done.Wait(2500) && failure == null, "不可选中目标的突进降级检查失败：" + failure);
            }
        }

        public static void MeleeApproachWaitsAndStopsWhenBossCannotBeAttacked()
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
                var boss = new SimActor(new EntityTestWorld().CreateCharacter(0), casts, movement);
                boss.Entity.HitboxRadius = 2;
                behavior.SetTarget(boss);
                var waiting = CreateActor(behavior, movement, casts, job: Job.VPR, position: new Vector2(4, 0));
                var returning = CreateActor(behavior, movement, casts, job: Job.MNK, position: new Vector2(20, 0));
                var urgent = CreateActor(behavior, movement, casts, job: Job.DRK, position: new Vector2(4, 0));
                var stopped = Vector2.Zero;
                waiting.MoveForMechanic(new Vector2(0, 15), 12, holdUntil: 14);
                urgent.MoveForMechanic(new Vector2(4, 5), 2, holdUntil: 14, urgent: true);

                scheduler.Schedule(0.3, () =>
                {
                    Assert(waiting.Pos == new Vector2(4, 0) && !waiting.IsMoving,
                        "机制时间充裕时，不能接到指令就立刻转向贪刀位置。");
                    Assert(returning.IsMoving, "检查前远离且无机制的近战应已开始回近战范围。");
                    boss.Entity.IsTargetable = false;
                });
                scheduler.Schedule(0.6, () => stopped = returning.Pos);
                scheduler.Schedule(2.3, () =>
                {
                    Assert(waiting.Pos == new Vector2(4, 0) && !waiting.IsMoving,
                        "Boss 不可选中后，尚未开始的贪刀绕路应取消，保留机制目的地。");
                    Assert(returning.Pos == stopped && !returning.IsMoving,
                        "Boss 不可选中后，已经开始的返回近战动作也应停止。");
                    Assert(urgent.Pos == new Vector2(4, 5), "Boss 不可选中不能取消真正的机制移动。");
                    done.Set();
                });
                scheduler.Start();
                Assert(done.Wait(4000) && failure == null, "近战贪刀与目标状态检查失败：" + failure);
            }
        }

        public static void DynamicFollowingKeepsItsOffsetAndEndsWithoutOverridingNewMovement()
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
                var follower = CreateActor(behavior, movement, casts);
                var replaced = CreateActor(behavior, movement, casts);
                var target = Vector2.Zero;
                var offset = Vector2.Zero;
                var stopped = Vector2.Zero;
                var maxSpeed = 0;
                var samples = 0;
                var samplesAtEnd = 0;
                var replacementSamples = 0;
                var samplesAtReplacement = 0;
                follower.Entity.OnActorMove = (pos, _, speed) =>
                {
                    maxSpeed = Math.Max(maxSpeed, speed);
                    follower.Entity.Pos3D = pos;
                };
                follower.FollowForMechanic(() =>
                {
                    samples++;
                    return SimPosition.Ellipse(target, 0.2f);
                }, 3.4);
                replaced.FollowForMechanic(() =>
                {
                    replacementSamples++;
                    return target;
                }, 10);

                scheduler.Schedule(0.5, () => offset = follower.Pos);

                for (var step = 1; step <= 12; step++)
                {
                    var index = step;
                    scheduler.Schedule(0.5 + step * 0.1, () => target = new Vector2(index * 0.3f, 0));
                }

                scheduler.Schedule(1.8, () => Assert(follower.Pos.X > 1.5f,
                    "持续改变目标时也应持续前进，不能不断重发首包导致停在原地。"));
                scheduler.Schedule(2.6, () =>
                {
                    Assert(Vector2.Distance(follower.Pos - target, offset) <= 0.026,
                        "动态范围中的随机偏移只能选一次，不能每次更新重新抽取。");
                    Assert(!follower.IsMoving, "目标停下并追上后应停止移动。");
                    replaced.MoveForMechanic(new Vector2(0, 3), 4.4, 6, urgent: true);
                    samplesAtReplacement = replacementSamples;
                    target = new Vector2(100, 0);
                });
                scheduler.Schedule(3.6, () =>
                {
                    Assert(!follower.IsMoving && maxSpeed == 78,
                        "临时移远的目标即使来不及追上也只按上限冲刺，到期停止，不抛出到位超时。");
                    stopped = follower.Pos;
                    samplesAtEnd = samples;
                    target = new Vector2(-100, 0);
                });
                scheduler.Schedule(4.6, () =>
                {
                    Assert(follower.Pos == stopped && samples == samplesAtEnd,
                        "结束后不能再采样或追赶动态目标。");
                    Assert(Vector2.Distance(replaced.Pos, new Vector2(0, 3)) <= 0.025 && replacementSamples == samplesAtReplacement,
                        "新移动命令应完全替换跟随，旧目标不能重新接管。");
                    done.Set();
                });

                scheduler.Start();
                Assert(done.Wait(6500) && failure == null, "动态跟随检查失败：" + failure);
            }
        }

        public static void TightDeadlinesUseSprintButNeverExceedItsLimit()
        {
            Exception failure = null;
            var sprintEffects = new HashSet<SimActor>();
            var sprintCount = new Dictionary<SimActor, int>();

            using (var done = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) =>
            {
                failure = error;
                done.Set();
            }))
            using (var movement = new SimMovementController(scheduler))
            using (var casts = new SimCastController(scheduler))
            using (var behavior = new SimActorBehaviorController(scheduler, movement, casts, (actor, enabled) =>
            {
                if (enabled)
                {
                    sprintEffects.Add(actor);
                    sprintCount.TryGetValue(actor, out var count);
                    sprintCount[actor] = count + 1;
                }
                else
                    sprintEffects.Remove(actor);
            }))
            {
                var normal = CreateActor(behavior, movement, casts);
                var sprint = CreateActor(behavior, movement, casts);
                var delayed = CreateActor(behavior, movement, casts);
                var normalMax = 0;
                var sprintMax = 0;
                var delayedMax = 0;

                normal.Entity.OnActorMove = (pos, _, speed) =>
                {
                    normalMax = Math.Max(normalMax, speed);
                    normal.Entity.Pos3D = pos;
                };

                sprint.Entity.OnActorMove = (pos, _, speed) =>
                {
                    sprintMax = Math.Max(sprintMax, speed);
                    sprint.Entity.Pos3D = pos;
                };

                delayed.Entity.OnActorMove = (pos, _, speed) =>
                {
                    delayedMax = Math.Max(delayedMax, speed);

                    if (scheduler.ElapsedSeconds >= 0.4)
                        delayed.Entity.Pos3D = pos;
                };

                normal.MoveForMechanic(new Vector2(8, 0), 3, 4, urgent: true);
                sprint.MoveForMechanic(new Vector2(8, 0), 1.55, 4, urgent: true);
                delayed.MoveForMechanic(new Vector2(8, 0), 1.95, 4, urgent: true);
                scheduler.Schedule(1.55, () => Assert(Vector2.Distance(sprint.Pos, new Vector2(8, 0)) < 0.025,
                    "正常速度来不及的移动应自动冲刺到位。"));

                scheduler.Schedule(2.1, () =>
                {
                    Assert(sprintEffects.Count == 0 && !sprintCount.ContainsKey(normal) &&
                        sprintCount[sprint] == 1 && sprintCount[delayed] == 1,
                        "只有冲刺才添加演出状态，到达目的地后应移除。");
                    Assert(normalMax == 60 && sprintMax == 78 && delayedMax == 78,
                        "正常移动保持 6 米/秒，接令时或途中来不及时最多使用 7.8 米/秒。");
                    Assert(Vector2.Distance(delayed.Pos, new Vector2(8, 0)) < 0.025, "途中延迟后应冲刺赶上目标。");
                    sprint.MoveForMechanic(new Vector2(10, 0), 3.5, 4, urgent: true);
                    sprintMax = 0;
                });

                scheduler.Schedule(3.6, () =>
                {
                    Assert(sprintMax == 60, "下一段时间充裕的移动应恢复正常跑速。");
                    sprint.MoveForMechanic(new Vector2(18, 0), 5.15, 6, urgent: true);
                });

                scheduler.Schedule(3.9, () =>
                {
                    Assert(sprintEffects.Contains(sprint) && sprintCount[sprint] == 2,
                        "下一次冲刺应重新添加状态，不受冷却限制。");
                    sprint.SuspendBehavior(1);
                });

                scheduler.Schedule(4.1, () =>
                {
                    Assert(!sprintEffects.Contains(sprint), "移动被暂停时也必须移除冲刺状态。");
                    done.Set();
                });

                scheduler.Start();
                Assert(done.Wait(6000), "自动冲刺检查超时。");
                scheduler.Cancel();
                Assert(failure == null, "自动冲刺检查失败：" + failure);
            }
        }

        public static void MissedDeadlinesKeepMovingAndLeaveJudgementToMechanics()
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
                var late = CreateActor(behavior, movement, casts);
                var near = CreateActor(behavior, movement, casts);
                var maxSpeed = 0;
                var nearTarget = new Vector2(4, 0);
                var stack = ActionArea.Circle(1);
                late.Entity.OnActorMove = (pos, _, speed) =>
                {
                    maxSpeed = Math.Max(maxSpeed, speed);
                    late.Entity.Pos3D = pos;
                };
                // 客户端已接近终点，但沿途发包与实际到达确认尚未完成。
                near.Entity.OnActorMove = (pos, _, __) => near.Entity.Pos3D = new Vector3(3.99f, 0, pos.Z);

                late.MoveForMechanic(new Vector2(20, 0), 0.2, 4, urgent: true);
                near.MoveForMechanic(nearTarget, 0.05, 4, urgent: true);
                scheduler.Schedule(0.45, () =>
                {
                    Assert(near.Pos != nearTarget && stack.Contains(near.Pos, nearTarget, 0),
                        "没有精确走到终点但已在分摊内时，仍应按实际位置参与判定。");
                    Assert(late.Pos.X > 0 && late.Pos.X < 20 && late.IsMoving,
                        "明显赶不上截止时间的队员也应继续赶路，不能中断模拟或瞬移到位。");
                });
                scheduler.Schedule(3.2, () =>
                {
                    Assert(maxSpeed == 78 && Vector2.Distance(late.Pos, new Vector2(20, 0)) <= 0.025,
                        "迟到后继续以不超过 1.3 倍的速度移动，最终应能到达目标。");
                    Assert(!near.IsMoving, "厘米级到位偏差应能正常结束移动。");
                    done.Set();
                });

                scheduler.Start();
                Assert(done.Wait(5000) && failure == null, "移动截止时间不能代替机制判定：" + failure);
            }
        }

        public static void NewMechanicMoveWaitsForKnockbackToFinish()
        {
            Exception failure = null;

            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) =>
            {
                failure = error;
                finished.Set();
            }))
            using (var movement = new SimMovementController(scheduler))
            using (var casts = new SimCastController(scheduler))
            using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
            {
                var actor = CreateActor(behavior, movement, casts);
                SimKnockback.Resolve(scheduler, new[] { actor }, KnockbackEffect.Directional((float)Deg(90), 20, 0.3f));
                scheduler.Schedule(0.1, () => actor.MoveForMechanic(new Vector2(22, 0), 1.5, 2, urgent: true));
                scheduler.Schedule(0.45, () => Assert(Vector2.Distance(actor.Pos, new Vector2(20, 0)) < 0.025,
                    "击退期间收到下一条移动指令，仍须先到击退落点，不能截断轨迹。"));

                scheduler.Schedule(1.6, () =>
                {
                    Assert(Vector2.Distance(actor.Pos, new Vector2(22, 0)) < 0.025 && !actor.IsMoving,
                        "暂停结束后必须从击退落点继续走向新的安全范围。");
                    finished.Set();
                });

                scheduler.Start();
                Assert(finished.Wait(3000) && failure == null, "击退后的机制移动检查失败：" + failure);
            }
        }

        public static void ForcedMovementSuspendsAndResumesBehavior()
        {
            Exception failure = null;

            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) =>
            {
                failure = error;
                finished.Set();
            }))
            using (var movement = new SimMovementController(scheduler))
            using (var casts = new SimCastController(scheduler))
            using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
            {
                var actor = CreateActor(behavior, movement, casts);
                actor.MoveForMechanic(new Vector2(8, 0), 3, 4, urgent: true);

                scheduler.Schedule(0.2, () =>
                {
                    actor.SuspendBehavior(0.8);
                    actor.Pos = new Vector2(20, 0);
                });

                scheduler.Schedule(0.8, () => Assert(actor.Pos == new Vector2(20, 0) && !actor.IsMoving, "暂停行为期间不能追赶旧目的地，覆盖强制移动。"));
                scheduler.Schedule(1.1, () => actor.MoveForMechanic(new Vector2(22, 0), 2.2, 3, urgent: true));

                scheduler.Schedule(2.3, () =>
                {
                    Assert(Vector2.Distance(actor.Pos, new Vector2(22, 0)) < 0.025, "暂停结束后应能继续处理新的移动指令。");
                    finished.Set();
                });

                scheduler.Start();
                Assert(finished.Wait(4000), "暂停行为检查超时。");
                Assert(failure == null, "暂停行为检查失败：" + failure);
            }
        }

        public static void EntityChoosesDepartureAndHonorsDeadlineAndUrgency()
        {
            Exception failure = null;

            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) =>
            {
                failure = error;
                finished.Set();
            }))
            using (var movement = new SimMovementController(scheduler))
            using (var casts = new SimCastController(scheduler))
            using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
            {
                var normal = CreateActor(behavior, movement, casts);
                var urgent = CreateActor(behavior, movement, casts);
                var immediate = CreateActor(behavior, movement, casts, delayArrival: false);
                urgent.ConfigureBehavior(new SimActorBehaviorOptions
                {
                    RouteAngleDegrees = 90
                });
                immediate.ConfigureBehavior(new SimActorBehaviorOptions
                {
                    DelayArrival = false,
                    RouteAngleDegrees = 90
                });
                var destination = new Vector2(3, 0);
                normal.MoveForMechanic(destination, 3, holdUntil: 4);
                urgent.MoveForMechanic(destination, 3, holdUntil: 4, urgent: true);
                immediate.MoveForMechanic(destination, 3, holdUntil: 4);

                scheduler.Schedule(1.4, () =>
                {
                    Assert(Vector2.Distance(urgent.Pos, destination) < 0.025 && !urgent.IsMoving,
                        "紧急意图应立即按正常跑速到达，不等到目标时间才行动。");
                    Assert(normal.Pos == Vector2.Zero && !normal.IsMoving,
                        "有余裕的普通意图应由实体安排稍后出发，不能收到指令就强制移动。");
                    Assert(Vector2.Distance(immediate.Pos, destination) < 0.025 && !immediate.IsMoving,
                        "关闭延迟到达后，即使不是紧急意图也应尽早出发并到达。");
                    Assert(urgent.Entity.NetworkMoves.TrueForAll(move => move.Position.Y == 0) &&
                        immediate.Entity.NetworkMoves.TrueForAll(move => move.Position.Y == 0),
                        "紧急意图或关闭延迟到达时，即使配置允许绕路也必须沿直线直接就位。");
                });

                scheduler.Schedule(3, () =>
                {
                    Assert(Vector2.Distance(normal.Pos, destination) < 0.025 && !normal.IsMoving,
                        "只给目标位置和时间，实体也应在截止前完成路线。");
                });

                scheduler.Schedule(3.3, () =>
                {
                    Assert(normal.Pos == destination && urgent.Pos == destination && immediate.Pos == destination,
                        "提前到达后必须保持站位，直到机制结束。");
                    finished.Set();
                });

                scheduler.Start();
                Assert(finished.Wait(6000), "实体自主安排移动的检查未完成。");
                scheduler.Dispose();
                Assert(failure == null, "实体自主移动失败：" + failure);
            }
        }

        public static void SmallCorrectionsReachTheRequestedRange()
        {
            Exception failure = null;

            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) =>
            {
                failure = error;
                finished.Set();
            }))
            using (var movement = new SimMovementController(scheduler))
            using (var casts = new SimCastController(scheduler))
            using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
            {
                var actor = CreateActor(behavior, movement, casts);
                var area = SimPosition.Ellipse(new Vector2(0.08f, 0), 0.01f);
                actor.MoveForMechanic(area, 1, urgent: true);

                scheduler.Schedule(0.6, () =>
                {
                    Assert(area.Contains(actor.Pos) && !actor.IsMoving && actor.Entity.NetworkMoves.Count > 0,
                        "离终点不到十厘米但仍在容差外时，也必须通过移动包完成修正。");
                    finished.Set();
                });

                scheduler.Start();
                Assert(finished.Wait(5000), "小距离机制移动的检查未完成。");
                scheduler.Dispose();
                Assert(failure == null, "小距离机制移动失败：" + failure);
            }
        }

        public static void ImmediateArrivalInterruptsAutomaticCasting()
        {
            var actions = GameDataService.Instance.GetSheet<ActionRow>();
            var hadOriginal = actions.TryGetValue(25859, out var original);
            actions[25859] = new ActionRow
            {
                RowId = 25859,
                Cast100ms = 50,
                Range = 25
            };

            try
            {
                Exception failure = null;

                using (var finished = new ManualResetEventSlim())
                using (var scheduler = new SimScheduler((error, _) =>
                {
                    failure = error;
                    finished.Set();
                }))
                using (var movement = new SimMovementController(scheduler))
                using (var casts = new SimCastController(scheduler))
                using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
                {
                    var world = new EntityTestWorld();
                    var actor = new SimActor(world.CreateCharacter(2), casts, movement);
                    actor.Entity.Job = Job.WHM;
                    actor.Entity.OnActorMove = (position, _, __) => actor.Entity.Pos3D = position;
                    behavior.Add(actor, new SimActorBehaviorOptions
                    {
                        DelayArrival = false,
                        IdleRadius = 0,
                        RouteAngleDegrees = 90
                    });
                    var boss = new SimActor(world.CreateCharacter(0), casts, movement);
                    boss.Entity.Pos = new Vector2(0, 5);
                    behavior.SetTarget(boss);

                    scheduler.Schedule(2.3, () =>
                    {
                        Assert(casts.Remaining(actor) > 0, "检查前假玩家应已经开始自动读条。");
                        actor.MoveForMechanic(new Vector2(3, 0), 5, holdUntil: 6);
                        Assert(casts.Remaining(actor) == 0 && actor.Entity.CastingActionId == 0,
                            "关闭延迟后应立即取消演出读条，不能等到滑步窗口才动。");
                    });

                    scheduler.Schedule(3.5, () =>
                    {
                        Assert(actor.Pos == new Vector2(3, 0) && !actor.IsMoving,
                            "法系收到立即就位的意图后应连续跑到目标，不受之前读条影响。");
                        finished.Set();
                    });

                    scheduler.Start();
                    Assert(finished.Wait(6000), "立即就位取消读条的检查未完成。");
                    scheduler.Dispose();
                    Assert(failure == null, "立即就位取消读条失败：" + failure);
                }
            }
            finally
            {
                if (!hadOriginal)
                    actions.Remove(25859);
                else
                    actions[25859] = original;
            }
        }

        public static void RoutesRespectAnglesAndScaleWithDistance()
        {
            var left = false;
            var right = false;

            foreach (var angle in new[]
            {
                1d,
                30d,
                90d
            }

            )
                foreach (var distance in new[]
                {
                    0.5f,
                    2f,
                    8f,
                    20f
                }

                )
                    for (var seed = 0; seed < 100; seed++)
                    {
                        var origin = new Vector2(31, -47);
                        var forward = Vector2.Normalize(new Vector2(3, 4));
                        var side = new Vector2(-forward.Y, forward.X);
                        var destination = origin + forward * distance;
                        var route = SimActorBehaviorController.CreateRoute(origin, destination, angle, new Random(seed));
                        Assert(route[route.Length - 1] == destination, "绕路不能改变最终安全点。");
                        var previous = origin;

                        foreach (var point in route)
                        {
                            var step = point - previous;
                            var ahead = Vector2.Dot(step, forward);
                            var sideways = Vector2.Dot(step, side);
                            var heading = ToDegrees(Math.Atan2(Math.Abs(sideways), ahead));
                            Assert(ahead >= -0.0001 && heading <= angle + 0.02,
                        "路线每一段都应向目标推进，且偏角不超过配置上限。");
                            Assert(Math.Abs(Vector2.Dot(point - origin, side)) <= distance * 0.3 * Math.Sin(Deg(angle)) + 0.0001,
                        "绕路偏移应随路程和角度缩小，不能让短距离移动绕出大圈。");
                            previous = point;
                        }

                        var firstSide = Vector2.Dot(route[0] - origin, side);
                        left |= firstSide < 0;
                        right |= firstSide > 0;
                    }

            Assert(left && right, "绕路应能随机向左右两侧偏转。");
            Assert(SimActorBehaviorController.CreateRoute(Vector2.Zero, new Vector2(20, 0), 0, new Random(1)).Length == 1,
                "关闭绕路时应直接前往终点。");
            Assert(SimActorBehaviorController.CreateRoute(Vector2.Zero, new Vector2(0.1f, 0), 30, new Random(1)).Length == 1,
                "很小的站位修正不应额外插入拐点。");
        }

        public static void IdleMeleeReturnsAfterTheHoldAndYieldsToNewMechanics()
        {
            Exception failure = null;

            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) =>
            {
                failure = error;
                finished.Set();
            }))
            using (var movement = new SimMovementController(scheduler))
            using (var casts = new SimCastController(scheduler))
            using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
            {
                var melee = CreateActor(behavior, movement, casts, job: Job.VPR, position: new Vector2(10, 0));
                var immediate = CreateActor(behavior, movement, casts, delayArrival: false, job: Job.DRK, position: new Vector2(10, 0));
                var retarget = CreateActor(behavior, movement, casts, job: Job.MNK, position: new Vector2(20, 0));
                var ranged = CreateActor(behavior, movement, casts, job: Job.BRD, position: new Vector2(10, 0));
                var disabled = CreateActor(behavior, movement, casts, job: Job.VPR, position: new Vector2(10, 0));
                disabled.ConfigureBehavior(new SimActorBehaviorOptions
                {
                    AttackTarget = false,
                    IdleRadius = 0
                });

                foreach (var actor in new[]
                {
                    melee,
                    immediate,
                    retarget,
                    ranged,
                    disabled
                }

                )
                    actor.MoveForMechanic(actor.Pos, 0.5, holdUntil: 1.5, urgent: true);

                var boss = new SimActor(new EntityTestWorld().CreateCharacter(0), casts, movement);
                boss.Entity.HitboxRadius = 2;
                behavior.SetTarget(boss);

                scheduler.Schedule(1.2, () =>
                {
                    Assert(melee.Pos.X == 10 && immediate.Pos.X == 10 && retarget.Pos.X == 20,
                        "机制判定要求保持站位时，近战不能提前离开远处的安全点。");
                });

                scheduler.Schedule(2, () =>
                {
                    Assert(retarget.IsMoving && retarget.Pos.X < 20, "机制结束后应主动返回近战，不等待空闲走动冷却。");
                    retarget.MoveForMechanic(new Vector2(22, 0), 3.7, holdUntil: 4.5, urgent: true);
                });

                scheduler.Schedule(3.8, () =>
                {
                    Assert(melee.Pos == new Vector2(4, 0) && immediate.Pos == new Vector2(4, 0),
                        "空闲近战应回到身位圈外两米，即使空闲半径为零或关闭延迟到达。");
                    Assert(retarget.Pos == new Vector2(22, 0) && !retarget.IsMoving,
                        "新的紧急站位应立即取代返回近战的路线。");
                    Assert(ranged.Pos.X == 10 && disabled.Pos.X == 10,
                        "远程与关闭攻击的角色不应主动贴近 Boss。");
                    finished.Set();
                });

                scheduler.Start();
                Assert(finished.Wait(6500), "机制结束返回近战的检查未完成。");
                scheduler.Dispose();
                Assert(failure == null, "机制结束返回近战失败：" + failure);
            }
        }

        public static void MeleeUsesTheGapBeforeTheNextMechanicWithoutMissingItsDeadline()
        {
            Exception failure = null;

            using (var finished = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) =>
            {
                failure = error;
                finished.Set();
            }))
            using (var movement = new SimMovementController(scheduler))
            using (var casts = new SimCastController(scheduler))
            using (var behavior = new SimActorBehaviorController(scheduler, movement, casts))
            {
                var normal = CreateActor(behavior, movement, casts, job: Job.VPR, position: new Vector2(10, 0));
                var tight = CreateActor(behavior, movement, casts, job: Job.VPR, position: new Vector2(10, 0));
                var urgent = CreateActor(behavior, movement, casts, job: Job.VPR, position: new Vector2(10, 0));
                var immediate = CreateActor(behavior, movement, casts, delayArrival: false, job: Job.VPR, position: new Vector2(10, 0));

                foreach (var actor in new[]
                {
                    normal,
                    tight,
                    urgent,
                    immediate
                }

                )
                    actor.MoveForMechanic(actor.Pos, 0.5, holdUntil: 1, urgent: true);

                var boss = new SimActor(new EntityTestWorld().CreateCharacter(0), casts, movement);
                boss.Entity.HitboxRadius = 2;
                behavior.SetTarget(boss);

                scheduler.Schedule(1, () =>
                {
                    normal.MoveForMechanic(new Vector2(10, 0), 6, holdUntil: 7);
                    tight.MoveForMechanic(new Vector2(12, 0), 3, holdUntil: 7);
                    urgent.MoveForMechanic(new Vector2(12, 0), 6, holdUntil: 7, urgent: true);
                    immediate.MoveForMechanic(new Vector2(12, 0), 6, holdUntil: 7);
                });

                scheduler.Schedule(3.7, () =>
                {
                    Assert(normal.Pos == new Vector2(4, 0) && !normal.IsMoving,
                        "下一次仍使用原来的远处站位时，应在随机反应时间后回到近战输出。");

                    foreach (var actor in new[]
                    {
                        tight,
                        urgent,
                        immediate
                    }

                    )
                        Assert(actor.Pos == new Vector2(12, 0) && actor.Entity.NetworkMoves.TrueForAll(move => move.Position.X >= 10),
                            "时间紧张、紧急移动或关闭延迟到达时不能绕回近战。");
                });

                scheduler.Schedule(6.2, () =>
                {
                    Assert(normal.Pos == new Vector2(10, 0) && !normal.IsMoving,
                        "返回近战输出后仍应按时回到原定机制站位，并保持到判定结束。");
                    finished.Set();
                });

                scheduler.Start();
                Assert(finished.Wait(9000), "机制间隔返回近战的检查未完成。");
                scheduler.Dispose();
                Assert(failure == null, "机制间隔返回近战失败：" + failure);
            }
        }

        private static SimActor CreateActor(SimActorBehaviorController behavior, SimMovementController movement, SimCastController casts,
            bool delayArrival = true, Job job = Job.ADV, Vector2 position = default(Vector2))
        {
            var actor = new SimActor(new EntityTestWorld().CreateCharacter(2), casts, movement);
            actor.Entity.Job = job;
            actor.Entity.Pos = position;
            // 测试适配器模拟客户端收到 ActorMove 后的位置更新。
            actor.Entity.OnActorMove = (next, _, __) => actor.Entity.Pos3D = next;
            behavior.Add(actor, new SimActorBehaviorOptions
            {
                DelayArrival = delayArrival,
                RouteAngleDegrees = 0,
                IdleRadius = 0
            });
            return actor;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }
    }
}
