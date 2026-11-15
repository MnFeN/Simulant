using Simulant.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Simulant.Simulation.Timeline;

namespace Simulant.Tests
{
    internal static class HotbarInputTests
    {
        public static void CacheResolvesSkillsGeneralActionsAndMacroIcons()
        {
            var keys = new byte[120 * 11];
            var bars = new byte[10 * 16 * 0xE8];
            // 当前第一栏显示第 4 页；同时验证其他栏的第二套绑定。
            Slot(bars, 3, 0, 1, 7548, 0);
            Slot(bars, 1, 1, 7, 99, 869);
            Slot(bars, 2, 2, 10, 4, 104);
            Slot(bars, 2, 3, 2, 7559, 869); // 物品不能仅凭图标冒充技能。
            keys[0] = 65;
            keys[(12 + 1) * 11 + 2] = 66;
            keys[(12 + 1) * 11 + 3] = 2;
            keys[(24 + 2) * 11] = 67;
            keys[(24 + 3) * 11] = 68;
            var cache = HotbarBinding.Read(keys, bars, 3, id => id == 4 ? 3u : 0,
                new Dictionary<uint, uint> { [869] = 7559, [104] = 3 });
            Check(cache.Count == 3 && cache[0].Action == 7548 && cache[1].Action == 7559 && cache[2].Action == 3,
                "应识别实际显示的栏位、宏图标和通用冲刺，不匹配物品。");
            Check(cache[1].Key == 66 && cache[1].Modifiers == 2, "第二套绑定应保留 Ctrl 修饰键。");
            Array.Clear(keys, 0, keys.Length);
            Array.Clear(bars, 0, bars.Length);
            Check(cache[0].Key == 65 && cache[1].Action == 7559, "缓存建立后不受栏位与键位变化影响。");
        }

        public static void KeysOnlyFireOnPressAndRequireExactModifiers()
        {
            var key = new HotbarBinding(65, 2, 7559);
            var keyboard = new byte[0x496];
            keyboard[4 + 65 * 4] = 3;
            Check(!key.Pressed(keyboard, true), "裸键不能触发 Ctrl 组合。");
            keyboard[0x495] = 2;
            Check(!key.Pressed(keyboard, true), "按住裸键后再按修饰键不能补触发。");
            keyboard[4 + 65 * 4] = 0;
            key.Pressed(keyboard, true);
            keyboard[4 + 65 * 4] = 3;
            Check(key.Pressed(keyboard, true) && !key.Pressed(keyboard, true), "每次按下只触发一次。");
            keyboard[4 + 65 * 4] = 9;
            Check(!key.Pressed(keyboard, true), "游戏长按重复不能再次施放。");
            keyboard[4 + 65 * 4] = 0;
            key.Pressed(keyboard, false);
            keyboard[4 + 65 * 4] = 3;
            Check(!key.Pressed(keyboard, false) && !key.Pressed(keyboard, true), "失焦期间按住的键不应在恢复焦点时补发。");
        }

        public static void LocalActionsShareNormalCooldownAndResetAtEachStart()
        {
            var host = new PluginHost { FirewallEnabled = true };
            var player = host.EntitySpawner.World.CreateCharacter(0);
            host.EntityProvider.Player = player;
            var service = host.PlayerActions;
            try
            {
                service.BeginSimulation();
                Flush(service);
                var captures = SimHotbarInput.Captures;
                service.Receive("ac 冲刺");
                service.Receive("ac 冲刺");
                service.Receive("ac 亲疏自行");
                service.Receive("ac 沉稳咏唱");
                Flush(service);
                Check(player.Native.StatusManager.SetCalls == 2, "重复指令应被同一冷却拦下。");
                Check(service.Cooldown(3).Duration == 60 && service.Cooldown(7548).Duration == 120,
                    "冲刺和防击退应使用正常冷却。");
                Check(service.Cooldown(7559).Remaining > 0 && !player.HasStatus(160),
                    "两种防击退必须共用冷却，换指令名称不能绕过。");
                Thread.Sleep(60);
                Check(SimHotbarInput.Captures == captures, "运行中轮询按键不得刷新快捷栏缓存。");
                service.BeginSimulation();
                Flush(service);
                Check(SimHotbarInput.Captures == captures + 1 && service.Cooldown(3).Remaining == 0,
                    "下一轮应重新读取一次快捷栏并重置冷却。");
                service.Receive("ac 冲刺");
                Flush(service);
                Check(player.Native.StatusManager.SetCalls == 3, "重启后可以立即使用技能。");

                Simulant.Game.Data.GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Status>()[4163] =
                    new Lumina.Excel.Sheets.Status { LockActions = true };
                var status = player.Native.StatusManager.GetStatus(5);
                status.StatusId.Value = 4163;
                service.Receive("ac 沉稳咏唱");
                Flush(service);
                Check(service.Cooldown(7559).Remaining == 0, "眩晕导致技能未使用时不能消耗冷却。");
                status.StatusId.Value = 0;
                host.FirewallEnabled = false;
                service.Receive("ac 沉稳咏唱");
                Check(!player.HasStatus(160), "防火墙关闭后不能施加本机模拟技能。");
            }
            finally
            {
                service.Reset();
            }
        }

        private static void Flush(SimActionService service)
        {
            var scheduler = (SimScheduler)typeof(SimActionService).GetField("_scheduler", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
            using (var done = new ManualResetEventSlim())
            {
                scheduler.ScheduleAfter(0, done.Set);
                Check(done.Wait(5000), "模拟技能队列未完成。");
            }
        }

        private static void Slot(byte[] bars, int bar, int slot, byte type, uint id, uint icon)
        {
            var offset = (bar * 16 + slot) * 0xE8;
            Array.Copy(BitConverter.GetBytes(id), 0, bars, offset + 0xB8, 4);
            bars[offset + 0xC7] = type;
            Array.Copy(BitConverter.GetBytes(icon), 0, bars, offset + 0xD0, 4);
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }
    }
}

namespace Simulant.Core
{
    // 离线测试替换游戏内存与原生调用，键位解码、冷却与调度使用正式代码。
    internal sealed class SimHotbarInput
    {
        internal static int Captures;
        internal SimHotbarInput() => Interlocked.Increment(ref Captures);
        internal void Poll(Action<uint> execute)
        {
        }
    }

    internal sealed class SimActionRecasts : IDisposable
    {
        internal SimActionRecasts(Entity.Character player)
        {
        }
        internal float Duration(uint action) => action == 3 ? 60 : 120;
        internal void Reset()
        {
        }

        internal void Start(uint action)
        {
        }

        public void Dispose()
        {
        }
    }
}
