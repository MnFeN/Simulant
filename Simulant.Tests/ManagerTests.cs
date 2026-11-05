using Simulant.ACT;
using Simulant.Core.Entity;
using Simulant.Game.ExtractedCsv;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using ActionRow = Simulant.Game.ExtractedCsv.Rows.Action;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Tests
{
    internal static class ManagerTests
    {
        internal static void ManagerClearsOwnedEntitiesAndOnlyDetachesBorrowedActors()
        {
            var spawner = new EntitySpawner();
            using (var scheduler = new SimScheduler())
            {
                var manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                var borrowedEntity = spawner.World.CreateCharacter(0);
                var borrowed = manager.Attach(borrowedEntity);
                Assert(ReferenceEquals(borrowed, manager.Attach(borrowedEntity)), "Attach 必须复用模拟角色。");
                var owned = manager.SpawnBNpc(123);
                var ornament = manager.SpawnEObj(new EventObjectSpawnSpec { Index = 2 });
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

        internal static void ManagerRejectsCreationOutsideItsSessionAndSupportsRestart()
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
                Throws<InvalidOperationException>(() => manager.SpawnEObj(new EventObjectSpawnSpec { Index = 2 }));
                manager.Scheduler = second;
                var current = manager.SpawnBNpc(456);
                Assert(current.IsActive && current.Entity.Identity != old.Entity.Identity,
                    "重启必须创建具有新生命周期的模拟角色。");
                manager.Clear();
            }
        }

        internal static void ManagerCleanupContinuesAfterReleaseAndLoggingFailures()
        {
            var spawner = new EntitySpawner();
            var errors = 0;
            using (var scheduler = new SimScheduler())
            {
                var manager = new SimEntityManager(spawner, _ =>
                {
                    errors++;
                    throw new Exception("日志记录失败");
                }) { Scheduler = scheduler };
                var first = manager.SpawnBNpc(1);
                var second = manager.SpawnBNpc(2);
                first.MoveTo(new Vector2(100, 0));
                second.MoveTo(new Vector2(0, 100));
                spawner.BeforeDelete = entity =>
                {
                    if (ReferenceEquals(entity, first.Entity)) throw new Exception("原生释放失败");
                };
                manager.Clear();
                Assert(errors == 1 && !first.IsActive && !second.IsActive && !second.Entity.IsAlive,
                    "一次释放或日志记录失败，不得妨碍停止并释放其余模拟角色。");
                Assert(scheduler.PendingCount == 0, "清理失败后不得遗留排队的移动更新。");
            }
        }

        internal static void ManagerDeletingOneActorPreservesOtherMovementAndCast()
        {
            TriggernometryInterop.Reset();
            CsvManager.Instance.Get<ActionRow>().Clear();
            AddCombinedAction(1, 0.4f, true);
            AddCombinedAction(2, 0.4f, true);
            var spawner = new EntitySpawner();
            Exception failure = null;
            using (var continued = new ManualResetEventSlim())
            using (var completed = new ManualResetEventSlim())
            using (var scheduler = new SimScheduler((error, _) => failure = error))
            {
                var manager = new SimEntityManager(spawner) { Scheduler = scheduler };
                try
                {
                    var first = manager.SpawnBNpc(1);
                    var second = manager.SpawnBNpc(2);
                    var deleted = false;
                    var firstWritesAtDelete = 0;
                    second.Entity.OnPositionWritten = _ => { if (deleted) continued.Set(); };
                    first.MoveTo(new Vector2(1000, 0), 10);
                    second.MoveTo(new Vector2(0, 1000), 10);
                    first.Cast(1);
                    second.Cast(2);
                    var shownTags = TriggernometryInterop.Commands.Select(OmenTag).ToArray();
                    Assert(scheduler.PendingCount == 3,
                        "两次读条与两个角色的移动应只排入两个完成节点和一个共享更新节点。");
                    scheduler.Schedule(0.04, () =>
                    {
                        manager.Delete(first);
                        firstWritesAtDelete = first.Entity.PositionWrites.Count;
                        deleted = true;
                        Assert(second.IsActive && second.IsMoving && second.Entity.CastingActionId == 2,
                            "删除一个角色不得停止另一角色的移动或读条。");
                        Assert(TriggernometryInterop.Commands.Where(IsOmenRemoval).Select(OmenTag)
                            .SequenceEqual(new[] { shownTags[0] }), "删除时只能移除该角色自己的预兆。");
                    }, "删除组合测试中的首个角色");
                    scheduler.Schedule(0.5, completed.Set, "等待保留角色完成读条");
                    scheduler.Start();
                    Assert(continued.Wait(5000), "删除首个角色后，另一角色未继续移动：" + failure);
                    Assert(completed.Wait(5000), "保留角色的读条未进入完成阶段：" + failure);
                    scheduler.Dispose();
                    Assert(failure == null, "组合行为执行失败：" + failure);
                    Assert(!first.IsActive && first.Entity.PositionWrites.Count == firstWritesAtDelete &&
                        first.Entity.PlayedTimelines.Count == 0, "被删除角色不得继续移动或播放读条完成动作。");
                    Assert(second.IsActive && second.Entity.PlayedTimelines.SequenceEqual(new ushort[] { 102 }),
                        "保留角色必须独立完成读条并恰好播放一次完成动作。");
                    Assert(TriggernometryInterop.Commands.Where(IsOmenRemoval).Select(OmenTag).SequenceEqual(shownTags),
                        "两个角色的预兆必须分别在删除和自然完成时移除。");
                    manager.Clear();
                    Assert(spawner.Deleted.Count == 2 && !second.IsActive,
                        "最终清理必须恰好回收两个自有实体。");
                }
                finally
                {
                    scheduler.Dispose();
                    manager.Clear();
                    TriggernometryInterop.Reset();
                }
            }
        }

        internal static void ManagerStopAndRestartDiscardAllPreviousBehaviors()
        {
            TriggernometryInterop.Reset();
            CsvManager.Instance.Get<ActionRow>().Clear();
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
                var manager = new SimEntityManager(spawner) { Scheduler = firstScheduler };
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
                    Assert(firstScheduler.PendingCount == 4,
                        "停止前应存在两个读条完成节点、一个延迟预兆和一个共享移动更新。");
                    firstScheduler.Start();
                    try
                    {
                        Assert(entered.Wait(5000), "组合测试未进入移动回调：" + failure);
                        firstScheduler.Cancel();
                    }
                    finally { release.Set(); }
                    firstScheduler.Dispose();
                    manager.Clear();
                    Assert(failure == null && firstScheduler.PendingCount == 0,
                        "主动停止不得产生执行错误或遗留调度节点：" + failure);
                    Assert(!visible.IsActive && !delayed.IsActive && !visible.IsMoving && !delayed.IsMoving &&
                        visible.Entity.CastingActionId == 0 && delayed.Entity.CastingActionId == 0,
                        "停止后必须解除所有角色并清除其读条和移动状态。");
                    var commands = TriggernometryInterop.Commands.ToArray();
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
                        current.Entity.PlayedTimelines.SequenceEqual(new ushort[] { 102 }),
                        "重启后的角色必须能独立完成移动与读条：" + failure);
                    Assert(visible.Entity.PositionWrites.Count == visibleWrites && delayed.Entity.PositionWrites.Count == delayedWrites &&
                        visible.Entity.PlayedTimelines.Count == 0 && delayed.Entity.PlayedTimelines.Count == 0 &&
                        TriggernometryInterop.Commands.SequenceEqual(commands),
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
                    TriggernometryInterop.Reset();
                }
            }
        }

        private static void AddCombinedAction(int id, float castTime, bool omen)
        {
            CsvManager.Instance.Get<ActionRow>()[id] = new ActionRow
            {
                Index = id, CastTime = castTime, AnimationEndId = (short)(100 + id),
                OmenId = (ushort)(omen ? 1 : 0), ShapeType = 2, ScaleY = 5,
                Omen = new Simulant.Game.ExtractedCsv.Rows.Omen { Name = "组合测试预兆" }
            };
        }

        private static bool IsOmenRemoval(string command)
            => command.StartsWith("Action: Remove", StringComparison.Ordinal);

        private static string OmenTag(string command)
            => command.Split('\n').Single(line => line.StartsWith("Tag: ", StringComparison.Ordinal)).Substring(5).Trim();

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
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
        internal Action<EntityBase> BeforeDelete;
        internal int SpawnCount;
        private ushort _nextSlot = 1;

        public Character SpawnPlayer(Job job) => SpawnBNpc(new CharacterSpawnSpec());
        public Character SpawnBNpc(CharacterSpawnSpec spec)
        {
            SpawnCount++;
            return World.CreateCharacter(_nextSlot++);
        }

        public EventObject SpawnEObj(EventObjectSpawnSpec spec)
        {
            SpawnCount++;
            World.Objects[spec.Index] = new EntityObservation(new IntPtr(200 + spec.Index),
                (uint)(20 + spec.Index), ObjectKind.EventObj);
            return (EventObject)World.Registry.RegisterCreated(spec.Index);
        }

        public void Delete(EntityBase entity)
        {
            BeforeDelete?.Invoke(entity);
            Deleted.Add(entity);
            World.Registry.Invalidate(entity);
            World.Objects[entity.Identity.Slot] = null;
        }
    }
}
