using Simulant.Core.Entity;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Simulation.Runtime;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Numerics;
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
