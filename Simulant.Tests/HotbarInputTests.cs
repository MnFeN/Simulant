using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Core.Party;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Simulant.Simulation.Runtime;
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
                service.RequestAction((uint)SimLocalAction.Sprint);
                service.Receive("ac 冲刺");
                service.Receive("ac 亲疏自行");
                service.Receive("ac 沉稳咏唱");
                Flush(service);
                Check(player.Native.StatusManager.SetCalls == 2, "按钮和指令重复触发技能时应共用冷却。");
                Check(service.Cooldown((uint)SimLocalAction.Sprint).Duration == 60 &&
                    service.Cooldown((uint)SimLocalAction.ArmsLength).Duration == 120,
                    "冲刺和防击退应使用正常冷却。");
                Check(service.Cooldown((uint)SimLocalAction.Surecast).Remaining > 0 && !player.HasStatus(160),
                    "两种防击退必须共用冷却，换指令名称不能绕过。");
                Thread.Sleep(60);
                Check(SimHotbarInput.Captures == captures, "运行中轮询按键不得刷新快捷栏缓存。");
                service.BeginSimulation();
                Flush(service);
                Check(SimHotbarInput.Captures == captures + 1 && service.Cooldown((uint)SimLocalAction.Sprint).Remaining == 0,
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
                Check(service.Cooldown((uint)SimLocalAction.Surecast).Remaining == 0, "眩晕导致技能未使用时不能消耗冷却。");
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

        public static void TankActionsOnlyConsumeCooldownWhenTheExerciseAcceptsThem()
        {
            var host = new PluginHost { FirewallEnabled = true };
            var player = host.EntitySpawner.World.CreateCharacter(0);
            player.Job = Simulant.Game.FFCS.Client.Game.Character.Job.PLD;
            host.EntityProvider.Player = player;
            var service = host.PlayerActions;
            const uint provoke = (uint)SimLocalAction.Provoke;
            const uint shirk = (uint)SimLocalAction.Shirk;
            var accepted = 0;
            try
            {
                service.BeginSimulation();
                Flush(service);
                service.Receive("ac 挑衅");
                Flush(service);
                Check(!service.CanUseEnmityAction(provoke) && service.Cooldown(provoke).Remaining == 0,
                    "练习未登记换仇处理时，技能应保持未使用。");

                service.SetEnmityActionHandler(action =>
                {
                    if (action != provoke)
                        return false;
                    accepted++;
                    return true;
                });
                service.Receive("ac 退避");
                service.Receive("ac 挑衅");
                service.Receive("ac 挑衅");
                Flush(service);
                Check(service.CanUseEnmityAction(provoke) && accepted == 1 && service.Cooldown(provoke).Duration == 30 &&
                    service.Cooldown(provoke).Remaining > 0 &&
                    service.Cooldown(shirk).Remaining == 0, "换仇技能只应在练习确认可用时执行并开始冷却。");

                service.SetEnmityActionHandler(action => action == shirk);
                service.Receive("ac 退避");
                Flush(service);
                Check(service.Cooldown(shirk).Duration == 120 && service.Cooldown(shirk).Remaining > 0,
                    "退避应使用独立的 120 秒冷却。");
                service.SetEnmityActionHandler(null);
                Check(!service.CanUseEnmityAction(provoke) && !service.CanUseEnmityAction(shirk), "练习移除处理器后应禁用换仇按钮。");
            }
            finally
            {
                service.Reset();
            }
        }

        public static void TankActionsUseIndependentEncounterEnmityTables()
        {
            var host = new PluginHost { FirewallEnabled = true };
            host.EntitySpawner.NetworkReady = true;
            var player = host.EntitySpawner.World.CreateCharacter(0);
            player.Job = Simulant.Game.FFCS.Client.Game.Character.Job.WAR;
            host.EntityProvider.Player = player;
            var service = host.PlayerActions;
            var party = new SimPartyService();
            using (var scheduler = new SimScheduler())
            using (var ready = new CountdownEvent(2))
            {
                var manager = new SimEntityManager(host.EntitySpawner) { Scheduler = scheduler };
                var self = manager.Attach(player);
                var other = manager.SpawnBNpc(2);
                manager.Party.Add(self);
                manager.Party.Add(other);
                manager.ShowParty(party);
                SimActor primary = null;
                SimActor secondary = null;
                manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec { BNpcBaseId = 17833 }, actor =>
                {
                    primary = actor;
                    manager.SetCombatTarget(primary);
                    ready.Signal();
                });
                manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec { BNpcBaseId = 17835 }, actor =>
                {
                    secondary = actor;
                    manager.SetSecondaryBossEnmity(secondary);
                    ready.Signal();
                });

                try
                {
                    service.BeginSimulation(manager);
                    scheduler.Start();
                    Check(ready.Wait(5000), "双 Boss 的初始仇恨没有就绪。");
                    var primaryOpening = party.EnmityByTarget[primary.Id];
                    var secondaryOpening = party.EnmityByTarget[secondary.Id];
                    Check(service.CanUseEnmityAction((uint)SimLocalAction.Provoke) && service.CanUseEnmityAction((uint)SimLocalAction.Shirk) &&
                        primaryOpening[0] == 100 && secondaryOpening[1] == 100 &&
                        primaryOpening[1] < 100 && primaryOpening[1] == secondaryOpening[0],
                        "两个 Boss 应分别有主坦和副坦一仇。");

                    manager.SetCombatTarget(null);
                    manager.SetPartyEnmity(secondary, alsoSetCombatTarget: false, 60, 100);
                    manager.SetPartyEnmity(primary, alsoSetCombatTarget: false, 100, 45);
                    Check(party.EnmityByTarget[primary.Id].SequenceEqual(new[] { 100, 45 }) &&
                        party.EnmityByTarget[secondary.Id].SequenceEqual(new[] { 60, 100 }),
                        "练习可以为两张仇恨表分别设置回放数值。");

                    SimPartyService.SelectedTarget = other.Address;
                    service.Receive("ac 挑衅");
                    Flush(service);
                    Check(service.Cooldown((uint)SimLocalAction.Provoke).Remaining == 0, "挑衅必须选中 Boss。");

                    SimPartyService.SelectedTarget = secondary.Address;
                    service.Receive("ac 挑衅");
                    Flush(service);
                    Check(party.EnmityByTarget[primary.Id].SequenceEqual(new[] { 100, 45 }) &&
                        party.EnmityByTarget[secondary.Id].SequenceEqual(new[] { 100, 83 }),
                        "挑衅应增加被选中 Boss 的仇恨，不改变另一张表。");

                    primary.Entity.IsTargetable = false;
                    secondary.Entity.IsTargetable = false;
                    SimPartyService.SelectedTarget = other.Address;
                    SimPartyService.SoftSelectedTarget = primary.Address;
                    service.Receive("ac 退避");
                    Flush(service);
                    Check(party.EnmityByTarget[primary.Id].SequenceEqual(new[] { 100, 93 }) &&
                        party.EnmityByTarget[secondary.Id].SequenceEqual(new[] { 69, 100 }),
                        "双 Boss 不可选中且旧软目标仍指向 Boss 时，退避应对已选队友生效。");
                    Check(service.Cooldown((uint)SimLocalAction.Shirk).Remaining > 0,
                        "成功退避后应开始冷却。");
                    Check(player.ActionEffects.SequenceEqual(new[] { (uint)SimLocalAction.Provoke, (uint)SimLocalAction.Shirk }),
                        "两个换仇技能应各播放一次演出。");

                    service.BeginSimulation(manager);
                    Flush(service);
                    SimPartyService.SelectedTarget = secondary.Address;
                    service.RequestAction((uint)SimLocalAction.Shirk);
                    Flush(service);
                    Check(party.EnmityByTarget[primary.Id].SequenceEqual(new[] { 63, 100 }) &&
                        party.EnmityByTarget[secondary.Id].SequenceEqual(new[] { 44, 100 }),
                        "保留不可选中的 Boss 目标时，退避应自动给队友，两张仇恨表都应更新。");

                    var enmityUpdates = party.EnmitySetCount;
                    manager.SetCombatTarget(primary);
                    Check(party.EnmitySetCount == enmityUpdates, "机制开场指定战斗目标时，应保留准备期间的两张仇恨表。");
                    service.EndSimulation(manager);
                    Check(!service.CanUseEnmityAction((uint)SimLocalAction.Shirk), "练习结束后快捷控件应解除与当前仇恨表的关联。");
                }
                finally
                {
                    SimPartyService.SelectedTarget = IntPtr.Zero;
                    SimPartyService.SoftSelectedTarget = IntPtr.Zero;
                    service.Reset();
                    manager.Clear();
                }
            }
        }

        public static void ShirkOnlyNeedsALivingSimulatedTeammate()
        {
            var host = new PluginHost { FirewallEnabled = true };
            var player = host.EntitySpawner.World.CreateCharacter(0);
            player.Job = Simulant.Game.FFCS.Client.Game.Character.Job.PLD;
            host.EntityProvider.Player = player;
            var service = host.PlayerActions;
            const uint shirk = (uint)SimLocalAction.Shirk;

            using (var scheduler = new SimScheduler())
            {
                var manager = new SimEntityManager(host.EntitySpawner) { Scheduler = scheduler };
                manager.Party.Add(manager.Attach(player));
                var teammate = manager.SpawnBNpc(2);
                manager.Party.Add(teammate);
                try
                {
                    scheduler.Start();
                    service.BeginSimulation(manager);
                    Flush(service);
                    Check(service.CanUseEnmityAction(shirk), "有存活队友时，无须先登记 Boss 仇恨或展示小队即可退避。");
                    service.RequestAction(shirk);
                    Flush(service);
                    Check(service.Cooldown(shirk).Remaining > 0 && player.ActionEffects.SequenceEqual(new[] { shirk }),
                        "没有选中目标时也应播放退避并开始冷却。");
                    teammate.Entity.HP = 0;
                    Check(!service.CanUseEnmityAction(shirk), "只剩倒地队友时应禁用退避。");
                }
                finally
                {
                    service.Reset();
                    manager.Clear();
                }
            }
        }

        public static void SoloPracticeAllowsProvokeWhenTheBossIsTargetable()
        {
            var host = new PluginHost { FirewallEnabled = true };
            host.EntitySpawner.NetworkReady = true;
            var player = host.EntitySpawner.World.CreateCharacter(0);
            player.Job = Simulant.Game.FFCS.Client.Game.Character.Job.PLD;
            host.EntityProvider.Player = player;
            var service = host.PlayerActions;

            using (var scheduler = new SimScheduler())
            using (var ready = new ManualResetEventSlim())
            {
                var manager = new SimEntityManager(host.EntitySpawner) { Scheduler = scheduler };
                manager.Party.Add(manager.Attach(player));
                // 测试生成器顺序分配槽位，先占用本地槽位，使 Boss 使用偶数网络槽位。
                manager.SpawnBNpc(9020);
                SimActor boss = null;
                manager.SpawnNetworkBNpc(new NetworkNpcSpawnSpec { BNpcBaseId = 17839 }, actor =>
                {
                    boss = actor;
                    manager.SetPartyEnmity(boss, 100);
                    ready.Set();
                });

                try
                {
                    service.BeginSimulation(manager);
                    scheduler.Start();
                    Check(ready.Wait(5000), "单人 Boss 的仇恨没有就绪。");
                    Check(service.CanUseEnmityAction((uint)SimLocalAction.Provoke) && !service.CanUseEnmityAction((uint)SimLocalAction.Shirk),
                        "单人练习应启用挑衅，退避需要队友。");

                    SimPartyService.SelectedTarget = boss.Address;
                    boss.Entity.IsTargetable = false;
                    service.Receive("ac 挑衅");
                    Flush(service);
                    Check(service.Cooldown((uint)SimLocalAction.Provoke).Remaining == 0, "不可选中的 Boss 不能消耗挑衅冷却。");

                    boss.Entity.IsTargetable = true;
                    service.RequestAction((uint)SimLocalAction.Provoke);
                    Flush(service);
                    Check(service.Cooldown((uint)SimLocalAction.Provoke).Remaining > 0 && player.ActionEffects.Contains((uint)SimLocalAction.Provoke),
                        "单人对选中的 Boss 挑衅后应播放演出并开始冷却。");
                }
                finally
                {
                    SimPartyService.SelectedTarget = IntPtr.Zero;
                    service.Reset();
                    manager.Clear();
                }
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
        internal bool Supports(uint action) => true;
        internal float Duration(uint action) => action == (uint)SimLocalAction.Sprint ? 60 :
            action == (uint)SimLocalAction.Provoke ? 30 : 120;
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
