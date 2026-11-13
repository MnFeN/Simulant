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
                    var another = new SimEntityManager(scene.Spawner) { Scheduler = anotherScheduler };
                    var foreign = another.Attach(outsider);
                    Throws<InvalidOperationException>(() => scene.Manager.SetStatus(foreign, 200, 30, 0, scene.Source));
                    Throws<InvalidOperationException>(() => scene.Manager.SetStatus(scene.Player, 200, 30, 0, foreign));
                    another.Clear();
                }
                for (var i = 0; i < 30; i++) scene.Statuses.GetStatus(i).StatusId.Value = (ushort)(1000 + i);
                Throws<InvalidOperationException>(() => scene.Manager.SetStatus(scene.Player, 200, 30, 0, scene.Source));
                Assert(scene.Statuses.SetCalls == 0 && scene.Statuses.GetStatus(30).StatusId.Get() == 0,
                    "参数、所有者或前 30 槽容量不符时不得原生写入或使用后半区。");
            }
        }

        public static void SessionStatusesRetainCleanupAfterPartialSetFailure()
        {
            using (var scene = new Scene())
            {
                scene.Statuses.AfterSet = _ => { throw new InvalidOperationException("状态写入后调用失败。"); };
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
                scene.Statuses.BeforeRemove = _ => { throw new InvalidOperationException("状态移除失败。"); };
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
                scene.Statuses.GetStatus(0).SourceObject.Value = new GameObjectId { ObjectId = 777 };
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
                scene.Statuses.BeforeRemove = _ => { throw new Exception("不可访问旧世界状态。"); };
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
                scene.Statuses.BeforeRemove = _ => { throw new InvalidOperationException("状态移除失败。"); };
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
                scene.Statuses.AfterRemove = slot => { if (slot == 0) throw new InvalidOperationException("状态清空后返回失败。"); };
                scene.Manager.Clear();
                Assert(scene.Manager.HasPendingCleanup && scene.Statuses.GetStatus(1).StatusId.Get() == 0 &&
                    scene.Statuses.RemoveCalls == 2, "一项失败不得阻止其他状态清理，部分成功仍保留重试责任。");
                scene.Statuses.AfterRemove = null;
                scene.Manager.Clear();
                Assert(!scene.Manager.HasPendingCleanup && scene.Statuses.RemoveCalls == 2,
                    "重试应识别已清空槽位，不重复移除。");
            }
        }

        private sealed class Scene : IDisposable
        {
            internal readonly EntitySpawner Spawner = new EntitySpawner();
            private readonly SimScheduler _scheduler = new SimScheduler();
            internal readonly SimEntityManager Manager;
            internal readonly SimActor Player, Source;
            internal readonly StatusManager Statuses;
            internal Scene()
            {
                Manager = new SimEntityManager(Spawner) { Scheduler = _scheduler };
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

        private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static T Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T error) { return error; }
            throw new Exception("预期抛出 " + typeof(T).Name + "。");
        }
    }
}
