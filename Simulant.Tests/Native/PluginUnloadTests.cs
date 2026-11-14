namespace Simulant.Tests.Native
{
    using Simulant;
    using Simulant.ACT;
    using Simulant.Core;
    using Simulant.Core.Entity;
    using Simulant.Core.Environment;
    using Simulant.Game.Data;
    using Simulant.Game.FFCS.Client.Game;
    using Simulant.Game.FFCS.Client.Game.Event;
    using Simulant.Game.FFCS.Client.Game.Object;
    using Simulant.Simulation;
    using Simulant.Simulation.Runtime;
    using Simulant.Simulation.Timeline;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    // 引用正式 DLL 的卸载入口、界面、会话、区域和防火墙；仅构造离线宿主并替换原生边界。
    internal static class PluginUnloadTests
    {
        private static readonly BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static int _passed;
        private static string _gamePath;
        internal static Scene Current;

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 1) throw new ArgumentException("卸载验证需要客户端目录，以只读方式加载地图表。");
                _gamePath = args[0];
                VerifySuccess(false);
                VerifySuccess(true);
                foreach (var failure in new[] { "回收实体", "加载地图", "设置区域", "恢复位置", "恢复音乐", "恢复收包", "释放代码洞" })
                    VerifyRetry(failure);
                Console.WriteLine("真实插件卸载故障注入通过：" + _passed + " 项。");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("验证失败：插件退出模拟并卸载。\n" + error);
                return 1;
            }
        }

        private static void VerifySuccess(bool interruptedEntry)
        {
            var scene = new Scene(interruptedEntry);
            scene.Main.DeInitPlugin();
            scene.AssertCompleted();
            var stages = new[] { "停止预设", "回收实体", "加载地图", "设置区域", "恢复位置", "恢复音乐", "恢复收包", "恢复发包", "释放代码洞" };
            var last = -1;
            foreach (var stage in stages)
            {
                var index = scene.Events.IndexOf(stage);
                Assert(index > last, "卸载顺序不正确：" + stage + "；实际：" + string.Join("、", scene.Events));
                last = index;
            }
            var count = scene.Events.Count;
            scene.Main.DeInitPlugin();
            Assert(scene.Events.Count == count, "卸载成功后不能重复执行原生恢复。");
            Passed(interruptedEntry ? "进入模拟中途失败也先返回真实区域再卸载" : "停止预设和调试后恢复真实地图、位置和通信");
        }

        private static void VerifyRetry(string failure)
        {
            var scene = new Scene(false);
            scene.Memory.Failure = failure;
            Throws<AggregateException>(scene.Main.DeInitPlugin);
            Assert(ReferenceEquals(Get(scene.Main, "_host"), scene.Host) && ReferenceEquals(PluginHost.Instance, scene.Host),
                "失败时必须保留旧宿主，不能宣称已卸载。");
            Assert(!scene.UI.IsDisposed && !(bool)Get(scene.Host, "_disposeCompleted"), "失败时必须保留恢复入口。");
            Assert(!scene.Session.IsRunning && !scene.DebugScheduler.IsRunning && scene.Phase.IsCompleted && !scene.Phase.Result,
                "恢复失败不能让模拟节点或阶段等待继续执行。");
            var hookFailure = failure == "恢复收包" || failure == "释放代码洞";
            if (!hookFailure)
            {
                Assert(scene.PendingReturn && !scene.Events.Contains("恢复收包") && !scene.Events.Contains("恢复发包"),
                    "实体或地图恢复失败时不得提前恢复通信，必须保留真实地图记录。");
                if (failure == "回收实体") Assert(!scene.Events.Contains("加载地图"), "实体未清理完成不能先切图。");
            }
            else
                Assert(!scene.PendingReturn && scene.FirewallPending, "地图已恢复后，仅保留尚未完成的防火墙清理。");

            var loads = scene.Events.Count(item => item == "加载地图");
            scene.Memory.Failure = null;
            scene.Main.DeInitPlugin();
            scene.AssertCompleted();
            Assert(scene.Events.Count(item => item == "停止预设") == 1, "重试卸载不能重复执行预设 OnStop。");
            if (hookFailure)
                Assert(scene.Events.Count(item => item == "加载地图") == loads, "仅防火墙失败时不能重复切图。");
            Passed(failure + "失败保留记录，下一次禁用可完成卸载");
        }

        private static void Passed(string name) { _passed++; Console.WriteLine("通过：" + name); }
        internal static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new Exception("预期抛出 " + typeof(T).Name + "。");
        }
        internal static object Get(object target, string name) => target.GetType().GetField(name, Members).GetValue(target);
        internal static void Set(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
        private static object Create(string name, params object[] args)
            => Activator.CreateInstance(typeof(PluginHost).Assembly.GetType(name, true), Members, null, args, null);
        private static object Invoke(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, Members).Invoke(target, args); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }

        internal sealed class Scene
        {
            internal readonly List<string> Events = new List<string>();
            internal readonly PluginMain Main = new PluginMain();
            internal readonly PluginHost Host;
            internal readonly UnloadMemory Memory;
            internal readonly Control UI;
            internal readonly SimSession Session;
            internal readonly SimScheduler DebugScheduler = new SimScheduler();
            internal readonly Task<bool> Phase;
            internal readonly ManualResetEventSlim Prepared = new ManualResetEventSlim();
            private readonly object _zone;
            private readonly object _firewall;
            private bool _entityPresent;
            internal bool PendingReturn => (bool)_zone.GetType().GetProperty("HasPendingReturn", Members).GetValue(_zone);
            internal bool FirewallPending => (bool)_firewall.GetType().GetProperty("HasPendingCleanup", Members).GetValue(_firewall);

            internal Scene(bool interruptedEntry)
            {
                Current = this;
                Memory = new UnloadMemory(Events);
                var native = new UnloadPlugin(Memory, () =>
                {
                    Assert(!Session.IsRunning && !DebugScheduler.IsRunning && DebugScheduler.PendingCount == 0,
                        "回收实体前必须停止预设和调试调度器。");
                    _entityPresent = false;
                });
                typeof(NamazuInterop).GetProperty("Plugin").GetSetMethod(true).Invoke(null, new object[] { new NamazuPlugin(native) });
                GameMain.InstancePtr = UnloadMemory.Game;
                GameMain.LoadZoneFuncPtr = UnloadPlugin.LoadFunction;
                EventFramework.InstancePtrPtr = UnloadMemory.EventPointer;
                EventFramework.SetTerritoryTypeIdFuncPtr = UnloadPlugin.TerritoryFunction;
                EventFramework.SetupInstanceContentFuncPtr = UnloadPlugin.SetupFunction;
                EventFramework.FinalizeInstanceContentFuncPtr = UnloadPlugin.FinalizeFunction;
                GameObjectManager.InstancePtr = new IntPtr(0x70000);
                ClientObjectManager.InstancePtr = new IntPtr(0x80000);
                ClientObjectManager.DeleteObjectByIndexFuncPtr = UnloadPlugin.DeleteFunction;
                BGMSystem.InstancePtr = UnloadMemory.BgmPointer;

                // 绕过连接 ACT、异步读取游戏数据的构造阶段，全部待验证方法仍直接来自正式 DLL。
                Host = (PluginHost)FormatterServices.GetUninitializedObject(typeof(PluginHost));
                Set(Host, "<Configuration>k__BackingField", new Configuration());
                var data = new GameDataService(_gamePath);
                typeof(GameDataService).GetMethod("Activate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(data, null);
                Set(Host, "_gameData", data);
                foreach (var gate in new[] { "_initLock", "_subscriptionGate", "_disposeGate", "_debugGate" }) Set(Host, gate, new object());
                Set(Host, "_statusLabel", new Label());
                Set(Host, "PluginLog", Create("Simulant.Core.PluginLog"));
                var registry = new EntityRegistry(slot => slot == 0
                    ? new EntityObservation(UnloadMemory.Player, 1, ObjectKind.Pc)
                    : slot == 1 && _entityPresent ? new EntityObservation(new IntPtr(0x50000), 2, ObjectKind.BattleNpc) : (EntityObservation?)null);
                var spawner = new EntitySpawner(registry, new EntityProvider(registry, () => 1));
                Set(Host, "EntityRegistry", registry);
                Set(Host, "EntityProvider", new EntityProvider(registry, () => 1));
                Set(Host, "EntitySpawner", spawner);
                Set(Host, "<PluginReady>k__BackingField", true);
                _zone = Create("Simulant.Core.Zone.ZoneService", Host);
                _firewall = Create("Simulant.Core.Firewall.FirewallService", Host);
                Set(Host, "ZoneService", _zone);
                Set(Host, "FirewallService", _firewall);
                Set(Host, "EnvironmentService", Create("Simulant.Core.Environment.EnvironmentService", Host));
                UI = (Control)Create("Simulant.UI.SimulantUI", Host);
                Set(Host, "_ui", UI);
                Set(Main, "_host", Host);
                PluginHost.Instance = Host;
                SeedHook("_receive", UnloadMemory.Receive, false);
                SeedHook("_send", UnloadMemory.Send, true);

                if (interruptedEntry) Memory.Failure = "加载地图";
                var entered = (bool)Invoke(_zone, "TryEnterTerritory", 65000, 0);
                Assert(entered != interruptedEntry && PendingReturn, "测试必须通过真实区域入口取得返回责任。");
                Memory.Failure = null;
                Memory.Put(UnloadMemory.Player + 0xB0, new Vector3(100, 0, 100));
                // 部分进入失败不允许阶段应用；此用例只需验证真实返回记录仍参与卸载。
                Phase = (Task<bool>)Invoke(_zone, "EnterPhase", PhaseData.Empty, true);
                _entityPresent = true;
                var entity = registry.RegisterCreated(1);
                var owned = Activator.CreateInstance(typeof(EntitySpawner).GetNestedType("OwnedEntity", BindingFlags.NonPublic), true);
                Set(owned, "Entity", entity);
                Set(owned, "LocalIndex", (ushort)1);
                ((IDictionary)Get(spawner, "_owned")).Add(entity.Identity, owned);

                var preset = new UnloadPreset();
                Session = preset.CreateSimSession(Host);
                var presetControl = Get(UI, "presetControl");
                Set(presetControl, "_preset", preset);
                Set(presetControl, "_session", Session);
                Session.Start();
                Assert(Prepared.Wait(3000), "测试预设未开始执行。");
                DebugScheduler.Schedule(60, () => { throw new Exception("已取消调试节点不应执行。"); });
                DebugScheduler.Start();
                Set(Host, "_debugScheduler", DebugScheduler);
                Set(Host, "_debugEntities", new SimEntityManager(spawner));
                Events.Clear();
            }

            private void SeedHook(string field, IntPtr target, bool cave)
            {
                var state = Activator.CreateInstance(_firewall.GetType().GetNestedType("HookState", BindingFlags.NonPublic), true);
                Set(state, "Memory", NamazuInterop.Plugin.Memory);
                Set(state, "ProcessHandle", Memory.ProcessHandle);
                Set(state, "Target", target);
                Set(state, "Original", new byte[cave ? 15 : 5]);
                Set(state, "Cave", cave ? UnloadMemory.Cave : IntPtr.Zero);
                Set(state, "Enabled", true);
                Set(state, "NeedsRestore", true);
                Set(_firewall, field, state);
            }

            internal void AssertCompleted()
            {
                Assert(Get(Main, "_host") == null && PluginHost.Instance == null && UI.IsDisposed, "完整恢复后才可释放宿主和界面。");
                Assert(!Session.IsRunning && DebugScheduler.PendingCount == 0 && Phase.IsCompleted && !Phase.Result,
                    "完整卸载必须取消所有模拟调度和旧阶段等待。");
                Assert(!PendingReturn && !FirewallPending && !_entityPresent, "卸载后不得残留地图、防火墙或实体回收责任。");
                Assert(Memory.Read<uint>(UnloadMemory.Game + 0x4108) == 177 &&
                    Memory.Read<Vector3>(UnloadMemory.Player + 0xB0) == new Vector3(1, 3, 2), "必须恢复原地图及原始玩家位置。");
                Assert((bool)Get(Host, "_disposeCompleted"), "全部恢复完成后才可标记卸载成功。");
                Assert(Get(Host, "_gameData") == null, "完整卸载必须释放客户端数据。");
                Throws<InvalidOperationException>(() => { var unused = GameDataService.Instance; });
                Prepared.Dispose();
            }
        }
    }

    public sealed class UnloadPreset : SimPresetBase
    {
        public override int TerritoryId => 65000;
        public override string Name => "卸载验证";
        public override string Author => "测试";
        public override DateTime LastUpdated => new DateTime(2026, 11, 6);
        public override PhaseData Phase => PhaseData.Empty;
        public override string Description => "验证真实卸载调用链。";
        public override List<SimOptionBase> Options => new List<SimOptionBase>();
        public override int Level => 1;
        public override Type SimLogicType => typeof(UnloadLogic);
    }

    public sealed class UnloadLogic : SimLogicBase
    {
        public UnloadLogic(PluginHost host, SimPresetBase preset) : base(host, preset) { }
        protected override void BuildTimeline(SimTimeline timeline)
        {
            timeline.At(0, () => PluginUnloadTests.Current.Prepared.Set());
            timeline.At(60, () => { throw new Exception("已取消预设节点不应执行。"); });
        }
        protected override void OnStop() => PluginUnloadTests.Current.Events.Add("停止预设");
    }

    // 复用本地实体故障注入的内存储存，不接触真实进程。
    public sealed class UnloadMemory
    {
        public static readonly IntPtr Game = new IntPtr(0x100000), EventPointer = new IntPtr(0x200000),
            BgmPointer = new IntPtr(0x300000), Scene = new IntPtr(0x320000), Player = new IntPtr(0x40000),
            Receive = new IntPtr(0xA00000), Send = new IntPtr(0xB00000), Cave = new IntPtr(0xC00000);
        private readonly FakeLocalMemory _storage = new FakeLocalMemory();
        private readonly List<string> _events;
        public string Failure;
        public IntPtr ProcessHandle => new IntPtr(101);
        public bool IsProcessOpen => true;
        public UnloadMemory(List<string> events)
        {
            _events = events;
            Put(Game + 0x4108, 177u);
            Put(EventPointer, new IntPtr(0x210000));
            Put(BgmPointer, new IntPtr(0x310000));
            Put(new IntPtr(0x3100C0), Scene);
            Put(Player + 0xB0, new Vector3(1, 3, 2));
            Put(Player, new IntPtr(0x60000));
            Put(new IntPtr(0x60000 + 54 * 8), UnloadPlugin.PositionFunction);
        }
        public void Step(string name)
        {
            _events.Add(name);
            if (Failure == name) throw new InvalidOperationException("注入卸载失败：" + name);
        }
        public void Put<T>(IntPtr address, T value) where T : struct => _storage.Put(address, value);
        public T Read<T>(IntPtr address) where T : struct => _storage.Read<T>(address);
        public byte[] ReadBytes(IntPtr address, int length) => _storage.ReadBytes(address, length);
        public void Write<T>(IntPtr address, T value) where T : struct
        {
            if (address == Player + 0xB0) Step("恢复位置");
            if (address == Scene + 4) Step("恢复音乐");
            Put(address, value);
        }
        public int WriteBytes(IntPtr address, byte[] bytes)
        {
            PluginUnloadTests.Assert(address == Receive || address == Send, "不能恢复不属于本次宿主的函数入口。");
            Step(address == Receive ? "恢复收包" : "恢复发包");
            return bytes.Length;
        }
        public bool FreeMemory(IntPtr address)
        {
            PluginUnloadTests.Assert(address == Cave, "不能释放不属于本次宿主的代码洞。");
            Step("释放代码洞");
            return true;
        }
    }

    public sealed class UnloadPlugin
    {
        public string State => "Ready";
        public readonly object CallScheduler = new object();
        private readonly object PluginUi = new object();
        public static readonly IntPtr LoadFunction = new IntPtr(0x101), TerritoryFunction = new IntPtr(0x102),
            SetupFunction = new IntPtr(0x103), FinalizeFunction = new IntPtr(0x104), DeleteFunction = new IntPtr(0x105),
            PositionFunction = new IntPtr(0x106);
        public UnloadMemory Memory { get; }
        private readonly Action _deleted;
        public UnloadPlugin(UnloadMemory memory, Action deleted) { Memory = memory; _deleted = deleted; }
        public T Call<T>(IntPtr function, params object[] args) where T : struct
        {
            if (function == LoadFunction)
            {
                Memory.Step("加载地图");
                Memory.Put(UnloadMemory.Game + 0x4108, Convert.ToUInt32(args[1]));
            }
            else if (function != SetupFunction && function != FinalizeFunction)
                throw new InvalidOperationException("未知区域原生调用。");
            return default(T);
        }
        public void Call(IntPtr function, params object[] args)
        {
            if (function == TerritoryFunction) Memory.Step("设置区域");
            else if (function == DeleteFunction) { Memory.Step("回收实体"); _deleted(); }
            else if (function != PositionFunction) throw new InvalidOperationException("未知卸载原生调用。");
        }
    }
}
