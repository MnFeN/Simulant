using Simulant.Core;
using Simulant.Game.Data;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading;
using ActionRow = Lumina.Excel.Sheets.Action;

namespace Simulant.Tests
{
    internal static class CastTests
    {
        public static void ActLogsUseTheCastCenterAndOneSequenceForAllTargets()
        {
            using (var scene = new CastTestScene())
            {
                var events = new List<(int Id, string[] Fields, DateTimeOffset? Timestamp)>();
                var config = new Configuration();
                scene.AddAction(0x123, 2);
                var caster = scene.Actor(0);
                var target1 = scene.Actor(1);
                var target2 = scene.Actor(2);
                caster.Pos3D = new Vector3(100, 90, 0);
                caster.Heading = 0;
                caster.CombatLog = new SimCombatLog(() => config.GenerateActLogs,
                    (id, payload, time) => events.Add((id, payload.Split('|'), time)), null);

                caster.ExecuteActionEffect(0x123, target1);
                Assert(events.Count == 0, "日志选项关闭时不能记录技能。");

                var culture = Thread.CurrentThread.CurrentCulture;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                    config.GenerateActLogs = true;
                    caster.Cast(0x123, omenDelay: null, target: target1, omenPosition: new Vector3(90, 80, 5),
                        omenHeading: -(float)Math.PI / 2, castTime: 1.25f, executeOnFinish: false);
                    caster.StopCasting();
                    caster.ExecuteActionEffectAt(0x123, new Vector3(90, 80, 5), -(float)Math.PI / 2,
                        hitTargets: new[] { target1, target2 });
                    caster.ExecuteActionEffectAt(0x123, new Vector3(90, 80, 5), 0);
                }
                finally
                {
                    Thread.CurrentThread.CurrentCulture = culture;
                }

                Assert(events.Select(entry => entry.Id).SequenceEqual(new[] { 20, 263, 23, 22, 22, 264, 21, 264 }),
                    "开始咏唱、敌方中断、多目标与空目标技能应分别记录对应的协议行。");
                Assert(events[0].Timestamp == events[1].Timestamp &&
                    events[0].Fields[6] == "1.250" && events[0].Fields[10] == "0.00" &&
                    Math.Abs(double.Parse(events[1].Fields[2], CultureInfo.InvariantCulture) - 90) < 0.02 &&
                    Math.Abs(double.Parse(events[1].Fields[5], CultureInfo.InvariantCulture) + Math.PI / 2) < 0.002,
                    "传统咏唱行使用实体面向，107 行使用技能落点和独立的预兆面向。");
                var first = events[3].Fields;
                var second = events[4].Fields;
                var extra = events[5].Fields;
                Assert(first.Length == 52 && second.Length == 52 && extra.Length == 9 &&
                    first[42] == second[42] && first[42] == extra[2] &&
                    first[43] == "0" && second[43] == "1" && first[44] == "2" && second[44] == "2" &&
                    events[3].Timestamp == events[5].Timestamp,
                    "两条 16 行必须具有匹配的时间、序号、索引及总目标数，且与 108 的序号对应。");
                Assert(events[6].Fields[4] == "E0000000" && events[6].Fields[44] == "0",
                    "无命中仍发送无目标的 15 行，目标数为零。");
            }
        }

        public static void TetherHelpersPreserveNativeArgumentsAndActLogs()
        {
            using (var scene = new CastTestScene())
            {
                var events = new List<(int Id, string[] Fields)>();
                var source = scene.Actor(0);
                var target = scene.Actor(1);
                source.CombatLog = new SimCombatLog(() => true,
                    (id, payload, time) => events.Add((id, payload.Split('|'))), null);

                source.TetherTo(target, 134);
                Assert(source.Entity.LastActorControl == (Simulant.Game.ActorControlCategory.Tether, 0u, 134u, target.Id, 15u),
                    "连线默认使用槽位 0、指定目标和宽度参数 15。");
                var logged = events.Single();
                Assert(logged.Id == 35 && logged.Fields[2] == target.Id.ToString("X8") &&
                    logged.Fields[5] == "0000" && logged.Fields[6] == "0086" && logged.Fields[8] == "000F",
                    "连线辅助方法应使用同一个 ACT 日志入口。");

                source.CancelTether();
                Assert(source.Entity.LastActorControl == (Simulant.Game.ActorControlCategory.TetherCancel, 0u, 0u, 0u, 0u),
                    "默认取消应无条件清除槽位 0。");
                source.CancelTether(slot: 1, tetherId: 134);
                Assert(source.Entity.LastActorControl == (Simulant.Game.ActorControlCategory.TetherCancel, 1u, 134u, 0u, 0u)
                    && events.Count == 1, "按编号取消应传入指定槽位和编号，且保留原有日志行为。");
            }
        }

        public static void CustomCastTimeControlsNativeCastCompletionAndOmen()
        {
            using (var scene = new CastTestScene())
            {
                scene.AddAction(1, 10, omen: true);
                var fast = scene.Actor(0);
                var normal = scene.Actor(1);
                fast.Cast(1, omenDelay: 2, castTime: 1);
                normal.Cast(1, omenDelay: null);
                Assert(fast.Entity.CastDuration == 1 && Math.Abs(normal.Entity.CastDuration - 9.7) < 0.001,
                    "指定时长必须写入原生读条；未指定时应从技能表扣除 0.3 秒结算间隔。");
                Assert(scene.Casts.Remaining(fast) == 1 && Math.Abs(scene.Casts.Remaining(normal) - 9.7) < 0.001,
                    "读条剩余时间必须使用实际咏唱时长。");
                scene.Scheduler.Schedule(1.1, () =>
                    Assert(fast.Entity.CastingActionId == 0 && fast.Entity.PlayedTimelines.Count == 0,
                        "读条结束后必须保留结算间隔，不能提前播放技能。"));
                scene.RunPast(1.4);
                Assert(fast.Entity.CastingActionId == 0 && fast.Entity.PlayedTimelines.Single() == 101,
                    "缩短后的读条必须按新期限完成并播放技能。");
                Assert(normal.Entity.CastingActionId == 1 && normal.Entity.PlayedTimelines.Count == 0,
                    "同技能的普通读条不应被加速。");
                Assert(VfxManager.Commands.Count == 0,
                    "超过新咏唱期限的延迟预兆不应出现。");
            }
        }

        public static void CompletedCastCanMoveButStopCancelsPendingEffect()
        {
            using (var scene = new CastTestScene())
            {
                scene.AddAction(1, 0.5f, omen: true);
                var actor = scene.Actor(0);
                var target = scene.Actor(1);
                actor.Cast(1, target: target);
                scene.Scheduler.Schedule(0.3, () =>
                {
                    Assert(actor.Entity.CastingActionId == 0 && scene.Casts.Remaining(actor) == 0,
                        "0.5 秒表时长应在 0.2 秒结束读条，不再限制移动。");
                    var pending = scene.Scheduler.PendingCount;
                    scene.Casts.CancelVisual(actor);
                    Assert(scene.Scheduler.PendingCount == pending,
                        "已经完成读条后开始移动，不能取消等待中的结算演出。");
                    actor.StopCasting();
                    Assert(scene.Scheduler.PendingCount == pending - 1,
                        "显式停止必须仍能取消读条结束后的演出节点。");
                });
                scene.RunPast(0.6);
                Assert(actor.Entity.ActionEffects.Count == 0, "停止后不得出现迟到的技能演出。");
            }
        }

        public static void ManualResolutionDoesNotDuplicateCastEffect()
        {
            using (var scene = new CastTestScene())
            {
                scene.AddAction(1, 0.5f, omen: true);
                var actor = scene.Actor(0);
                actor.Cast(1, executeOnFinish: false);
                scene.Scheduler.Schedule(0.55, () =>
                {
                    Assert(actor.Entity.CastingActionId == 0 && actor.Entity.PlayedTimelines.Count == 0,
                        "显式判定的读条只能清理预兆，不得先自动播放一次技能。");
                    actor.Execute(1);
                });
                scene.RunPast(0.65);
                Assert(actor.Entity.PlayedTimelines.SequenceEqual(new ushort[] { 101 }),
                    "机制判定应只播放一次结算演出。");
            }
        }

        public static void ReplacingCastCancelsOldCompletionAndDelayedOmen()
        {
            using (var scene = new CastTestScene())
            {
                scene.AddAction(1, 1, omen: true);
                scene.AddAction(2, 0.04f);
                var actor = scene.Actor(0);
                actor.Cast(1, omenDelay: 0.05f);
                Assert(scene.Scheduler.PendingCount == 3, "延迟预兆、读条完成和结算演出应各有一个节点。");
                actor.Cast(2);
                Assert(scene.Scheduler.PendingCount == 1, "替换读条必须移除旧读条和演出的全部节点。");
                scene.RunPast(0.08);
                Assert(actor.Entity.CastStarts.SequenceEqual(new uint[]
                {
                    1, 2
                }), "两个请求都必须启动各自的原生读条。");
                Assert(actor.Entity.FinalizedCasts.SequenceEqual(new uint[]
                {
                    1, 2
                }), "替换与完成操作必须分别结束对应的读条。");
                Assert(actor.Entity.PlayedTimelines.SequenceEqual(new ushort[]
                {
                    102
                }), "只有替换后的读条可以播放完成动作。");
                Assert(VfxManager.Commands.Count == 0, "已取消的延迟预兆不得出现。");
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
                Assert(visible.Entity.CastingActionId == 0 && scene.Scheduler.PendingCount == 3,
                    "StopCasting 必须仅清除指定模拟角色的读条，并保留另一角色的调度。");
                scene.Casts.Dispose();
                scene.Casts.Dispose();
                Assert(delayed.Entity.CastingActionId == 0 && scene.Scheduler.PendingCount == 0,
                    "释放必须清除所有剩余读条及其延迟效果。");
                visible.Cast(1);
                Assert(scene.Scheduler.PendingCount == 0, "已释放的读条控制器不得接受后续模拟角色回调。");
                scene.RunPast(0.08);
                Assert(VfxManager.Commands.Count(command => !IsRemoval(command)) == 1 &&
                    VfxManager.Commands.Count(IsRemoval) == 1,
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
                Assert(actor.Entity.CastStarts.SequenceEqual(new uint[]
                {
                    1, 2
                }) &&
                    actor.Entity.FinalizedCasts.SequenceEqual(new uint[]
                {
                    1, 2
                }) &&
                    actor.Entity.PlayedTimelines.SequenceEqual(new ushort[]
                {
                    102
                }),
                    "停止后必须能重新读条，且只有新读条可以播放完成动作。");
                Assert(VfxManager.Commands.Count(command => !IsRemoval(command)) == 1 &&
                    VfxManager.Commands.Count(IsRemoval) == 1,
                    "重复停止只能移除一次旧读条的预兆。");
            }
        }

        public static void ExplicitOmenTransformDoesNotReadPendingActorMove()
        {
            using (var scene = new CastTestScene())
            {
                scene.AddAction(1, 1, omen: true);
                var actor = scene.Actor(0);
                actor.Pos = new Vector2(100, 100);
                actor.Heading = 0;
                actor.Cast(1, omenPosition: new Vector3(87, 92, 3), omenHeading: 1.25f);
                var command = VfxManager.Commands.Single();
                Assert(command.Position == new Vector3(87, 92, 3) && command.Heading == 1.25f,
                    "实体仍在场中朝南时，光波预兆也必须使用调用者指定的落点与方向。");
                actor.StopCasting();
                actor.Pos = new Vector2(101, 102);
                actor.Cast(1);
                command = VfxManager.Commands.Last();
                Assert(command.Position == new Vector3(101, 102, 0) && command.Heading == 0,
                    "下一次未指定位置的读条必须恢复读取实体当前位置，不能继承旧预兆的覆盖值。");
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
                var shows = VfxManager.Commands.Where(command => !IsRemoval(command)).Select(Tag).ToArray();
                Assert(shows.Length == 2 && shows.Distinct().Count() == 2, "两个模拟角色不得共享预兆移除标签。");
                first.StopCasting();
                Assert(VfxManager.Commands.Where(IsRemoval).Select(Tag).SequenceEqual(new[]
                {
                    shows[0]
                }),
                    "取消一个模拟角色的读条时，只能移除该角色的预兆。");
                second.Cast(1);
                var newShows = VfxManager.Commands.Where(command => !IsRemoval(command)).Select(Tag).ToArray();
                Assert(newShows.Distinct().Count() == 3, "重新读条必须使用新标签，不得复用上次读条的标签。");
                Assert(VfxManager.Commands.Where(IsRemoval).Select(Tag).SequenceEqual(shows),
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

                VfxManager.OnCommand = command =>
                {
                    if (IsRemoval(command) || replaced)
                        return;

                    replaced = true;
                    actor.Cast(2);
                    throw expected;
                };

                try
                {
                    actor.Cast(1);
                    throw new Exception("预期抛出原始回调错误。");
                }
                catch (InvalidOperationException error)
                {
                    Assert(ReferenceEquals(error, expected), "失败的旧回调必须保留原始错误。");
                }

                VfxManager.OnCommand = null;
                var shown = VfxManager.Commands.Where(command => !IsRemoval(command)).Select(Tag).ToArray();
                var removed = VfxManager.Commands.Where(IsRemoval).Select(Tag).ToArray();
                Assert(shown.Length == 2 && removed.SequenceEqual(new[]
                {
                    shown[0]
                }),
                    "同步替换必须移除旧的可见预兆并保留新预兆。");
                Assert(actor.Entity.CastingActionId == 2 && scene.Scheduler.PendingCount == 2,
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
                var oldTag = Tag(VfxManager.Commands.Single());

                VfxManager.OnCommand = command =>
                {
                    if (!IsRemoval(command) || Tag(command) != oldTag)
                        return;

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
            foreach (var executeOuter in new[]
            {
                false,
                true
            }

            )
                using (var scene = new CastTestScene())
                {
                    scene.AddAction(1, 1, omen: true);
                    scene.AddAction(2, 1);
                    scene.AddAction(3, 1);
                    var actor = scene.Actor(0);
                    actor.Cast(1);
                    var oldTag = Tag(VfxManager.Commands.Single());

                    VfxManager.OnCommand = command =>
                    {
                        if (IsRemoval(command) && Tag(command) == oldTag)
                            actor.Cast(3);
                    };

                    if (executeOuter)
                        actor.Execute(2);
                    else
                        actor.Cast(2);

                    Assert(actor.Entity.CastingActionId == 3 && actor.Entity.CastStarts.SequenceEqual(new uint[]
                    {
                    1, 3
                    }) &&
                    actor.Entity.PlayedTimelines.Count == 0 && actor.Entity.BaseTimelineResetCount == 0 &&
                    scene.Scheduler.PendingCount == 2,
                    "取消回调启动新读条后，较早的 Cast 或 Execute 必须让出控制权，不能重复登记或覆盖新动画。");
                }
        }

        public static void CastCancellationReentryCannotResumeAfterCleanup()
        {
            foreach (var executeOuter in new[]
            {
                false,
                true
            }

            )
                foreach (var disposeController in new[]
                {
                    false,
                    true
                }

                )
                    using (var scene = new CastTestScene())
                    {
                        scene.AddAction(1, 1, omen: true);
                        scene.AddAction(2, 1);
                        var actor = scene.Actor(0);
                        actor.Cast(1);
                        var oldTag = Tag(VfxManager.Commands.Single());

                        VfxManager.OnCommand = command =>
                        {
                            if (!IsRemoval(command) || Tag(command) != oldTag)
                                return;

                            if (disposeController)
                                scene.Casts.Dispose();
                            else
                                actor.Dispose();
                        };

                        if (executeOuter)
                            actor.Execute(2);
                        else
                            actor.Cast(2);

                        Assert(actor.Entity.IsAlive && actor.Entity.CastingActionId == 0 &&
                    actor.Entity.CastStarts.SequenceEqual(new uint[]
                        {
                    1
                        }) &&
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
                var oldTag = Tag(VfxManager.Commands.Single());

                VfxManager.OnCommand = command =>
                {
                    if (!IsRemoval(command) || Tag(command) != oldTag)
                        return;

                    // 外部原生读条不会进入本控制器的字典，不能仅靠字典判断动画所有权。
                    actor.Entity.SetCastInfo(999, 2);
                    replaced.Set();
                };

                scene.Scheduler.Start();
                Assert(replaced.Wait(5000), "完成清理没有触发外部替代读条。");
                scene.Scheduler.Dispose();
                scene.AssertNoFailure();
                Assert(actor.Entity.CastingActionId == 999 && actor.Entity.FinalizedCasts.SequenceEqual(new uint[]
                {
                    1
                }) &&
                    actor.Entity.PlayedTimelines.Count == 0 && actor.Entity.BaseTimelineResetCount == 0,
                    "完成清理期间出现外部读条后，不得重置其基础动画或播放旧技能完成动作。");
            }
        }

        public static void DelayedOmenSamplesCurrentPosition()
        {
            using (var scene = new CastTestScene())
            using (var shown = new ManualResetEventSlim())
            {
                scene.AddAction(1, 1, omen: true);
                var actor = scene.Actor(0);
                VfxManager.Call command = null;

                VfxManager.OnCommand = value =>
                {
                    if (IsRemoval(value))
                        return;

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
                Assert(command.Position == new Vector3(1.5f, 2.5f, 3.5f) && command.Heading == 0.5f,
                    "延迟渲染必须采样当前位置和朝向。");
            }
        }

        public static void OmenPassesDurationHeadingAndScale()
        {
            NumericCultureTests.InCommaCultures(() =>
            {
                using (var scene = new CastTestScene())
                {
                    scene.AddAction(1, 4, omen: true);
                    var action = GameDataService.Instance.GetSheet<ActionRow>()[1];
                    action.CastType = 4;
                    action.XAxisModifier = 7;
                    var actor = scene.Actor(0);
                    new SimOmenRenderer().Show(actor, action, 3.7, "区域设置验证",
                        new Vector3(100.25f, -2.5f, 0.75f), -0.7853982f);
                    var command = VfxManager.Commands.Single();
                    Assert(command.Duration == 3.7 && command.Heading == -0.7853982f
                         && command.Position == new Vector3(100.25f, -2.5f, 0.75f) && command.Scale.X == 3.5f,
                        "应向 VFX 传入预兆时长、负角度、坐标和线条半宽。");
                }
            });
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

                VfxManager.OnCommand = command =>
                {
                    if (IsRemoval(command) && ++removalCount == 1)
                        throw new InvalidOperationException("一个回调失败");
                };

                try
                {
                    scene.Casts.Dispose();
                    throw new Exception("预期抛出汇总的清理错误。");
                }
                catch (AggregateException error)
                {
                    Assert(error.InnerExceptions.Count == 1, "只应报告失败的回调。");
                }

                VfxManager.OnCommand = null;
                Assert(scene.Scheduler.PendingCount == 0 && first.Entity.CastingActionId == 0 && second.Entity.CastingActionId == 0 &&
                    removalCount == 2, "一次外部清理失败后，不得让另一模拟角色的读条或预兆继续运行。");
                scene.Casts.Dispose();
                Assert(VfxManager.Commands.Count(IsRemoval) == 2, "重复释放不得重复执行清理回调。");
            }
        }

        public static void CompletionReadFailureStillRemovesOmenAndState()
        {
            using (var scene = new CastTestScene())
            {
                scene.AddAction(1, 0.4f, omen: true);
                var actor = scene.Actor(0);
                actor.Cast(1);
                var shownTag = Tag(VfxManager.Commands.Single());
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
                Assert(VfxManager.Commands.Where(IsRemoval).Select(Tag).SequenceEqual(new[]
                {
                    shownTag
                }),
                    "读条信息读取失败后，完成清理仍必须移除可见预兆。");
                Assert(actor.Entity.FinalizedCasts.Count == 0 && actor.Entity.PlayedTimelines.Count == 0,
                    "读取失败后不得写入已消失的实体。");
                scene.Casts.Dispose();
                Assert(scene.Scheduler.PendingCount == 0 && VfxManager.Commands.Count(IsRemoval) == 1,
                    "完成操作必须移除自身状态，避免后续释放重复清理或遗留排队任务。");
            }
        }

        private static bool IsRemoval(VfxManager.Call command) => command.IsRemoval;
        private static string Tag(VfxManager.Call command) => command.Tag;
        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
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
                VfxManager.Reset();
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
                            Path = "test_omen_" + id
                        }
                    }
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
                VfxManager.OnCommand = null;

                try
                {
                    Casts.Dispose();
                }
                finally
                {
                    Movement.Dispose();
                    VfxManager.Reset();
                }
            }
        }
    }
}
