using Simulant.Core.Entity;
using Simulant.Game.Data;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using ActionRow = Lumina.Excel.Sheets.Action;

namespace Simulant.Tests
{
    internal static class MechanicTests
    {
        public static void CorrectSelfBaitWithExtraHitSuggestsPositionWithoutAssigningBlame()
        {
            var error = MechanicError.Bait("第 8 轮 D3 的扇形点名", new[] { 0 }, new[] { 0, 2 },
                expectedTarget: 0, actualTarget: 0, source: 6, reason: "允许最多 1 人，实际 2 人。");
            var roles = new[] { "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };
            var text = error.Format(0, slot => roles[slot]);
            Assert(text.Contains("· 应有 1 人：[MT]") && text.Contains("· 实际 2 人：[MT] H1") &&
                text.Contains("· 多余：H1") && text.EndsWith("· 推测：可能是自身站歪了"),
                "自身正确引导却波及队友时，应提示可能是自身站歪了，不能输出未知。");
            Assert(error.Offenders.Count == 0, "文字推测不能自动把引导者或被波及者加入画叉名单。");
            Assert(error.Format(1, slot => roles[slot]).EndsWith("· 推测：未知"), "无关队员不能被推测为引导失误。");
            Assert(error.Format(2, slot => roles[slot]).EndsWith("· 推测：自身不应参与"), "多余的受击队员仍按错误参与提示。");
            error.WithAttribution(new[] { 2 });
            Assert(error.Format(0, slot => roles[slot]).EndsWith("· 推测：未知"), "机制已明确其他责任人时，不能再推测自身引导失误。");
        }

        public static void WrongBaitTargetKeepsMissingAndUnwantedBaitHints()
        {
            var error = MechanicError.Bait("扇形点名", new[] { 0 }, new[] { 2 }, expectedTarget: 0, actualTarget: 2);
            Assert(error.Format(0, slot => slot.ToString()).EndsWith("· 推测：自身未去引导"), "错失引导不能被归为方向错误。");
            Assert(error.Format(2, slot => slot.ToString()).EndsWith("· 推测：自身不应引导"), "抢到引导仍应提示自身不应引导。");
        }

        public static void SnapshotReadsOnceAndKeepsOriginalSlots()
        {
            using (var scene = new Scene())
            {
                var detached = scene.Manager.Attach(scene.Spawner.World.CreateCharacter(3));
                detached.Dispose();
                scene.Player.Pos = new Vector2(1, 0);
                scene.Other.Pos = new Vector2(3, 0);
                var before = scene.Player.Entity.PositionReads;
                var snapshot = PartySnapshot.Capture(new[] { scene.Player, detached, null, scene.Other });
                scene.Player.Pos = new Vector2(100, 100);
                Assert(snapshot.Members.Count == 2 && snapshot.Get(3).Actor == scene.Other && snapshot.Get(0).Position == new Vector2(1, 0),
                    "快照必须跳过失效角色，保留原始下标，并冻结读取时的位置。");
                Assert(snapshot.InArea(ActionArea.Circle(5), Vector2.Zero).Count == 2 &&
                    snapshot.AnyInArea(ActionArea.Circle(2), Vector2.Zero) && snapshot.Nearest(Vector2.Zero).Value.Slot == 0 &&
                    snapshot.Nearest(Vector2.Zero, scene.Player).Value.Slot == 3,
                    "范围、最近成员及来源排除查询应使用同一份坐标。");
                Assert(scene.Player.Entity.PositionReads == before + 1, "多次范围查询不得重读实体位置。");
                Throws<InvalidOperationException>(() => snapshot.Get(1));
                var empty = new PartySnapshot(Array.Empty<PartyMemberSnapshot>());
                Assert(!empty.Nearest(Vector2.Zero).HasValue, "无候选时最近成员必须为空。");
                Throws<InvalidOperationException>(() => empty.InArea(default(ActionArea), Vector2.Zero));
                Throws<ArgumentOutOfRangeException>(() => empty.AnyInArea(ActionArea.Circle(1), new Vector2(float.NaN, 0)));
            }
        }

        public static void AreaMechanicKeepsInvalidSoakersInItsResult()
        {
            using (var scene = new Scene())
            {
                scene.Player.Pos = new Vector2(10, 10);
                scene.Other.Pos = new Vector2(11, 10);
                var order = new List<string>();
                var mechanic = new AreaMechanic
                {
                    Area = ActionArea.Circle(3), RequiredPlayers = 2,
                    CenterAtResolve = snapshot => snapshot.Get(0).Position,
                    HeadingAtResolve = snapshot => 1.25f,
                    Started = context => order.Add("开始"),
                    Check = members => members.Any(member => member.Slot == 1) ? "队员没有所需层数。" : null,
                    Succeeded = result => order.Add("成功"),
                    Failed = result =>
                    {
                        Assert(result.Participants.Count == 2 && !result.Success && result.Reason == "队员没有所需层数。" &&
                            result.Center == new Vector2(10, 10) && result.Heading == 1.25f,
                            "额外条件失败不得把实际踩塔者从人数或冻结结果中筛掉。");
                        order.Add("失败");
                    },
                    Resolved = result => order.Add("结果"),
                    Cleanup = context => order.Add("清理"),
                };
                scene.Manager.RunMechanic(mechanic);
                scene.Scheduler.Start();
                Await(scene.ErrorReported);
                scene.Scheduler.Dispose();
                Assert(scene.Errors.Count == 1 && scene.Errors[0] is SimMechanicFailedException &&
                    order.SequenceEqual(new[] { "开始", "失败" }), "失败应报告原因并立即中断，不能推进结果回调或清理现场。");
                Throws<InvalidOperationException>(() => scene.Manager.RunMechanic(mechanic));
                scene.Manager.Clear();
                Assert(order.SequenceEqual(new[] { "开始", "失败", "清理" }), "显式停止时才应清理失败机制。");
            }
        }

        public static void MechanicGroupEvaluatesEverythingBeforeAnyCallback()
        {
            using (var scene = new Scene())
            using (var finished = new ManualResetEventSlim())
            {
                scene.Player.Pos = Vector2.Zero;
                scene.Other.Pos = new Vector2(1, 0);
                var ruleState = true;
                var callbacks = new List<int>();
                var first = new AreaMechanic
                {
                    Area = ActionArea.Circle(2), RequiredPlayers = 2,
                    Resolved = result => { callbacks.Add(1); ruleState = false; scene.Other.Pos = new Vector2(100, 0); },
                };
                var second = new AreaMechanic
                {
                    Area = ActionArea.Circle(2), RequiredPlayers = 2,
                    Check = members => ruleState ? null : "不应读取到其他结果回调修改后的状态。",
                    Resolved = result => { Assert(result.Success && result.Participants.Count == 2, "第二座塔应沿用共同快照与预先判定结果。"); callbacks.Add(2); },
                };
                var reads = scene.Other.Entity.PositionReads;
                scene.Manager.RunMechanic(new MechanicGroup(first, second)
                {
                    Resolved = result =>
                    {
                        Assert(result.Success && result.Children.Count == 2 &&
                            ReferenceEquals(result.Children[0].Snapshot, result.Children[1].Snapshot), "组内机制必须共享同一个快照对象。");
                        callbacks.Add(3);
                    },
                    Cleanup = context => finished.Set(),
                });
                scene.Scheduler.Start();
                Await(finished);
                scene.Scheduler.Dispose();
                scene.AssertNoError();
                Assert(callbacks.SequenceEqual(new[] { 1, 2, 3 }) && scene.Other.Entity.PositionReads == reads + 1,
                    "必须先判定所有区域，再发各子结果，最后发组结果，且只读一轮实体位置。");
            }
        }

        public static void MechanicNodesUsePlannedTimeAndCancelWithTheirOwner()
        {
            using (var scene = new Scene())
            using (var finished = new ManualResetEventSlim())
            {
                var order = new List<string>();
                var extra = false;
                scene.Scheduler.Schedule(0, () => scene.Manager.RunMechanic(new AreaMechanic
                {
                    Area = ActionArea.Circle(2), ResolveAfter = 0.02,
                    Started = context =>
                    {
                        context.ScheduleAfter(0.3, () => extra = true);
                        Thread.Sleep(80);
                    },
                    Resolved = result => order.Add("判定"),
                }));
                scene.Scheduler.Schedule(0.04, () => { order.Add("后续"); finished.Set(); });
                scene.Scheduler.Start();
                Await(finished);
                scene.Scheduler.Dispose();
                scene.AssertNoError();
                Assert(order.SequenceEqual(new[] { "判定", "后续" }) && !extra && scene.Scheduler.PendingCount == 0,
                    "开始回调的耗时不能整体推迟判定；结束后必须取消机制添加的剩余节点。");
            }
        }

        public static void MechanicPartialStartRetainsCleanupAndActorsForRetry()
        {
            using (var scene = new Scene())
            using (var restart = new SimScheduler())
            {
                var failures = true;
                var cleanups = 0;
                SimActor created = null;
                var mechanic = new AreaMechanic
                {
                    Area = ActionArea.Circle(2),
                    Started = context =>
                    {
                        created = context.Entities.SpawnBNpc(1);
                        throw new InvalidOperationException("模拟开始特效部分成功后失败。");
                    },
                    Cleanup = context =>
                    {
                        cleanups++;
                        Assert(scene.Player.IsActive && created.IsActive && scene.Manager.Party.Count == 2,
                            "清理回调完成前不得先解除角色或清空小队。");
                        if (failures) throw new InvalidOperationException("模拟清理失败。");
                    },
                };
                Throws<InvalidOperationException>(() => scene.Manager.RunMechanic(mechanic));
                Assert(cleanups == 0 && created.IsActive, "开始回调部分成功后失败时应保留现场，不立刻清理。");
                scene.Manager.Clear();
                Assert(scene.Manager.HasPendingCleanup && created.Entity.IsAlive && scene.Spawner.Deleted.Count == 0,
                    "机制清理失败必须保留实体与恢复责任。");
                Throws<InvalidOperationException>(() => scene.Manager.Delete(created));
                Throws<InvalidOperationException>(() => scene.Manager.Scheduler = restart);
                failures = false;
                scene.Manager.Clear();
                Assert(!scene.Manager.HasPendingCleanup && !created.Entity.IsAlive && scene.Player.Entity.IsAlive && cleanups == 2,
                    "再次停止应重试清理，再回收自有实体，保留真实玩家。");
            }
        }

        public static void MechanicCallbackFailureRetainsSceneUntilStopped()
        {
            using (var scene = new Scene())
            using (var cleaned = new ManualResetEventSlim())
            {
                scene.Manager.RunMechanic(new AreaMechanic
                {
                    Area = ActionArea.Circle(2),
                    Resolved = result => { throw new InvalidOperationException("模拟结果演出失败。"); },
                    Cleanup = context => cleaned.Set(),
                });
                scene.Scheduler.Start();
                Await(scene.ErrorReported);
                scene.Scheduler.Dispose();
                Assert(scene.Errors.Count == 1 && scene.Scheduler.PendingCount == 0 && !cleaned.IsSet && scene.Player.IsActive,
                    "结果回调失败应取消后续节点并保留现场，把异常交回同一调度器。");
                Assert(scene.Errors[0].StackTrace.Contains(nameof(MechanicCallbackFailureRetainsSceneUntilStopped)),
                    "机制中断后重新抛出异常应保留实际失败回调的调用栈。");
                scene.Manager.Clear();
                Assert(cleaned.IsSet, "手动停止后才执行失败机制的清理回调。");
            }
        }

        public static void MechanicWorldChangeSkipsRemainingCallbacksAndCleanup()
        {
            using (var scene = new Scene())
            using (var reset = new ManualResetEventSlim())
            {
                var writes = 0;
                scene.Manager.RunMechanic(new MechanicGroup(
                    new AreaMechanic
                    {
                        Area = ActionArea.Circle(2),
                        Succeeded = result => { scene.Spawner.World.Registry.Reset(); reset.Set(); },
                        Resolved = result => writes++, Cleanup = context => writes++,
                    },
                    new AreaMechanic { Area = ActionArea.Circle(2), Resolved = result => writes++, Cleanup = context => writes++ })
                { Resolved = result => writes++, Cleanup = context => writes++ });
                scene.Scheduler.Start();
                Await(reset);
                scene.Scheduler.Dispose();
                scene.Manager.Clear();
                scene.AssertNoError();
                Assert(writes == 0 && !scene.Manager.HasPendingCleanup, "回调使世界失效后，不得继续演出或向新地图执行旧清理。");
            }
        }

        public static void MechanicFailureInterruptsGroupAndPreservesActorsAndCasting()
        {
            const int actionId = 41001;
            var rows = GameDataService.Instance.GetSheet<ActionRow>();
            var hadPrevious = rows.TryGetValue(actionId, out var previous);
            rows[actionId] = new ActionRow { RowId = (uint)actionId, Cast100ms = 600, AnimationEnd = -1 };
            try
            {
                using (var scene = new Scene())
                {
                    var helper = scene.Manager.SpawnBNpc(1);
                    scene.Player.Cast(actionId);
                    scene.Player.MoveTo(new Vector2(100, 0));
                    scene.Other.MoveTo(new Vector2(100, 0));
                    var checks = 0;
                    var callbacks = new List<string>();
                    var cleanups = 0;
                    scene.Manager.RunMechanic(new AreaMechanic
                    {
                        Area = ActionArea.Circle(2), ResolveAfter = 100,
                        Cleanup = context => cleanups++,
                    });
                    scene.Manager.RunMechanic(new MechanicGroup(
                        new AreaMechanic
                        {
                            Name = "测试双人塔", Area = ActionArea.Circle(2),
                            Check = members => { checks++; return "缺少一人。"; },
                            Failed = result => callbacks.Add("失败演出"),
                            Resolved = result => callbacks.Add("不应减层"),
                            Cleanup = context => cleanups++,
                        },
                        new AreaMechanic
                        {
                            Area = ActionArea.Circle(2),
                            Check = members => { checks++; return null; },
                            Resolved = result => callbacks.Add("不应释放后续技能"),
                            Cleanup = context => cleanups++,
                        })
                    {
                        Resolved = result => callbacks.Add("不应推进整轮"),
                        Cleanup = context => cleanups++,
                    });
                    scene.Scheduler.Start();
                    Await(scene.ErrorReported);
                    scene.Scheduler.Dispose();
                    Assert(checks == 2 && callbacks.SequenceEqual(new[] { "失败演出" }),
                        "同组应先完成全部判定，但首次失败后不得执行结果回调和后续演出。");
                    Assert(scene.Errors.Count == 1 && scene.Errors[0].Message == "测试双人塔：缺少一人。",
                        "失败异常必须包含对应机制名和实际原因。");
                    Assert(cleanups == 0 && scene.Player.IsActive && scene.Other.IsActive && helper.IsActive &&
                        scene.Spawner.Deleted.Count == 0 && scene.Manager.Party.Count == 2,
                        "中断必须保留小队、实体与所有已开始机制的清理责任。");
                    Assert(!scene.Player.IsMoving && !scene.Other.IsMoving && scene.Scheduler.PendingCount == 0 &&
                        scene.Player.Entity.CastingActionId == actionId && scene.Player.Entity.FinalizedCasts.Count == 0,
                        "中断应停止移动及后续节点，同时保留现有读条动画。");
                    scene.Manager.Clear();
                    Assert(cleanups == 4 && scene.Player.Entity.CastingActionId == 0 && !helper.Entity.IsAlive &&
                        scene.Player.Entity.IsAlive && !scene.Manager.HasPendingCleanup,
                        "手动停止才应清理全部机制、结束演出并回收自有实体。");
                }
            }
            finally
            {
                if (hadPrevious) rows[actionId] = previous;
                else rows.Remove(actionId);
            }
        }

        public static void MechanicInterruptContinuesAfterMovementStopFailure()
        {
            using (var scene = new Scene())
            using (var started = new ManualResetEventSlim())
            {
                var helper = scene.Manager.SpawnBNpc(1);
                scene.Other.Entity.OnActorMove = (position, heading, speed) => started.Set();
                scene.Other.MoveNetworkTo(new Vector2(100, 0));
                helper.MoveTo(new Vector2(100, 0));
                scene.Scheduler.Start();
                Await(started);
                scene.Scheduler.Dispose();
                scene.Other.Entity.OnActorMove = (position, heading, speed) => { throw new InvalidOperationException("模拟停止移动包失败。"); };
                Throws<AggregateException>(() => scene.Manager.Interrupt());
                Assert(!helper.IsMoving && helper.IsActive && scene.Other.IsActive,
                    "某角色停止失败不能阻止停止其余角色，且不能回收任何角色。");
                scene.Other.Entity.OnActorMove = null;
                scene.Manager.Clear();
                Assert(!scene.Manager.HasPendingCleanup, "手动停止应重试失败移动并完成回收。");
            }
        }

        public static void MechanicManualStopCanCleanOwnedResourcesAfterInterruption()
        {
            using (var scene = new Scene())
            {
                var helper = scene.Manager.SpawnBNpc(1);
                var cleanups = 0;
                scene.Manager.RunMechanic(new AreaMechanic
                {
                    Area = ActionArea.Circle(2), ResolveAfter = 100,
                    Cleanup = context => { context.Entities.Delete(helper); cleanups++; },
                });
                scene.Manager.RunMechanic(new AreaMechanic
                {
                    Area = ActionArea.Circle(2), ResolveAfter = 100,
                    Cleanup = context => cleanups++,
                });
                scene.Manager.Interrupt();
                Throws<InvalidOperationException>(() => scene.Manager.Delete(helper));
                scene.Manager.Clear();
                Assert(cleanups == 2 && !helper.Entity.IsAlive && !scene.Manager.HasPendingCleanup,
                    "整体停止期间，各机制必须能释放自己创建的资源，不受其他待清理机制阻挡。");
            }
        }

        public static void MechanicGroupReuseDoesNotCancelAnExistingOwner()
        {
            using (var scene = new Scene())
            {
                var existingCleanups = 0;
                var owned = new AreaMechanic { Area = ActionArea.Circle(2), ResolveAfter = 100, Cleanup = context => existingCleanups++ };
                scene.Manager.RunMechanic(owned);
                // 作为子项要求零延迟；这里只验证 Claim 拒绝已使用实例时不能拿走旧所有权。
                owned.ResolveAfter = 0;
                Throws<InvalidOperationException>(() => scene.Manager.RunMechanic(new MechanicGroup(
                    new AreaMechanic { Area = ActionArea.Circle(2) }, owned)));
                Assert(existingCleanups == 0 && scene.Scheduler.PendingCount == 1,
                    "组装失败时不能清理另一个正在运行的机制。");
                scene.Manager.Clear();
                Assert(existingCleanups == 1, "会话停止时仍应由原所有者清理已有机制。");
            }
        }

        public static void MechanicResourceCleanupDoesNotCancelParallelMechanics()
        {
            using (var scene = new Scene())
            {
                var helper = scene.Manager.SpawnBNpc(1);
                var cleanups = 0;
                scene.Manager.RunMechanic(new AreaMechanic { Area = ActionArea.Circle(2), ResolveAfter = 100, Cleanup = context => cleanups++ });
                scene.Manager.Delete(helper);
                Assert(cleanups == 0 && scene.Scheduler.PendingCount == 1 && !helper.Entity.IsAlive,
                    "回收单个演出实体不能取消无关的并行机制。");
                scene.Manager.Clear();
                Assert(cleanups == 1, "最终停止会话应清理剩余机制。");
            }
        }

        public static void MechanicValidationHappensBeforeStart()
        {
            using (var scene = new Scene())
            {
                var starts = 0;
                Throws<InvalidOperationException>(() => scene.Manager.RunMechanic(new AreaMechanic
                { Area = ActionArea.Circle(2), RequiredPlayers = 1, MinPlayers = 1, Started = context => starts++ }));
                Throws<InvalidOperationException>(() => scene.Manager.RunMechanic(new AreaMechanic
                { Area = default(ActionArea), Started = context => starts++ }));
                Assert(starts == 0 && scene.Scheduler.PendingCount == 0, "错误规则必须在原生演出与调度之前拒绝。");
            }
        }

        public static void MechanicGroupDoesNotCleanUnstartedChildren()
        {
            using (var scene = new Scene())
            {
                var firstCleanup = 0;
                var laterStarted = 0;
                var laterCleanup = 0;
                Throws<InvalidOperationException>(() => scene.Manager.RunMechanic(new MechanicGroup(
                    new AreaMechanic
                    {
                        Area = ActionArea.Circle(2),
                        Started = context => { throw new InvalidOperationException("第一项开始失败。"); },
                        Cleanup = context => firstCleanup++,
                    },
                    new AreaMechanic
                    {
                        Area = ActionArea.Circle(2),
                        Started = context => laterStarted++,
                        Cleanup = context => laterCleanup++,
                    })));
                Assert(firstCleanup == 0 && laterStarted == 0 && laterCleanup == 0,
                    "组的首项开始失败时应保留已经产生的演出，并中断后续开始回调。");
                scene.Manager.Clear();
                Assert(firstCleanup == 1 && laterStarted == 0 && laterCleanup == 0,
                    "手动停止仅清理已开始的项，不能释放尚未创建的资源。");
            }
        }

        private sealed class Scene : IDisposable
        {
            internal readonly EntitySpawner Spawner = new EntitySpawner();
            internal readonly SimEntityManager Manager;
            internal readonly SimScheduler Scheduler;
            internal readonly List<Exception> Errors = new List<Exception>();
            internal readonly ManualResetEventSlim ErrorReported = new ManualResetEventSlim();
            internal readonly SimActor Player, Other;

            internal Scene()
            {
                Scheduler = new SimScheduler((error, name) =>
                {
                    Errors.Add(error);
                    try { Manager.Interrupt(); }
                    finally { ErrorReported.Set(); }
                });
                Manager = new SimEntityManager(Spawner) { Scheduler = Scheduler };
                Player = Manager.Attach(Spawner.World.CreateCharacter(0));
                Other = Manager.Attach(Spawner.World.CreateCharacter(2));
                Manager.Party.Add(Player);
                Manager.Party.Add(Other);
            }
            internal void AssertNoError() { if (Errors.Count != 0) throw new AggregateException("机制测试收到调度异常。", Errors); }
            public void Dispose() { Scheduler.Dispose(); Manager.Clear(); ErrorReported.Dispose(); }
        }

        private static void Await(ManualResetEventSlim signal)
        {
            if (!signal.Wait(3000)) throw new Exception("等待机制测试节点超时。");
        }
        private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new Exception("预期抛出 " + typeof(T).Name + "。");
        }
    }
}
