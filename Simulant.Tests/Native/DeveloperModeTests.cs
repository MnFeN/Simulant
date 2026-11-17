using Simulant.Game;
using Simulant.Core;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.Game;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TerritoryType = Lumina.Excel.Sheets.TerritoryType;

namespace Simulant.Tests.Native
{
    internal static class DeveloperModeTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FakeLocalMemory Memory = new FakeLocalMemory();

        private static int Main(string[] args)
        {
            var previousPlugin = GameInterop.Connection;
            var previousGame = GameMain.InstancePtr;
            try
            {
                NativeTestContext.Use(Memory, new FakeLocalPlugin(Memory));
                GameMain.InstancePtr = new IntPtr(0x10000);
                using (var data = new GameDataService(args[0]))
                {
                    typeof(GameDataService).GetMethod("Activate", Members).Invoke(data, null);
                    foreach (var method in typeof(DeveloperModeTests).GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Where(method => method.ReturnType == typeof(void) && method.GetParameters().Length == 0))
                    {
                        method.Invoke(null, null);
                        Console.WriteLine("通过：" + method.Name);
                    }
                }
                Assert(Memory.WriteCalls == 0 && Memory.AllocateCalls == 0, "权限检查不应写入游戏内存。");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("开发者模式验证失败：" + error);
                return 1;
            }
            finally
            {
                GameMain.InstancePtr = previousGame;
                typeof(GameInterop).GetProperty("Connection").GetSetMethod(true).Invoke(null, new object[]
                {
                    previousPlugin
                });
            }
        }

        public static void NormalModeAllowsOnlyInnTerritoryTypes()
        {
            var host = CreateHost(new Configuration());
            Memory.Put(GameMain.InstancePtr + 0x4100, 2u);
            var innCount = 0;
            foreach (var territory in GameDataService.Instance.GetSheet<TerritoryType>())
            {
                Memory.Put(GameMain.InstancePtr + 0x4108, territory.RowId);
                var isInn = territory.TerritoryIntendedUse.RowId == (uint)TerritoryIntendedUseEnum.Inn;
                CheckAllowed(host, "EnsureFirewallAllowed", isInn);
                if (isInn)
                    innCount++;
            }
            Assert(innCount > 0, "客户端数据中应有旅馆类型，不能通过空集合验证。");
            Memory.Put(GameMain.InstancePtr + 0x4108, uint.MaxValue);
            CheckAllowed(host, "EnsureFirewallAllowed", false);
            Memory.Put(GameMain.InstancePtr + 0x4100, 1u);
            CheckAllowed(host, "EnsureFirewallAllowed", false);
        }

        public static void DeveloperModeAllowsDebugWithoutFirewallButStillRequiresInitialization()
        {
            var configuration = new Configuration();
            var host = CreateHost(configuration);
            CheckAllowed(host, "EnsureDebugAllowed", false);
            configuration.DeveloperMode = true;
            CheckAllowed(host, "EnsureDebugAllowed", true);
            CheckAllowed(host, "EnsureFirewallAllowed", true);
            foreach (var property in new[]
            {
                "PluginReady", "IsInitializing", "Disposed"
            })
            {
                var normal = property == "PluginReady";
                Set(host, "<" + property + ">k__BackingField", !normal);
                CheckAllowed(host, "EnsureDebugAllowed", false);
                CheckAllowed(host, "EnsureFirewallAllowed", false);
                Set(host, "<" + property + ">k__BackingField", normal);
            }
            configuration.DeveloperMode = false;
            CheckAllowed(host, "EnsureDebugAllowed", false);

            var firewallType = typeof(PluginHost).Assembly.GetType("Simulant.Core.Firewall.FirewallService", true);
            var firewall = Activator.CreateInstance(firewallType, new object[]
            {
                host
            });
            var hookType = firewallType.GetNestedType("HookState", BindingFlags.NonPublic);
            foreach (var name in new[]
            {
                "_receive", "_send"
            })
            {
                var hook = Activator.CreateInstance(hookType, true);
                Set(hook, "Enabled", true);
                Set(firewall, name, hook);
            }
            Set(host, "FirewallService", firewall);
            CheckAllowed(host, "EnsureDebugAllowed", true);
        }

        private static PluginHost CreateHost(Configuration configuration)
        {
            var host = (PluginHost)FormatterServices.GetUninitializedObject(typeof(PluginHost));
            Set(host, "<PluginReady>k__BackingField", true);
            Set(host, "<Configuration>k__BackingField", configuration);
            return host;
        }

        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, Members).SetValue(target, value);

        private static void CheckAllowed(PluginHost host, string method, bool expected)
        {
            var allowed = true;
            try
            {
                typeof(PluginHost).GetMethod(method, Members).Invoke(host, null);
            }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException)
            {
                allowed = false;
            }
            Assert(allowed == expected, method + " 的执行限制与预期不符。");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
