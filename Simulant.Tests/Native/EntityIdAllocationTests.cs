namespace Simulant.Tests.Native
{
    using Simulant.Core.Entity;
    using Simulant.Game.FFCS.Client.Game.Character;
    using Simulant.Game.FFCS.Client.Game.Object;
    using System;
    using System.Linq;
    using System.Reflection;
    using Scene = Simulant.Tests.Native.LocalCreationTests.Scene;

    // 复用真实生成器故障注入替身；只设置计数器边界，不替换 ID 分配实现。
    internal static class EntityIdAllocationTests
    {
        private const uint NpcBase = 0x40FF0000;
        private const uint PlayerBase = 0x10FF0000;
        private static int _passed;

        private static int Main()
        {
            try
            {
                Run();
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("验证失败：实体 ID 分配。\n" + error);
                return 1;
            }
        }

        private static void Run()
        {
            Check("非玩家 ID 区间用尽后拒绝分配且没有原生副作用", () => VerifyExhaustion(false));
            Check("玩家 ID 区间用尽后拒绝分配且没有原生副作用", () => VerifyExhaustion(true));
            Check("非玩家计数器归零后从客户端最大旧 ID 继续", () => VerifyClientMaximum(false));
            Check("玩家计数器归零后从客户端最大旧 ID 继续", () => VerifyClientMaximum(true));
            Check("玩家和非玩家区间只受自身前缀影响", VerifyIndependentPrefixes);
            Check("删除记录后仍从本进程最大已分配 ID 继续", VerifyDeletionKeepsCounter);
            Check("隐藏并释放的 EObj 仍占用其 ID", VerifyHiddenObject);
            Check("尚未进入对象表的网络请求仍占用其 ID", VerifyPendingNetwork);
            Check("已交付但尚未清理的资源仍占用其 ID", VerifyOwnedRecord);
            Check("失败本地创建的已知身份仍占用其 ID", VerifyFailedCreation);
            Check("指针表读取失败不创建本地角色", () => VerifyLocalScanFailure(false));
            Check("实体 ID 读取失败不创建本地角色", () => VerifyLocalScanFailure(true));
            Check("扫描失败不覆盖或隐藏原 EObj", VerifyEventObjectScanFailure);
            Check("扫描失败不发送网络生成包", VerifyNetworkScanFailure);
            Check("缺少战斗角色表不影响本地创建", VerifyOptionalCharacterManager);
            Check("只在战斗角色表中的对象也占用 ID", VerifyCharacterManagerOnly);
            Check("重复原生指针只读取一次 ID", VerifyDuplicatePointers);
            Console.WriteLine("真实 ID 分配故障注入通过：" + _passed + " 项。");
        }

        private static void VerifyExhaustion(bool player)
        {
            var baseId = player ? PlayerBase : NpcBase;
            foreach (var clientAtLimit in new[] { false, true })
            {
                var scene = new Scene();
                SetObject(scene, 818, new IntPtr(0x800000),
                    baseId + (clientAtLimit ? (uint)ushort.MaxValue : 20u), ObjectKind.EventObj);
                SetCounter(player, clientAtLimit ? 0 : ushort.MaxValue);
                var error = Throws<InvalidOperationException>(() => SpawnLocal(scene, player));
                Assert(error.Message.Contains("ID") && (error.Message.Contains("用尽") || error.Message.Contains("耗尽")),
                    "区间末端必须明确报告 ID 用尽，不能回绕或寻找低位空洞。");
                Assert(scene.Plugin.CreateCalls == 0 && scene.Plugin.DeleteCalls == 0 && scene.Plugin.CallCount == 0 &&
                    scene.Memory.WriteCalls == 0 && scene.Memory.AllocateCalls == 0 && !scene.Pending,
                    "客户端或本进程计数器达到末端时，拒绝分配不得产生原生副作用。");
                Assert(scene.Memory.ReadBytesCalls == 1, "拒绝分配仍应只批量读取一次全局指针表。");
                scene.Spawner.Clear();
            }

            var lastSlotScene = new Scene();
            SetCounter(player, ushort.MaxValue - 1);
            var last = SpawnLocal(lastSlotScene, player);
            Assert(last.Id == baseId + ushort.MaxValue, "区间最后一个合法 ID 应仍可使用。");
            lastSlotScene.Spawner.Delete(last);
            var calls = lastSlotScene.Plugin.CallCount;
            var writes = lastSlotScene.Memory.WriteCalls;
            Throws<InvalidOperationException>(() => SpawnLocal(lastSlotScene, player));
            Assert(lastSlotScene.Plugin.CallCount == calls && lastSlotScene.Memory.WriteCalls == writes,
                "删除区间最后一个实体也不能回绕复用旧 ID。");
            lastSlotScene.Spawner.Clear();
        }

        private static void VerifyClientMaximum(bool player)
        {
            var scene = new Scene();
            var baseId = player ? PlayerBase : NpcBase;
            var occupied = new[] { baseId + 0x23, baseId + 0x2345, baseId + 0x1234 };
            var slots = new ushort[] { 3, 500, 818 };
            for (var i = 0; i < occupied.Length; i++) SetObject(scene, slots[i], new IntPtr(0x800000 + i * 0x1000), occupied[i], ObjectKind.EventObj);
            SetCounter(player, 0);
            var entity = SpawnLocal(scene, player);
            Assert(entity.Id == baseId + 0x2346,
                "计数器归零后必须从客户端同前缀最大低位编号加一，不能从较低空洞开始。");
            Assert(scene.Memory.ReadBytesCalls == 1, "一次分配只能批量读取一次全局指针表。");
            foreach (var i in Enumerable.Range(0, occupied.Length))
                Assert(scene.Memory.ReadsByAddress[0x800000 + i * 0x1000 + 0x78] == 1,
                    "确定最大编号时每个原生对象 ID 只能读取一次。");
            scene.Spawner.Clear();
        }

        private static void VerifyIndependentPrefixes()
        {
            foreach (var player in new[] { false, true })
            {
                var scene = new Scene();
                var baseId = player ? PlayerBase : NpcBase;
                var otherBase = player ? NpcBase : PlayerBase;
                SetObject(scene, 2, new IntPtr(0x800000), otherBase + ushort.MaxValue, ObjectKind.Pc);
                SetObject(scene, 500, new IntPtr(0x801000), 0x4000FFFF, ObjectKind.BattleNpc);
                SetObject(scene, 818, new IntPtr(0x802000), 0x10FEFFFF, ObjectKind.EventObj);
                SetCounter(!player, ushort.MaxValue);
                SetCounter(player, 0);
                var entity = SpawnLocal(scene, player);
                Assert(entity.Id == baseId + 1, "其他前缀的高编号及计数器不能推高或耗尽当前区间。");
                scene.Spawner.Clear();
            }
        }

        private static void VerifyDeletionKeepsCounter()
        {
            foreach (var player in new[] { false, true })
            {
                var scene = new Scene();
                var baseId = player ? PlayerBase : NpcBase;
                SetCounter(player, 0x7000);
                var original = SpawnLocal(scene, player);
                Assert(original.Id == baseId + 0x7001, "没有更高客户端 ID 时必须承接本进程已有计数器。");
                scene.Spawner.Delete(original);
                Assert(!scene.Spawner.Owns(original) && !original.IsAlive, "基线实体必须已经实际删除并移除记录。");
                SetObject(scene, 818, new IntPtr(0x800000), baseId + 7, ObjectKind.EventObj);
                var current = SpawnLocal(scene, player);
                Assert(current.Id == original.Id + 1,
                    "客户端只剩较低 ID 时，删除旧记录也不能令本进程计数器倒退复用。");
                scene.Spawner.Clear();
            }
        }

        private static void VerifyHiddenObject()
        {
            var scene = new Scene();
            SetCounter(false, 0x1000);
            var hidden = scene.Spawner.SpawnEObj(new EventObjectSpawnSpec { Index = 0, BaseId = 1 });
            var oldId = hidden.Id;
            scene.Spawner.Delete(hidden);
            Assert(scene.Plugin.HiddenObjects.Count == 1 && !scene.Spawner.Owns(hidden) && !hidden.IsAlive,
                "基线必须经过实际 EObj 隐藏与释放路径。");
            Assert(scene.Memory.Read<IntPtr>(new IntPtr(0x20020 + 449 * IntPtr.Size)) != IntPtr.Zero,
                "隐藏 EObj 仍应留在全局原生表。");
            SetCounter(false, 0);
            var current = scene.Spawner.SpawnBNpc(1, 1, 1);
            Assert(current.Id == oldId + 1, "已释放但仍留在对象表的隐藏 EObj ID 不能复用。");
            scene.Spawner.Clear();
        }

        private static void VerifyPendingNetwork()
        {
            var scene = new Scene();
            CharacterManager.InstancePtr = new IntPtr(0xA0000);
            SetCounter(false, 0x2000);
            var request = BeginNetwork(scene);
            Assert(scene.Plugin.NetworkIds.Single() == NpcBase + 0x2001, "基线网络生成必须实际发包并预留高位 ID。");
            Assert(scene.Memory.Read<IntPtr>(new IntPtr(0x20020 + 2 * IntPtr.Size)) == IntPtr.Zero,
                "请求必须仍处于尚未进入对象表的状态。");
            SetCounter(false, 0);
            var current = scene.Spawner.SpawnBNpc(1, 1, 1);
            Assert(current.Id == NpcBase + 0x2002 && request != null, "本地生成必须从未完成网络请求的高位 ID 继续递增。");
            scene.Spawner.Clear();
        }

        private static void VerifyOwnedRecord()
        {
            var scene = new Scene();
            SetCounter(false, 0x3000);
            var original = scene.Spawner.SpawnBNpc(1, 1, 1);
            scene.Memory.Put(scene.ObjectPointer, IntPtr.Zero);
            SetCounter(false, 0);
            var current = scene.Spawner.SpawnBNpc(1, 1, 1);
            Assert(current.Id == original.Id + 1, "原生表暂时缺项时，保留的所有权记录仍须占用 ID。");
            scene.Spawner.Clear();
        }

        private static void VerifyFailedCreation()
        {
            var scene = new Scene();
            SetCounter(false, 0x4000);
            scene.Plugin.FailDelete = true;
            scene.Memory.BeforeWrite = address =>
            {
                if (address == scene.Native + 0xB0) throw new InvalidOperationException("模拟 ID 已赋值后的初始化失败。");
            };
            Throws<AggregateException>(() => scene.Spawner.SpawnBNpc(1, 1, 1));
            Assert(scene.Pending && scene.FailedCount == 1 && scene.Memory.Read<uint>(scene.Native + 0x78) == NpcBase + 0x4001,
                "必须实际留下带已分配 ID 的未交付失败记录。");
            scene.Memory.BeforeWrite = null;
            scene.Memory.Put(scene.ObjectPointer, IntPtr.Zero);
            scene.Memory.Put(scene.LocalPointer, IntPtr.Zero);
            SetCounter(false, 0);
            var current = scene.Spawner.SpawnEObj(new EventObjectSpawnSpec { Index = 0 });
            Assert(current.Id == NpcBase + 0x4002 && scene.Plugin.DeleteCalls == 1,
                "全局表已缺项时仍须从失败记录的高位 ID 继续，且不能顺带回收未知原对象。");
            scene.Spawner.ResetWorld();
        }

        private static void VerifyLocalScanFailure(bool idRead)
        {
            var scene = new Scene();
            if (idRead)
            {
                var pointer = new IntPtr(0x800000);
                SetObject(scene, 818, pointer, NpcBase + 1, ObjectKind.EventObj);
                scene.Memory.FailReadAddress = pointer + 0x78;
            }
            else scene.Memory.FailReadBytes = true;
            Throws<InvalidOperationException>(() => scene.Spawner.SpawnBNpc(1, 1, 1));
            Assert(scene.Plugin.CreateCalls == 0 && scene.Plugin.DeleteCalls == 0 && scene.Plugin.CallCount == 0 &&
                scene.Memory.WriteCalls == 0 && scene.Memory.AllocateCalls == 0 && !scene.Pending,
                "扫描失败必须发生在本地原生创建之前，不能形成新分配或回滚。");
        }

        private static void VerifyEventObjectScanFailure()
        {
            var scene = new Scene();
            var original = scene.Spawner.SpawnEObj(new EventObjectSpawnSpec { Index = 0 });
            var calls = scene.Plugin.CallCount;
            var writes = scene.Memory.WriteCalls;
            var allocations = scene.Memory.AllocateCalls;
            scene.Memory.FailReadBytes = true;
            Throws<InvalidOperationException>(() => scene.Spawner.SpawnEObj(new EventObjectSpawnSpec { Index = 0 }));
            Assert(scene.Spawner.Owns(original) && original.IsAlive && scene.Plugin.HiddenObjects.Count == 0 &&
                scene.Plugin.CallCount == calls && scene.Memory.WriteCalls == writes && scene.Memory.AllocateCalls == allocations,
                "扫描失败不能隐藏旧 EObj、发送覆盖包或破坏回收责任。");
            scene.Memory.FailReadBytes = false;
            scene.Spawner.Clear();
        }

        private static void VerifyNetworkScanFailure()
        {
            var scene = new Scene();
            CharacterManager.InstancePtr = new IntPtr(0xA0000);
            scene.Memory.FailReadBytes = true;
            Throws<InvalidOperationException>(() => BeginNetwork(scene));
            Assert(scene.Plugin.NetworkIds.Count == 0 && scene.Plugin.CallCount == 0 && scene.Memory.WriteCalls == 0 && scene.Memory.AllocateCalls == 0,
                "扫描失败不能预先发送网络包或分配包内存。");
            scene.Spawner.Clear();
        }

        private static void VerifyOptionalCharacterManager()
        {
            var scene = new Scene();
            Assert(CharacterManager.InstancePtr == IntPtr.Zero, "基线应缺少战斗角色表。");
            var current = scene.Spawner.SpawnBNpc(1, 1, 1);
            Assert(current.IsAlive && scene.Plugin.CreateCalls == 1 && scene.Memory.ReadBytesCalls == 1,
                "本地角色应只依赖自身管理器和全局表，不能强依赖网络角色表。");
            scene.Spawner.Clear();
        }

        private static void VerifyCharacterManagerOnly()
        {
            var scene = new Scene();
            CharacterManager.InstancePtr = new IntPtr(0xA0000);
            var native = new IntPtr(0x800000);
            scene.Memory.Put(new IntPtr(0xA0050 + 99 * IntPtr.Size), native);
            scene.Memory.Put(native + 0x78, NpcBase + 0x5000);
            SetCounter(false, 0);
            var current = scene.Spawner.SpawnBNpc(1, 1, 1);
            Assert(current.Id == NpcBase + 0x5001, "必须从尚未登记到全局表的战斗角色最大 ID 继续。");
            scene.Spawner.Clear();
        }

        private static void VerifyDuplicatePointers()
        {
            var scene = new Scene();
            CharacterManager.InstancePtr = new IntPtr(0xA0000);
            var native = new IntPtr(0x800000);
            SetObject(scene, 10, native, NpcBase + 0x6000, ObjectKind.BattleNpc);
            scene.Memory.Put(new IntPtr(0x20020 + 11 * IntPtr.Size), native);
            scene.Memory.Put(new IntPtr(0xA0050), native);
            SetCounter(false, 0);
            var current = scene.Spawner.SpawnBNpc(1, 1, 1);
            Assert(current.Id == NpcBase + 0x6001 && scene.Memory.ReadsByAddress[native.ToInt64() + 0x78] == 1 && scene.Memory.ReadBytesCalls == 1,
                "重复指针必须去重，避免同一角色跨表重复读取 ID。");
            scene.Spawner.Clear();
        }

        private static object BeginNetwork(Scene scene)
        {
            var args = new object[] { new NetworkNpcSpawnSpec { BNpcBaseId = 1, ModelCharaId = 1, Battalion = 4 }, null };
            try { typeof(EntitySpawner).GetMethod("BeginSpawnNetworkBNpc", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scene.Spawner, args); }
            catch (TargetInvocationException error) { throw error.InnerException; }
            return args[1];
        }

        private static Simulant.Core.Entity.Character SpawnLocal(Scene scene, bool player)
        {
            if (!player) return scene.Spawner.SpawnBNpc(1, 1, 1);
            SetObject(scene, 0, new IntPtr(0x900000), 10, ObjectKind.Pc);
            return scene.Spawner.SpawnPlayer(Job.PLD, (Simulant.Core.Entity.Character)scene.Registry.Get(0));
        }

        private static void SetObject(Scene scene, ushort slot, IntPtr native, uint id, ObjectKind kind)
        {
            scene.Memory.Put(new IntPtr(0x20020 + slot * IntPtr.Size), native);
            scene.Memory.Put(native + 0x78, id);
            scene.Memory.Put(native + 0x8C, slot);
            scene.Memory.Put(native + 0x90, kind);
        }

        private static void SetCounter(bool player, int value)
        {
            typeof(EntitySpawner).GetField(player ? "_playerIdIndex" : "_nonPlayerIdIndex", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, (ushort)value);
        }

        private static void Check(string name, Action action)
        {
            action();
            _passed++;
            Console.WriteLine("通过：" + name);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static T Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T error) { return error; }
            throw new Exception("预期抛出 " + typeof(T).Name + "。");
        }
    }

}
