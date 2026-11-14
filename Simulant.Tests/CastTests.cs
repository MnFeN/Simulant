using Simulant.ACT;
using Simulant.Game.Data;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading;
using ActionRow = Lumina.Excel.Sheets.Action;

namespace Simulant.Tests
{
    internal static class CastTests
    {
        public static void ReplacingCastCancelsOldCompletionAndDelayedOmen()
        {
            using (var scene = new CastTestScene())
            {
                scene.AddAction(1, 1, omen: true);
                scene.AddAction(2, 0.04f);
                var actor = scene.Actor(0);
                actor.Cast(1, omenDelay: 0.05f);
                Assert(scene.Scheduler.PendingCount == 2, "延迟预兆和读条完成应各有一个节点。");
                actor.Cast(2);
                Assert(scene.Scheduler.PendingCount == 1, "替换读条必须移除两个旧调度节点。");
                scene.RunPast(0.08);
                Assert(actor.Entity.CastStarts.SequenceEqual(new uint[] { 1, 2 }), "两个请求都必须启动各自的原生读条。");
                Assert(actor.Entity.FinalizedCasts.SequenceEqual(new uint[] { 1, 2 }), "替换与完成操作必须分别结束对应的读条。");
                Assert(actor.Entity.PlayedTimelines.SequenceEqual(new ushort[] { 102 }), "只有替换后的读条可以播放完成动作。");
                Assert(TriggernometryInterop.Commands.Count == 0, "已取消的延迟预兆不得出现。");
            }
        }

        public static void CastCancelAndDisposeRemovePendingWork()
        {
            using (var scene = new CastTestScene())
            {
                scene.AddAction(1, 1, omen: true);
                var visible = scene.Actor(0);
                var delayed = scene.Actor(1);
                visible.Cast(1);
                delayed.Cast(1, omenDelay: 0.05f);
                visible.StopCasting();
                Assert(visible.Entity.CastingActionId == 0 && scene.Scheduler.PendingCount == 2,
                    "StopCasting 必须仅清除指定模拟角色的读条，并保留另一角色的调度。");
                scene.Casts.Dispose();
                scene.Casts.Dispose();
                Assert(delayed.Entity.CastingActionId == 0 && scene.Scheduler.PendingCount == 0,
                    "释放必须清除所有剩余读条及其延迟效果。");
                visible.Cast(1);
                Assert(scene.Scheduler.PendingCount == 0, "已释放的读条控制器不得接受后续模拟角色回调。");
                scene.RunPast(0.08);
                Assert(TriggernometryInterop.Commands.Count(command => !IsRemoval(command)) == 1 &&
                    TriggernometryInterop.Commands.Count(IsRemoval) == 1,
                    "只能显示已可见的预兆，并恰好移除一次。");
                Assert(visible.Entity.PlayedTimelines.Count == 0 && delayed.Entity.PlayedTimelines.Count == 0,
                    "已取消的读条不得播放完成动作。");
                Assert(visible.IsActive && delayed.IsActive, "停止读条不得解除对应模拟角色的关联。");
            }
        }

        public static void StopCastingPreservesMovementAndAllowsRecast()
        {
            using (var scene = new CastTestScene())
            {
                scene.AddAction(1, 1, omen: true);
                scene.AddAction(2, 0.04f);
                var actor = scene.Actor(0);
                var target = new Vector2(0.2f, 0);
                actor.MoveTo(target, 2);
                actor.Cast(1);
                actor.StopCasting();
                actor.StopCasting();
                Assert(actor.IsActive && actor.IsMoving && actor.Entity.CastingActionId == 0 &&
                    scene.Scheduler.PendingCount == 1,
                    "重复停止读条必须保留角色、移动目标及唯一的移动更新节点。");
                actor.Cast(2);
                scene.RunPast(0.2);
                Assert(actor.Pos == target && !actor.IsMoving,
                    "停止读条后，已有移动仍必须到达原目标。");
                Assert(actor.Entity.CastStarts.SequenceEqual(new uint[] { 1, 2 }) &&
                    actor.Entity.FinalizedCasts.SequenceEqual(new uint[] { 1, 2 }) &&
                    actor.Entity.PlayedTimelines.SequenceEqual(new ushort[] { 102 }),
                    "停止后必须能重新读条，且只有新读条可以播放完成动作。");
                Assert(TriggernometryInterop.Commands.Count(command => !IsRemoval(command)) == 1 &&
                    TriggernometryInterop.Commands.Count(IsRemoval) == 1,
                    "重复停止只能移除一次旧读条的预兆。");
            }
        }

        public static void OmenTagsAreIndependentAcrossActorsAndRecasts()
        {
            using (var scene = new CastTestScene())
            {
                scene.AddAction(1, 1, omen: true);
                var first = scene.Actor(0);
                var second = scene.Actor(1);
                first.Cast(1);
                second.Cast(1);
                var shows = TriggernometryInterop.Commands.Where(command => !IsRemoval(command)).Select(Tag).ToArray();
                Assert(shows.Length == 2 && shows.Distinct().Count() == 2, "两个模拟角色不得共享预兆移除标签。");
                first.StopCasting();
                Assert(TriggernometryInterop.Commands.Where(IsRemoval).Select(Tag).SequenceEqual(new[] { shows[0] }),
                    "取消一个模拟角色的读条时，只能移除该角色的预兆。");
                second.Cast(1);
                var newShows = TriggernometryInterop.Commands.Where(command => !IsRemoval(command)).Select(Tag).ToArray();
                Assert(newShows.Distinct().Count() == 3, "重新读条必须使用新标签，不得复用上次读条的标签。");
                Assert(TriggernometryInterop.Commands.Where(IsRemoval).Select(Tag).SequenceEqual(shows),
                    "重新读条必须移除旧预兆，且不得影响新预兆。");
            }
        }

        public static void ExternalCastAndRecycledEntityAreProtected()
        {
            using (var scene = new CastTestScene())
            {
                scene.AddAction(1, 0.04f);
                var externallyChanged = scene.Actor(0);
                var explicitlyCancelled = scene.Actor(1);
                var recycled = scene.Actor(2);
                externallyChanged.Cast(1);
                explicitlyCancelled.Cast(1);
                recycled.Cast(1);
                externallyChanged.Entity.CastingActionId = 999;
                explicitlyCancelled.Entity.CastingActionId = 999;
                explicitlyCancelled.StopCasting();
                scene.World.Registry.RegisterCreated(2);
                scene.RunPast(0.08);
                Assert(externallyChanged.Entity.CastingActionId == 999 && explicitlyCancelled.Entity.CastingActionId == 999,
                    "完成和取消操作必须保留借用实体上无关的替代读条。");
                Assert(externallyChanged.Entity.FinalizedCasts.Count == 0 && explicitlyCancelled.Entity.FinalizedCasts.Count == 0 &&
                    recycled.Entity.FinalizedCasts.Count == 0, "失效状态不得结束无关读条或被回收实体的读条。");
                Assert(externallyChanged.Entity.PlayedTimelines.Count == 0 && recycled.Entity.PlayedTimelines.Count == 0 &&
                    externallyChanged.Entity.BaseTimelineResetCount == 0 && recycled.Entity.BaseTimelineResetCount == 0,
                    "旧完成节点不得重置替代读条或被回收实体，也不得为其播放动作。");
            }
        }

        public static void ReentrantOmenReplacementSurvivesOuterCallbackFailure()
        {
            using (var scene = new CastTestScene())
            {
                scene.AddAction(1, 1, omen: true);
                scene.AddAction(2, 1, omen: true);
                var actor = scene.Actor(0);
                var expected = new InvalidOperationException("旧外部回调失败");
                var replaced = false;
                TriggernometryInterop.OnCommand = command =>
                {
                    if (IsRemoval(command) || replaced) return;
                    replaced = true;
                    actor.Cast(2);
                    throw expected;
                };
                try { actor.Cast(1); throw new Exception("预期抛出原始回调错误。"); }
                catch (InvalidOperationException error)
                {
                    Assert(ReferenceEquals(error, expected), "失败的旧回调必须保留原始错误。");
                }
                TriggernometryInterop.OnCommand = null;
                var shown = TriggernometryInterop.Commands.Where(command => !IsRemoval(command)).Select(Tag).ToArray();
                var removed = TriggernometryInterop.Commands.Where(IsRemoval).Select(Tag).ToArray();
                Assert(shown.Length == 2 && removed.SequenceEqual(new[] { shown[0] }),
                    "同步替换必须移除旧的可见预兆并保留新预兆。");
                Assert(actor.Entity.CastingActionId == 2 && scene.Scheduler.PendingCount == 1,
                    "被替换的外层回调出错时，不得取消新读条。");
            }
        }

        public static void CompletionRemovalCanStartANewCastWithoutOverwritingIt()
        {
            using (var scene = new CastTestScene())
            using (var replaced = new ManualResetEventSlim())
            {
                scene.AddAction(1, 0.4f, omen: true);
                scene.AddAction(2, 1);
                var actor = scene.Actor(0);
                actor.Cast(1);
                var oldTag = Tag(TriggernometryInterop.Commands.Single());
                TriggernometryInterop.OnCommand = command =>
                {
                    if (!IsRemoval(command) || Tag(command) != oldTag) return;
                    actor.Cast(2);
                    replaced.Set();
                };
                scene.Scheduler.Start();
                Assert(replaced.Wait(5000), "读条完成时未移除预兆。");
                scene.Scheduler.Dispose();
                scene.AssertNoFailure();
                Assert(actor.Entity.CastingActionId == 2 && actor.Entity.PlayedTimelines.Count == 0 &&
                    actor.Entity.BaseTimelineResetCount == 0,
                    "旧完成操作不得覆盖其清理回调同步启动的新读条。");
            }
        }

        public static void CastCancellationReentryPreservesTheNewerCast()
        {
            foreach (var executeOuter in new[] { false, true })
            using (var scene = new CastTestScene())
            {
                scene.AddAction(1, 1, omen: true);
                scene.AddAction(2, 1);
                scene.AddAction(3, 1);
                var actor = scene.Actor(0);
                actor.Cast(1);
                var oldTag = Tag(TriggernometryInterop.Commands.Single());
                TriggernometryInterop.OnCommand = command =>
                {
                    if (IsRemoval(command) && Tag(command) == oldTag) actor.Cast(3);
                };
                if (executeOuter) actor.Execute(2);
                else actor.Cast(2);
                Assert(actor.Entity.CastingActionId == 3 && actor.Entity.CastStarts.SequenceEqual(new uint[] { 1, 3 }) &&
                    actor.Entity.PlayedTimelines.Count == 0 && actor.Entity.BaseTimelineResetCount == 0 &&
                    scene.Scheduler.PendingCount == 1,
                    "取消回调启动新读条后，较早的 Cast 或 Execute 必须让出控制权，不能重复登记或覆盖新动画。");
            }
        }

        public static void CastCancellationReentryCannotResumeAfterCleanup()
        {
            foreach (var executeOuter in new[] { false, true })
            foreach (var disposeController in new[] { false, true })
            using (var scene = new CastTestScene())
            {
                scene.AddAction(1, 1, omen: true);
                scene.AddAction(2, 1);
                var actor = scene.Actor(0);
                actor.Cast(1);
                var oldTag = Tag(TriggernometryInterop.Commands.Single());
                TriggernometryInterop.OnCommand = command =>
                {
                    if (!IsRemoval(command) || Tag(command) != oldTag) return;
                    if (disposeController) scene.Casts.Dispose();
                    else actor.Dispose();
                };
                if (executeOuter) actor.Execute(2);
                else actor.Cast(2);
                Assert(actor.Entity.IsAlive && actor.Entity.CastingActionId == 0 &&
                    actor.Entity.CastStarts.SequenceEqual(new uint[] { 1 }) &&
                    actor.Entity.PlayedTimelines.Count == 0 && actor.Entity.BaseTimelineResetCount == 0 &&
                    scene.Scheduler.PendingCount == 0,
                    "取消回调解除角色或释放控制器后，旧调用不得在仍存活的借用实体上重新读条或播放动作。");
            }
        }

        public static void CompletionRemovalPreservesAnExternalReplacementCast()
        {
            using (var scene = new CastTestScene())
            using (var replaced = new ManualResetEventSlim())
            {
                scene.AddAction(1, 0.4f, omen: true);
                var actor = scene.Actor(0);
                actor.Cast(1);
                var oldTag = Tag(TriggernometryInterop.Commands.Single());
                TriggernometryInterop.OnCommand = command =>
                {
                    if (!IsRemoval(command) || Tag(command) != oldTag) return;
                    // 外部原生读条不会进入本控制器的字典，不能仅靠字典判断动画所有权。
                    actor.Entity.SetCastInfo(999, 2);
                    replaced.Set();
                };
                scene.Scheduler.Start();
                Assert(replaced.Wait(5000), "完成清理没有触发外部替代读条。");
                scene.Scheduler.Dispose();
                scene.AssertNoFailure();
                Assert(actor.Entity.CastingActionId == 999 && actor.Entity.FinalizedCasts.SequenceEqual(new uint[] { 1 }) &&
                    actor.Entity.PlayedTimelines.Count == 0 && actor.Entity.BaseTimelineResetCount == 0,
                    "完成清理期间出现外部读条后，不得重置其基础动画或播放旧技能完成动作。");
            }
        }

        public static void DelayedOmenSamplesCurrentPositionWithInvariantNumbers()
        {
            using (var scene = new CastTestScene())
            using (var shown = new ManualResetEventSlim())
            {
                scene.AddAction(1, 1, omen: true);
                var actor = scene.Actor(0);
                string command = null;
                TriggernometryInterop.OnCommand = value =>
                {
                    if (IsRemoval(value)) return;
                    command = value;
                    shown.Set();
                };
                actor.Cast(1, omenDelay: 0.02f);
                scene.Scheduler.Schedule(0, () =>
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                    actor.Pos3D = new Vector3(1.5f, 2.5f, 3.5f);
                    actor.Heading = 0.5f;
                });
                scene.Scheduler.Start();
                Assert(shown.Wait(5000), "延迟预兆未出现。");
                scene.Scheduler.Dispose();
                scene.AssertNoFailure();
                Assert(command.Contains("O: 1.5, 2.5, 3.5") && command.Contains("θ: 0.5"),
                    "延迟渲染必须采样当前位置和朝向，并使用不受区域设置影响的小数点。");
            }
        }

        public static void CastDisposeCleansEveryActorAfterCallbackFailure()
        {
            using (var scene = new CastTestScene())
            {
                scene.AddAction(1, 1, omen: true);
                var first = scene.Actor(0);
                var second = scene.Actor(1);
                first.Cast(1);
                second.Cast(1);
                var removalCount = 0;
                TriggernometryInterop.OnCommand = command =>
                {
                    if (IsRemoval(command) && ++removalCount == 1) throw new InvalidOperationException("一个回调失败");
                };
                try { scene.Casts.Dispose(); throw new Exception("预期抛出汇总的清理错误。"); }
                catch (AggregateException error) { Assert(error.InnerExceptions.Count == 1, "只应报告失败的回调。"); }
                TriggernometryInterop.OnCommand = null;
                Assert(scene.Scheduler.PendingCount == 0 && first.Entity.CastingActionId == 0 && second.Entity.CastingActionId == 0 &&
                    removalCount == 2, "一次外部清理失败后，不得让另一模拟角色的读条或预兆继续运行。");
                scene.Casts.Dispose();
                Assert(TriggernometryInterop.Commands.Count(IsRemoval) == 2, "重复释放不得重复执行清理回调。");
            }
        }

        public static void CompletionReadFailureStillRemovesOmenAndState()
        {
            using (var scene = new CastTestScene())
            {
                scene.AddAction(1, 0.4f, omen: true);
                var actor = scene.Actor(0);
                actor.Cast(1);
                var shownTag = Tag(TriggernometryInterop.Commands.Single());
                var expected = new InvalidOperationException("访问读条信息时实体消失");
                actor.Entity.BeforeCastingActionIdRead = () =>
                {
                    scene.World.Registry.Reset();
                    throw expected;
                };
                scene.Scheduler.Start();
                Assert(SpinWait.SpinUntil(() => !scene.Scheduler.IsRunning, 5000),
                    "完成节点未触发模拟的原生读取错误。");
                scene.Scheduler.Dispose();
                Assert(ReferenceEquals(scene.Failure, expected), "调度器必须报告原始的原生读取错误。");
                Assert(TriggernometryInterop.Commands.Where(IsRemoval).Select(Tag).SequenceEqual(new[] { shownTag }),
                    "读条信息读取失败后，完成清理仍必须移除可见预兆。");
                Assert(actor.Entity.FinalizedCasts.Count == 0 && actor.Entity.PlayedTimelines.Count == 0,
                    "读取失败后不得写入已消失的实体。");
                scene.Casts.Dispose();
                Assert(scene.Scheduler.PendingCount == 0 && TriggernometryInterop.Commands.Count(IsRemoval) == 1,
                    "完成操作必须移除自身状态，避免后续释放重复清理或遗留排队任务。");
            }
        }

        private static bool IsRemoval(string command) => command.StartsWith("Action: Remove", StringComparison.Ordinal);
        private static string Tag(string command)
            => command.Split('\n').Single(line => line.StartsWith("Tag: ", StringComparison.Ordinal)).Substring(5).Trim();
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private sealed class CastTestScene : IDisposable
        {
            internal readonly EntityTestWorld World = new EntityTestWorld();
            internal readonly SimScheduler Scheduler;
            internal readonly SimCastController Casts;
            internal readonly SimMovementController Movement;
            private Exception _failure;
            internal Exception Failure => _failure;

            internal CastTestScene()
            {
                TriggernometryInterop.Reset();
                GameDataService.Instance.GetSheet<ActionRow>().Clear();
                Scheduler = new SimScheduler((error, _) => _failure = error);
                Casts = new SimCastController(Scheduler);
                Movement = new SimMovementController(Scheduler);
            }

            internal SimActor Actor(ushort slot) => new SimActor(World.CreateCharacter(slot), Casts, Movement);
            internal void AddAction(int id, float castTime, bool omen = false)
            {
                GameDataService.Instance.GetSheet<ActionRow>()[id] = new ActionRow
                {
                    RowId = (uint)id, Cast100ms = checked((ushort)(castTime * 10)), AnimationEnd = (short)(100 + id),
                    CastType = 2, EffectRange = 5,
                    Omen = new Lumina.Excel.RowRef<Lumina.Excel.Sheets.Omen> { RowId = (uint)(omen ? 1 : 0), ValueNullable = new Lumina.Excel.Sheets.Omen { Path = "test_omen_" + id } }
                };
            }

            internal void RunPast(double seconds)
            {
                using (var finished = new ManualResetEventSlim())
                {
                    Scheduler.Schedule(seconds, finished.Set);
                    Scheduler.Start();
                    Assert(finished.Wait(5000), "调度器未执行到读条后的验证节点：" + _failure);
                    Scheduler.Dispose();
                    AssertNoFailure();
                }
            }

            internal void AssertNoFailure() => Assert(_failure == null, "读条工作线程出错：" + _failure);

            public void Dispose()
            {
                Scheduler.Dispose();
                TriggernometryInterop.OnCommand = null;
                try { Casts.Dispose(); }
                finally { Movement.Dispose(); TriggernometryInterop.Reset(); }
            }
        }
    }
}
