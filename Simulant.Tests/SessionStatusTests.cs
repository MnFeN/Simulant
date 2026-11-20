using Simulant.Core.Entity;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;

namespace Simulant.Tests
{
    internal static class SessionStatusTests
    {
        public static void InvulnerabilityUsesEachTankJobAndDoesNotBypassMechanicFailures()
        {
            var jobs = new[] { Simulant.Game.FFCS.Client.Game.Character.Job.PLD,
                Simulant.Game.FFCS.Client.Game.Character.Job.WAR, Simulant.Game.FFCS.Client.Game.Character.Job.DRK,
                Simulant.Game.FFCS.Client.Game.Character.Job.GNB };
            var actions = new uint[] { 30, 43, 3638, 16152 };
            var statuses = new ushort[] { 82, 409, 810, 1836 };
            using (var scene = new Scene())
                for (var i = 0; i < jobs.Length; i++)
                {
                    scene.Player.Job = jobs[i];
                    Assert(Simulant.Core.SimActionService.ParseAction("ac 无敌", jobs[i]) == actions[i], "通用无敌命令应按职业选择技能。");
                    Assert(scene.Manager.UseInvulnerability(scene.Player), "坦克应能使用本职业无敌。");
                    Assert(scene.Player.IsInvulnerable && scene.Statuses.GetStatus(0).StatusId.Get() == statuses[i], "四个坦克应获得各自的无敌状态。");
                    var mechanic = new Simulant.Simulation.Mechanics.AreaMechanic
                    {
                        Area = Simulant.Simulation.Geometry.ActionArea.Circle(2),
                        MaxPlayers = 0
                    };
                    mechanic.Claim(new Simulant.Simulation.Mechanics.MechanicContext(scene.Manager, scene._scheduler, new[] { scene.Player }));
                    Assert(!mechanic.EvaluateForGroup(Simulant.Simulation.Mechanics.PartySnapshot.Capture(new[] { scene.Player })).Success,
                        "无敌不能让普通机制判定自动免错。");
                    mechanic.Stop();
                    scene.Manager.RemoveStatus(scene.Player, statuses[i]);
                }
        }

        public static void UnderfilledStacksApplyMortalityToActualParticipantsOnly()
        {
            using (var scene = new Scene())
            {
                var actors = new[] { scene.Player, scene.Source };
                scene.Manager.MarkUnderfilledStack(actors, 2, scene.Source);
                Assert(!scene.Player.Entity.HasStatus(4372), "足额分摊不应附加死亡刻印。");
                scene.Manager.MarkUnderfilledStack(new[] { scene.Player }, 4, scene.Source);
                Assert(scene.Player.Entity.HasStatus(4372) && !scene.Source.Entity.HasStatus(4372), "人数不足只惩罚实际参与者。");
                scene.Manager.MarkUnderfilledStack(new[] { scene.Player }, 4, scene.Source);
                Assert(scene.Statuses.GetStatus(0).Param.Get() == 2, "重复人数不足应增加死亡刻印层数。");
            }
        }

        public static void LocalActionsRespectStunFromOtherManagersAndStatusRemoval()
        {
            Simulant.Game.Data.GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Status>()[4163] =
                new Lumina.Excel.Sheets.Status
                {
                    LockMovement = true,
                    LockActions = true
                };
            Simulant.Game.Data.GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Status>()[13] =
                new Lumina.Excel.Sheets.Status
                {
                    LockMovement = true
                };
            using (var scene = new Scene())
            {
                scene.Manager.Party.Add(scene.Source);
                scene.Manager.Party.Add(scene.Player);
                scene.Manager.UseLocalAction(scene.Player, 7548);
                Assert(scene.Player.IsKnockbackImmune && !scene.Source.IsKnockbackImmune,
                    "本机技能只影响指定玩家，不影响其他角色。");
                Assert(scene.Statuses.GetStatus(0).StatusId.Get() == 1209 && scene.Statuses.GetStatus(0).RemainingTime.Get() == 6,
                    "亲疏自行应施加六秒对应状态。");
                var effect = KnockbackEffect.Directional(0, 20, 0.8f);
                SimKnockback.Resolve(scene._scheduler, new[] { scene.Player }, effect);
                Assert(scene.Player.Entity.NativeKnockbackCalls == 0, "有防击退状态时不应调用本机击退。");
                SimKnockback.Resolve(scene._scheduler, new[] { scene.Player }, effect, ignoreImmunity: true);
                Assert(scene.Player.Entity.NativeKnockbackCalls == 1, "不可免疫的击退应仍能执行。");
                scene.Manager.RemoveStatus(scene.Player, 1209);
                SimKnockback.Resolve(scene._scheduler, new[] { scene.Player }, effect);
                Assert(scene.Player.Entity.NativeKnockbackCalls == 2, "状态移除后恢复击退。");

                var external = scene.Statuses.GetStatus(0);
                external.StatusId.Value = 4163;
                external.RemainingTime.Value = 9;
                scene.Manager.UseLocalAction(scene.Player, 7559);
                Assert(!scene.Player.IsKnockbackImmune, "被回返眩晕后不能通过默语补开防击退。");
                external.StatusId.Value = 0;
                scene.Manager.UseLocalAction(scene.Player, 7559);
                Assert(scene.Player.IsKnockbackImmune && scene.Statuses.GetStatus(0).StatusId.Get() == 160,
                    "解除眩晕后沉稳咏唱命令应正常生效。");
                scene.Manager.RemoveStatus(scene.Player, 160);
                scene.Manager.SetStatus(scene.Player, 13, 9, 0, scene.Source);
                scene.Manager.UseLocalAction(scene.Player, 7548);
                Assert(scene.Player.IsKnockbackImmune, "止步只禁止移动，不能阻止使用防击退技能。");
                scene.Manager.Clear();
                Assert(scene.Statuses.GetStatus(0).StatusId.Get() == 0, "停止时必须清理命令产生的状态。");
            }
        }

        public static void LocalActionsPlayPlayerTimelineAndKeepNetworkVisuals()
        {
            var actions = new uint[] { 3, 7548, 7559 };
            var sheet = Simulant.Game.Data.GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Action>();
            foreach (var id in actions)
                sheet[(int)id] = new Lumina.Excel.Sheets.Action { RowId = id, AnimationEnd = (short)(id + 100) };
            using (var scene = new Scene())
            {
                foreach (var id in actions)
                {
                    scene.Manager.UseLocalAction(scene.Player, id);
                    Assert(scene.Player.Entity.PlayedTimelines.Contains((ushort)(id + 100)),
                        "本机模拟技能必须补播技能表中的角色动作。");
                }
                scene.Manager.UseLocalAction(scene.Source, 7548);
                Assert(scene.Source.Entity.PlayedTimelines.Count == 0,
                    "网络队员继续使用技能结算演出，不重复补播本机动作。");
                Assert(scene.Player.Entity.HasStatus(50) && scene.Player.IsKnockbackImmune,
                    "补播动作不能改变冲刺和防击退的状态效果。");
            }
        }

        public static void KnockbackImmunityReadsActualStatusesAcrossManagers()
        {
            using (var scene = new Scene())
            {
                var fake = scene.Manager.Attach(scene.Spawner.World.CreateCharacter(2));
                fake.Job = Simulant.Game.FFCS.Client.Game.Character.Job.SCH;
                scene.Manager.UseKnockbackImmunity(fake);
                Assert(fake.Entity.Native.StatusManager.GetStatus(0).StatusId.Get() == 160,
                    "读条职业的假队员应使用沉稳咏唱。");
                SimKnockback.Resolve(scene._scheduler, new[] { fake }, KnockbackEffect.Directional(0, 20, 0.8f));
                Assert(fake.Entity.NetworkMoves.Count == 0, "假队员有防击退时也不能发送击退移动包。");

                scene.Statuses.GetStatus(0).StatusId.Value = 1209;
                scene.Statuses.GetStatus(0).RemainingTime.Value = 6;
                Assert(scene.Player.IsKnockbackImmune, "副本必须识别其他管理器施加的防击退状态。");
                scene.Statuses.GetStatus(0).StatusId.Value = 0;
                scene.Manager.RemoveStatus(fake, 160);
                Assert(!scene.Player.IsKnockbackImmune && !fake.IsKnockbackImmune,
                    "实际状态移除后应恢复击退。");
            }
        }

        public static void SessionStatusesPreserveOriginalAndRefreshOwnedSlot()
        {
            using (var scene = new Scene())
            {
                var original = scene.Statuses.GetStatus(0);
                original.StatusId.Value = 100;
                original.RemainingTime.Value = 15;
                scene.Manager.SetStatus(scene.Player, 200, 30, 2, scene.Source);
                scene.Manager.SetStatus(scene.Player, 200, 45, 3, scene.Source);
                var status = scene.Statuses.GetStatus(1);
                Assert(original.StatusId.Get() == 100 && original.RemainingTime.Get() == 15 &&
                    status.StatusId.Get() == 200 && status.Param.Get() == 3 && status.RemainingTime.Get() == 45 &&
                    status.SourceObject.Get().Id == scene.Source.Id && scene.Statuses.GetStatus(2).StatusId.Get() == 0,
                    "重复点名必须更新自己的槽，并保留先前状态。");
                scene.Manager.SetStatus(scene.Player, 200, 1.96f, 0, scene.Source);
                scene.Manager.SetStatus(scene.Player, 200, 1.96f, 0, scene.Source);
                Assert(status.Param.Get() == 0 && status.RemainingTime.Get() == 1.96f && scene.Statuses.SetCalls == 4,
                    "原生同编号、同来源的刷新返回 false；短时易伤的重复施加也不应因此中断。");
                scene.Manager.RemoveStatus(scene.Player, 100);
                scene.Manager.RemoveStatus(scene.Player, 200);
                Assert(original.StatusId.Get() == 100 && status.StatusId.Get() == 0 && scene.Statuses.RemoveCalls == 1,
                    "只能移除本会话持有的状态。");
            }
        }

        public static void SessionStatusesValidateActorsAndCapacityBeforeNativeCall()
        {
            using (var scene = new Scene())
            {
                Throws<ArgumentOutOfRangeException>(() => scene.Manager.SetStatus(scene.Player, 0, 30, 0, scene.Source));
                Throws<ArgumentOutOfRangeException>(() => scene.Manager.SetStatus(scene.Player, 200, float.NaN, 0, scene.Source));
                var outsider = scene.Spawner.World.CreateCharacter(3);

                using (var anotherScheduler = new SimScheduler())
                {
                    var another = new SimEntityManager(scene.Spawner)
                    {
                        Scheduler = anotherScheduler
                    };
                    var foreign = another.Attach(outsider);
                    Throws<InvalidOperationException>(() => scene.Manager.SetStatus(foreign, 200, 30, 0, scene.Source));
                    Throws<InvalidOperationException>(() => scene.Manager.SetStatus(scene.Player, 200, 30, 0, foreign));
                    another.Clear();
                }

                for (var i = 0; i < 30; i++)
                    scene.Statuses.GetStatus(i).StatusId.Value = (ushort)(1000 + i);

                Throws<InvalidOperationException>(() => scene.Manager.SetStatus(scene.Player, 200, 30, 0, scene.Source));
                Assert(scene.Statuses.SetCalls == 0 && scene.Statuses.GetStatus(30).StatusId.Get() == 0,
                    "参数、所有者或前 30 槽容量不符时不得原生写入或使用后半区。");
            }
        }

        public static void SessionStatusesRetainCleanupAfterPartialSetFailure()
        {
            using (var scene = new Scene())
            {
                scene.Statuses.AfterSet = _ =>
                {
                    throw new InvalidOperationException("状态写入后调用失败。");
                };

                Throws<InvalidOperationException>(() => scene.Manager.SetStatus(scene.Player, 200, 30, 0, scene.Source));
                scene.Manager.Clear();
                Assert(scene.Statuses.GetStatus(0).StatusId.Get() == 0 && scene.Statuses.RemoveCalls == 1 &&
                    !scene.Manager.HasPendingCleanup, "写入部分成功后必须仍能清理借用玩家上的点名。");
            }
        }

        public static void SessionStatusesRejectReadbackMismatchAndPreserveResponsibility()
        {
            using (var scene = new Scene())
            {
                scene.Statuses.AfterSet = slot => scene.Statuses.GetStatus(slot).Param.Value = 99;
                var error = Throws<InvalidOperationException>(() => scene.Manager.SetStatus(scene.Player, 200, 30, 2, scene.Source));
                Assert(error.Message.Contains("槽 0") && error.Message.Contains("期望编号 0x00C8、参数 2") &&
                    error.Message.Contains("实际编号 0x00C8、参数 99") && error.Message.Contains("剩余"),
                    "读回不符必须报告目标槽、期望值和实际状态，便于实机判断失败原因。");
                scene.Manager.Clear();
                Assert(scene.Statuses.RemoveCalls == 1 && scene.Statuses.GetStatus(0).StatusId.Get() == 0,
                    "读回参数不符也必须保留清理责任。");
            }

            using (var scene = new Scene())
            {
                scene.Statuses.AfterSet = slot => scene.Statuses.GetStatus(slot).StatusId.Value = 0;
                Throws<InvalidOperationException>(() => scene.Manager.SetStatus(scene.Player, 200, 1.96f, 0, scene.Source));
                scene.Manager.Clear();
                Assert(scene.Statuses.RemoveCalls == 0 && !scene.Manager.HasPendingCleanup,
                    "即使原生返回 true，首次写入未读到状态也须报错；清理不得删除空槽或猜测已自然到期。");
            }

            using (var scene = new Scene())
            {
                scene.Manager.SetStatus(scene.Player, 200, 30, 0, scene.Source);
                scene.Statuses.AfterSet = slot => scene.Statuses.GetStatus(slot).Param.Value = 99;
                Throws<InvalidOperationException>(() => scene.Manager.SetStatus(scene.Player, 200, 1.96f, 2, scene.Source));
                scene.Manager.Clear();
                Assert(scene.Statuses.RemoveCalls == 1 && !scene.Manager.HasPendingCleanup,
                    "原生刷新返回 false 也不能掩盖真实读回不符，失败后仍须清理本会话状态。");
            }
        }

        public static void SessionStatusesRetryRemovalBeforeReleasingSource()
        {
            using (var scene = new Scene())
            using (var restart = new SimScheduler())
            {
                scene.Manager.SetStatus(scene.Player, 200, 30, 0, scene.Source);

                scene.Statuses.BeforeRemove = _ =>
                {
                    throw new InvalidOperationException("状态移除失败。");
                };

                scene.Manager.Clear();
                Assert(scene.Manager.HasPendingCleanup && scene.Source.Entity.IsAlive && scene.Spawner.Deleted.Count == 0,
                    "借用玩家的点名清理失败时，来源实体不得回收。");
                Throws<InvalidOperationException>(() => scene.Manager.Scheduler = restart);
                scene.Statuses.BeforeRemove = null;
                scene.Manager.Clear();
                Assert(!scene.Manager.HasPendingCleanup && !scene.Source.Entity.IsAlive && scene.Player.Entity.IsAlive &&
                    scene.Statuses.RemoveCalls == 2, "再次停止应重试点名，然后回收来源并保留自己。");
                scene.Manager.Scheduler = restart;
            }
        }

        public static void SessionStatusesTreatReplacementAndExpiryAsReleased()
        {
            using (var scene = new Scene())
            {
                scene.Manager.SetStatus(scene.Player, 200, 30, 0, scene.Source);
                scene.Statuses.GetStatus(0).SourceObject.Value = new GameObjectId
                {
                    ObjectId = 777
                };
                Throws<InvalidOperationException>(() => scene.Manager.SetStatus(scene.Player, 200, 30, 0, scene.Source));
                scene.Manager.SetStatus(scene.Player, 201, 30, 0, scene.Source);
                scene.Statuses.GetStatus(1).StatusId.Value = 0;
                scene.Manager.Clear();
                Assert(scene.Statuses.RemoveCalls == 0 && scene.Statuses.GetStatus(0).SourceObject.Get().Id == 777 &&
                    scene.Statuses.GetStatus(0).StatusId.Get() == 200, "外部替换的状态和自然消失的状态都不得再移除。");
            }
        }

        public static void SessionStatusesWorldChangeSkipsOldAddress()
        {
            using (var scene = new Scene())
            {
                scene.Manager.SetStatus(scene.Player, 200, 30, 0, scene.Source);
                scene.Spawner.World.Registry.Reset();

                scene.Statuses.BeforeRemove = _ =>
                {
                    throw new Exception("不可访问旧世界状态。");
                };

                scene.Manager.Clear();
                Assert(scene.Statuses.RemoveCalls == 0 && !scene.Manager.HasPendingCleanup,
                    "世界失效后应丢弃旧记录，而不是写回复用地址。");
            }
        }

        public static void SessionStatusesDeleteAndDetachCleanDependenciesFirst()
        {
            using (var scene = new Scene())
            {
                scene.Manager.SetStatus(scene.Player, 200, 30, 0, scene.Source);

                scene.Statuses.BeforeRemove = _ =>
                {
                    throw new InvalidOperationException("状态移除失败。");
                };

                Throws<AggregateException>(() => scene.Manager.Delete(scene.Source));
                Assert(scene.Source.IsActive && scene.Spawner.DeleteAttempts.Count == 0,
                    "直接删除来源前也必须成功移除其点名。");
                scene.Statuses.BeforeRemove = null;
                scene.Manager.Detach(scene.Player);
                Assert(!scene.Player.IsActive && scene.Player.Entity.IsAlive && scene.Statuses.GetStatus(0).StatusId.Get() == 0,
                    "借用玩家解除关联时应移除点名，保留角色本身。");
                scene.Manager.Delete(scene.Source);
            }
        }

        public static void SessionStatusesRetryPartialRemovalWithoutTouchingOtherStatuses()
        {
            using (var scene = new Scene())
            {
                scene.Manager.SetStatus(scene.Player, 200, 30, 0, scene.Source);
                scene.Manager.SetStatus(scene.Player, 201, 30, 0, scene.Source);

                scene.Statuses.AfterRemove = slot =>
                {
                    if (slot == 0)
                        throw new InvalidOperationException("状态清空后返回失败。");
                };

                scene.Manager.Clear();
                Assert(scene.Manager.HasPendingCleanup && scene.Statuses.GetStatus(1).StatusId.Get() == 0 &&
                    scene.Statuses.RemoveCalls == 2, "一项失败不得阻止其他状态清理，部分成功仍保留重试责任。");
                scene.Statuses.AfterRemove = null;
                scene.Manager.Clear();
                Assert(!scene.Manager.HasPendingCleanup && scene.Statuses.RemoveCalls == 2,
                    "重试应识别已清空槽位，不重复移除。");
            }
        }

        public static void MovementLocksFollowStatusOverlapAndPreserveExistingRestriction()
        {
            Simulant.Game.Data.GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Status>()[9000] =
                new Lumina.Excel.Sheets.Status
                {
                    LockMovement = true
                };
            Simulant.Game.Data.GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Status>()[9001] =
                new Lumina.Excel.Sheets.Status
                {
                    LockMovement = true
                };
            var condition = Conditions.Instance();
            condition.SetMovementLocked(false);

            using (var scene = new Scene())
            {
                scene.Manager.SetStatus(scene.Player, 9000, 10, 0, scene.Source);
                scene.Manager.SetStatus(scene.Player, 9001, 20, 0, scene.Source);
                Assert(scene.Player.MovementLocked && condition.SufferingStatusAffliction.Get(),
                    "禁止移动状态必须同时限制本机输入与模拟移动。");
                Assert(scene._scheduler.PendingCount == 1, "多个状态只共用一个到期检查节点。");
                scene.Manager.RemoveStatus(scene.Player, 9000);
                Assert(condition.SufferingStatusAffliction.Get(), "移除一个状态不能解除另一个仍生效的限制。");
                scene.Manager.RemoveStatus(scene.Player, 9001);
                Assert(!scene.Player.MovementLocked && !condition.SufferingStatusAffliction.Get(),
                    "最后一个禁止移动状态移除后必须立即恢复移动。");
            }

            condition.SetMovementLocked(true);

            using (var scene = new Scene())
            {
                scene.Manager.SetStatus(scene.Player, 9000, 10, 0, scene.Source);
                scene.Manager.Clear();
                Assert(condition.SufferingStatusAffliction.Get(), "停止时不能清除模拟开始前已有的原生限制。");
            }

            condition.SetMovementLocked(false);
        }

        public static void MovementLockRefreshExpiresWithoutWaitingForStatusRemoval()
        {
            Simulant.Game.Data.GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Status>()[9000] =
                new Lumina.Excel.Sheets.Status
                {
                    LockMovement = true
                };
            Conditions.Instance().SetMovementLocked(false);

            using (var scene = new Scene())
            using (var done = new System.Threading.ManualResetEventSlim())
            {
                scene.Manager.SetStatus(scene.Player, 9000, 0.12f, 0, scene.Source);
                scene._scheduler.Schedule(0.06, () => scene.Manager.SetStatus(scene.Player, 9000, 0.4f, 0, scene.Source));
                var heldAfterRefresh = false;
                scene._scheduler.Schedule(0.25, () => heldAfterRefresh = scene.Player.MovementLocked);
                scene._scheduler.Schedule(0.65, done.Set);
                scene._scheduler.Start();
                Assert(done.Wait(2500), "状态刷新及到期检查超时。");
                scene._scheduler.Dispose();
                Assert(heldAfterRefresh && !scene.Player.MovementLocked && !Conditions.Instance().SufferingStatusAffliction.Get(),
                    "刷新应延长限制，达到新期限后即便状态槽未清空也必须解除。");
            }
        }

        public static void MovementLocksReleaseOnInterruptAndWorldChange()
        {
            Simulant.Game.Data.GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Status>()[9000] =
                new Lumina.Excel.Sheets.Status
                {
                    LockMovement = true
                };
            Conditions.Instance().SetMovementLocked(false);

            using (var scene = new Scene())
            {
                scene.Manager.SetStatus(scene.Player, 9000, 10, 0, scene.Source);
                scene.Manager.Interrupt();
                Assert(!Conditions.Instance().SufferingStatusAffliction.Get() && !scene.Player.MovementLocked &&
                    scene.Statuses.GetStatus(0).StatusId.Get() == 9000 && scene.Source.IsActive,
                    "错误中断必须解除移动限制，同时保留状态图标、角色与已展示的现场。");
            }

            using (var scene = new Scene())
            {
                scene.Manager.SetStatus(scene.Player, 9000, 10, 0, scene.Source);
                scene.Spawner.World.Registry.Reset();
                // 新身份上的限制不能被旧会话恢复；模拟客户端重置后由新场景设为 true。
                Conditions.Instance().SetMovementLocked(true);
                scene.Manager.Clear();
                Assert(Conditions.Instance().SufferingStatusAffliction.Get() && !scene.Manager.HasPendingCleanup,
                    "世界变化应丢弃旧限制责任，不操作新身份或阻塞环境恢复。");
            }

            Conditions.Instance().SetMovementLocked(false);
        }

        private sealed class Scene : IDisposable
        {
            internal readonly EntitySpawner Spawner = new EntitySpawner();
            internal readonly SimScheduler _scheduler = new SimScheduler();
            internal readonly SimEntityManager Manager;
            internal readonly SimActor Player, Source;
            internal readonly StatusManager Statuses;
            internal Scene()
            {
                Manager = new SimEntityManager(Spawner)
                {
                    Scheduler = _scheduler
                };
                Player = Manager.Attach(Spawner.World.CreateCharacter(0));
                Source = Manager.SpawnBNpc(1);
                Statuses = Player.Entity.Native.StatusManager;
            }

            public void Dispose()
            {
                Statuses.BeforeRemove = null;
                Statuses.AfterRemove = null;
                Manager.Clear();
                _scheduler.Dispose();
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        private static T Throws<T>(Action action)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (T error)
            {
                return error;
            }

            throw new Exception("预期抛出 " + typeof(T).Name + "。");
        }
    }
}
