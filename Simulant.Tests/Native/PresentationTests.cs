using Simulant.ACT;
using Simulant.Core.Environment;
using Simulant.Game.FFCS.Client.System.Framework;
using Simulant.Game.FFCS.Client.UI;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;

namespace Simulant.Tests.Native
{
    internal static class PresentationTests
    {
        private static Type GameType(string name) => typeof(NamazuInterop).Assembly.GetType("Simulant." + name, true);

        private static int Main()
        {
            using (var plugin = new PresentationPlugin())
            {
                typeof(NamazuInterop).GetProperty("Plugin").GetSetMethod(true)
                    .Invoke(null, new object[] { new NamazuPlugin(plugin) });

                try
                {
                    VerifyChat(plugin);
                    VerifyMarkers(plugin);
                    VerifyVfx(plugin);
                    VerifyHotbar(plugin);
                    Console.WriteLine("本地消息、标点和特效调用验证通过。");
                    return 0;
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine(error);
                    return 1;
                }
            }
        }

        private static void VerifyChat(PresentationPlugin plugin)
        {
            var shell = GameType("Game.FFCS.Client.UI.RaptureShellModule");
            var ui = new IntPtr(0x20000);
            var vtable = new IntPtr(0x20100);
            var framework = new IntPtr(0x30000);
            Framework.InstancePtrPtr = new IntPtr(0x10000);
            plugin.Memory.Write(Framework.InstancePtrPtr, framework);
            plugin.Memory.Write(framework + 0x2B68, ui);
            plugin.Memory.Write(ui, vtable);
            plugin.Memory.Write(vtable + 9 * IntPtr.Size, PresentationPlugin.GetShell);
            SetAddress(shell, "ProcessChatBoxFuncPtr", PresentationPlugin.Chat);

            const string message = "本地提示";
            var chat = GameType("Core.GameChat");
            chat.GetMethod("SendEcho").Invoke(null, new object[] { message, true });
            Assert(plugin.Message == "/e <se.11>\n" + message, "提示文本与音效应通过一个 UTF-8 指令传入。");
            Assert(plugin.Memory.Bytes.Freed.SequenceEqual(plugin.Memory.Bytes.Allocations), "消息缓冲在调用完成后应立即释放。");
        }

        private static void VerifyMarkers(PresentationPlugin plugin)
        {
            var type = GameType("Game.FFCS.Client.Game.UI.MarkingController");
            var controller = new IntPtr(0x40000);
            SetAddress(type, "InstancePtr", controller);
            SetAddress(type, "SetLocalMarkerFuncPtr", PresentationPlugin.Mark);
            var markers = GameType("Core.Environment.GameMarkers");
            var markerType = GameType("Game.FFCS.Client.Game.UI.MarkerType");
            markers.GetMethod("Mark", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { Enum.Parse(markerType, "Square"), 0x10000001u });
            Assert(plugin.MarkerArguments.SequenceEqual(new object[] { controller, 10u, 0x10000001u, 0u }),
                "实体标记必须使用本地函数及原生编号。");

            var start = controller + 0x1E0;
            var one = start + 4 * 0x20;
            plugin.Memory.Write(start + 0x1C, (byte)1);
            plugin.Memory.Write(one + 0x0C, 0xABCDu);
            var layout = WaymarkLayout.Center(0, 0).At("1", 12.5f, -3.25f, 4);
            markers.GetMethod("PlaceWaymarks").Invoke(null, new object[] { layout });
            Assert(plugin.Memory.Read<Vector3>(one) == new Vector3(12.5f, 4, -3.25f) &&
                plugin.Memory.Read<int>(one + 0x10) == 12500 && plugin.Memory.Read<int>(one + 0x14) == 4000 &&
                plugin.Memory.Read<int>(one + 0x18) == -3250 && plugin.Memory.Read<byte>(one + 0x1C) == 1 &&
                plugin.Memory.Read<byte>(start + 0x1C) == 0 && plugin.Memory.Read<uint>(one + 0x0C) == 0xABCDu,
                "场地标点应更新两种坐标和启用位，同时保留未知字段并清除未使用槽。");
            markers.GetMethod("ClearWaymarks").Invoke(null, null);
            Assert(plugin.Memory.Read<byte>(one + 0x1C) == 0, "清理应关闭已启用槽。");
        }

        private static void VerifyVfx(PresentationPlugin plugin)
        {
            var native = GameType("Game.FFCS.Client.Graphics.Scene.VfxObject");
            SetAddress(native, "CreateStaticFuncPtr", PresentationPlugin.Create);
            SetAddress(native, "RunFuncPtr", PresentationPlugin.Run);
            SetAddress(native, "FadeOutFuncPtr", PresentationPlugin.Fade);
            SetAddress(native, "CreateActorFuncPtr", PresentationPlugin.Actor);
            var manager = GameType("Core.VfxManager");
            var target = new IntPtr(0x81000);
            manager.GetMethod("ShowLockOn").Invoke(null, new object[] { target, "m0489trg_a0c" });
            Assert(plugin.ActorPath == "vfx/lockon/eff/m0489trg_a0c.avfx" &&
                (IntPtr)plugin.ActorArguments[1] == target && (IntPtr)plugin.ActorArguments[2] == target,
                "点名应通过原生 Actor VFX，来源和目标均为指定实体。");
            var preset = GameType("Content.U7b.Presets.P5地火Logic");
            var logic = FormatterServices.GetUninitializedObject(preset);
            preset.GetMethod("CreateAuxiliaryLines", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(logic, null);
            var lines = plugin.Started.ToArray();
            Assert(lines.Length == 12 && lines.All(p => plugin.Memory.Read<Vector4>(p + 0x260).W == 0.5f),
                "地火应显示两组六条半透明辅助线。");
            Assert(plugin.Memory.Read<Vector3>(lines[0] + 0x50) == new Vector3(70, 0, 95) &&
                plugin.Memory.Read<Vector3>(lines[1] + 0x50) == new Vector3(105, 0, 70) &&
                plugin.Memory.Read<Vector4>(lines[0] + 0x60).Y > 0 && plugin.Memory.Read<Vector4>(lines[1] + 0x60).Y < 0,
                "地火辅助线必须保留原来的两组世界坐标与相反朝向。");

            manager.GetMethod("ShowOmen").Invoke(null, new object[] {
                "preview", Vector3.Zero, 0f, Vector3.One, 0.02, new object(), null, CancellationToken.None
            });
            var preview = plugin.Started.Last();
            preset.GetMethod("RemoveAuxiliaryLines", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(logic, null);
            Assert(lines.All(p => plugin.Faded.Contains(p)), "按所有者清理时应淡出该预设的所有辅助线。");
            Assert(SpinWait.SpinUntil(() => plugin.Faded.Contains(preview), 3000), "未主动清理的预兆应在到期后淡出。");
            manager.GetMethod("Clear").Invoke(null, null);
        }

        private static void VerifyHotbar(PresentationPlugin plugin)
        {
            var previousFunction = AtkModule.GetAddonByNameFuncPtr;
            try
            {
                AtkModule.GetAddonByNameFuncPtr = PresentationPlugin.GetAddon;
                var atk = new AtkModule { Ptr = new IntPtr(0x90000) };
                var unitManager = new IntPtr(0x91000);
                plugin.Memory.Write(atk.Ptr + 0x2C0, unitManager);
                plugin.Memory.Write(plugin.Hotbar + 0x2CB, (byte)5);
                var page = GameType("Core.SimHotbarInput").GetMethod("ReadCurrentHotbarPage",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert((byte)page.Invoke(null, new object[] { atk }) == 5 &&
                    plugin.HotbarName == "_ActionBar" && plugin.HotbarManager == unitManager && plugin.HotbarIndex == 1,
                    "The hotbar page must come from the primary native action bar.");
            }
            finally
            {
                AtkModule.GetAddonByNameFuncPtr = previousFunction;
            }
        }

        private static void SetAddress(Type type, string name, IntPtr value) => type.GetProperty(name).SetValue(null, value);

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }

    public sealed class PresentationMemory
    {
        public readonly PartyMemory Bytes = new PartyMemory();
        public Process Process { get; } = Process.GetCurrentProcess();
        public bool IsProcessOpen => Bytes.IsProcessOpen;
        public IntPtr ProcessHandle => Bytes.ProcessHandle;
        public IntPtr AllocateMemory(int size) => Bytes.AllocateMemory(size);
        public bool FreeMemory(IntPtr address) => Bytes.FreeMemory(address);
        public T Read<T>(IntPtr address) where T : struct => Bytes.Read<T>(address);
        public byte[] ReadBytes(IntPtr address, int size) => Bytes.ReadBytes(address, size);
        public void Write<T>(IntPtr address, T value) where T : struct => Bytes.Write(address, value);
        public int WriteBytes(IntPtr address, byte[] bytes) => Bytes.WriteBytes(address, bytes);
        public bool WriteString(IntPtr address, string text, Encoding encoding)
            => WriteBytes(address, encoding.GetBytes(text + '\0')) > 0;
    }

    public sealed class PresentationPlugin : IDisposable
    {
        public static readonly IntPtr GetShell = new IntPtr(0x10), Chat = new IntPtr(0x11), Mark = new IntPtr(0x12),
            Create = new IntPtr(0x13), Run = new IntPtr(0x14), Fade = new IntPtr(0x15), Actor = new IntPtr(0x16),
            GetAddon = new IntPtr(0x17);
        public readonly IntPtr Hotbar = new IntPtr(0x92000);
        private int _vfxCount;
        public string State => "Ready";
        public readonly object CallScheduler = new object();
        public readonly object SigScanner = new object();
        public readonly PresentationMemory Memory = new PresentationMemory();
        public readonly ConcurrentQueue<IntPtr> Started = new ConcurrentQueue<IntPtr>();
        public readonly ConcurrentQueue<IntPtr> Faded = new ConcurrentQueue<IntPtr>();
        public string Message;
        public object[] MarkerArguments;
        public object[] ActorArguments;
        public string ActorPath, HotbarName;
        public IntPtr HotbarManager;
        public int HotbarIndex;

        public T Call<T>(IntPtr function, params object[] args) where T : struct
        {
            IntPtr result;
            if (function == GetShell)
                result = new IntPtr(0x20200);
            else if (function == Create)
                result = new IntPtr(0x50000 + ++_vfxCount * 0x400);
            else if (function == Actor)
            {
                ActorArguments = args;
                var bytes = Memory.ReadBytes((IntPtr)args[0], 64);
                ActorPath = Encoding.UTF8.GetString(bytes, 0, Array.IndexOf(bytes, (byte)0));
                result = new IntPtr(0x80000);
            }
            else if (function == GetAddon)
            {
                HotbarManager = (IntPtr)args[0];
                var bytes = Memory.ReadBytes((IntPtr)args[1], 16);
                HotbarName = Encoding.ASCII.GetString(bytes, 0, Array.IndexOf(bytes, (byte)0));
                HotbarIndex = (int)args[2];
                result = Hotbar;
            }
            else
            {
                Call(function, args);
                result = IntPtr.Zero;
            }

            return (T)(object)result;
        }

        public void Call(IntPtr function, params object[] args)
        {
            if (function == Chat)
            {
                var text = Memory.Read<IntPtr>((IntPtr)args[1]);
                var length = Memory.Read<long>((IntPtr)args[1] + 0x18);
                Message = Encoding.UTF8.GetString(Memory.ReadBytes(text, checked((int)length)));
            }
            else if (function == Mark)
                MarkerArguments = args;
            else if (function == Run)
                Started.Enqueue((IntPtr)args[0]);
            else if (function == Fade)
                Faded.Enqueue((IntPtr)args[0]);
            else
                throw new ArgumentException("未预期的原生函数：" + function);
        }

        public void Dispose() => Memory.Process.Dispose();
    }
}
