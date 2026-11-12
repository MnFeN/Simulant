namespace Simulant.Tests.Native
{
    using Simulant.ACT;
    using Simulant.Core;
    using Simulant.Core.Entity;
    using Simulant.Game.FFCS.Client.Game;
    using Simulant.Game.FFCS.Client.Game.Control;
    using Simulant.Game.FFCS.Client.Game.Group;
    using Simulant.Game.FFCS.Client.Game.Network;
    using Simulant.Game.FFCS.Client.Game.Object;
    using Simulant.Game.FFCS.Client.Game.UI;
    using Simulant.Game.FFCS.Client.Network;
    using Simulant.Simulation.Runtime;
    using Simulant.Simulation.Timeline;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading;

    // 直接调用正式 DLL 的小队服务，仅替换进程内存和原生小队收包函数。
    internal static class PartyServiceTests
    {
        internal const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static int _passed;

        private static int Main()
        {
            try
            {
                Check("小队包布局及末尾成员和状态读写", VerifyPacketLayout);
                Check("状态调用使用完整的 64 位来源身份，并拒绝结构体参数", VerifyStatusCallArguments);
                Check("血量收包保留全部 60 个状态和角色上限", VerifyNetworkVitals);
                Check("血量收包拒绝越界、本地角色与不完整快照", VerifyNetworkVitalsValidation);
                Check("血量收包失败仍释放远程包内存", VerifyNetworkVitalsFailure);
                Check("恢复全部 60 条状态与尾部值，保留新 Owner 指针", VerifyFullRestoration);
                Check("快照捕获不完整时不接管或写入小队", VerifyCaptureFailure);
                Check("名单更新部分失败仍保留原快照并可恢复", VerifyShowFailure);
                Check("原生名单恢复失败保留所有者并可重试", VerifyNativeRestoreFailure);
                Check("状态值恢复短写保留所有者并可重试", VerifyValueRestoreFailure);
                Check("另一所有者不能接管或恢复当前小队", VerifyExclusiveOwner);
                Check("进程和结构变化不写回旧快照", VerifyChangedContext);
                Check("恢复中途世界失效不再写回旧快照", VerifyWorldChangeDuringRestore);
                Check("仇恨参数完整校验后才写入并可恢复", VerifyEnmityValidation);
                Check("仇恨更新失败仍恢复入战前状态", VerifyEnmityFailure);
                Check("原先已在战斗中的自己恢复后仍在战斗", VerifyExistingCombat);
                Check("退战恢复部分失败保留所有权并可重试", VerifyCombatRestoreFailure);
                Check("切换目标后仅按需补发仇恨并正确处理软目标", VerifyEnmityRefresh);
                Check("仇恨收包期间切换目标不会误报失败", VerifyTargetChangeDuringEnmity);
                Check("成员移除更新名单且最终恢复原小队", VerifyRemoveMember);
                Check("移除成员重新采集留下队员的当前血蓝与状态", VerifyRemoveMemberPreservesCurrentValues);
                Console.WriteLine("真实小队服务故障注入通过：" + _passed + " 项。");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("验证失败：模拟小队与仇恨恢复。\n" + error);
                return 1;
            }
        }

        private static void VerifyPacketLayout()
        {
            Assert(Marshal.SizeOf(typeof(UpdatePartyPacket)) == 0xE58 &&
                Marshal.SizeOf(typeof(UpdatePartyPacketMember)) == 0x1C8 &&
                Marshal.SizeOf(typeof(UpdatePartyPacketStatus)) == 12, "小队更新包的结构大小不匹配。");
            Assert(Marshal.OffsetOf(typeof(UpdatePartyPacket), "MemberCount").ToInt32() == 0xE51 &&
                Marshal.OffsetOf(typeof(UpdatePartyPacketMember), "Statuses").ToInt32() == 0x60 &&
                Marshal.OffsetOf(typeof(UpdatePartyPacketMember), "EntityId").ToInt32() == 0x38,
                "关键字段偏移不匹配。");
            var packet = new UpdatePartyPacket { MemberCount = 8, LeaderIndex = 7, PartyId = 123 };
            var member = new UpdatePartyPacketMember { EntityId = 0x10FF1234, CurrentHP = 300 };
            member.SetStatus(29, new UpdatePartyPacketStatus { StatusId = 777, RemainingTime = 12.5f, SourceEntityId = 456 });
            packet.SetMember(7, member);
            Assert(packet.GetMember(7).EntityId == member.EntityId && packet.GetMember(7).GetStatus(29).StatusId == 777 &&
                packet.PartyId == 123, "末尾成员与末尾状态写入不得越界覆盖包尾元数据。");
            Throws<ArgumentOutOfRangeException>(() => packet.GetMember(8));
            Throws<ArgumentOutOfRangeException>(() => member.GetStatus(30));
        }

        private static void VerifyStatusCallArguments()
        {
            using (var scene = new Scene())
            {
                var previous = StatusManager.SetStatusFuncPtr;
                try
                {
                    StatusManager.SetStatusFuncPtr = PartyPlugin.StatusFunction;
                    var manager = new StatusManager { Ptr = new IntPtr(0x700000) };
                    var source = new GameObjectId { ObjectId = 0x40FF1234, Type = 2 };
                    Assert(Marshal.SizeOf(typeof(GameObjectId)) == sizeof(ulong), "来源身份必须为 8 字节。");
                    Throws<ArgumentException>(() => scene.Plugin.Call<bool>(PartyPlugin.StatusFunction,
                        manager.Ptr, 29, (ushort)43, 12.5f, (ushort)3, source, true));
                    Assert(manager.SetStatus(29, 43, 12.5f, 3, source, true), "原生状态调用返回值应传回调用方。");
                    var args = scene.Plugin.LastStatusArguments;
                    Assert(args.Length == 7 && (IntPtr)args[0] == manager.Ptr && (int)args[1] == 29 &&
                        (ushort)args[2] == 43 && (float)args[3] == 12.5f && (ushort)args[4] == 3 &&
                        args[5] is ulong && (ulong)args[5] == 0x0000000240FF1234UL && (bool)args[6],
                        "状态调用必须保留参数顺序、类型与来源身份的高 32 位。");
                    Assert(!manager.SetStatus(29, 43, 1.96f, 4, source, true) &&
                        manager.GetStatus(29).Param.Get() == 4 && manager.GetStatus(29).RemainingTime.Get() == 1.96f,
                        "编号、来源不变时，原生返回 false 仍应正常更新参数和持续时间。");
                    Assert(manager.SetStatus(0, 158, 30, 0, new GameObjectId { ObjectId = Scene.SelfId }, false),
                        "普通玩家来源也应能够设置状态。");
                    Assert((ulong)scene.Plugin.LastStatusArguments[5] == Scene.SelfId &&
                        !(bool)scene.Plugin.LastStatusArguments[6], "普通来源及刷新标志应保持原值。");
                }
                finally { StatusManager.SetStatusFuncPtr = previous; }
            }
        }

        private static void VerifyNetworkVitals()
        {
            Assert(Marshal.SizeOf(typeof(StatusEffectListDoublePacket)) == 0x2E8 &&
                Marshal.OffsetOf(typeof(StatusEffectListDoublePacket), "CurrentHP").ToInt32() == 0x16C &&
                Marshal.OffsetOf(typeof(StatusEffectListDoublePacket), "Statuses").ToInt32() == 0x17C,
                "完整状态包必须保持两组 30 槽与血量头部的原生布局。");
            using (var scene = new Scene())
            {
                var actor = scene.Actors[0].Entity;
                scene.Memory.Put(actor.Address + 0x1CE, (byte)37);
                for (var i = 0; i < 60; i++)
                {
                    var status = actor.Address + 0x23B8 + i * 16;
                    scene.Memory.Put(status, (ushort)(1000 + i));
                    scene.Memory.Put(status + 2, (ushort)(200 + i));
                    scene.Memory.Put(status + 4, 10.5f + i);
                    scene.Memory.Put(status + 8, 0x100000000UL + (uint)(3000 + i));
                }
                actor.SetNetworkVitals(25000);
                var packet = scene.Plugin.VitalsPackets.Single();
                Assert(scene.Plugin.VitalsTargets.Single() == actor.Id && packet.CurrentHP == 25000 &&
                    packet.MaxHP == 60000 && packet.CurrentMP == 10000 && packet.MaxMP == 10000 &&
                    packet.ClassJob == 19 && packet.Level == 100 && packet.Level_2 == 100 && packet.DamageShield == 37,
                    "收包必须保留血蓝上限、默认魔力、职业、等级与护盾，且发送给所选网络实体。");
                for (var i = 0; i < 60; i++)
                {
                    var status = packet.GetStatus(i);
                    Assert(status.StatusId == 1000 + i && status.Param == 200 + i &&
                        status.RemainingTime == 10.5f + i && status.SourceEntityId == 3000 + i,
                        "更新血量不得清空或调换状态槽：" + i);
                }
                Assert(packet.GetStatus(0).StatusId == 1000 && packet.GetStatus(29).StatusId == 1029 &&
                    packet.GetStatus(30).StatusId == 1030 && packet.GetStatus(59).StatusId == 1059,
                    "前后半区交界和末尾状态必须保持对应关系。");
                Throws<ArgumentOutOfRangeException>(() => packet.GetStatus(60));
                Throws<ArgumentOutOfRangeException>(() => packet.SetStatus(-1, default(StatusEffectListStatus)));
                actor.SetNetworkVitals(60000, 3210);
                Assert(scene.Plugin.VitalsPackets[1].CurrentMP == 3210 && actor.HP == 60000 && actor.MP == 3210,
                    "显式魔力参数应经收包更新，而不是通过小队成员内存改写。");
                Assert(scene.Memory.Allocations.Count == 2 && scene.Memory.Freed.Count == 2 &&
                    scene.Memory.Allocations.All(scene.Memory.Freed.Contains), "每次收包应释放对应的远程分配。");
            }
        }

        private static void VerifyNetworkVitalsValidation()
        {
            using (var scene = new Scene())
            {
                var actor = scene.Actors[0].Entity;
                Throws<ArgumentOutOfRangeException>(() => actor.SetNetworkVitals(60001));
                Throws<ArgumentOutOfRangeException>(() => actor.SetNetworkVitals(100, 10001));
                scene.Memory.Put(actor.Address + 0x1B8, 65536u);
                Throws<InvalidOperationException>(() => actor.SetNetworkVitals(100));
                scene.Memory.Put(actor.Address + 0x1B8, 10000u);
                scene.Memory.Put(actor.Address + 0x8C, (ushort)200);
                Throws<InvalidOperationException>(() => actor.SetNetworkVitals(100));
                scene.Memory.Put(actor.Address + 0x8C, (ushort)3);
                Throws<InvalidOperationException>(() => actor.SetNetworkVitals(100));
                scene.Memory.Put(actor.Address + 0x8C, (ushort)2);
                scene.Memory.ShortReadAddress = actor.Address + 0x23B8;
                Throws<InvalidOperationException>(() => actor.SetNetworkVitals(100));
                Assert(scene.Plugin.VitalsPackets.Count == 0 && scene.Memory.Allocations.Count == 0,
                    "校验和完整状态读取失败时，不得分配包内存或调用原生函数。");
            }
        }

        private static void VerifyNetworkVitalsFailure()
        {
            using (var scene = new Scene())
            {
                scene.Plugin.FailVitals = true;
                Throws<InvalidOperationException>(() => scene.Actors[0].Entity.SetNetworkVitals(123));
                Assert(scene.Memory.Allocations.Count == 1 && scene.Memory.Freed.SequenceEqual(scene.Memory.Allocations),
                    "原生收包抛错后必须释放已分配的远程状态包。");
            }
        }

        private static void VerifyFullRestoration()
        {
            using (var scene = new Scene())
            {
                scene.Show();
                Assert(scene.Plugin.Packets.Single().GetMember(0).EntityId == Scene.SelfId,
                    "原生展示必须将自己排在首位。");
                var rewrittenOwner = scene.Memory.Read<IntPtr>(Scene.MainGroup);
                Assert(rewrittenOwner != scene.OriginalOwners[0], "替身必须确实模拟原生收包重建状态所有者。");
                scene.Invoke("Restore", scene.Owner);
                scene.AssertRestored();
                Assert(!scene.Owned, "完整恢复后应解除所有权。");
                var packetCount = scene.Plugin.Packets.Count;
                var writes = scene.Memory.WriteCount;
                scene.Invoke("Restore", scene.Owner);
                scene.Invoke("RestoreAll");
                Assert(scene.Plugin.Packets.Count == packetCount && scene.Memory.WriteCount == writes,
                    "成功恢复后不能重复发送名单或写回快照。");
            }
        }

        private static void VerifyCaptureFailure()
        {
            using (var scene = new Scene())
            {
                scene.Memory.ShortReadAddress = Scene.MainGroup + 8;
                Throws<InvalidOperationException>(scene.Show);
                Assert(!scene.Owned && scene.Plugin.Packets.Count == 0 && scene.Memory.WriteCount == 0,
                    "快照读取不完整时不能发送名单、写入内存或登记小队所有权。");
                scene.Memory.ShortReadAddress = IntPtr.Zero;
                scene.Memory.Put(Scene.MainGroup + 0x7FE1, (byte)1);
                Throws<InvalidOperationException>(scene.Show);
                Assert(!scene.Owned && scene.Plugin.Packets.Count == 0, "团队状态必须在修改名单前拒绝。");
                scene.Memory.Put(Scene.MainGroup + 0x7FE1, (byte)0);
                scene.Show();
                scene.Invoke("RestoreAll");
                scene.AssertRestored();
            }
        }

        private static void VerifyShowFailure()
        {
            using (var scene = new Scene())
            {
                scene.Plugin.FailUpdate = true;
                Throws<InvalidOperationException>(scene.Show);
                Assert(scene.Owned && scene.Memory.Read<byte>(Scene.MainGroup + 0x7FDC) == 3,
                    "原生收包已经改写名单后抛错，也必须保留进入前的快照与所有者。");
                scene.Plugin.FailUpdate = false;
                scene.Invoke("Restore", scene.Owner);
                scene.AssertRestored();
            }
        }

        private static void VerifyNativeRestoreFailure()
        {
            using (var scene = new Scene())
            {
                scene.Show();
                scene.Plugin.FailUpdate = true;
                Throws<InvalidOperationException>(() => scene.Invoke("Restore", scene.Owner));
                Assert(scene.Owned, "原生恢复失败不能释放快照所有权。");
                scene.Plugin.FailUpdate = false;
                scene.Invoke("RestoreAll");
                scene.AssertRestored();
                Assert(!scene.Owned && scene.Plugin.Packets.Count == 3, "再次恢复必须重试原名单并完成清理。");
            }
        }

        private static void VerifyValueRestoreFailure()
        {
            using (var scene = new Scene())
            {
                scene.Show();
                scene.Memory.ShortWriteAddress = Scene.MainGroup + PartyMember.Size + 8;
                Throws<InvalidOperationException>(() => scene.Invoke("Restore", scene.Owner));
                Assert(scene.Owned, "后续成员状态短写也必须保留完整恢复责任。");
                scene.Memory.ShortWriteAddress = IntPtr.Zero;
                scene.Invoke("Restore", scene.Owner);
                scene.AssertRestored();
            }
        }

        private static void VerifyExclusiveOwner()
        {
            using (var scene = new Scene())
            {
                scene.Show();
                var another = new object();
                var packets = scene.Plugin.Packets.Count;
                var writes = scene.Memory.WriteCount;
                Throws<InvalidOperationException>(() => scene.Invoke("Show", another, scene.Actors));
                scene.Invoke("Restore", another);
                Assert(scene.Owned && scene.Plugin.Packets.Count == packets && scene.Memory.WriteCount == writes,
                    "另一会话不能覆盖当前快照或恢复不属于自己的名单。");
                scene.Invoke("Restore", scene.Owner);
            }
        }

        private static void VerifyChangedContext()
        {
            foreach (var change in new[] { "进程句柄", "进程关闭", "内存实例", "小队地址", "界面地址" })
            {
                using (var scene = new Scene())
                {
                    scene.Show();
                    var writes = scene.Memory.WriteCount;
                    var packets = scene.Plugin.Packets.Count;
                    if (change == "进程句柄") scene.Memory.ProcessHandle = new IntPtr(202);
                    if (change == "进程关闭") scene.Memory.IsProcessOpen = false;
                    if (change == "内存实例") scene.Plugin.Memory = new PartyMemory();
                    if (change == "小队地址") GroupManager.InstancePtr += 0x1000;
                    if (change == "界面地址") UIState.InstancePtr += 0x1000;
                    Throws<InvalidOperationException>(() => scene.Invoke("Restore", scene.Owner));
                    Assert(scene.Owned && scene.Memory.WriteCount == writes && scene.Plugin.Packets.Count == packets,
                        change + "变化时不得向旧世界恢复名单或状态。");
                    if (change == "内存实例") Assert(scene.Plugin.Memory.WriteCount == 0,
                        "更换内存实例后也不能把旧快照写入新进程。");
                    scene.Invoke("InvalidateWorld");
                    scene.Invoke("RestoreAll");
                    Assert(!scene.Owned && scene.Memory.WriteCount == writes && scene.Plugin.Packets.Count == packets,
                        "世界失效应直接丢弃旧责任，不能补做旧世界的恢复。");
                }
            }
        }

        private static void VerifyWorldChangeDuringRestore()
        {
            using (var scene = new Scene())
            using (var invalidated = new ManualResetEventSlim())
            {
                scene.Show();
                var writes = scene.Memory.WriteBytesCount;
                Exception invalidateFailure = null;
                Thread invalidator = null;
                var versionField = scene.Service.GetType().GetField("_worldVersion", Members);
                var originalVersion = Convert.ToInt64(versionField.GetValue(scene.Service));
                scene.Plugin.BeforeUpdateReturn = () =>
                {
                    invalidator = new Thread(() =>
                    {
                        try { scene.Invoke("InvalidateWorld"); }
                        catch (Exception error) { invalidateFailure = error; }
                        finally { invalidated.Set(); }
                    }) { IsBackground = true };
                    invalidator.Start();
                    Assert(SpinWait.SpinUntil(() => Convert.ToInt64(versionField.GetValue(scene.Service)) != originalVersion, 5000),
                        "世界失效没有先发布新代次。");
                    Assert(!invalidated.IsSet, "世界失效应仍在等待当前恢复释放小队锁。");
                };
                Throws<InvalidOperationException>(() => scene.Invoke("Restore", scene.Owner));
                Assert(invalidated.Wait(5000) && invalidator.Join(5000) && invalidateFailure == null,
                    "恢复退出后世界失效必须完成，不能互相死锁。");
                Assert(scene.Memory.WriteBytesCount == writes && !scene.Owned,
                    "原生收包期间已发生世界失效，返回后不能再恢复状态或仇恨快照。");
                var packets = scene.Plugin.Packets.Count;
                scene.Invoke("RestoreAll");
                Assert(scene.Plugin.Packets.Count == packets, "失效释放责任后不得重新写回旧小队。");
            }
        }

        private static void VerifyEnmityValidation()
        {
            using (var scene = new Scene())
            {
                scene.Show();
                var writes = scene.Memory.WriteCount;
                var invalid = new Action[]
                {
                    () => scene.Invoke("SetEnmity", scene.Owner, 123u, scene.Actors, new[] { 1, 2, 3 }),
                    () => scene.Invoke("SetEnmity", scene.Owner, 0x40FF9999u, scene.Actors, new[] { 1, 2, 3 }),
                    () => scene.Invoke("SetEnmity", scene.Owner, Scene.EnemyId, scene.Actors, new[] { 1 }),
                    () => scene.Invoke("SetEnmity", scene.Owner, Scene.EnemyId, scene.Actors, new[] { 1, 2, 101 }),
                    () => scene.Invoke("SetEnmity", scene.Owner, Scene.EnemyId, scene.Actors, new[] { 1, 2, -1 }),
                    () => scene.Invoke("SetEnmity", scene.Owner, Scene.EnemyId, new[] { scene.Actors[0], scene.Actors[0] }, new[] { 1, 2 }),
                    () => scene.Invoke("SetEnmity", scene.Owner, Scene.EnemyId, new[] { scene.Outsider }, new[] { 1 })
                };
                foreach (var action in invalid)
                {
                    Throws<ArgumentException>(action);
                    Assert(scene.Memory.WriteCount == writes, "任何仇恨参数不合法时，都必须在首个原生写入前拒绝。");
                }
                scene.Invoke("SetEnmity", scene.Owner, Scene.EnemyId, scene.Actors, new[] { 10, 100, 50 });
                Assert(scene.Memory.Read<int>(Scene.HateAddress + 0x100) == 3 &&
                    scene.Memory.Read<uint>(Scene.HateAddress + 0x104) == Scene.EnemyId &&
                    scene.Memory.Read<uint>(Scene.HateAddress) == scene.Actors[1].Id &&
                    scene.Memory.Read<int>(Scene.HateAddress + 4) == 100,
                    "合法仇恨应通过原生收包按降序写入成员 ID、百分比、目标和人数。");
                var packet = scene.Plugin.HatePackets.Single();
                Assert(scene.Plugin.HateTargets.Single() == Scene.EnemyId && packet.MemberCount == 3 &&
                    packet.GetMember(0).EntityId == scene.Actors[1].Id && packet.GetMember(1).EntityId == scene.Actors[2].Id &&
                    packet.GetMember(2).EntityId == scene.Actors[0].Id && scene.InCombat,
                    "仇恨包必须对应所选 Boss，队列按百分比排序并使自己进入战斗显示状态。");
                scene.Invoke("RestoreAll");
                scene.AssertRestored();
                Assert(!scene.InCombat, "恢复小队时必须撤销模拟临时设置的入战状态。");
            }
        }

        private static void VerifyEnmityFailure()
        {
            using (var scene = new Scene())
            {
                scene.Show();
                scene.Plugin.FailHate = true;
                Throws<InvalidOperationException>(() => scene.Invoke("SetEnmity", scene.Owner, Scene.EnemyId,
                    scene.Actors, new[] { 100, 50, 10 }));
                Assert(scene.Owned && scene.InCombat && scene.Plugin.HatePackets.Count == 1,
                    "仇恨收包部分成功后抛错必须保留原始快照和临时入战恢复责任。");
                Assert(scene.Memory.Allocations.SequenceEqual(scene.Memory.Freed), "失败的仇恨包也必须释放远程内存。");
                scene.Plugin.FailHate = false;
                scene.Invoke("RestoreAll");
                scene.AssertRestored();
                Assert(!scene.InCombat, "仇恨发送失败后的整体清理仍必须恢复原先未入战状态。");
            }
            using (var scene = new Scene())
            {
                scene.Show();
                scene.Plugin.FailActorControl = true;
                Throws<InvalidOperationException>(() => scene.Invoke("SetEnmity", scene.Owner, Scene.EnemyId,
                    scene.Actors, new[] { 100, 50, 10 }));
                Assert(scene.Owned && scene.InCombat, "入战控制部分成功后抛错也必须保留恢复责任。");
                scene.Plugin.FailActorControl = false;
                scene.Invoke("RestoreAll");
                scene.AssertRestored();
                Assert(!scene.InCombat, "失败的入战控制必须能够在清理阶段恢复。");
            }
        }

        private static void VerifyExistingCombat()
        {
            using (var scene = new Scene())
            {
                scene.Memory.Put(scene.Actors[1].Entity.Address + 0x1D4, (byte)0x82);
                scene.Show();
                scene.Invoke("SetEnmity", scene.Owner, Scene.EnemyId, scene.Actors, new[] { 100, 50, 10 });
                scene.Invoke("RestoreAll");
                scene.AssertRestored();
                Assert(scene.InCombat && scene.Memory.Read<byte>(scene.Actors[1].Entity.Address + 0x1D4) == 0x82,
                    "原先已经在战斗中的自己不能被模拟清理强制退战，也不能丢失其他角色标志。");
            }
        }

        private static void VerifyEnmityRefresh()
        {
            using (var scene = new Scene())
            {
                scene.Show();
                scene.SelectTarget(IntPtr.Zero);
                scene.Invoke("SetEnmity", scene.Owner, Scene.EnemyId, scene.Actors, new[] { 100, 50, 10 });
                Assert(scene.Plugin.HatePackets.Count == 0, "未选中对应目标时不应发送该目标的仇恨。");
                scene.SelectTarget(scene.Plugin.Entities[Scene.EnemyId]);
                scene.Invoke("RefreshEnmity", scene.Owner);
                scene.Invoke("RefreshEnmity", scene.Owner);
                Assert(scene.Plugin.HatePackets.Count == 1, "首次选中目标应补发一次，匹配的数据不能重复发送。");
                scene.Memory.Put(Scene.HateAddress + 0x100, 0);
                scene.Invoke("RefreshEnmity", scene.Owner);
                scene.Invoke("RefreshEnmity", scene.Owner);
                Assert(scene.Plugin.HatePackets.Count == 2, "客户端清空仇恨记录后应补发一次。");
                scene.SelectTarget(scene.Actors[0].Entity.Address);
                scene.Invoke("RefreshEnmity", scene.Owner);
                Assert(scene.Plugin.HatePackets.Count == 2, "选择其他实体时不能以旧目标仇恨覆盖原生显示。");
                scene.SelectTarget(IntPtr.Zero);
                scene.Memory.Put(Scene.TargetAddress + 0x88, scene.Plugin.Entities[Scene.EnemyId]);
                scene.Invoke("RefreshEnmity", scene.Owner);
                scene.Invoke("RefreshEnmity", scene.Owner);
                Assert(scene.Plugin.HatePackets.Count == 3, "无硬目标时可跟随软目标且只补发一次。");
                scene.SelectTarget(scene.Actors[0].Entity.Address);
                scene.Memory.Put(Scene.HateAddress + 0x100, 0);
                scene.Invoke("RefreshEnmity", scene.Owner);
                Assert(scene.Plugin.HatePackets.Count == 3, "已有硬目标时不能被软目标覆盖。");
                scene.Invoke("RestoreAll");
                var count = scene.Plugin.HatePackets.Count;
                scene.SelectTarget(scene.Plugin.Entities[Scene.EnemyId]);
                scene.Invoke("RefreshEnmity", scene.Owner);
                Assert(scene.Plugin.HatePackets.Count == count, "恢复完成后任何迟到刷新都不能重新发送仇恨。");
            }
        }

        private static void VerifyCombatRestoreFailure()
        {
            using (var scene = new Scene())
            {
                scene.Show();
                scene.Invoke("SetEnmity", scene.Owner, Scene.EnemyId, scene.Actors, new[] { 100, 50, 10 });
                scene.Plugin.FailActorControl = true;
                Throws<InvalidOperationException>(() => scene.Invoke("RestoreAll"));
                Assert(scene.Owned && !scene.InCombat, "退战控制部分成功后抛错时不能提前解除小队恢复责任。");
                scene.Plugin.FailActorControl = false;
                scene.Invoke("RestoreAll");
                scene.AssertRestored();
                Assert(!scene.Owned && !scene.InCombat, "重试成功后才可解除小队恢复责任。");
            }
        }

        private static void VerifyTargetChangeDuringEnmity()
        {
            using (var scene = new Scene())
            {
                scene.Show();
                scene.Plugin.BeforeHateReturn = () =>
                {
                    scene.SelectTarget(scene.Actors[0].Entity.Address);
                    scene.Memory.PutBytes(Scene.HateAddress, new byte[Hate.Size]);
                };
                scene.Invoke("SetEnmity", scene.Owner, Scene.EnemyId, scene.Actors, new[] { 100, 50, 10 });
                Assert(scene.Owned && scene.Plugin.HatePackets.Count == 1 &&
                    scene.Memory.Read<int>(Scene.HateAddress + 0x100) == 0,
                    "收包中途切到队友并清空仇恨不应被当作原生布局错误，也不能恢复旧目标的数据。");
                scene.Invoke("RefreshEnmity", scene.Owner);
                Assert(scene.Plugin.HatePackets.Count == 1, "仍选中队友时不能补发 Boss 的仇恨。");
                scene.SelectTarget(scene.Plugin.Entities[Scene.EnemyId]);
                scene.Invoke("RefreshEnmity", scene.Owner);
                scene.Invoke("RefreshEnmity", scene.Owner);
                Assert(scene.Plugin.HatePackets.Count == 2 && scene.Memory.Read<int>(Scene.HateAddress + 0x100) == 3,
                    "回到 Boss 后必须补发一次，并在数据一致后停止重复发送。");
                scene.Invoke("RestoreAll");
                scene.AssertRestored();
            }
            using (var scene = new Scene())
            {
                scene.Show();
                scene.Plugin.BeforeHateReturn = () => scene.Memory.PutBytes(Scene.HateAddress, new byte[Hate.Size]);
                Throws<InvalidOperationException>(() => scene.Invoke("SetEnmity", scene.Owner, Scene.EnemyId,
                    scene.Actors, new[] { 100, 50, 10 }));
                Assert(scene.Owned, "仍选中 Boss 但原生读回不符时，应继续报错并保留恢复责任。");
                scene.Invoke("RestoreAll");
                scene.AssertRestored();
            }
        }

        private static void VerifyRemoveMember()
        {
            using (var scene = new Scene())
            {
                scene.Show();
                scene.Invoke("SetEnmity", scene.Owner, Scene.EnemyId, scene.Actors, new[] { 100, 50, 10 });
                scene.Invoke("RemoveMember", scene.Owner, scene.Actors[0].Id);
                Assert(scene.Owned && scene.Memory.Read<byte>(Scene.MainGroup + 0x7FDC) == 2,
                    "移除一个模拟队员后必须保留其余两人及原始恢复责任。");
                var hate = scene.Plugin.HatePackets.Last();
                Assert(hate.MemberCount == 2 && hate.GetMember(0).EntityId == scene.Actors[1].Id &&
                    hate.GetMember(1).EntityId == scene.Actors[2].Id,
                    "移除模拟队员后应同步移除其仇恨条目，保留其余队员的排序。");
                scene.Invoke("RemoveMember", scene.Owner, scene.Actors[2].Id);
                scene.AssertRestored();
                Assert(!scene.Owned && !scene.InCombat, "只剩自己时应恢复进入前的小队与入战状态。");
            }
        }

        private static void VerifyRemoveMemberPreservesCurrentValues()
        {
            using (var scene = new Scene())
            {
                scene.Show();
                for (var i = 1; i <= 2; i++)
                {
                    var actor = scene.Actors[i].Entity;
                    actor.HP = (uint)(12000 + i);
                    actor.MP = (uint)(2300 + i);
                    foreach (var slot in new[] { 0, 29 })
                    {
                        var status = actor.Address + 0x23B8 + slot * 16;
                        scene.Memory.Put(status, (ushort)(1000 + i * 100 + slot));
                        scene.Memory.Put(status + 2, (ushort)(i + slot));
                        scene.Memory.Put(status + 4, 5.5f + i + slot);
                        scene.Memory.Put(status + 8, (ulong)Scene.EnemyId);
                    }
                }
                scene.Invoke("RemoveMember", scene.Owner, scene.Actors[0].Id);
                var packet = scene.Plugin.Packets.Last();
                Assert(packet.MemberCount == 2, "移除一个成员后应只发送留下的两名队员。");
                for (var i = 1; i <= 2; i++)
                {
                    var member = packet.GetMember(i - 1);
                    Assert(member.EntityId == scene.Actors[i].Id && member.CurrentHP == 12000 + i &&
                        member.CurrentMP == 2300 + i && member.MaxHP == 60000 && member.MaxMP == 10000,
                        "更新名单必须读取当前血蓝量，不能重放初次入队时的数值。");
                    foreach (var slot in new[] { 0, 29 })
                    {
                        var status = member.GetStatus(slot);
                        Assert(status.StatusId == 1000 + i * 100 + slot && status.Param == i + slot &&
                            status.RemainingTime == 5.5f + i + slot && status.SourceEntityId == Scene.EnemyId,
                            "名单中的状态内容必须跟随留下队员的当前状态。");
                    }
                }
                scene.Invoke("RestoreAll");
                scene.AssertRestored();
            }
        }

        private static void Check(string name, Action action) { action(); _passed++; Console.WriteLine("通过：" + name); }
        internal static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new Exception("预期抛出 " + typeof(T).Name + "。");
        }
        internal static void Set(object target, string name, object value)
            => target.GetType().GetField(name, Members).SetValue(target, value);

        private sealed class Scene : IDisposable
        {
            internal static readonly IntPtr GroupAddress = new IntPtr(0x100000), MainGroup = GroupAddress + 0x20,
                UIAddress = new IntPtr(0x200000), HateAddress = UIAddress + 8, TargetAddress = new IntPtr(0x280000);
            internal const uint SelfId = 0x1001, EnemyId = 0x40FF1234;
            internal readonly PartyMemory Memory = new PartyMemory();
            internal readonly PartyPlugin Plugin;
            internal readonly object Owner = new object();
            internal readonly object Service;
            internal readonly SimActor[] Actors;
            internal readonly SimActor Outsider;
            internal readonly byte[][] OriginalStatuses = new byte[2][];
            internal readonly byte[][] OriginalNames = new byte[2][];
            internal readonly IntPtr[] OriginalOwners = new IntPtr[2];
            internal readonly byte[] OriginalHate;
            private readonly SimScheduler _scheduler = new SimScheduler();
            private readonly SimEntityManager _manager;
            internal bool Owned => (bool)Invoke("IsOwnedBy", Owner);
            internal bool InCombat => (Memory.Read<byte>(Actors[1].Entity.Address + 0x1D4) & 2) != 0;

            internal Scene()
            {
                Plugin = new PartyPlugin(Memory, MainGroup);
                typeof(NamazuInterop).GetProperty("Plugin").GetSetMethod(true).Invoke(null, new object[] { new NamazuPlugin(Plugin) });
                GroupManager.InstancePtr = GroupAddress;
                UIState.InstancePtr = UIAddress;
                TargetSystem.InstancePtr = TargetAddress;
                PacketDispatcher.HandleUpdatePartyPacketFuncPtr = PartyPlugin.UpdateFunction;
                PacketDispatcher.HandleStatusEffectListDoublePacketFuncPtr = PartyPlugin.VitalsFunction;
                PacketDispatcher.HandleUpdateHatePacketFuncPtr = PartyPlugin.HateFunction;
                PacketDispatcher.HandleActorControlFuncPtr = PartyPlugin.ControlFunction;
                Memory.Put(MainGroup + 0x7FC8, 321L);
                Memory.Put(MainGroup + 0x7FD0, 654L);
                Memory.Put(MainGroup + 0x7FD8, 1u);
                Memory.Put(MainGroup + 0x7FDC, (byte)2);
                for (var i = 0; i < 2; i++)
                {
                    var member = MainGroup + i * PartyMember.Size;
                    OriginalOwners[i] = new IntPtr(0xD00000 + i * 0x100);
                    Memory.Put(member, OriginalOwners[i]);
                    for (var j = 0; j < PartyMember.StatusCount; j++)
                    {
                        var status = member + 8 + j * 16;
                        Memory.Put(status, (ushort)(100 + i * 100 + j));
                        Memory.Put(status + 2, (ushort)(10 + j));
                        Memory.Put(status + 4, j + 0.25f);
                        Memory.Put(status + 8, 0x200000000UL + (uint)j);
                    }
                    Memory.PutBytes(member + 0x3C8, Enumerable.Range(0, 24).Select(value => (byte)(value + i)).ToArray());
                    OriginalStatuses[i] = Memory.ReadBytes(member + 8, 0x3D8);
                    Memory.Put(member + 0x400, SelfId + (uint)i);
                    Memory.Put(member + 0x3F0, 1000UL + (uint)i);
                    Memory.Put(member + 0x3F8, 2000UL + (uint)i);
                    Memory.Put(member + 0x40C, 1000u);
                    Memory.Put(member + 0x410, 2000u);
                    Memory.Put(member + 0x414, (ushort)5000);
                    Memory.Put(member + 0x416, (ushort)10000);
                    Memory.Put(member + 0x469, (byte)19);
                    Memory.Put(member + 0x46A, (byte)100);
                    Memory.Put(member + 0x484, (byte)9);
                    Memory.Put(member + 0x46B, (byte)(37 + i));
                    Memory.Put(member + 0x3E0, new Vector3(11 + i, 22 + i, 33 + i));
                    OriginalNames[i] = new byte[64];
                    var fullName = Encoding.UTF8.GetBytes("原始小队成员名字超过三十一字节" + i + "\0");
                    Array.Copy(fullName, OriginalNames[i], fullName.Length);
                    Memory.PutBytes(member + 0x41C, OriginalNames[i]);
                }
                OriginalHate = Enumerable.Range(0, Hate.Size).Select(i => (byte)(i % 251)).ToArray();
                Memory.PutBytes(HateAddress, OriginalHate);

                var observations = new EntityObservation?[10];
                foreach (var slot in new ushort[] { 0, 2, 4, 6, 8 })
                {
                    var address = new IntPtr(0x300000 + slot * 0x10000);
                    var id = slot == 0 ? SelfId : slot == 6 ? EnemyId : 0x10FF0000u + slot;
                    var kind = slot == 6 ? ObjectKind.BattleNpc : ObjectKind.Pc;
                    observations[slot] = new EntityObservation(address, id, kind);
                    Plugin.Entities[id] = address;
                    Memory.Put(address, new IntPtr(0x500000));
                    Memory.Put(address + 0x78, id);
                    Memory.Put(address + 0x90, kind);
                    Memory.Put(new IntPtr(0x500000 + 6 * 8), PartyPlugin.NameFunction);
                    Memory.Put(address + 0x8C, slot);
                    Memory.PutBytes(address + 0x30, Encoding.UTF8.GetBytes("测试队员" + slot + "\0"));
                    Memory.Put(address + 0x1AC, 50000u);
                    Memory.Put(address + 0x1B0, 60000u);
                    Memory.Put(address + 0x1B4, 10000u);
                    Memory.Put(address + 0x1B8, 10000u);
                    Memory.Put(address + 0x1CA, (byte)19);
                    Memory.Put(address + 0x1CB, (byte)100);
                }
                var registry = new EntityRegistry(slot => observations[slot], observations.Length);
                var provider = new EntityProvider(registry, () => SelfId);
                _manager = new SimEntityManager(new EntitySpawner(registry, provider));
                typeof(SimEntityManager).GetProperty("Scheduler", Members).SetValue(_manager, _scheduler, null);
                Actors = new[] { _manager.Attach((Character)registry.Get(2)), _manager.Attach((Character)registry.Get(0)),
                    _manager.Attach((Character)registry.Get(4)) };
                Outsider = _manager.Attach((Character)registry.Get(8));
                SelectTarget(Plugin.Entities[EnemyId]);
                var host = (PluginHost)FormatterServices.GetUninitializedObject(typeof(PluginHost));
                Set(host, "<PluginReady>k__BackingField", true);
                Set(host, "EntityProvider", provider);
                var firewallType = typeof(PluginHost).Assembly.GetType("Simulant.Core.Firewall.FirewallService", true);
                var firewall = Activator.CreateInstance(firewallType, Members, null, new object[] { host }, null);
                foreach (var name in new[] { "_receive", "_send" })
                {
                    var hook = Activator.CreateInstance(firewallType.GetNestedType("HookState", BindingFlags.NonPublic), true);
                    Set(hook, "Enabled", true);
                    Set(firewall, name, hook);
                }
                Set(host, "FirewallService", firewall);
                Service = Activator.CreateInstance(typeof(PluginHost).Assembly.GetType("Simulant.Core.Party.SimPartyService", true),
                    Members, null, new object[] { host }, null);
            }

            internal object Invoke(string name, params object[] args)
            {
                try { return Service.GetType().GetMethod(name, Members).Invoke(Service, args); }
                catch (TargetInvocationException error) { throw error.InnerException; }
            }
            internal void Show() => Invoke("Show", Owner, Actors);
            internal void SelectTarget(IntPtr address) => Memory.Put(TargetAddress + 0x80, address);
            internal void AssertRestored()
            {
                Assert(Memory.Read<byte>(MainGroup + 0x7FDC) == 2 && Memory.Read<uint>(MainGroup + 0x7FD8) == 1 &&
                    Memory.Read<long>(MainGroup + 0x7FC8) == 321 && Memory.Read<long>(MainGroup + 0x7FD0) == 654,
                    "原始人数、队长和小队编号必须恢复。");
                for (var i = 0; i < 2; i++)
                {
                    var pointer = MainGroup + i * PartyMember.Size;
                    Assert(Memory.Read<uint>(pointer + 0x400) == SelfId + (uint)i &&
                        Memory.ReadBytes(pointer + 8, 0x3D8).SequenceEqual(OriginalStatuses[i]),
                        "必须恢复全部 60 条状态、计时、高位来源和尾部计数字段。");
                    Assert(Memory.Read<IntPtr>(pointer) == Plugin.LastOwners[i] &&
                        Memory.Read<IntPtr>(pointer) != OriginalOwners[i],
                        "不能把旧 StatusManager.Owner 指针覆盖回原生收包重新初始化的成员。");
                    Assert(Memory.ReadBytes(pointer + 0x41C, 64).SequenceEqual(OriginalNames[i]) &&
                        Memory.Read<Vector3>(pointer + 0x3E0) == new Vector3(11 + i, 22 + i, 33 + i) &&
                        Memory.Read<byte>(pointer + 0x484) == 9 && Memory.Read<byte>(pointer + 0x46B) == 37 + i,
                        "名单包只能表达短名字，恢复还必须保留原64字节名字、位置、同步标志与护盾值。");
                }
                Assert(Memory.ReadBytes(HateAddress, Hate.Size).SequenceEqual(OriginalHate), "仇恨快照必须完整恢复。");
            }
            public void Dispose()
            {
                Invoke("InvalidateWorld");
                _manager.Clear();
                _scheduler.Dispose();
            }
        }
    }

    // 按字节存储离线数据，保证同一片区域的值型读取与快照读写看到相同内容。
    public sealed class PartyMemory
    {
        private readonly Dictionary<long, byte> _bytes = new Dictionary<long, byte>();
        public IntPtr ProcessHandle = new IntPtr(101);
        public bool IsProcessOpen = true;
        public IntPtr ShortReadAddress;
        public IntPtr ShortWriteAddress;
        public int WriteCount;
        public int WriteBytesCount;
        private int _allocations;
        public readonly List<IntPtr> Allocations = new List<IntPtr>();
        public readonly List<IntPtr> Freed = new List<IntPtr>();

        public void PutBytes(IntPtr address, byte[] data)
        {
            for (var i = 0; i < data.Length; i++) _bytes[address.ToInt64() + i] = data[i];
        }
        public byte[] ReadBytes(IntPtr address, int count)
        {
            var data = new byte[address == ShortReadAddress ? count - 1 : count];
            for (var i = 0; i < data.Length; i++) _bytes.TryGetValue(address.ToInt64() + i, out data[i]);
            return data;
        }
        public T Read<T>(IntPtr address) where T : struct
        {
            var type = typeof(T);
            var valueType = type.IsEnum ? Enum.GetUnderlyingType(type) : type;
            var size = Marshal.SizeOf(valueType);
            var pointer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(ReadBytes(address, size), 0, pointer, size);
                var value = Marshal.PtrToStructure(pointer, valueType);
                return type.IsEnum ? (T)Enum.ToObject(type, value) : (T)value;
            }
            finally { Marshal.FreeHGlobal(pointer); }
        }
        public void Put<T>(IntPtr address, T value) where T : struct
        {
            object data = value;
            if (typeof(T).IsEnum) data = Convert.ChangeType(value, Enum.GetUnderlyingType(typeof(T)));
            var size = Marshal.SizeOf(data);
            var pointer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(data, pointer, false);
                var bytes = new byte[size];
                Marshal.Copy(pointer, bytes, 0, size);
                PutBytes(address, bytes);
            }
            finally { Marshal.FreeHGlobal(pointer); }
        }
        public void Write<T>(IntPtr address, T value) where T : struct { WriteCount++; Put(address, value); }
        public int WriteBytes(IntPtr address, byte[] data)
        {
            WriteCount++;
            WriteBytesCount++;
            var bytes = address == ShortWriteAddress ? data.Take(data.Length - 1).ToArray() : data;
            PutBytes(address, bytes);
            return bytes.Length;
        }
        public IntPtr AllocateMemory(int size)
        {
            var address = new IntPtr(0x900000 + ++_allocations * 0x10000);
            Allocations.Add(address);
            return address;
        }
        public bool FreeMemory(IntPtr address) { Freed.Add(address); return true; }
    }

    public sealed class PartyPlugin
    {
        private readonly object PluginUi = new object();
        public static readonly IntPtr NameFunction = new IntPtr(0x101), UpdateFunction = new IntPtr(0x102),
            StatusFunction = new IntPtr(0x103), VitalsFunction = new IntPtr(0x104),
            HateFunction = new IntPtr(0x105), ControlFunction = new IntPtr(0x106);
        public object[] LastStatusArguments;
        private readonly IntPtr _group;
        public PartyMemory Memory;
        public bool FailUpdate;
        public bool FailVitals;
        public bool FailHate;
        public bool FailActorControl;
        public Action BeforeUpdateReturn;
        public Action BeforeHateReturn;
        public readonly List<UpdatePartyPacket> Packets = new List<UpdatePartyPacket>();
        public readonly List<StatusEffectListDoublePacket> VitalsPackets = new List<StatusEffectListDoublePacket>();
        public readonly List<uint> VitalsTargets = new List<uint>();
        public readonly List<UpdateHatePacket> HatePackets = new List<UpdateHatePacket>();
        public readonly List<uint> HateTargets = new List<uint>();
        public readonly List<object[]> ActorControls = new List<object[]>();
        public readonly Dictionary<uint, IntPtr> Entities = new Dictionary<uint, IntPtr>();
        public readonly IntPtr[] LastOwners = new IntPtr[8];
        public PartyPlugin(PartyMemory memory, IntPtr group) { Memory = memory; _group = group; }
        public T Call<T>(IntPtr function, params object[] args) where T : struct
        {
            ValidateArguments(args);
            if (function == NameFunction) return (T)(object)((IntPtr)args[0] + 0x30);
            if (function == StatusFunction)
            {
                LastStatusArguments = args;
                var slot = (IntPtr)args[0] + 8 + (int)args[1] * 0x10;
                var id = (ushort)args[2];
                var source = (ulong)args[5];
                var identityChanged = Memory.Read<ushort>(slot) != id || Memory.Read<ulong>(slot + 8) != source;
                Memory.Put(slot, id);
                Memory.Put(slot + 2, (ushort)args[4]);
                Memory.Put(slot + 4, (float)args[3]);
                Memory.Put(slot + 8, source);
                return (T)(object)identityChanged;
            }
            if (function == HateFunction)
            {
                PartyServiceTests.Assert(args.Length == 2 && args[0] is uint && args[1] is IntPtr,
                    "仇恨收包只能接收目标 ID 和远程包地址。");
                var pointer = (IntPtr)args[1];
                PartyServiceTests.Assert(Memory.Allocations.Contains(pointer) && !Memory.Freed.Contains(pointer),
                    "原生收包必须引用仍有效的远程分配。");
                var packet = Memory.Read<UpdateHatePacket>(pointer);
                HatePackets.Add(packet);
                HateTargets.Add((uint)args[0]);
                var hate = UIState.InstancePtr + 8;
                Memory.PutBytes(hate, new byte[Hate.Size]);
                for (var i = 0; i < packet.MemberCount; i++)
                {
                    var member = packet.GetMember(i);
                    Memory.Put(hate + i * 8, member.EntityId);
                    Memory.Put(hate + i * 8 + 4, (int)member.Enmity);
                }
                Memory.Put(hate + 0x100, (int)packet.MemberCount);
                Memory.Put(hate + 0x104, (uint)args[0]);
                var beforeReturn = BeforeHateReturn;
                BeforeHateReturn = null;
                beforeReturn?.Invoke();
                if (FailHate) throw new InvalidOperationException("模拟仇恨收包部分成功后失败。");
                return (T)(object)0L;
            }
            if (function == ControlFunction)
            {
                ActorControls.Add(args);
                PartyServiceTests.Assert(Convert.ToUInt16(args[1]) == 4, "小队测试仅允许设置入战状态。");
                var address = Entities[Convert.ToUInt32(args[0])] + 0x1D4;
                var flags = Memory.Read<byte>(address);
                Memory.Put(address, (byte)(Convert.ToUInt32(args[2]) == 0 ? flags & ~2 : flags | 2));
                if (FailActorControl) throw new InvalidOperationException("模拟入战控制部分成功后失败。");
                return (T)(object)IntPtr.Zero;
            }
            throw new InvalidOperationException("未知的小队测试原生返回值调用。");
        }
        public void Call(IntPtr function, params object[] args)
        {
            ValidateArguments(args);
            if (function == VitalsFunction)
            {
                PartyServiceTests.Assert(args.Length == 3 && args[0] is uint && args[1] is IntPtr &&
                    args[2] is byte && (byte)args[2] == 0, "状态收包必须传实体 ID、远程包地址与实时标记。");
                var pointer = (IntPtr)args[1];
                PartyServiceTests.Assert(Memory.Allocations.Contains(pointer) && !Memory.Freed.Contains(pointer),
                    "状态包必须先写入远程分配，再将地址交给原生函数。");
                var vitalsPacket = Memory.Read<StatusEffectListDoublePacket>(pointer);
                VitalsPackets.Add(vitalsPacket);
                VitalsTargets.Add((uint)args[0]);
                var actor = Entities[(uint)args[0]];
                Memory.Put(actor + 0x1AC, vitalsPacket.CurrentHP);
                Memory.Put(actor + 0x1B0, vitalsPacket.MaxHP);
                Memory.Put(actor + 0x1B4, (uint)vitalsPacket.CurrentMP);
                Memory.Put(actor + 0x1B8, (uint)vitalsPacket.MaxMP);
                for (var i = 0; i < 60; i++)
                {
                    var status = vitalsPacket.GetStatus(i);
                    var address = actor + 0x23B8 + i * 16;
                    Memory.Put(address, status.StatusId);
                    Memory.Put(address + 2, status.Param);
                    Memory.Put(address + 4, status.RemainingTime);
                    Memory.Put(address + 8, (ulong)status.SourceEntityId);
                }
                if (FailVitals) throw new InvalidOperationException("模拟角色状态收包失败。");
                return;
            }
            if (function != UpdateFunction) throw new InvalidOperationException("未知的小队测试原生调用。");
            PartyServiceTests.Assert(Convert.ToByte(args[2]) == 0, "更新必须作用于主小队。");
            var packet = Memory.Read<UpdatePartyPacket>((IntPtr)args[1]);
            Packets.Add(packet);
            Memory.Put(_group + 0x7FC8, packet.PartyId);
            Memory.Put(_group + 0x7FD0, packet.PartyId_2);
            Memory.Put(_group + 0x7FD8, (uint)packet.LeaderIndex);
            Memory.Put(_group + 0x7FDC, packet.MemberCount);
            for (var i = 0; i < 8; i++)
            {
                var pointer = _group + i * PartyMember.Size;
                var member = packet.GetMember(i);
                LastOwners[i] = new IntPtr(0xE00000 + Packets.Count * 0x1000 + i * 0x100);
                Memory.Put(pointer, LastOwners[i]);
                Memory.PutBytes(pointer + 8, new byte[0x3D8]);
                Memory.Put(pointer + 0x400, member.EntityId);
                Memory.PutBytes(pointer + 0x41C, new byte[64]);
                Memory.Put(pointer + 0x3E0, Vector3.Zero);
                Memory.Put(pointer + 0x484, (byte)1);
                Memory.Put(pointer + 0x46B, (byte)0);
                for (var j = 0; j < UpdatePartyPacketMember.StatusCount; j++)
                {
                    var status = member.GetStatus(j);
                    Memory.Put(pointer + 8 + j * 16, status.StatusId);
                    Memory.Put(pointer + 10 + j * 16, status.Param);
                    // 模拟原生函数不恢复剩余时间，且包本身无法表达后 30 项状态。
                    Memory.Put(pointer + 16 + j * 16, (ulong)status.SourceEntityId);
                }
            }
            BeforeUpdateReturn?.Invoke();
            if (FailUpdate) throw new InvalidOperationException("模拟原生小队更新部分成功后失败。");
        }

        private static void ValidateArguments(object[] args)
        {
            // 与 PostNamazu 的调用边界一致：基础数值、指针、布尔和枚举可归一化，自定义结构体不可直接传入。
            foreach (var arg in args)
            {
                if (arg != null && (arg.GetType().IsEnum || arg is bool || arg is UIntPtr || arg is IntPtr ||
                    arg is sbyte || arg is byte || arg is short || arg is ushort || arg is char ||
                    arg is int || arg is uint || arg is long || arg is ulong || arg is float || arg is double)) continue;
                throw new ArgumentException("原生调用不接受此参数类型：" + (arg?.GetType().Name ?? "空值"), nameof(args));
            }
        }
    }
}
