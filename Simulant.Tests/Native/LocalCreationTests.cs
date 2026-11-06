namespace Simulant.Tests.Native
{
    using Simulant.ACT;
    using Simulant.Core.Entity;
    using Simulant.Game.FFCS.Client.Game.Character;
    using Simulant.Game.FFCS.Client.Game.Object;
    using Simulant.Game.FFCS.Client.Game.Network;
    using Simulant.Game.FFCS.Client.Network;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    // 引用真实插件 DLL，只替换 PostNamazu 的原生读写与调用边界；不会连接 ACT 或游戏。
    internal static class LocalCreationTests
    {
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
                Console.Error.WriteLine("验证失败：本地创建与失败清理。\n" + error);
                return 1;
            }
        }

        private static void Run()
        {
            foreach (var getFailure in new[] { false, true })
                foreach (var dispose in new[] { false, true })
                    Check("创建双失败后" + (dispose ? "释放" : "清理") + "可重试：" + (getFailure ? "读取" : "初始化"),
                        () => VerifyRetry(getFailure, dispose));

            Check("仅回收失败分配，保留已交付实体", VerifyDeliveredEntity);
            Check("实体 ID 变化时拒绝删除", () => VerifyChanged(scene => scene.Memory.Put(scene.Native + 0x78, 999u), false));
            Check("实体指针变化时拒绝删除", () => VerifyChanged(scene => scene.SetIdentity(new IntPtr(0x50000), 444u), false));
            Check("本地表单独变化时拒绝删除", () => VerifyChanged(scene => scene.Memory.Put(scene.LocalPointer, new IntPtr(0x50000)), false));
            Check("全部对象表单独变化时拒绝删除", () => VerifyChanged(scene => scene.Memory.Put(scene.ObjectPointer, new IntPtr(0x50000)), false));
            Check("对象槽位变化时拒绝删除", () => VerifyChanged(scene => scene.Memory.Put(scene.Native + 0x8C, (ushort)300), false));
            Check("对象种类变化时拒绝删除", () => VerifyChanged(scene => scene.Memory.Put(scene.Native + 0x90, ObjectKind.Pc), false));
            Check("本地管理器变化时不访问旧内存", () => VerifyChanged(scene => ClientObjectManager.InstancePtr = new IntPtr(0x60000), true));
            Check("对象管理器变化时不访问旧内存", () => VerifyChanged(scene => GameObjectManager.InstancePtr = new IntPtr(0x70000), true));
            Check("进程句柄变化时不访问旧内存", () => VerifyChanged(scene => scene.Memory.ProcessHandle = new IntPtr(202), true));
            Check("进程关闭后不访问旧内存", () => VerifyChanged(scene => scene.Memory.IsProcessOpen = false, true));
            Check("内存实例变化时不访问新旧内存", () => VerifyChanged(scene => scene.Plugin.Memory = new FakeLocalMemory(), true));
            Check("未知身份不能在后续重试时认领", VerifyUnknownIdentity);
            Check("世界重置只丢弃记录，不访问旧内存", VerifyWorldReset);
            Check("初始化时两表替换不得首次删除新对象", () => VerifySetupConflict(scene => scene.SetIdentity(new IntPtr(0x50000), 999u)));
            Check("初始化时本地表替换不得首次删除新对象", () => VerifySetupConflict(scene => scene.Memory.Put(scene.LocalPointer, new IntPtr(0x50000))));
            Check("初始化时全部对象表替换不得首次删除新对象", () => VerifySetupConflict(scene => scene.Memory.Put(scene.ObjectPointer, new IntPtr(0x50000))));
            Check("初始化时槽位不符不得首次删除", () => VerifySetupConflict(scene => scene.Memory.Put(scene.Native + 0x8C, (ushort)300)));
            Check("已见替换对象后原地址重现也不得认领", VerifyConflictRemainsAfterOriginalReturns);
            Console.WriteLine("真实生成器故障注入通过：" + _passed + " 项。");
        }

        private static void VerifyRetry(bool getFailure, bool dispose)
        {
            var scene = new Scene();
            scene.FailCreation(getFailure);
            Assert(scene.Pending && scene.FailedCount == 1 && scene.OwnedCount == 0,
                "未交付失败分配必须独立保留，不能加入已交付资源集合。");
            var beforeCreate = scene.Plugin.CreateCalls;
            Throws<InvalidOperationException>(() => scene.Spawner.SpawnBNpc(1, 1, 1));
            Assert(scene.Plugin.CreateCalls == beforeCreate, "失败记录未清理时不得再次分配本地角色。");
            Throws<AggregateException>(() => { if (dispose) scene.Spawner.Dispose(); else scene.Spawner.Clear(); });
            Assert(scene.Pending && scene.Plugin.DeleteCalls == 2, "失败重试必须保留记录且每轮只删除一次。");
            scene.Plugin.FailDelete = false;
            if (dispose) scene.Spawner.Dispose(); else scene.Spawner.Clear();
            Assert(!scene.Pending && scene.FailedCount == 0 && scene.Plugin.DeleteCalls == 3,
                "恢复后必须删除原分配并移除失败记录。");
            if (dispose) scene.Spawner.Dispose(); else scene.Spawner.Clear();
            Assert(scene.Plugin.DeleteCalls == 3, "成功清理项不得再次删除。");
            Assert(scene.Plugin.DeletedIndices.All(index => index == 1), "重试删除必须使用原本地索引。");
            scene.Spawner.Dispose();
        }

        private static void VerifyDeliveredEntity()
        {
            var scene = new Scene();
            var delivered = scene.Spawner.SpawnBNpc(1, 1, 1);
            Assert(scene.Spawner.Owns(delivered) && delivered.IsAlive, "基线实体必须实际创建并交付。");
            scene.FailCreation(false);
            scene.Plugin.FailDelete = false;
            scene.Invoke("RetryFailedCreations");
            Assert(!scene.Pending && scene.Spawner.Owns(delivered) && delivered.IsAlive,
                "失败创建专用回收不能影响已交付实体。");
            Assert(scene.Plugin.DeletedIndices.SequenceEqual(new ushort[] { 2, 2 }),
                "专用重试只能删除第二次创建的未交付分配。");
            scene.Spawner.Clear();
            Assert(scene.Plugin.DeletedIndices.SequenceEqual(new ushort[] { 2, 2, 1 }),
                "已交付实体必须保留到正常资源清理时回收。");
        }

        private static void VerifyChanged(Action<Scene> mutate, bool noRead)
        {
            var scene = new Scene();
            scene.FailCreation(false);
            scene.Plugin.FailDelete = false;
            var previousReads = scene.Memory.ReadCalls;
            var previousWrites = scene.Memory.WriteCalls;
            var previousCalls = scene.Plugin.CallCount;
            mutate(scene);
            Throws<AggregateException>(() => scene.Spawner.Clear());
            Assert(scene.Pending && scene.FailedCount == 1 && scene.Plugin.DeleteCalls == 1 && scene.Plugin.CallCount == previousCalls,
                "归属变化时必须保留失败记录，不能调用旧函数或删除当前对象。");
            Assert(scene.Memory.WriteCalls == previousWrites, "拒绝回收不得写入原内存。");
            if (noRead) Assert(scene.Memory.ReadCalls == previousReads, "上下文不匹配时不得读取原内存。");
            if (!ReferenceEquals(scene.Plugin.Memory, scene.Memory))
                Assert(scene.Plugin.Memory.ReadCalls == 0 && scene.Plugin.Memory.WriteCalls == 0,
                    "进程替换后不得读写新进程来回收旧分配。");
            var readsAfterFailure = scene.Memory.ReadCalls;
            scene.Spawner.ResetWorld();
            scene.Spawner.Clear();
            Assert(!scene.Pending && scene.Memory.ReadCalls == readsAfterFailure && scene.Plugin.CallCount == previousCalls,
                "重置必须直接解除旧记录，不能补做旧世界的回收。");
        }

        private static void VerifyUnknownIdentity()
        {
            var scene = new Scene();
            scene.Plugin.BeforeSetup = () => scene.Memory.FailRead = true;
            scene.FailCreation(false);
            scene.Memory.FailRead = false;
            scene.Plugin.FailDelete = false;
            var reads = scene.Memory.ReadCalls;
            var calls = scene.Plugin.CallCount;
            Throws<AggregateException>(() => scene.Spawner.Clear());
            Assert(scene.Pending && scene.Memory.ReadCalls == reads && scene.Plugin.CallCount == calls,
                "初次未采到身份时，即使槽位后来可读也不能重新认领并删除。");
            scene.Spawner.ResetWorld();
            Assert(!scene.Pending, "未知身份记录只能由世界重置解除。");
        }

        private static void VerifyWorldReset()
        {
            var scene = new Scene();
            scene.FailCreation(false);
            scene.Memory.FailRead = true;
            scene.Memory.FailWrite = true;
            scene.Plugin.FailEveryCall = true;
            var reads = scene.Memory.ReadCalls;
            var writes = scene.Memory.WriteCalls;
            var calls = scene.Plugin.CallCount;
            scene.Spawner.ResetWorld();
            scene.Spawner.Clear();
            Assert(!scene.Pending && scene.FailedCount == 0 && scene.Memory.ReadCalls == reads &&
                scene.Memory.WriteCalls == writes && scene.Plugin.CallCount == calls,
                "世界重置及其后的清理不得访问旧内存或调用旧函数。");
            Throws<InvalidOperationException>(() => scene.Spawner.SpawnBNpc(1, 1, 1));
            Assert(scene.Plugin.CallCount == calls, "未恢复世界时不得创建新角色。");
            scene.Memory.FailRead = false;
            scene.Memory.FailWrite = false;
            scene.Plugin.FailEveryCall = false;
            scene.Plugin.FailSetup = false;
            scene.Plugin.FailDelete = false;
            scene.Invoke("ResumeWorld");
            var resumed = scene.Spawner.SpawnBNpc(1, 1, 1);
            Assert(resumed.IsAlive && scene.Spawner.Owns(resumed), "新世界恢复后应能重新创建。");
            scene.Spawner.Clear();
        }

        private static void VerifySetupConflict(Action<Scene> mutate)
        {
            var scene = new Scene();
            scene.Plugin.BeforeSetup = () => mutate(scene);
            scene.Plugin.FailSetup = true;
            scene.Plugin.FailDelete = false;
            var error = Throws<AggregateException>(() => scene.Spawner.SpawnBNpc(1, 1, 1));
            Assert(error.InnerExceptions.Count == 2 && ReferenceEquals(error.InnerExceptions[0], scene.Plugin.SetupFailure),
                "冲突必须保留初始化异常并报告无法安全回收。");
            Assert(scene.Pending && scene.FailedCount == 1 && scene.Plugin.DeleteCalls == 0,
                "首次回滚明确遇到身份冲突时不能按旧索引删除。");
            scene.SetIdentity(scene.Native, 101u);
            var reads = scene.Memory.ReadCalls;
            var calls = scene.Plugin.CallCount;
            Throws<AggregateException>(() => scene.Spawner.Clear());
            Assert(scene.Pending && scene.Memory.ReadCalls == reads && scene.Plugin.CallCount == calls && scene.Plugin.DeleteCalls == 0,
                "初始化时已见身份冲突，即使原字段重新出现也不得再认领。");
            scene.Spawner.ResetWorld();
            scene.Spawner.Clear();
            Assert(!scene.Pending && scene.Plugin.DeleteCalls == 0, "重置应直接移除冲突记录。");
        }

        private static void VerifyConflictRemainsAfterOriginalReturns()
        {
            var scene = new Scene();
            scene.FailCreation(false);
            scene.Plugin.FailDelete = false;
            scene.SetIdentity(new IntPtr(0x50000), 999u);
            Throws<AggregateException>(() => scene.Spawner.Clear());
            Assert(scene.Pending && scene.Plugin.DeleteCalls == 1, "首次重试遇到替换对象时不得删除。");
            scene.SetIdentity(scene.Native, 101u);
            var reads = scene.Memory.ReadCalls;
            var calls = scene.Plugin.CallCount;
            Throws<AggregateException>(() => scene.Spawner.Clear());
            Assert(scene.Pending && scene.Memory.ReadCalls == reads && scene.Plugin.CallCount == calls && scene.Plugin.DeleteCalls == 1,
                "已经确认资源被替换后，旧地址和字段重现也不能证明仍为原对象。");
            scene.Spawner.ResetWorld();
            scene.Spawner.Clear();
            Assert(!scene.Pending && scene.Plugin.DeleteCalls == 1, "只有世界重置能解除冲突记录。");
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

        internal sealed class Scene
        {
            internal readonly FakeLocalMemory Memory = new FakeLocalMemory();
            internal readonly FakeLocalPlugin Plugin;
            internal readonly EntitySpawner Spawner;
            internal readonly EntityRegistry Registry;
            internal IntPtr Native { get { return Plugin.LastNative; } }
            internal IntPtr LocalPointer { get { return new IntPtr(0x10010 + Plugin.LastIndex * 0x10); } }
            internal IntPtr ObjectPointer { get { return new IntPtr(0x20020 + (200 + Plugin.LastIndex) * IntPtr.Size); } }
            internal bool Pending { get { return (bool)typeof(EntitySpawner).GetProperty("HasPendingCreationCleanup", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Spawner, null); } }
            internal int FailedCount { get { return ((IList)typeof(EntitySpawner).GetField("_failedLocalCreations", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Spawner)).Count; } }
            internal int OwnedCount { get { return ((IDictionary)typeof(EntitySpawner).GetField("_owned", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Spawner)).Count; } }

            internal Scene()
            {
                Plugin = new FakeLocalPlugin(Memory);
                typeof(NamazuInterop).GetProperty("Plugin").GetSetMethod(true).Invoke(null, new object[] { new NamazuPlugin(Plugin) });
                ClientObjectManager.InstancePtr = new IntPtr(0x10000);
                GameObjectManager.InstancePtr = new IntPtr(0x20000);
                CharacterManager.InstancePtr = IntPtr.Zero;
                ClientObjectManager.CreateBattleCharacterFuncPtr = FakeLocalPlugin.CreateFunction;
                ClientObjectManager.GetObjectByIndexFuncPtr = FakeLocalPlugin.GetFunction;
                ClientObjectManager.DeleteObjectByIndexFuncPtr = FakeLocalPlugin.DeleteFunction;
                CharacterSetupContainer.SetupBNpcFuncPtr = FakeLocalPlugin.SetupFunction;
                CharacterSetupContainer.CopyFromCharacterFuncPtr = FakeLocalPlugin.CopyFunction;
                PacketDispatcher.HandleSpawnObjectPacketFuncPtr = FakeLocalPlugin.EventObjectFunction;
                PacketDispatcher.HandleSpawnNpcPacketFuncPtr = FakeLocalPlugin.NetworkFunction;
                PacketDispatcher.HandleDespawnCharacterPacketFuncPtr = FakeLocalPlugin.DespawnFunction;
                PacketDispatcher.HandleActorControlFuncPtr = FakeLocalPlugin.ActorControlFunction;
                Registry = new EntityRegistry();
                Spawner = new EntitySpawner(Registry, new EntityProvider(Registry, () => 0), null, () => true);
                Invoke("ResumeWorld");
            }

            internal void Invoke(string method)
            {
                try { typeof(EntitySpawner).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Spawner, null); }
                catch (TargetInvocationException error) { throw error.InnerException; }
            }

            internal void FailCreation(bool get)
            {
                Plugin.FailGet = get;
                Plugin.FailSetup = !get;
                Plugin.FailDelete = true;
                var error = Throws<AggregateException>(() => Spawner.SpawnBNpc(1, 1, 1));
                Assert(error.InnerExceptions.Count == 2 && ReferenceEquals(error.InnerExceptions[0], Plugin.SetupFailure) &&
                    ReferenceEquals(error.InnerExceptions[1], Plugin.DeleteFailure), "必须保留实际初始化与回收异常。");
                Assert(Pending, "双失败后必须存在真实失败分配记录。");
            }

            internal void SetIdentity(IntPtr pointer, uint id)
            {
                Memory.Put(LocalPointer, pointer);
                Memory.Put(ObjectPointer, pointer);
                Memory.Put(pointer + 0x78, id);
                Memory.Put(pointer + 0x8C, (ushort)(200 + Plugin.LastIndex));
                Memory.Put(pointer + 0x90, ObjectKind.BattleNpc);
            }
        }
    }

    // dynamic 包装跨程序集访问要求替身类型及入口公开。
    public sealed class FakeLocalMemory
    {
        private readonly Dictionary<long, object> _values = new Dictionary<long, object>();
        public IntPtr ProcessHandle = new IntPtr(101);
        public bool IsProcessOpen = true;
        public bool FailRead;
        public bool FailWrite;
        public int ReadCalls;
        public int WriteCalls;
        public int ReadBytesCalls;
        public int AllocateCalls;
        public int FreeCalls;
        public bool FailReadBytes;
        public IntPtr FailReadAddress;
        public Action<IntPtr> BeforeWrite;
        public readonly Dictionary<long, int> ReadsByAddress = new Dictionary<long, int>();
        private int _allocationIndex;

        public void Put<T>(IntPtr address, T value) where T : struct { _values[address.ToInt64()] = value; }
        public T Read<T>(IntPtr address) where T : struct
        {
            ReadCalls++;
            int count;
            ReadsByAddress.TryGetValue(address.ToInt64(), out count);
            ReadsByAddress[address.ToInt64()] = count + 1;
            if (FailRead || address == FailReadAddress) throw new InvalidOperationException("模拟内存读取失败。");
            object value;
            return _values.TryGetValue(address.ToInt64(), out value) ? (T)value : default(T);
        }
        public void Write<T>(IntPtr address, T value) where T : struct
        {
            WriteCalls++;
            if (BeforeWrite != null) BeforeWrite(address);
            if (FailWrite) throw new InvalidOperationException("模拟内存写入失败。");
            Put(address, value);
        }

        public byte[] ReadBytes(IntPtr address, int count)
        {
            ReadBytesCalls++;
            if (FailRead || FailReadBytes) throw new InvalidOperationException("模拟指针表批量读取失败。");
            var bytes = new byte[count];
            for (var offset = 0; offset + IntPtr.Size <= count; offset += IntPtr.Size)
            {
                object value;
                if (!_values.TryGetValue(address.ToInt64() + offset, out value)) continue;
                var pointer = (IntPtr)value;
                Array.Copy(BitConverter.GetBytes(pointer.ToInt64()), 0, bytes, offset, IntPtr.Size);
            }
            return bytes;
        }

        public IntPtr AllocateMemory(int size)
        {
            AllocateCalls++;
            return new IntPtr(0xF00000 + ++_allocationIndex * 0x10000);
        }

        public bool FreeMemory(IntPtr address)
        {
            FreeCalls++;
            _values.Remove(address.ToInt64());
            return true;
        }
    }

    public sealed class FakeLocalPlugin
    {
        private object PluginUi;
        public static readonly IntPtr CreateFunction = new IntPtr(0x101);
        public static readonly IntPtr GetFunction = new IntPtr(0x102);
        public static readonly IntPtr DeleteFunction = new IntPtr(0x103);
        public static readonly IntPtr SetupFunction = new IntPtr(0x104);
        public static readonly IntPtr EventObjectFunction = new IntPtr(0x105);
        public static readonly IntPtr HideFunction = new IntPtr(0x106);
        public static readonly IntPtr NetworkFunction = new IntPtr(0x107);
        public static readonly IntPtr DespawnFunction = new IntPtr(0x108);
        public static readonly IntPtr CopyFunction = new IntPtr(0x109);
        public static readonly IntPtr ActorControlFunction = new IntPtr(0x110);
        public FakeLocalMemory Memory;
        public bool FailGet;
        public bool FailSetup;
        public bool FailDelete;
        public bool FailEveryCall;
        public Action BeforeSetup;
        public int CreateCalls;
        public int DeleteCalls;
        public int CallCount;
        public ushort LastIndex;
        public IntPtr LastNative;
        public readonly List<ushort> DeletedIndices = new List<ushort>();
        public readonly List<IntPtr> HiddenObjects = new List<IntPtr>();
        public readonly List<uint> NetworkIds = new List<uint>();
        public readonly Exception SetupFailure = new InvalidOperationException("模拟角色读取或初始化失败。");
        public readonly Exception DeleteFailure = new InvalidOperationException("模拟角色删除失败。");
        public FakeLocalPlugin(FakeLocalMemory memory) { Memory = memory; PluginUi = new object(); }

        public T Call<T>(IntPtr function, params object[] args) where T : struct
        {
            CallCount++;
            if (FailEveryCall) throw new InvalidOperationException("不应调用旧函数。");
            if (function == CreateFunction)
            {
                LastIndex = (ushort)++CreateCalls;
                LastNative = new IntPtr(0x30000 + LastIndex * 0x10000);
                Memory.Put(new IntPtr(0x10010 + LastIndex * 0x10), LastNative);
                Memory.Put(new IntPtr(0x20020 + (200 + LastIndex) * IntPtr.Size), LastNative);
                Memory.Put(LastNative + 0x78, (uint)(100 + LastIndex));
                Memory.Put(LastNative + 0x8C, (ushort)(200 + LastIndex));
                Memory.Put(LastNative + 0x90, ObjectKind.BattleNpc);
                return (T)(object)(uint)LastIndex;
            }
            if (function == GetFunction)
            {
                if (FailGet) throw SetupFailure;
                return (T)(object)LastNative;
            }
            if (function == CopyFunction) return (T)(object)0ul;
            if (function == EventObjectFunction)
            {
                var packet = Memory.Read<SpawnObjectPacket>((IntPtr)args[1]);
                var pointer = new IntPtr(0xB00000 + packet.ObjectIndex * 0x10000);
                Memory.Put(new IntPtr(0x20020 + (449 + packet.ObjectIndex) * IntPtr.Size), pointer);
                Memory.Put(pointer + 0x78, packet.EntityId);
                Memory.Put(pointer + 0x8C, (ushort)(449 + packet.ObjectIndex));
                Memory.Put(pointer + 0x90, ObjectKind.EventObj);
                Memory.Put(pointer, new IntPtr(0xAA0000));
                Memory.Put(new IntPtr(0xAA0000 + 13 * 8), HideFunction);
                return (T)(object)pointer;
            }
            throw new InvalidOperationException("未知泛型原生调用。");
        }

        public void Call(IntPtr function, params object[] args)
        {
            CallCount++;
            if (FailEveryCall) throw new InvalidOperationException("不应调用旧函数。");
            if (function == SetupFunction)
            {
                if (BeforeSetup != null) BeforeSetup();
                if (FailSetup) throw SetupFailure;
                return;
            }
            if (function == HideFunction)
            {
                HiddenObjects.Add((IntPtr)args[0]);
                return;
            }
            if (function == NetworkFunction)
            {
                NetworkIds.Add(Convert.ToUInt32(args[0]));
                return;
            }
            if (function == DespawnFunction) return;
            if (function == DeleteFunction)
            {
                DeleteCalls++;
                var index = Convert.ToUInt16(args[1]);
                DeletedIndices.Add(index);
                if (FailDelete) throw DeleteFailure;
                Memory.Put(new IntPtr(0x10010 + index * 0x10), IntPtr.Zero);
                Memory.Put(new IntPtr(0x20020 + (200 + index) * IntPtr.Size), IntPtr.Zero);
                return;
            }
            throw new InvalidOperationException("未知原生调用。");
        }
    }

}
