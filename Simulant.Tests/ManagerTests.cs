using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Game;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using ActionRow = Lumina.Excel.Sheets.Action;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Tests
{
    internal static class ManagerTests
    {
        public static void BossFollowsLiveEnmityWithNetworkMovementAndPausesForCasts()
        {
            Exception failure = null;
            using (var scheduler = new SimScheduler((error, name) => failure = error))
            using (var finished = new ManualResetEventSlim())
            {
                var spawner = new EntitySpawner { NetworkReady = true };
                var manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                var player = manager.Attach(spawner.World.CreateCharacter(0));
                var other = manager.SpawnBNpc(2);
                manager.Party.Add(player);
                player.Pos = new Vector2(14, 0);
                player.Entity.HitboxRadius = 0.5f;

                manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec { BNpcBaseId = 17823 }, boss =>
                {
                    boss.Entity.HitboxRadius = 2;
                    boss.Entity.OnActorMove = (position, heading, speed) =>
                    {
                        boss.Entity.Pos3D = position;
                        boss.Entity.Heading = heading;
                    };

                    Assert(boss.FollowTopEnmity == EnmityFollowMode.Disabled, "普通网络 NPC 默认不开启追踪。");
                    manager.SetPartyEnmity(boss, 100);
                    boss.FollowTopEnmity = EnmityFollowMode.FacingOnly;
                    scheduler.ScheduleAfter(0.24, () =>
                    {
                        Assert(boss.Pos == Vector2.Zero && Math.Abs(boss.Heading - Math.PI / 2) < 0.01 &&
                            boss.Entity.NetworkMoves.Count > 0, "仅转向模式应通过 ActorMove 朝玩家转身并保持位置。");
                        boss.FollowTopEnmity = EnmityFollowMode.MoveAndFace;
                    });

                    Vector2 pulled = default;
                    var packets = 0;
                    scheduler.ScheduleAfter(1.65, () =>
                    {
                        pulled = boss.Pos;
                        Assert(Vector2.Distance(pulled, new Vector2(9.5f, 0)) < 0.1f &&
                            boss.Entity.NetworkMoves.Count(move => move.Position.X > 0 && move.Position.X < pulled.X) >= 2,
                            "Boss 应分段追到距一仇中心 4.5 米的位置。");
                        boss.Entity.SetCastInfo(40203, 1);
                        player.Pos = new Vector2(-15, 0);
                        packets = boss.Entity.NetworkMoves.Count;
                    });
                    scheduler.ScheduleAfter(1.95, () =>
                    {
                        Assert(boss.Pos == pulled && boss.Entity.NetworkMoves.Count == packets, "跟随应在读条期间暂停。");
                        boss.Entity.CastingActionId = 0;
                        other.Pos = pulled + new Vector2(0, 14);
                        manager.Party.Add(other);
                        manager.ShowParty(new Simulant.Core.Party.SimPartyService());
                        manager.SetPartyEnmity(boss, 30, 100);
                        boss.FollowTopEnmity = EnmityFollowMode.FacingOnly;
                    });
                    scheduler.ScheduleAfter(2.25, () =>
                    {
                        Assert(boss.Pos == pulled && Math.Abs(boss.Heading) < 0.01,
                            "同一个 Boss 应在换仇后原地改朝新的一仇。");
                        boss.FollowTopEnmity = EnmityFollowMode.Disabled;
                        other.Pos = pulled - new Vector2(14, 0);
                        packets = boss.Entity.NetworkMoves.Count;
                    });
                    scheduler.ScheduleAfter(2.5, () =>
                    {
                        Assert(boss.Entity.NetworkMoves.Count == packets, "关闭跟随后不再自动发送 ActorMove。");
                        finished.Set();
                    });
                });

                scheduler.Start();
                Assert(finished.Wait(5000), "Boss 跟随验证未完成：" + failure);
                manager.Clear();
                Assert(scheduler.PendingCount == 0, "清理应停止跟随及其行为节点。");
            }
        }

        public static void EnemyListMembershipIsIndependentOfTargetabilityAndCasting()
        {
            Exception failure = null;
            using (var scheduler = new SimScheduler((error, name) => failure = error))
            using (var published = new ManualResetEventSlim())
            {
                var spawner = new EntitySpawner { NetworkReady = true };
                var list = new SimEnemyList(null);
                var manager = new SimEntityManager(spawner) { Scheduler = scheduler, EnemyList = list };
                var self = spawner.World.CreateCharacter(0);
                manager.Attach(self);
                var boss = manager.SpawnBNpc(17823);
                SimActor hourglass = null;
                SimActor helper = null;
                manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec
                {
                    BNpcBaseId = 17837,
                    Targetable = false,
                    EnemyListVisibility = true
                }, actor =>
                {
                    hourglass = actor;
                    actor.Entity.IsTargetable = false;
                });
                manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec
                {
                    BNpcBaseId = 9020,
                    Targetable = false,
                    EnemyListVisibility = false
                }, actor =>
                {
                    helper = actor;
                    actor.Entity.IsTargetable = false;
                    actor.Entity.SetCastInfo(40299, 5);
                });

                var publications = 0;
                list.OnRefresh = (playerId, enemies) =>
                {
                    Assert(playerId == self.Id && hourglass != null && helper != null,
                        "使用自身 ID 发布列表，并等待网络实体就绪。");
                    switch (++publications)
                    {
                        case 1:
                            Assert(enemies.Select(enemy => enemy.EntityId).SequenceEqual(new[] { boss.Id, hourglass.Id }),
                                "沙漏虽不可选中且尚未读条，仍应按机制入列。正在读条的辅助实体不入列。");
                            boss.Entity.IsTargetable = false;
                            helper.Entity.IsTargetable = true;
                            manager.SetEnemyListVisibility(hourglass, false);
                            break;
                        case 2:
                            Assert(enemies.Select(enemy => enemy.EntityId).SequenceEqual(new[] { boss.Id }),
                                "不可选中的首领应保留，显式退场的沙漏和显式隐藏的可选中实体应移出。");
                            boss.Entity.IsTargetable = true;
                            manager.SetEnemyListVisibility(hourglass, true);
                            break;
                        case 3:
                            Assert(enemies.Select(enemy => enemy.EntityId).SequenceEqual(new[] { boss.Id, hourglass.Id }),
                                "首领恢复可选中后沿用原条目，沙漏可再次入列。");
                            published.Set();
                            break;
                    }
                };

                scheduler.Start();
                Assert(published.Wait(5000), "敌对列表未完成预期的生命周期：" + failure);
                manager.Clear();
                Assert(list.RestoreCount == 1 && scheduler.PendingCount == 0, "清理必须恢复原列表并取消轮询。");
            }
        }

        public static void EventObjectAnimationWritesActorControlExtraWhenEnabled()
        {
            using (var scheduler = new SimScheduler())
            {
                var enabled = false;
                var logs = new List<(int Id, string Payload)>();
                var manager = new SimEntityManager(new EntitySpawner())
                {
                    Scheduler = scheduler,
                    CombatLog = new SimCombatLog(() => enabled, (id, payload, time) => logs.Add((id, payload)), null)
                };
                var entity = manager.SpawnEObj(0, 2014199, Vector3.Zero);

                manager.PlayEObjAnimation(entity, 1, 2);
                Assert(entity.LastActorControl == (ActorControlCategory.EObjAnimation, 1u, 2u, 0u, 0u) && logs.Count == 0,
                    "关闭日志选项时仍应播放 EObj 动画。");

                enabled = true;
                manager.PlayEObjAnimation(entity, 1, 2);
                manager.PlayEObjAnimation(entity, 0x20, 0x10);
                manager.PlayEObjAnimation(entity, 0, 8);

                Assert(entity.LastActorControl == (ActorControlCategory.EObjAnimation, 0u, 8u, 0u, 0u) && logs.SequenceEqual(new[]
                {
                    (273, $"{entity.Id:X8}|019D|1|2|0|0"),
                    (273, $"{entity.Id:X8}|019D|20|10|0|0"),
                    (273, $"{entity.Id:X8}|019D|0|8|0|0")
                }), "每次播放 EObj 动画应以同一实体 ID 及参数写入一条 273 日志。");
                manager.Clear();
            }
        }

        public static void ManagerClearsOwnedEntitiesAndOnlyDetachesBorrowedActors()
        {
            var spawner = new EntitySpawner();

            using (var scheduler = new SimScheduler())
            {
                var manager = new SimEntityManager(spawner)
                {
                    Scheduler = scheduler
                };
                var borrowedEntity = spawner.World.CreateCharacter(0);
                var borrowed = manager.Attach(borrowedEntity);
                Assert(ReferenceEquals(borrowed, manager.Attach(borrowedEntity)), "Attach 必须复用模拟角色。");
                var owned = manager.SpawnBNpc(123);
                var ornament = manager.SpawnEObj(new EventObjectSpawnSpec
                {
                    Index = 2
                });
                borrowed.MoveTo(new Vector2(10, 0));
                owned.MoveTo(new Vector2(0, 10));
                manager.Delete(borrowed);
                Assert(borrowed.IsActive && spawner.Deleted.Count == 0, "Delete 不得接管借用的实体。");
                manager.Clear();
                Assert(!borrowed.IsActive && !owned.IsActive && scheduler.PendingCount == 0,
                    "Clear 必须停止所有模拟角色并取消共享移动更新节点。");
                Assert(borrowedEntity.IsAlive && !owned.Entity.IsAlive && !ornament.IsAlive,
                    "只能释放本会话创建的实体。");
                Assert(spawner.Deleted.Count == 2, "每个自有实体必须恰好释放一次。");
                manager.Delete(owned);
                manager.Clear();
                Assert(spawner.Deleted.Count == 2, "延迟回调和重复清理不得产生副作用。");
            }
        }

        public static void ManagerRejectsCreationOutsideItsSessionAndSupportsRestart()
        {
            var spawner = new EntitySpawner();
            var manager = new SimEntityManager(spawner);
            Throws<InvalidOperationException>(() => manager.SpawnBNpc(123));
            Assert(spawner.SpawnCount == 0, "原生创建前必须拒绝无效的会话状态。");

            using (var first = new SimScheduler())
            using (var second = new SimScheduler())
            {
                manager.Scheduler = first;
                var old = manager.SpawnBNpc(123);
                manager.Detach(old);
                Assert(!old.IsActive && old.Entity.IsAlive, "Detach 停止行为后必须保留实体所有权。");
                manager.Clear();
                Throws<InvalidOperationException>(() => manager.SpawnBNpc(123));
                Throws<InvalidOperationException>(() => manager.SpawnEObj(new EventObjectSpawnSpec
                {
                    Index = 2
                }));
                manager.Scheduler = second;
                var current = manager.SpawnBNpc(456);
                Assert(current.IsActive && current.Entity.Identity != old.Entity.Identity,
                    "重启必须创建具有新生命周期的模拟角色。");
                manager.Clear();
            }
        }

        public static void ManagerCleanupContinuesAfterReleaseAndLoggingFailures()
        {
            var spawner = new EntitySpawner();
            var errors = 0;

            using (var scheduler = new SimScheduler())
            {
                var manager = new SimEntityManager(spawner, _ =>
                {
                    errors++;
                    throw new Exception("日志记录失败");
                })
                {
                    Scheduler = scheduler
                };
                var first = manager.SpawnBNpc(1);
                var second = manager.SpawnBNpc(2);
                first.MoveTo(new Vector2(100, 0));
                second.MoveTo(new Vector2(0, 100));

                spawner.BeforeDelete = entity =>
                {
                    if (ReferenceEquals(entity, first.Entity))
                        throw new Exception("原生释放失败");
                };

                manager.Clear();
                Assert(errors == 1 && !first.IsActive && !second.IsActive && !second.Entity.IsAlive,
                    "一次释放或日志记录失败，不得妨碍停止并释放其余模拟角色。");
                Assert(scheduler.PendingCount == 0, "清理失败后不得遗留排队的移动更新。");
            }
        }

        public static void ManagerRetriesFailedOwnedCleanupBeforeRestart()
        {
            var spawner = new EntitySpawner();
            var errors = 0;
            var failedAttempts = 0;

            using (var firstScheduler = new SimScheduler())
            using (var secondScheduler = new SimScheduler())
            {
                var manager = new SimEntityManager(spawner, _ => errors++)
                {
                    Scheduler = firstScheduler
                };

                try
                {
                    var failed = manager.SpawnBNpc(1);
                    var successful = manager.SpawnBNpc(2);

                    spawner.BeforeDelete = entity =>
                    {
                        if (ReferenceEquals(entity, failed.Entity) && ++failedAttempts == 1)
                            throw new InvalidOperationException("首次删除失败");
                    };

                    manager.Clear();
                    Assert(errors == 1 && !failed.IsActive && !successful.IsActive && failed.Entity.IsAlive &&
                        !successful.Entity.IsAlive, "清理失败必须停止所有行为，同时继续回收其余实体。");
                    Assert(manager.HasPendingCleanup, "失败的实体回收记录必须对会话持有者保持可见。");
                    Throws<InvalidOperationException>(() => manager.Scheduler = secondScheduler);
                    Throws<InvalidOperationException>(() => manager.SpawnBNpc(3));
                    Assert(ReferenceEquals(manager.Scheduler, firstScheduler), "尚有失败清理记录时不得切换到新会话。");
                    manager.Clear();
                    Assert(errors == 1 && !failed.Entity.IsAlive && failedAttempts == 2 && spawner.Deleted.Count == 2,
                        "再次清理必须重试失败实体，不能在第一次失败时丢失其所有权。");
                    Assert(!manager.HasPendingCleanup, "重试完成后必须解除剩余清理状态。");
                    Assert(spawner.DeleteAttempts.Count(entity => ReferenceEquals(entity, successful.Entity)) == 1,
                        "重试失败实体时不能再次调用已成功实体的删除入口。");
                    manager.Clear();
                    Assert(spawner.DeleteAttempts.Count == 3, "全部清理成功后的重复清理不得再次触碰原生实体。");
                    manager.Scheduler = secondScheduler;
                    var current = manager.SpawnBNpc(3);
                    Assert(current.IsActive, "失败记录清空后必须允许新会话创建角色。");
                }
                finally
                {
                    spawner.BeforeDelete = null;
                    manager.Clear();
                }
            }
        }

        public static void ManagerBlocksRestartDuringCleanupAndPreservesNewControllers()
        {
            var spawner = new EntitySpawner();
            Exception cleanupFailure = null;
            Exception restartFailure = null;
            Exception schedulerFailure = null;

            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var cleared = new ManualResetEventSlim())
            using (var restartAttempted = new ManualResetEventSlim())
            using (var arrived = new ManualResetEventSlim())
            using (var firstScheduler = new SimScheduler())
            using (var secondScheduler = new SimScheduler((error, _) =>
            {
                schedulerFailure = error;
                arrived.Set();
            }))
            {
                var manager = new SimEntityManager(spawner, error => cleanupFailure = error)
                {
                    Scheduler = firstScheduler
                };
                Thread cleaner = null;
                Thread restarter = null;

                try
                {
                    var old = manager.SpawnBNpc(1);

                    spawner.BeforeDelete = entity =>
                    {
                        if (!ReferenceEquals(entity, old.Entity))
                            return;

                        entered.Set();
                        Assert(release.Wait(5000), "清理线程未能释放删除屏障。");
                    };

                    cleaner = new Thread(() =>
                    {
                        try
                        {
                            manager.Clear();
                        }
                        catch (Exception error)
                        {
                            cleanupFailure = error;
                        }
                        finally
                        {
                            cleared.Set();
                        }
                    })
                    {
                        IsBackground = true
                    };

                    cleaner.Start();
                    Assert(entered.Wait(5000), "旧会话未进入原生删除屏障。");
                    Assert(manager.HasPendingCleanup, "原生回收仍在进行时不能报告清理已经完成。");

                    restarter = new Thread(() =>
                    {
                        try
                        {
                            manager.Scheduler = secondScheduler;
                        }
                        catch (Exception error)
                        {
                            restartFailure = error;
                        }
                        finally
                        {
                            restartAttempted.Set();
                        }
                    })
                    {
                        IsBackground = true
                    };

                    restarter.Start();
                    Assert(restartAttempted.Wait(5000) && restartFailure is InvalidOperationException,
                        "旧清理仍在执行时必须拒绝重启，不能先换入新控制器再由旧清理释放。");
                    Assert(!cleared.IsSet && ReferenceEquals(manager.Scheduler, firstScheduler),
                        "被屏障暂停的清理期间只能保留旧调度器。");
                    release.Set();
                    Assert(cleared.Wait(5000) && cleaner.Join(5000) && restarter.Join(5000), "清理或重启测试线程未结束。");
                    Assert(cleanupFailure == null && !old.Entity.IsAlive, "旧会话清理失败：" + cleanupFailure);
                    manager.Scheduler = secondScheduler;
                    var current = manager.SpawnBNpc(2);
                    current.MoveTo(new Vector2(1, 0), 100, onArrived: arrived.Set);
                    secondScheduler.Start();
                    Assert(arrived.Wait(5000), "清理结束后的新移动控制器没有完成移动。");
                    secondScheduler.Dispose();
                    Assert(schedulerFailure == null && current.IsActive && current.Pos == new Vector2(1, 0),
                        "旧清理不得释放或污染后续会话的控制器：" + schedulerFailure);
                }
                finally
                {
                    release.Set();
                    cleaner?.Join(5000);
                    restarter?.Join(5000);
                    secondScheduler.Dispose();
                    spawner.BeforeDelete = null;
                    manager.Clear();
                }
            }
        }

        public static void ManagerRetriesUndeliveredLocalCreationAndBlocksRestart()
        {
            var spawnAttempts = new Action<SimEntityManager>[]
            {
                manager => manager.SpawnBNpc(1),
                manager => manager.SpawnPlayer(Job.PLD)
            };

            foreach (var spawn in spawnAttempts)
            {
                var spawner = new EntitySpawner();
                var errors = 0;
                var failRetry = true;

                using (var first = new SimScheduler())
                using (var second = new SimScheduler())
                {
                    var manager = new SimEntityManager(spawner, _ => errors++)
                    {
                        Scheduler = first
                    };

                    try
                    {
                        spawner.NextUnreleasedCreationFailure = new InvalidOperationException("模拟本地创建和回滚均失败。");
                        Throws<InvalidOperationException>(() => spawn(manager));
                        Assert(spawner.SpawnCount == 1 && manager.HasPendingCleanup &&
                            spawner.HasPendingCreationCleanup && spawner.DeleteAttempts.Count == 0,
                            "首次创建尚未交付实体就抛错时，管理器仍必须暴露遗留清理责任。");
                        Throws<InvalidOperationException>(() => manager.Scheduler = second);
                        Assert(ReferenceEquals(manager.Scheduler, first), "不能绕过未交付实体的失败记录更换调度器。");

                        spawner.BeforeRetryFailedCreations = () =>
                        {
                            if (failRetry)
                                throw new InvalidOperationException("模拟未交付实体暂时无法回收。");
                        };

                        manager.Clear();
                        Assert(errors == 1 && manager.HasPendingCleanup && spawner.FailedCreationCleanupAttempts == 1,
                            "没有已交付实体的 Clear 也必须重试底层遗留资源，失败后保留清理状态。");
                        Throws<InvalidOperationException>(() => manager.Scheduler = second);
                        Throws<InvalidOperationException>(() => manager.SpawnBNpc(2));
                        Throws<InvalidOperationException>(() => manager.SpawnPlayer(Job.PLD));
                        Throws<InvalidOperationException>(() => manager.SpawnEObj(new EventObjectSpawnSpec
                        {
                            Index = 2
                        }));
                        Assert(spawner.SpawnCount == 1, "停止并保留创建清理期间，各生成入口都不能再次调用底层创建。");

                        failRetry = false;
                        manager.Clear();
                        Assert(!manager.HasPendingCleanup && !spawner.HasPendingCreationCleanup &&
                            spawner.FailedCreationCleanupAttempts == 2 && errors == 1,
                            "重试成功后应解除创建失败产生的重启限制。");
                        manager.Clear();
                        Assert(spawner.FailedCreationCleanupAttempts == 2, "清空后的重复停止不能重复回收未交付资源。");
                        manager.Scheduler = second;
                        Assert(manager.SpawnBNpc(3).IsActive, "完成未交付资源清理后应允许新一轮创建。");
                    }
                    finally
                    {
                        spawner.BeforeRetryFailedCreations = null;
                        manager.Clear();
                    }
                }
            }
        }

        public static void ManagerCreationCleanupPreservesOtherManagersEntities()
        {
            var spawner = new EntitySpawner();

            using (var firstScheduler = new SimScheduler())
            using (var secondScheduler = new SimScheduler())
            {
                var failedManager = new SimEntityManager(spawner)
                {
                    Scheduler = firstScheduler
                };
                var otherManager = new SimEntityManager(spawner)
                {
                    Scheduler = secondScheduler
                };

                try
                {
                    var other = otherManager.SpawnBNpc(1);
                    other.MoveTo(new Vector2(10, 0));
                    spawner.NextUnreleasedCreationFailure = new InvalidOperationException("模拟另一管理器创建回滚失败。");
                    Throws<InvalidOperationException>(() => failedManager.SpawnBNpc(2));
                    failedManager.Clear();
                    Assert(!failedManager.HasPendingCleanup && spawner.FailedCreationCleanupAttempts == 1,
                        "失败管理器应能够回收尚未交付的创建遗留资源。");
                    Assert(other.IsActive && other.Entity.IsAlive && other.IsMoving && secondScheduler.PendingCount == 1 &&
                        spawner.DeleteAttempts.Count == 0,
                        "共享生成器的失败清理不能删除另一管理器的已交付实体或中断其移动。");
                    otherManager.Clear();
                    Assert(spawner.Deleted.Count == 1 && ReferenceEquals(spawner.Deleted[0], other.Entity),
                        "已交付实体仍应只由所属管理器清理。");
                }
                finally
                {
                    failedManager.Clear();
                    otherManager.Clear();
                }
            }
        }

        public static void ManagerWorldResetReleasesUndeliveredCreationBarrier()
        {
            var spawner = new EntitySpawner();

            using (var first = new SimScheduler())
            using (var second = new SimScheduler())
            {
                var manager = new SimEntityManager(spawner)
                {
                    Scheduler = first
                };

                try
                {
                    spawner.NextUnreleasedCreationFailure = new InvalidOperationException("模拟旧世界创建回滚失败。");
                    Throws<InvalidOperationException>(() => manager.SpawnBNpc(1));

                    spawner.BeforeRetryFailedCreations = () =>
                    {
                        throw new InvalidOperationException("旧世界暂时无法清理。");
                    };

                    manager.Clear();
                    Assert(manager.HasPendingCleanup && spawner.FailedCreationCleanupAttempts == 1,
                        "换图前应保留尚未完成的旧世界创建清理。");
                    spawner.ResetWorld();
                    Assert(!spawner.HasPendingCreationCleanup && !manager.HasPendingCleanup,
                        "世界失效后管理器应立即解除旧世界未交付资源的清理限制。");
                    manager.Clear();
                    Assert(spawner.FailedCreationCleanupAttempts == 1 && spawner.DeleteAttempts.Count == 0,
                        "换图后的清理不能再次访问旧世界遗留资源。");
                    manager.Scheduler = second;
                    Assert(manager.SpawnBNpc(2).IsActive, "旧世界失效后应允许绑定新调度器并创建新实体。");
                }
                finally
                {
                    spawner.BeforeRetryFailedCreations = null;
                    manager.Clear();
                }
            }
        }

        public static void ManagerDeletingOneActorPreservesOtherMovementAndCast()
        {
            VfxManager.Reset();
            GameDataService.Instance.GetSheet<ActionRow>().Clear();
            AddCombinedAction(1, 0.4f, true);
            AddCombinedAction(2, 0.4f, true);
            var spawner = new EntitySpawner();
            Exception failure = null;

            using (var continued = new ManualResetEventSlim())
            using (var completed = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => failure = error))
            {
                var manager = new SimEntityManager(spawner)
                {
                    Scheduler = scheduler
                };

                try
                {
                    var first = manager.SpawnBNpc(1);
                    var second = manager.SpawnBNpc(2);
                    var deleted = false;
                    var firstWritesAtDelete = 0;

                    second.Entity.OnPositionWritten = _ =>
                    {
                        if (deleted)
                            continued.Set();
                    };

                    first.MoveTo(new Vector2(1000, 0), 10);
                    second.MoveTo(new Vector2(0, 1000), 10);
                    first.Cast(1);
                    second.Cast(2);
                    var shownTags = VfxManager.Commands.Select(OmenTag).ToArray();
                    Assert(scheduler.PendingCount == 5,
                        "两次读条应各有一个读条完成节点和一个结算节点，两个角色共用一个移动更新节点。");

                    scheduler.Schedule(0.04, () =>
                    {
                        manager.Delete(first);
                        firstWritesAtDelete = first.Entity.PositionWrites.Count;
                        deleted = true;
                        Assert(second.IsActive && second.IsMoving && second.Entity.CastingActionId == 2,
                            "删除一个角色不得停止另一角色的移动或读条。");
                        Assert(VfxManager.Commands.Where(IsOmenRemoval).Select(OmenTag)
                            .SequenceEqual(new[]
                        {
                            shownTags[0]
                        }), "删除时只能移除该角色自己的预兆。");
                    }, "删除组合测试中的首个角色");

                    scheduler.Schedule(0.5, completed.Set, "等待保留角色完成读条");
                    scheduler.Start();
                    Assert(continued.Wait(5000), "删除首个角色后，另一角色未继续移动：" + failure);
                    Assert(completed.Wait(5000), "保留角色的读条未进入完成阶段：" + failure);
                    scheduler.Dispose();
                    Assert(failure == null, "组合行为执行失败：" + failure);
                    Assert(!first.IsActive && first.Entity.PositionWrites.Count == firstWritesAtDelete &&
                        first.Entity.PlayedTimelines.Count == 0, "被删除角色不得继续移动或播放读条完成动作。");
                    Assert(second.IsActive && second.Entity.PlayedTimelines.SequenceEqual(new ushort[]
                    {
                        102
                    }),
                        "保留角色必须独立完成读条并恰好播放一次完成动作。");
                    Assert(VfxManager.Commands.Where(IsOmenRemoval).Select(OmenTag).SequenceEqual(shownTags),
                        "两个角色的预兆必须分别在删除和自然完成时移除。");
                    manager.Clear();
                    Assert(spawner.Deleted.Count == 2 && !second.IsActive,
                        "最终清理必须恰好回收两个自有实体。");
                }
                finally
                {
                    scheduler.Dispose();
                    manager.Clear();
                    VfxManager.Reset();
                }
            }
        }

        public static void ManagerStopAndRestartDiscardAllPreviousBehaviors()
        {
            VfxManager.Reset();
            GameDataService.Instance.GetSheet<ActionRow>().Clear();
            AddCombinedAction(1, 0.4f, true);
            AddCombinedAction(2, 0.04f, false);
            var spawner = new EntitySpawner();
            Exception failure = null;

            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var restarted = new ManualResetEventSlim())
            using (var firstScheduler = new SimScheduler((error, _) => failure = error))
            using (var secondScheduler = new SimScheduler((error, _) => failure = error))
            {
                var manager = new SimEntityManager(spawner)
                {
                    Scheduler = firstScheduler
                };

                try
                {
                    var visible = manager.SpawnBNpc(1);
                    var delayed = manager.SpawnBNpc(2);
                    visible.Cast(1);
                    delayed.Cast(1, omenDelay: 0.05f);
                    visible.MoveTo(new Vector2(1000, 0), 10);
                    delayed.MoveTo(new Vector2(0, 1000), 10);

                    // 在首个移动更新内部暂停，保证停止发生在延迟预兆执行之前。
                    delayed.Entity.OnPositionWritten = _ =>
                    {
                        entered.Set();
                        Assert(release.Wait(5000), "未解除组合测试的移动同步点。");
                    };

                    Assert(firstScheduler.PendingCount == 6,
                        "停止前应存在两个读条完成节点、两个结算节点、一个延迟预兆和一个共享移动更新。");
                    firstScheduler.Start();

                    try
                    {
                        Assert(entered.Wait(5000), "组合测试未进入移动回调：" + failure);
                        firstScheduler.Cancel();
                    }
                    finally
                    {
                        release.Set();
                    }

                    firstScheduler.Dispose();
                    manager.Clear();
                    Assert(failure == null && firstScheduler.PendingCount == 0,
                        "主动停止不得产生执行错误或遗留调度节点：" + failure);
                    Assert(!visible.IsActive && !delayed.IsActive && !visible.IsMoving && !delayed.IsMoving &&
                        visible.Entity.CastingActionId == 0 && delayed.Entity.CastingActionId == 0,
                        "停止后必须解除所有角色并清除其读条和移动状态。");
                    var commands = VfxManager.Commands.ToArray();
                    Assert(commands.Length == 2 && !IsOmenRemoval(commands[0]) && IsOmenRemoval(commands[1]) &&
                        OmenTag(commands[0]) == OmenTag(commands[1]),
                        "已显示的预兆必须清理，尚未显示的延迟预兆不得执行。");
                    var visibleWrites = visible.Entity.PositionWrites.Count;
                    var delayedWrites = delayed.Entity.PositionWrites.Count;

                    manager.Scheduler = secondScheduler;
                    var current = manager.SpawnBNpc(3);
                    current.MoveTo(new Vector2(1, 0), 100);
                    current.Cast(2);
                    secondScheduler.Schedule(0.5, restarted.Set, "等待超过旧行为的截止时间");
                    secondScheduler.Start();
                    Assert(restarted.Wait(5000), "重启后的组合测试未完成：" + failure);
                    secondScheduler.Dispose();
                    Assert(failure == null && current.IsActive && current.Entity.Pos == new Vector2(1, 0) &&
                        current.Entity.PlayedTimelines.SequenceEqual(new ushort[]
                    {
                        102
                    }),
                        "重启后的角色必须能独立完成移动与读条：" + failure);
                    Assert(visible.Entity.PositionWrites.Count == visibleWrites && delayed.Entity.PositionWrites.Count == delayedWrites &&
                        visible.Entity.PlayedTimelines.Count == 0 && delayed.Entity.PlayedTimelines.Count == 0 &&
                        VfxManager.Commands.SequenceEqual(commands),
                        "重启后不得触发旧移动写入、读条完成或延迟预兆。");
                    manager.Clear();
                    Assert(spawner.Deleted.Count == 3, "两次运行创建的三个实体必须各回收一次。");
                }
                finally
                {
                    release.Set();
                    firstScheduler.Dispose();
                    secondScheduler.Dispose();
                    manager.Clear();
                    VfxManager.Reset();
                }
            }
        }

        public static void ManagerRestoresPartyBeforeDeletingItsMembersAndRetriesFailure()
        {
            var spawner = new EntitySpawner();
            var party = new Simulant.Core.Party.SimPartyService();
            var errors = 0;

            using (var firstScheduler = new SimScheduler())
            using (var secondScheduler = new SimScheduler())
            {
                var manager = new SimEntityManager(spawner, _ => errors++)
                {
                    Scheduler = firstScheduler
                };
                var first = manager.SpawnBNpc(1);
                var second = manager.SpawnBNpc(2);
                manager.Party.Add(first);
                manager.Party.Add(second);
                manager.ShowParty(party);
                Assert(party.IsOwnedBy(manager) && party.ShowCount == 1 && party.Members.SequenceEqual(new[]
                {
                    first, second
                }),
                    "展示小队必须以当前会话作为所有者，并保留小队顺序。");

                party.BeforeRestore = () =>
                {
                    throw new InvalidOperationException("模拟原生小队恢复失败。");
                };

                try
                {
                    manager.Clear();
                    Assert(errors == 1 && party.IsOwnedBy(manager) && manager.HasPendingCleanup,
                        "原生小队恢复失败必须保留所有者和会话清理责任。");
                    Assert(first.Entity.IsAlive && second.Entity.IsAlive && spawner.DeleteAttempts.Count == 0,
                        "名单尚未恢复时不能先删除仍被小队引用的模拟实体。");
                    Throws<InvalidOperationException>(() => manager.Scheduler = secondScheduler);
                    Throws<InvalidOperationException>(() => manager.SpawnBNpc(3));

                    party.BeforeRestore = () =>
                    {
                        Assert(first.Entity.IsAlive && second.Entity.IsAlive && spawner.DeleteAttempts.Count == 0,
                            "恢复重试必须仍发生在任何成员删除之前。");
                    };

                    manager.Clear();
                    Assert(!party.IsOwnedBy(manager) && !manager.HasPendingCleanup && party.RestoreAttempts == 2 &&
                        !first.Entity.IsAlive && !second.Entity.IsAlive && spawner.Deleted.Count == 2,
                        "恢复成功后才可释放小队实体并解除重启限制。");
                    manager.Clear();
                    Assert(party.RestoreAttempts == 2 && spawner.DeleteAttempts.Count == 2,
                        "成功恢复和删除的资源不应在重复清理时再次触碰。");
                    manager.Scheduler = secondScheduler;
                    manager.Clear();
                }
                finally
                {
                    party.BeforeRestore = null;
                    manager.Clear();
                }
            }
        }

        public static void ManagerUsesOneEnmityRefreshAndCancelsOnClear()
        {
            var spawner = new EntitySpawner();
            var party = new Simulant.Core.Party.SimPartyService();
            Exception failure = null;

            using (var refreshed = new ManualResetEventSlim())
            using (var observedAfterClear = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) =>
            {
                failure = error;
                observedAfterClear.Set();
            }))
            {
                var manager = new SimEntityManager(spawner)
                {
                    Scheduler = scheduler
                };

                try
                {
                    var member = manager.SpawnBNpc(2);
                    // 替身按顺序分配槽位，战斗目标必须使用有效的偶数网络槽。
                    var boss = manager.SpawnBNpc(1);
                    manager.Party.Add(member);
                    Throws<InvalidOperationException>(() => manager.SetPartyEnmity(boss, 100));
                    manager.ShowParty(party);
                    manager.SetPartyEnmity(boss, 100);
                    manager.SetPartyEnmity(boss, 50);
                    Assert(party.EnmitySetCount == 2 && party.EnmityTarget == boss.Id &&
                        party.EnmityValues.SequenceEqual(new[]
                    {
                        50
                    }) && scheduler.PendingCount == 1,
                        "多次更新仇恨必须共用一个刷新节点，服务应收到最后的目标与数值。");
                    party.OnRefreshEnmity = refreshed.Set;
                    scheduler.Start();
                    Assert(refreshed.Wait(5000), "仇恨刷新节点没有执行：" + failure);
                    manager.Clear();
                    var refreshes = party.EnmityRefreshCount;
                    Assert(scheduler.PendingCount == 0 && !party.IsOwnedBy(manager),
                        "停止模拟必须取消刷新节点并恢复小队。");
                    scheduler.ScheduleAfter(0.6, observedAfterClear.Set, "确认清理后没有迟到的仇恨刷新");
                    Assert(observedAfterClear.Wait(5000), "清理后的观察节点没有执行。");
                    Assert(failure == null && party.EnmityRefreshCount == refreshes,
                        "停止后不得再刷新小队仇恨：" + failure);
                    manager.Clear();
                    Assert(party.EnmityRefreshCount == refreshes, "重复停止不能重新触发刷新。");
                }
                finally
                {
                    manager.Clear();
                }
            }
        }

        private static void AddCombinedAction(int id, float castTime, bool omen)
        {
            GameDataService.Instance.GetSheet<ActionRow>()[id] = new ActionRow
            {
                RowId = (uint)id,
                Cast100ms = checked((ushort)(castTime * 10)),
                AnimationEnd = (short)(100 + id),
                CastType = 2,
                EffectRange = 5,
                Omen = new Lumina.Excel.RowRef<Lumina.Excel.Sheets.Omen>
                {
                    RowId = (uint)(omen ? 1 : 0),
                    ValueNullable = new Lumina.Excel.Sheets.Omen
                    {
                        Path = "组合测试预兆"
                    }
                }
            };
        }

        private static bool IsOmenRemoval(VfxManager.Call command) => command.IsRemoval;
        private static string OmenTag(VfxManager.Call command) => command.Tag;

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        private static void Throws<T>(Action action)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            throw new Exception("预期抛出 " + typeof(T).Name + "。");
        }
    }
}

// 仅替换原生创建与释放操作；会话管理器使用实际实现。
namespace Simulant.Core.Entity
{
    public sealed class CharacterSpawnSpec
    {
        public uint BNpcBaseId;
        public uint BNpcNameId;
        public byte Level;
    }

    public sealed class EventObjectSpawnSpec
    {
        public byte Index;
        public uint BaseId;
        public Vector3 Pos;
        public float Heading;
    }

    public sealed class EntitySpawner
    {
        internal readonly Simulant.Tests.EntityTestWorld World = new Simulant.Tests.EntityTestWorld();
        internal readonly List<EntityBase> Deleted = new List<EntityBase>();
        internal readonly List<EntityBase> DeleteAttempts = new List<EntityBase>();
        internal Action<EntityBase> BeforeDelete;
        internal int SpawnCount;
        // 模拟本地角色已分配、初始化和首次回滚均失败，但没有交付包装对象的创建。
        internal Exception NextUnreleasedCreationFailure;
        internal Action BeforeRetryFailedCreations;
        internal int FailedCreationCleanupAttempts;
        private int _pendingCreationCleanupCount;
        internal bool HasPendingCreationCleanup => _pendingCreationCleanupCount != 0;

        internal readonly List<NetworkCharacterSpawnRequest> NetworkRequests = new List<NetworkCharacterSpawnRequest>();
        internal readonly List<NetworkCharacterSpawnRequest> CancelledNetworkRequests = new List<NetworkCharacterSpawnRequest>();
        internal bool NetworkReady;
        internal int NetworkPollCount;
        internal Action<NetworkNpcSpawnSpec> BeforeNetworkBegin { get; set; }
        internal Action<NetworkPlayerSpawnSpec> BeforeNetworkPlayerBegin { get; set; }
        internal Action<NetworkCharacterSpawnRequest> AfterNetworkReserved { get; set; }
        internal Action<NetworkCharacterSpawnRequest> BeforeNetworkPoll { get; set; }
        internal Action<NetworkCharacterSpawnRequest> BeforeNetworkCancel { get; set; }

        private ushort _nextSlot = 1;

        internal void BeginSpawnNetworkBNpc(NetworkNpcSpawnSpec spec, out NetworkCharacterSpawnRequest request)
        {
            request = null;
            BeforeNetworkBegin?.Invoke(spec);
            BeginNetworkSpawn(ObjectKind.BattleNpc, Job.ADV, spec.Targetable, false, out request);
        }

        internal void BeginSpawnNetworkPlayer(NetworkPlayerSpawnSpec spec, out NetworkCharacterSpawnRequest request,
            bool deferDispatch = false)
        {
            request = null;
            BeforeNetworkPlayerBegin?.Invoke(spec);
            BeginNetworkSpawn(ObjectKind.Pc, spec.Job, spec.Targetable, deferDispatch, out request);
        }

        private void BeginNetworkSpawn(ObjectKind kind, Job job, bool targetable, bool deferDispatch,
            out NetworkCharacterSpawnRequest request)
        {
            request = new NetworkCharacterSpawnRequest((byte)_nextSlot++, kind, job)
            {
                Targetable = targetable
            };
            NetworkRequests.Add(request);
            if (!deferDispatch)
                DispatchNetworkCharacter(request);
        }

        internal void DispatchNetworkCharacter(NetworkCharacterSpawnRequest request)
        {
            request.Dispatched = true;
            SpawnCount++;

            try
            {
                AfterNetworkReserved?.Invoke(request);
            }
            catch (Exception spawnError)
            {
                try
                {
                    CancelNetworkCharacter(request);
                }
                catch (Exception cleanupError)
                {
                    throw new AggregateException("生成及回滚均失败。", spawnError, cleanupError);
                }

                throw;
            }
        }

        internal Character TryCompleteNetworkCharacter(NetworkCharacterSpawnRequest request)
        {
            NetworkPollCount++;
            BeforeNetworkPoll?.Invoke(request);

            if (request.Cancelled)
                throw new InvalidOperationException("已取消的网络生成请求不能再次领取。");

            if (!request.Dispatched || (!NetworkReady && !request.Ready))
                return null;

            if (request.Entity == null)
            {
                World.Objects[request.Index] = new EntityObservation(new IntPtr(100 + request.Index),
                    request.EntityId, request.Kind);
                request.Entity = (Character)World.Registry.RegisterCreated(request.Index);
                request.Entity.Job = request.Job;
            }

            return request.Entity;
        }

        internal void CancelNetworkCharacter(NetworkCharacterSpawnRequest request)
        {
            BeforeNetworkCancel?.Invoke(request);

            if (request.Released)
                return;

            if (request.Entity != null)
                Delete(request.Entity);

            request.Cancelled = true;
            request.Released = true;
            CancelledNetworkRequests.Add(request);
        }

        public Character SpawnPlayer(Job job) => SpawnBNpc(new CharacterSpawnSpec());
        public Character SpawnBNpc(CharacterSpawnSpec spec)
        {
            BeginLocalCreation();
            return World.CreateCharacter(_nextSlot++);
        }

        public EventObject SpawnEObj(EventObjectSpawnSpec spec)
        {
            SpawnCount++;
            World.Objects[spec.Index] = new EntityObservation(new IntPtr(200 + spec.Index),
                (uint)(20 + spec.Index), ObjectKind.EventObj);
            return (EventObject)World.Registry.RegisterCreated(spec.Index);
        }

        private void BeginLocalCreation()
        {
            SpawnCount++;
            var failure = NextUnreleasedCreationFailure;

            if (failure == null)
                return;

            NextUnreleasedCreationFailure = null;
            _pendingCreationCleanupCount++;
            throw failure;
        }

        internal void RetryFailedCreations()
        {
            if (!HasPendingCreationCleanup)
                return;

            FailedCreationCleanupAttempts++;
            BeforeRetryFailedCreations?.Invoke();
            _pendingCreationCleanupCount = 0;
        }

        internal void ResetWorld()
        {
            _pendingCreationCleanupCount = 0;
            World.Registry.Reset();
        }

        public void Delete(EntityBase entity)
        {
            DeleteAttempts.Add(entity);

            if (Deleted.Contains(entity))
                return;

            BeforeDelete?.Invoke(entity);
            Deleted.Add(entity);
            World.Registry.Invalidate(entity);
            World.Objects[entity.Identity.Slot] = null;
        }
    }

    internal sealed class NetworkCharacterSpawnRequest
    {
        internal NetworkCharacterSpawnRequest(byte index, ObjectKind kind, Job job)
        {
            Index = index;
            EntityId = (uint)(10 + index);
            Kind = kind;
            Job = job;
        }

        public byte Index { get; }
        public uint EntityId { get; }
        internal ObjectKind Kind { get; }
        internal Job Job { get; }
        internal bool Targetable { get; set; }
        internal bool Dispatched;

        internal Character Entity;
        internal bool Ready { get; set; }

        internal bool Cancelled;
        internal bool Released;
    }
}
