namespace Simulant.Tests.Native
{
    using Simulant.ACT;
    using Simulant.Core.Entity;
    using Simulant.Game.FFCS.Client.Game.Object;
    using Simulant.Simulation.Runtime;
    using Simulant.Simulation.Timeline;
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    internal static class NameTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static int Main()
        {
            try
            {
                VerifyNativeNameAndRestore();
                VerifyBufferFailures();
                VerifyAppliedFailureAndRestoreRetry();
                VerifyInvalidIdentityAndExternalName();
                Console.WriteLine("原生玩家改名与恢复验证通过：4 项。");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("原生玩家改名与恢复验证失败：\n" + error);
                return 1;
            }
        }

        private static void VerifyNativeNameAndRestore()
        {
            using (var scene = new Scene())
            {
                scene.Rename("MT黑骑");
                Assert(scene.Name == "MT黑骑" && scene.Plugin.NameChanges == 1, "必须通过原生改名入口设置临时名字并刷新名牌。");
                scene.Manager.Clear();
                Assert(scene.Name == Scene.Original && scene.Player.Entity.IsAlive && scene.Plugin.NameChanges == 2,
                    "还原超过网络名字长度的原名时，也应通过原生入口，且保留借用玩家。");
                Assert(scene.Memory.Bytes.Allocations.SequenceEqual(scene.Memory.Bytes.Freed), "改名和还原都必须释放远程缓冲区。");
            }
        }

        private static void VerifyBufferFailures()
        {
            foreach (var failure in new[] { "分配为空", "写入不全", "原生拒绝" })
                using (var scene = new Scene())
                {
                    scene.Memory.Failure = failure;
                    scene.Plugin.BeforeSet = () => { if (failure == "原生拒绝") throw new InvalidOperationException("原生函数拒绝调用。"); };
                    Throws<InvalidOperationException>(() => scene.Rename("D1蝰蛇"));
                    scene.Memory.Failure = null;
                    scene.Plugin.BeforeSet = null;
                    scene.Manager.Clear();
                    Assert(scene.Name == Scene.Original && !scene.Pending,
                        "改名前失败不能改变原名，停止时应释放未生效记录。");
                    Assert(scene.Memory.Bytes.Allocations.SequenceEqual(scene.Memory.Bytes.Freed), "失败时也必须释放已分配的远程名字缓冲区。");
                }
        }

        private static void VerifyAppliedFailureAndRestoreRetry()
        {
            using (var scene = new Scene())
            {
                scene.Plugin.AfterSet = () => { throw new InvalidOperationException("名字生效后返回失败。"); };
                Throws<InvalidOperationException>(() => scene.Rename("D1蝰蛇"));
                scene.Plugin.AfterSet = null;
                scene.Plugin.BeforeSet = () => { throw new InvalidOperationException("恢复暂时失败。"); };
                scene.Manager.Clear();
                Assert(scene.Pending && scene.Name == "D1蝰蛇", "失败后必须保留原名恢复责任，不能把别名当作原名。");
                scene.Plugin.BeforeSet = null;
                scene.Manager.Clear();
                Assert(!scene.Pending && scene.Name == Scene.Original && scene.Player.Entity.IsAlive,
                    "再次停止应还原最初名字并解除恢复责任。");
            }
        }

        private static void VerifyInvalidIdentityAndExternalName()
        {
            using (var scene = new Scene())
            {
                scene.Rename("MT黑骑");
                scene.Memory.Bytes.PutBytes(Scene.Address + 0x30, Encode("外部名字"));
                scene.Manager.Clear();
                Assert(scene.Name == "外部名字" && scene.Plugin.NameChanges == 1, "不能还原覆盖外部新改名。");
            }
            using (var scene = new Scene())
            {
                scene.Rename("MT黑骑");
                scene.Registry.Reset();
                scene.Manager.Clear();
                Assert(!scene.Pending && scene.Plugin.NameChanges == 1, "跨世界后不能写入旧实体地址。");
            }
        }

        internal static byte[] Encode(string name)
        {
            var result = new byte[64];
            var encoded = Encoding.UTF8.GetBytes(name);
            Array.Copy(encoded, result, encoded.Length);
            return result;
        }
        internal static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new Exception("预期抛出 " + typeof(T).Name + "。");
        }

        private sealed class Scene : IDisposable
        {
            internal static readonly IntPtr Address = new IntPtr(0x300000);
            internal const string Original = "原来的玩家名字超过三十一字节长度";
            internal readonly NameMemory Memory = new NameMemory();
            internal readonly NamePlugin Plugin;
            internal readonly SimEntityManager Manager;
            internal readonly SimActor Player;
            internal readonly EntityRegistry Registry;
            private readonly SimScheduler _scheduler = new SimScheduler();
            private readonly NamazuPlugin _previousPlugin;
            private readonly IntPtr _previousFunction;
            internal string Name => Encoding.UTF8.GetString(Memory.Bytes.ReadBytes(Address + 0x30, 64)).Split('\0')[0];
            internal bool Pending => (bool)typeof(SimEntityManager).GetProperty("HasPendingCleanup", Members).GetValue(Manager);

            internal Scene()
            {
                _previousPlugin = NamazuInterop.Plugin;
                _previousFunction = GameObject.SetNameFuncPtr;
                Plugin = new NamePlugin(Memory);
                typeof(NamazuInterop).GetProperty("Plugin").GetSetMethod(true).Invoke(null, new object[] { new NamazuPlugin(Plugin) });
                GameObject.SetNameFuncPtr = NamePlugin.SetFunction;
                Memory.Bytes.Put(Address, new IntPtr(0x500000));
                Memory.Bytes.Put(new IntPtr(0x500000 + 6 * 8), NamePlugin.GetFunction);
                Memory.Bytes.Put(Address + 0x78, 0x10012345u);
                Memory.Bytes.PutBytes(Address + 0x30, Encode(Original));
                Registry = new EntityRegistry(slot => slot == 0 ? (EntityObservation?)new EntityObservation(Address, 0x10012345, ObjectKind.Pc) : null, 10);
                Manager = new SimEntityManager(new EntitySpawner(Registry, new EntityProvider(Registry, () => 0x10012345)));
                typeof(SimEntityManager).GetProperty("Scheduler", Members).SetValue(Manager, _scheduler, null);
                Player = Manager.Attach((Character)Registry.Get(0));
            }
            internal void Rename(string name)
            {
                try { typeof(SimEntityManager).GetMethod("SetTemporaryName", Members).Invoke(Manager, new object[] { Player, name }); }
                catch (TargetInvocationException error) { throw error.InnerException; }
            }
            public void Dispose()
            {
                Memory.Failure = null;
                Plugin.BeforeSet = null;
                Plugin.AfterSet = null;
                _scheduler.Dispose();
                Manager.Clear();
                GameObject.SetNameFuncPtr = _previousFunction;
                typeof(NamazuInterop).GetProperty("Plugin").GetSetMethod(true).Invoke(null, new object[] { _previousPlugin });
            }
        }
    }

    public sealed class NameMemory
    {
        public readonly PartyMemory Bytes = new PartyMemory();
        public string Failure;
        public T Read<T>(IntPtr address) where T : struct => Bytes.Read<T>(address);
        public byte[] ReadBytes(IntPtr address, int count) => Bytes.ReadBytes(address, count);
        public IntPtr AllocateMemory(int size)
        {
            NameTests.Assert(size == 64, "原生名字缓冲区必须保留 64 字节容量。");
            return Failure == "分配为空" ? IntPtr.Zero : Bytes.AllocateMemory(size);
        }
        public int WriteBytes(IntPtr address, byte[] data)
        {
            if (Failure == "写入不全") Bytes.ShortWriteAddress = address;
            return Bytes.WriteBytes(address, data);
        }
        public bool FreeMemory(IntPtr address) => Bytes.FreeMemory(address);
    }

    public sealed class NamePlugin
    {
        private readonly object PluginUi = new object();
        public static readonly IntPtr SetFunction = new IntPtr(0x302), GetFunction = new IntPtr(0x303);
        public readonly NameMemory Memory;
        public Action BeforeSet, AfterSet;
        public int NameChanges;
        public NamePlugin(NameMemory memory) { Memory = memory; }
        public T Call<T>(IntPtr function, params object[] args) where T : struct
        {
            NameTests.Assert(function == GetFunction && args.Length == 1 && args[0] is IntPtr, "名字读取应走现有虚函数。");
            return (T)(object)((IntPtr)args[0] + 0x30);
        }
        public void Call(IntPtr function, params object[] args)
        {
            NameTests.Assert(function == SetFunction && args.Length == 2 && args.All(a => a is IntPtr),
                "原生改名入口只能传角色地址和名字远程地址，不能直接传字符串或结构体。");
            var buffer = (IntPtr)args[1];
            NameTests.Assert(Memory.Bytes.Allocations.Contains(buffer) && !Memory.Bytes.Freed.Contains(buffer),
                "原生改名只能读取尚未释放的远程参数。");
            BeforeSet?.Invoke();
            var bytes = Memory.Bytes.ReadBytes(buffer, 64);
            // 原生按字符串复制；终止符之后的旧尾部不影响名字及恢复判断。
            Memory.Bytes.PutBytes((IntPtr)args[0] + 0x30, bytes.Take(Array.IndexOf(bytes, (byte)0) + 1).ToArray());
            NameChanges++;
            AfterSet?.Invoke();
        }
    }
}
