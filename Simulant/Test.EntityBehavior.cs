using Simulant.Game.ExtractedCsv;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using ActionRow = Simulant.Game.ExtractedCsv.Rows.Action;

namespace Simulant
{
    internal partial class Test
    {
        private const uint EntityTestActionId = 0x299E; // 流体摆动：3.5 秒读条、90 度扇形预兆。
        private readonly object _entityTestGate = new object();
        private EntityBehaviorRun _entityBehaviorRun;

        // 一次测试的资源集中保存，停止时只回收本次生成的角色。
        private sealed class EntityBehaviorRun
        {
            public SimScheduler Scheduler;
            public SimEntityManager Entities;
            public readonly List<SimActor> Actors = new List<SimActor>();
            public int Checks;
            public int CleanupErrors;
            public int StoppingThreadId;
            public bool CleanupCompleted;
        }

        private void ToggleEntityBehaviorTest()
        {
            EntityBehaviorRun previous;
            lock (_entityTestGate) previous = _entityBehaviorRun;
            if (previous != null)
            {
                StopEntityBehaviorTest(previous, "手动停止");
                return;
            }

            EnsureEntityTestReady();
            if (!CsvManager.Instance.Get<ActionRow>().TryGetValue((int)EntityTestActionId, out var action) ||
                action.CastTime < 3 || action.OmenId == 0 || action.AnimationEndId < 0)
                throw new InvalidOperationException("实体测试技能数据不完整，需要带读条、预兆和结束动画的流体摆动。");

            var run = new EntityBehaviorRun();
            run.Scheduler = new SimScheduler((ex, node) =>
            {
                try { _host.LogError($"[实体测试] 节点「{node}」失败：{ex}"); }
                finally { StopEntityBehaviorTest(run, "异常终止"); }
            });
            run.Entities = new SimEntityManager(_host.EntitySpawner, ex =>
            {
                run.CleanupErrors++;
                _host.LogError("[实体测试] 回收失败：" + ex);
            }) { Scheduler = run.Scheduler };

            try
            {
                lock (_entityTestGate)
                {
                    EnsureEntityTestReady();
                    if (_entityBehaviorRun != null)
                        throw new InvalidOperationException("已有实体测试正在运行。");
                    _entityBehaviorRun = run;
                    run.Scheduler.Schedule(0, () => BeginEntityMovementTest(run, action.CastTime), "生成并移动测试角色");
                    LogEntityTest(run, "开始。请观察玩家前方三只实体；左右以玩家起始朝向为准，再次点击“调试”可随时停止，正常流程约 15 秒。");
                    run.Scheduler.Start();
                }
            }
            catch
            {
                StopEntityBehaviorTest(run, "启动失败");
                // 启动前置检查失败时，本次状态可能尚未登记。
                run.Scheduler.Dispose();
                run.Entities.Clear();
                throw;
            }
        }

        private void EnsureEntityTestReady()
        {
            if (_host.Disposed || !_host.PluginReady || _host.IsInitializing || !_host.CsvReady)
                throw new InvalidOperationException("请等待插件初始化和 CSV 加载完成后再运行实体测试。");
            if (!_host.FirewallEnabled)
                throw new InvalidOperationException("请先启用防火墙，再运行实体测试。");
        }

        private void BeginEntityMovementTest(EntityBehaviorRun run, float castTime)
        {
            EnsureEntityTestReady();
            var player = _host.EntityProvider.GetMyself();
            if (player == null || !player.IsAlive)
                throw new InvalidOperationException("未找到有效的玩家实体，无法确定测试位置。");
            var origin = player.Pos3D;
            var heading = player.Heading;
            var forward = new Vector2((float)Math.Sin(heading), (float)Math.Cos(heading));
            // 水平坐标的第二轴指向南；面向南时，角色右侧应为西（负 X）。
            var right = new Vector2(-forward.Y, forward.X);
            var center = new Vector2(origin.X, origin.Y) + forward * 5;
            for (var i = 0; i < 3; i++)
            {
                var actor = run.Entities.SpawnBNpc(4909, 3765, 90);
                run.Actors.Add(actor);
                var start = center + right * ((i - 1) * 8);
                actor.Pos3D = new Vector3(start, origin.Z);
                actor.Heading = heading;
                actor.SetReadyToDraw();
                actor.EnableDraw();
                LogEntityTest(run, $"角色 {(char)('A' + i)}：实体 ID {actor.Id:X8}，起点（{start.X:0.00}, {start.Y:0.00}）。");
            }

            var a = run.Actors[0];
            var b = run.Actors[1];
            var c = run.Actors[2];
            var targetA = a.Pos + forward * 10;
            var targetB = b.Pos + forward * 10;
            b.Heading = heading + (float)Math.PI / 2;
            var preservedHeading = b.Heading;
            var movementStartedAt = run.Scheduler.ElapsedSeconds;
            var aArrived = false;
            var bArrived = false;
            var stopVerified = false;
            var castStarted = false;
            var arrivalTimeout = run.Scheduler.ScheduleAfter(12, () =>
            {
                throw new TimeoutException("实体测试超时：未收到全部到达回调。");
            }, "等待移动到达回调超时");
            Action tryBeginCast = () =>
            {
                if (!aArrived || !bArrived || !stopVerified || castStarted) return;
                castStarted = true;
                arrivalTimeout.Cancel();
                CheckEntityTest(run, Vector2.Distance(a.Pos, targetA) < 0.05f &&
                    Vector2.Distance(b.Pos, targetB) < 0.05f, "改目标与正常移动均准确到达");
                var angle = b.Heading - preservedHeading;
                CheckEntityTest(run, Math.Abs(Math.Atan2(Math.Sin(angle), Math.Cos(angle))) < 0.01,
                    "移动时保留指定朝向");
                foreach (var actor in run.Actors)
                {
                    CheckEntityTest(run, Math.Abs(actor.Pos3D.Z - origin.Z) < 0.05f, "移动保持高度");
                    actor.Heading = heading;
                }
                BeginEntityCastTest(run, castTime, forward);
            };
            a.MoveTo(targetA, 2, animationId: 13);
            b.MoveTo(targetB, 4, faceMovement: false, animationId: 13, onArrived: () =>
            {
                bArrived = true;
                LogEntityTest(run, $"B 到达回调，起步后 {run.Scheduler.ElapsedSeconds - movementStartedAt:0.00} 秒，位置（{b.Pos.X:0.00}, {b.Pos.Y:0.00}）。");
                tryBeginCast();
            });
            c.MoveTo(c.Pos + forward * 10, 1.5f, animationId: 13);
            LogEntityTest(run, "1/4：A（左）途中改目标；B（中）向前移动但面向左侧，保留朝向；C（右）一秒后主动停止。B 移动 10 单位、速度 4，预计位移 2.50 秒。");

            run.Scheduler.ScheduleAfter(1, () =>
            {
                targetA += right * 4;
                a.MoveTo(targetA, 4, animationId: 13, onArrived: () =>
                {
                    aArrived = true;
                    LogEntityTest(run, $"A 到达回调，起步后 {run.Scheduler.ElapsedSeconds - movementStartedAt:0.00} 秒，位置（{a.Pos.X:0.00}, {a.Pos.Y:0.00}）。");
                    tryBeginCast();
                });
                c.StopMoving();
                var stoppedPosition = c.Pos3D;
                LogEntityTest(run, $"A 已替换移动目标；C 已主动停止，位置（{stoppedPosition.X:0.00}, {stoppedPosition.Y:0.00}）。");
                run.Scheduler.ScheduleAfter(0.5, () =>
                {
                    CheckEntityTest(run, !c.IsMoving && Vector3.Distance(c.Pos3D, stoppedPosition) < 0.05f,
                        "停止后位置保持不变");
                    stopVerified = true;
                    tryBeginCast();
                }, "验证主动停止");
            }, "途中改目标并停止第三个角色");
        }

        private void BeginEntityCastTest(EntityBehaviorRun run, float castTime, Vector2 forward)
        {
            var a = run.Actors[0];
            var b = run.Actors[1];
            var c = run.Actors[2];
            LogEntityTest(run, "2/4：同时读条，B（中）将重新读条，C（右）在读条 0.75 秒后删除。请观察扇形预兆和结束动作。");
            a.Cast(EntityTestActionId);
            b.Cast(EntityTestActionId, omenDelay: 1.5f);
            c.Cast(EntityTestActionId);
            CheckEntityTest(run, a.Entity.CastingActionId == EntityTestActionId &&
                b.Entity.CastingActionId == EntityTestActionId && c.Entity.CastingActionId == EntityTestActionId,
                "三个角色均写入读条状态");
            run.Scheduler.ScheduleAfter(0.75, () =>
            {
                b.Cast(EntityTestActionId, omenDelay: 0.5f);
                run.Entities.Delete(c);
                CheckEntityTest(run, !c.IsActive && !c.Entity.IsAlive && !c.IsMoving, "读条中删除角色并使旧引用失效");
                run.Scheduler.ScheduleAfter(castTime + 0.5, () =>
                {
                    CheckEntityTest(run, a.Entity.CastingActionId == 0 && b.Entity.CastingActionId == 0,
                        "自然读条与覆盖后的读条均结束");
                    BeginMovingCastTest(run, castTime, forward);
                }, "验证读条结束");
            }, "覆盖读条并回收第三个角色");
        }

        private void BeginMovingCastTest(EntityBehaviorRun run, float castTime, Vector2 forward)
        {
            var a = run.Actors[0];
            var b = run.Actors[1];
            LogEntityTest(run, "3/4：A（左）边走边读条，预兆延迟一秒出现，随后删除；B（中）继续正常读条。预兆固定在出现时的位置。");
            a.MoveTo(a.Pos + forward * 10, 2, animationId: 13);
            a.Cast(EntityTestActionId, omenDelay: 1);
            b.Cast(EntityTestActionId);
            run.Scheduler.ScheduleAfter(1.5, () =>
            {
                run.Entities.Delete(a);
                CheckEntityTest(run, !a.IsActive && !a.Entity.IsAlive && !a.IsMoving, "同时移动和读条的角色能够回收");
            }, "回收移动并读条的角色");
            run.Scheduler.ScheduleAfter(castTime + 0.5, () =>
            {
                CheckEntityTest(run, b.IsActive && b.Entity.CastingActionId == 0,
                    "删除其他角色不妨碍剩余角色完成读条");
                LogEntityTest(run, "4/4：剩余的 B 再次移动和读条，一秒后整体停止，验证待执行节点与延迟预兆清理。");
                b.MoveTo(b.Pos + forward * 8, 2, animationId: 13);
                b.Cast(EntityTestActionId, omenDelay: 2);
                run.Scheduler.ScheduleAfter(30, () =>
                {
                    throw new InvalidOperationException("测试结束后仍执行了应取消的节点。");
                }, "应随停止取消的节点");
                run.Scheduler.ScheduleAfter(1, () => StopEntityBehaviorTest(run, "正常完成"), "结束实体测试");
            }, "验证独立角色并测试整体停止");
        }

        private void LogEntityTest(EntityBehaviorRun run, string message)
            => _host.LogRuntime($"[实体测试] {run.Scheduler.ElapsedSeconds:0.00} 秒：{message}");

        private void CheckEntityTest(EntityBehaviorRun run, bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("实体测试未通过：" + description);
            run.Checks++;
            LogEntityTest(run, "通过：" + description);
        }

        internal void StopEntityBehaviorTest() => StopEntityBehaviorTest(null, "宿主停止或场景变化");

        private void StopEntityBehaviorTest(EntityBehaviorRun expected, string reason)
        {
            EntityBehaviorRun run;
            lock (_entityTestGate)
            {
                run = _entityBehaviorRun;
                if (run == null || (expected != null && !ReferenceEquals(run, expected))) return;
                if (run.StoppingThreadId != 0)
                {
                    // 外部线程停止时可能正在等待本回调退出；调度线程和重入的清理线程不能反向等待。
                    if (run.Scheduler.IsSchedulerThread || run.StoppingThreadId == Thread.CurrentThread.ManagedThreadId)
                        return;
                    while (!run.CleanupCompleted) Monitor.Wait(_entityTestGate);
                    return;
                }
                run.StoppingThreadId = Thread.CurrentThread.ManagedThreadId;
            }
            // 不能持锁等待线程：正在执行的完成/异常回调也会进入停止入口。
            try
            {
                try { run.Scheduler.Dispose(); }
                finally { run.Entities.Clear(); }
                var clean = !run.Scheduler.IsRunning && run.Scheduler.PendingCount == 0 && run.CleanupErrors == 0;
                foreach (var actor in run.Actors) clean &= !actor.IsActive && !actor.IsMoving;
                if (!clean)
                    _host.LogError($"[实体测试] {reason}，清理检查未通过；回收异常 {run.CleanupErrors} 次。");
                else
                    LogEntityTest(run, $"{reason}，已通过 {run.Checks} 项检查，调度队列与角色行为已清理。请另行确认画面中没有残留实体或预兆。");
            }
            finally
            {
                lock (_entityTestGate)
                {
                    run.CleanupCompleted = true;
                    if (ReferenceEquals(_entityBehaviorRun, run)) _entityBehaviorRun = null;
                    Monitor.PulseAll(_entityTestGate);
                }
            }
        }
    }
}
