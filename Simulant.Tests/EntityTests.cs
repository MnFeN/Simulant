using Simulant.Core.Entity;
using Simulant.Game.FFCS.Client.Game.Object;
using System;
using System.Linq;
using System.Threading;

namespace Simulant.Tests
{
    // 本项目链接实际的注册表与身份类型，通过可控的观察数据
    // 替代原生对象表；测试不会读写游戏内存。
    internal static class EntityTests
    {
        internal static void RegistryReusesOneWrapperPerLifetime()
        {
            var world = new EntityTestWorld();
            world.Objects[0] = Observe(100, 10);
            var first = world.Registry.Get(0);
            Assert(first is Character && first.IsAlive, "受支持的已占用槽位必须解析为有效角色。");
            Assert(ReferenceEquals(first, world.Registry.Get(0)), "重复查询必须复用有效的包装对象。");
            Assert(ReferenceEquals(first, world.Registry.GetEntities().Single()),
                "枚举和直接查询必须返回同一个包装对象。");

            var results = new EntityBase[8];
            var workers = results.Select((_, index) => new Thread(() => results[index] = world.Registry.Get(0))).ToArray();
            foreach (var worker in workers) worker.Start();
            foreach (var worker in workers) Assert(worker.Join(5000), "并发查询未完成。");
            Assert(results.All(entity => ReferenceEquals(first, entity)),
                "并发查询不得为同一生命周期创建多个包装对象。");
        }

        internal static void RegistryInvalidatesDisappearedAndReusedSlots()
        {
            var world = new EntityTestWorld();
            var observed = Observe(100, 10);
            world.Objects[0] = observed;
            var original = world.Registry.Get(0);

            world.Objects[0] = null;
            Assert(!original.IsAlive && world.Registry.Get(0) == null,
                "对象表槽位为空时，旧引用必须失效。");

            world.Objects[0] = observed;
            var reused = world.Registry.Get(0);
            Assert(reused.IsAlive && !original.IsAlive && original.Identity != reused.Identity,
                "观察到对象消失后，即使地址和 ID 相同，也必须创建新的生命周期。");

            world.Objects[0] = Observe(200, 10);
            Assert(!reused.IsAlive, "地址被替换后，旧引用必须在下次查询前失效。");
            var replacement = world.Registry.Get(0);
            Assert(replacement.IsAlive && replacement.Identity != reused.Identity,
                "槽位对象被替换后，生命周期身份必须更新。");
        }

        internal static void RegistryDetectsRecycledAddressFingerprint()
        {
            var world = new EntityTestWorld();
            world.Objects[0] = Observe(100, 10);
            var original = world.Registry.Get(0);
            world.Objects[0] = Observe(100, 11);
            var newId = world.Registry.Get(0);
            Assert(!original.IsAlive && newId.Identity != original.Identity,
                "同一地址上的实体 ID 改变后，不得保留旧身份。");

            world.Objects[0] = Observe(100, 11, ObjectKind.EventObj);
            var newKind = world.Registry.Get(0);
            Assert(newKind is EventObject && !newId.IsAlive && newKind.Identity != newId.Identity,
                "原生类型改变后，身份和包装对象类型都必须更新。");
        }

        internal static void RegistryTombstoneAndExplicitCreationAreSafe()
        {
            var world = new EntityTestWorld();
            world.Objects[0] = Observe(100, 10, ObjectKind.EventObj);
            var original = world.Registry.Get(0);
            world.Registry.Invalidate(original);
            world.Registry.Invalidate(original);
            Assert(!original.IsAlive && world.Registry.Get(0) == null,
                "显式移除的对象必须保持失效，即使其原生槽位仍被占用。");
            Assert(!world.Registry.GetEntities().Any(), "枚举不得恢复已标记失效的对象。");

            var recreated = world.Registry.RegisterCreated(0);
            Assert(recreated.IsAlive && recreated.Identity != original.Identity,
                "显式创建必须建立新的生命周期，即使原生字段完全相同。");
            world.Registry.Invalidate(original);
            Assert(recreated.IsAlive && ReferenceEquals(recreated, world.Registry.Get(0)),
                "旧生命周期的延迟清理不得使替代对象失效。");

            world.Registry.Invalidate(recreated);
            world.Objects[0] = Observe(100, 12, ObjectKind.EventObj);
            var changed = world.Registry.Get(0);
            Assert(changed != null && changed.IsAlive && changed.Identity != recreated.Identity,
                "原生观察数据改变后，必须解除旧的失效标记。");
        }

        internal static void RegistryResetInvalidatesTheWholeWorld()
        {
            var world = new EntityTestWorld();
            world.Objects[0] = Observe(100, 10);
            world.Objects[1] = Observe(200, 20);
            var old = world.Registry.GetEntities().ToArray();
            world.Registry.Reset();
            Assert(old.All(entity => !entity.IsAlive), "世界重置必须使之前的所有引用失效。");
            var current = world.Registry.GetEntities().ToArray();
            Assert(current.Length == 2 && current.All(entity => entity.IsAlive),
                "重置后必须仍能观察当前世界。");
            Assert(current.All(entity => entity.Identity.WorldEpoch != old[0].Identity.WorldEpoch),
                "不同世界中槽位和原生字段相同的对象必须具有不同的世界代次。");
        }

        internal static void RegistryKeepsDistinctObjectsWithSharedGameIds()
        {
            var world = new EntityTestWorld();
            world.Objects[0] = Observe(100, 0xE0000000);
            world.Objects[1] = Observe(200, 0xE0000000);
            var entities = world.Registry.GetEntities().ToArray();
            Assert(entities.Length == 2 && entities[0].Identity != entities[1].Identity,
                "使用相同占位游戏 ID 的本地对象必须通过槽位和生命周期区分。");
        }

        internal static void RegistryCleanupRetryMatchesOnlyTheOriginalLifetime()
        {
            var world = new EntityTestWorld();
            world.Objects[0] = Observe(100, 10);
            var original = world.Registry.Get(0);
            world.Registry.Invalidate(original);
            Assert(!original.IsAlive && world.Registry.Get(0) == null && world.Registry.MatchesCurrent(original),
                "已标记失效的实体必须仍能重试原生清理，同时保持公开包装对象失效。");
            var replacement = world.Registry.RegisterCreated(0);
            Assert(!world.Registry.MatchesCurrent(original) && world.Registry.MatchesCurrent(replacement),
                "清理重试不得作用于原生字段相同的新生命周期。");
            world.Registry.Invalidate(replacement);
            world.Objects[0] = Observe(200, 20);
            Assert(!world.Registry.MatchesCurrent(replacement), "观察到原生对象被替换后，也必须拒绝旧的清理重试。");
        }

        private static EntityObservation Observe(long address, uint id, ObjectKind kind = ObjectKind.BattleNpc)
            => new EntityObservation(new IntPtr(address), id, kind);

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }

    internal sealed class EntityTestWorld
    {
        internal readonly EntityObservation?[] Objects = new EntityObservation?[4];
        internal readonly EntityRegistry Registry;

        internal EntityTestWorld() { Registry = new EntityRegistry(slot => Objects[slot], Objects.Length); }

        internal Character CreateCharacter(ushort slot)
        {
            Objects[slot] = new EntityObservation(new IntPtr(100 + slot), (uint)(10 + slot), ObjectKind.BattleNpc);
            return (Character)Registry.Get(slot);
        }
    }
}
