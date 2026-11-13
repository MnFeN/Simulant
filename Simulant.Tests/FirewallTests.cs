using Simulant.ACT;
using Simulant.Core;
using Simulant.Core.Firewall;
using Simulant.Game;
using Simulant.Game.FFCS.Client.System.Framework;
using System;
using System.Linq;

namespace Simulant.Tests
{
    internal static class FirewallTests
    {
        internal static void FirewallEnableAndDisableAreIdempotent()
        {
            using (var scene = new FirewallScene())
            {
                scene.Host.FirewallLog = _ => throw new InvalidOperationException("模拟日志失败。");
                scene.Service.Enable();
                Assert(scene.Parser.NetworkTimeout == int.MaxValue, "开启收包拦截时应暂停解析插件的自动断流提示。");
                Assert(scene.Service.IsEnabled && scene.Service.HasActiveHooks && scene.Service.HasPendingCleanup,
                    "完整启用必须同时报告两个入口和清理责任。");
                Assert(scene.Memory.Blocks[scene.Receive].SequenceEqual(new byte[] { 0x31, 0xC0, 0xC3, 0x90, 0x90 }),
                    "收包补丁字节不能因状态管理修改而改变。");
                var cave = scene.Memory.Allocated.Single();
                var expectedEntry = new byte[] { 0xFF, 0x25, 0, 0, 0, 0 }
                    .Concat(BitConverter.GetBytes((ulong)cave)).Concat(new byte[] { 0x90 });
                Assert(scene.Memory.Blocks[scene.Send].SequenceEqual(expectedEntry), "发包入口必须跳转到持有的代码洞。");
                var expectedCave = new byte[] { 0x66, 0x81, 0x3A, 0x34, 0x12, 0x75, 0x1D }
                    .Concat(scene.SendOriginal).Concat(new byte[] { 0xFF, 0x25, 0, 0, 0, 0 })
                    .Concat(BitConverter.GetBytes((ulong)scene.Send + 15)).Concat(new byte[] { 0x31, 0xC0, 0xC3 }).ToArray();
                Assert(scene.Memory.Blocks[cave].Take(expectedCave.Length).SequenceEqual(expectedCave),
                    "心跳放行、原始指令与回跳字节必须保持原有布局。");
                var writes = scene.Memory.Writes.Count;
                scene.Service.Enable();
                Assert(scene.Memory.Writes.Count == writes && scene.Memory.AllocateCalls == 1,
                    "重复开启不能重写入口或再次分配代码洞。");
                scene.Service.Disable();
                scene.AssertRestored();
                Assert(!scene.Service.IsEnabled && !scene.Service.HasActiveHooks && !scene.Service.HasPendingCleanup,
                    "正常关闭后不能保留已清理记录。");
                writes = scene.Memory.Writes.Count;
                scene.Service.Disable();
                Assert(scene.Memory.Writes.Count == writes && scene.Memory.Freed.Count == 1,
                    "重复关闭不能重复写入或释放代码洞。");
            }
        }

        internal static void FirewallEnableFailuresRollBackEveryCompletedStage()
        {
            // 分别覆盖收包短写、分配失败、代码洞短写/异常、入口短写/异常及心跳地址缺失。
            for (var stage = 0; stage < 8; stage++)
            {
                using (var scene = new FirewallScene())
                {
                    var failure = new InvalidOperationException("模拟原生写入或分配异常。");
                    var selectedStage = stage;
                    scene.Memory.WriteResult = (address, bytes) =>
                    {
                        var receivePatch = address == scene.Receive && bytes[0] == 0x31;
                        var cavePatch = address != scene.Receive && address != scene.Send;
                        var sendPatch = address == scene.Send && bytes[0] == 0xFF;
                        if ((selectedStage == 0 && receivePatch) || (selectedStage == 3 && cavePatch) ||
                            (selectedStage == 5 && sendPatch)) return bytes.Length - 1;
                        if ((selectedStage == 4 && cavePatch) || (selectedStage == 6 && sendPatch)) throw failure;
                        return null;
                    };
                    if (stage == 1) scene.Memory.AllocateResult = () => IntPtr.Zero;
                    if (stage == 2) scene.Memory.AllocateResult = () => throw failure;
                    if (stage == 7) AddressStore.HeartBeatOpcodeLocatePtr = IntPtr.Zero;
                    var error = Throws<InvalidOperationException>(scene.Service.Enable);
                    if (stage == 2 || stage == 4 || stage == 6)
                        Assert(ReferenceEquals(error, failure), "成功回滚后必须保留最初的原生异常。");
                    scene.AssertRestored();
                    Assert(!scene.Service.HasPendingCleanup && !scene.Service.HasActiveHooks && !scene.Service.IsEnabled,
                        "开启失败但回滚成功后不能留下半开启状态，阶段：" + stage);
                    Assert(scene.Memory.Freed.Count == scene.Memory.Allocated.Count,
                        "已经分配但未启用的代码洞也必须回收，阶段：" + stage);
                    if (stage < 5 || stage == 7)
                        Assert(!scene.Memory.Writes.Any(write => write.Address == scene.Send),
                            "发包入口尚未写入时，回滚不应触碰它。");
                }
            }
        }

        internal static void FirewallEnableRollbackFailureRetainsOwnershipAndOriginalErrors()
        {
            using (var scene = new FirewallScene())
            {
                var original = new InvalidOperationException("模拟分配失败。");
                var cleanup = new InvalidOperationException("模拟收包恢复失败。");
                scene.Memory.AllocateResult = () => throw original;
                scene.Memory.WriteResult = (address, bytes) =>
                {
                    if (address == scene.Receive && bytes.SequenceEqual(scene.ReceiveOriginal)) throw cleanup;
                    return null;
                };
                scene.Host.FirewallLog = _ => throw new Exception("模拟日志异常。");
                var error = Throws<AggregateException>(scene.Service.Enable);
                Assert(error.InnerExceptions.Count == 2 && ReferenceEquals(error.InnerExceptions[0], original) &&
                    ReferenceEquals(error.InnerExceptions[1], cleanup), "日志异常不能掩盖开启异常与清理异常。");
                Assert(scene.Service.HasPendingCleanup && scene.Service.HasActiveHooks && !scene.Service.IsEnabled,
                    "回滚失败必须保留真实入口的清理责任，不能报告完整开启。");
                Assert(scene.Parser.NetworkTimeout == int.MaxValue, "收包补丁尚未恢复时不能提前恢复诊断阈值。");
                var allocations = scene.Memory.AllocateCalls;
                var writes = scene.Memory.Writes.Count;
                Throws<InvalidOperationException>(scene.Service.Enable);
                Assert(scene.Memory.AllocateCalls == allocations && scene.Memory.Writes.Count == writes,
                    "仍需清理时必须拒绝重新开启，不能叠加补丁。");
                scene.Memory.WriteResult = null;
                scene.Service.Disable();
                scene.AssertRestored();
                Assert(!scene.Service.HasPendingCleanup, "重试成功后必须释放失败记录。");
            }
        }

        internal static void FirewallDisableRetriesOnlyTheFailedHook()
        {
            foreach (var failReceive in new[] { true, false })
            foreach (var shortWrite in new[] { true, false })
            {
                using (var scene = new FirewallScene())
                {
                    scene.Service.Enable();
                    var failedTarget = failReceive ? scene.Receive : scene.Send;
                    var successfulTarget = failReceive ? scene.Send : scene.Receive;
                    scene.Memory.WriteResult = (address, bytes) =>
                    {
                        if (address != failedTarget) return null;
                        if (shortWrite) return bytes.Length - 1;
                        throw new InvalidOperationException("模拟入口恢复异常。");
                    };
                    Throws<AggregateException>(scene.Service.Disable);
                    Assert(scene.Service.HasPendingCleanup && scene.Service.HasActiveHooks && !scene.Service.IsEnabled,
                        "恢复失败后必须保留失败入口，同时撤销完整启用状态。");
                    Assert(scene.Memory.Freed.Count == (failReceive ? 1 : 0),
                        "只有发包入口已恢复后才可释放代码洞，另一端失败不能阻止清理成功端。");
                    var successfulWrites = scene.Memory.Writes.Count(write => write.Address == successfulTarget);
                    scene.Memory.WriteResult = null;
                    scene.Service.Disable();
                    Assert(scene.Memory.Writes.Count(write => write.Address == successfulTarget) == successfulWrites,
                        "重试关闭不能再次恢复已成功清理的入口。");
                    scene.AssertRestored();
                    Assert(!scene.Service.HasPendingCleanup && scene.Memory.Freed.Count == 1,
                        "重试后应完成全部清理，代码洞只成功释放一次。");
                }
            }
        }

        internal static void FirewallFreeFailureRetriesWithoutRewritingRestoredCode()
        {
            foreach (var throws in new[] { false, true })
            {
                using (var scene = new FirewallScene())
                {
                    scene.Service.Enable();
                    scene.Memory.FreeResult = _ =>
                    {
                        if (throws) throw new InvalidOperationException("模拟代码洞释放异常。");
                        return false;
                    };
                    Throws<AggregateException>(scene.Service.Disable);
                    scene.AssertRestored();
                    Assert(scene.Service.HasPendingCleanup && !scene.Service.HasActiveHooks && !scene.Service.IsEnabled,
                        "仅释放失败时必须保留清理记录，但不能报告入口仍被拦截。");
                    var writes = scene.Memory.Writes.Count;
                    Throws<InvalidOperationException>(scene.Service.Enable);
                    scene.Memory.FreeResult = null;
                    scene.Service.Disable();
                    Assert(scene.Memory.Writes.Count == writes && scene.Memory.FreeAttempts.Count == 2 &&
                        scene.Memory.Freed.Count == 1 && !scene.Service.HasPendingCleanup,
                        "释放重试只能释放同一个代码洞，不能重写已恢复的函数入口。");
                }
            }
        }

        internal static void FirewallCleanupUsesCapturedMemoryAndTargetsAfterProcessSwitch()
        {
            foreach (var useReset in new[] { false, true })
            {
                using (var scene = new FirewallScene())
                {
                    scene.Service.Enable();
                    var nextMemory = scene.SwitchCurrentProcess();
                    Framework.BeforeResolve = () => throw new Exception("清理不能重新解析当前收包入口。");
                    if (useReset) scene.Service.ResetProcess();
                    else scene.Service.Disable();
                    scene.AssertRestored();
                    Assert(scene.Memory.Freed.Count == 1 && !scene.Service.HasPendingCleanup,
                        "旧进程仍活着时必须通过旧实例完整恢复入口与代码洞。");
                    AssertUntouched(nextMemory);
                }
            }
        }

        internal static void FirewallProcessSwitchRetainsLiveCleanupFailuresForRetry()
        {
            using (var scene = new FirewallScene())
            {
                scene.Service.Enable();
                var nextMemory = scene.SwitchCurrentProcess();
                scene.Memory.WriteResult = (address, bytes) => address == scene.Receive ? (int?)0 : null;
                scene.Memory.FreeResult = _ => false;
                var error = Throws<AggregateException>(scene.Service.ResetProcess);
                Assert(error.InnerExceptions.Count == 2 && scene.Service.HasPendingCleanup && scene.Service.HasActiveHooks,
                    "进程切换应尝试清理两端，并保留全部仍可访问的失败资源。");
                AssertUntouched(nextMemory);
                var sendWrites = scene.Memory.Writes.Count(write => write.Address == scene.Send);
                scene.Memory.WriteResult = null;
                scene.Memory.FreeResult = null;
                scene.Service.Disable();
                scene.AssertRestored();
                Assert(!scene.Service.HasPendingCleanup && scene.Memory.Freed.Count == 1 &&
                    scene.Memory.Writes.Count(write => write.Address == scene.Send) == sendWrites,
                    "切换后的关闭重试必须恢复旧收包入口并只释放旧代码洞。");
                AssertUntouched(nextMemory);
            }
        }

        internal static void FirewallProcessResetDiscardsOnlyUnavailableOwnership()
        {
            foreach (var closed in new[] { true, false })
            {
                using (var scene = new FirewallScene())
                {
                    scene.Service.Enable();
                    var nextMemory = scene.SwitchCurrentProcess();
                    if (closed) scene.Memory.IsProcessOpen = false;
                    else scene.Memory.ProcessHandle = new IntPtr(99);
                    var writes = scene.Memory.Writes.Count;
                    Throws<AggregateException>(scene.Service.Disable);
                    Assert(scene.Service.HasPendingCleanup, "普通关闭不能自行遗忘尚未证明已失效的归属。");
                    scene.Service.ResetProcess();
                    Assert(!scene.Service.HasPendingCleanup && !scene.Service.HasActiveHooks && !scene.Service.IsEnabled,
                        "明确关闭或句柄变化的旧进程只能丢弃旧归属。");
                    Assert(scene.Memory.Writes.Count == writes && scene.Memory.FreeAttempts.Count == 0,
                        "丢弃失效归属不能对旧地址执行恢复或释放。");
                    Assert(scene.Parser.NetworkTimeout == 60, "游戏进程已退出时仍需恢复 ACT 内的原诊断阈值。");
                    AssertUntouched(nextMemory);
                }
            }
        }

        internal static void FirewallProcessResetRetryHandlesLaterExitAndResetsMode()
        {
            foreach (var singleHookRetry in new[] { false, true })
            foreach (var processClosed in new[] { false, true })
            {
                using (var scene = new FirewallScene())
                {
                    scene.Service.Enable();
                    var nextMemory = scene.SwitchCurrentProcess();
                    scene.Memory.WriteResult = (address, bytes) => address == scene.Receive ? (int?)0 : null;
                    scene.Memory.FreeResult = _ => false;
                    Throws<AggregateException>(scene.Service.ResetProcess);
                    Assert(scene.Service.HasPendingCleanup, "第一次进程切换清理失败时应保留旧入口和代码洞。");

                    var writes = scene.Memory.Writes.Count;
                    var freeAttempts = scene.Memory.FreeAttempts.Count;
                    if (processClosed) scene.Memory.IsProcessOpen = false;
                    else scene.Memory.ProcessHandle = new IntPtr(99);
                    if (singleHookRetry)
                    {
                        scene.Service.ReceiveHookDisable();
                        Assert(scene.Service.HasPendingCleanup, "只清除收包归属时还需保留发包代码洞记录。");
                        scene.Service.SendHookDisable();
                    }
                    else scene.Service.Disable();
                    Assert(!scene.Service.HasPendingCleanup && !scene.Service.HasActiveHooks,
                        "旧进程在切换清理失败后才失效时，关闭重试也必须结束旧归属。");
                    Assert(scene.Memory.Writes.Count == writes && scene.Memory.FreeAttempts.Count == freeAttempts,
                        "丢弃已失效归属不能继续写入旧地址或释放旧代码洞。");
                    AssertUntouched(nextMemory);

                    // 在当前 B 进程重新安装后，未收到新的进程切换就仍应使用普通关闭规则。
                    nextMemory.Blocks[Framework.ReceiveTarget] = (byte[])scene.ReceiveOriginal.Clone();
                    nextMemory.Blocks[AddressStore.OnSendPacketFuncPtr] = (byte[])scene.SendOriginal.Clone();
                    nextMemory.Blocks[AddressStore.HeartBeatOpcodeLocatePtr + 4] = new byte[] { 0x34, 0x12 };
                    scene.Service.Enable();
                    Assert(scene.Service.IsEnabled, "旧记录全部清空后应允许在当前进程重新安装。");
                    nextMemory.IsProcessOpen = false;
                    if (singleHookRetry)
                    {
                        Throws<InvalidOperationException>(scene.Service.ReceiveHookDisable);
                        Throws<InvalidOperationException>(scene.Service.SendHookDisable);
                    }
                    else Throws<AggregateException>(scene.Service.Disable);
                    Assert(scene.Service.HasPendingCleanup,
                        "新安装不得继承上一轮切换清理模式，普通关闭仍须保留不可用归属。");
                }
            }
        }

        internal static void FirewallRejectsIncompleteCaptureBeforePatching()
        {
            foreach (var failReceive in new[] { true, false })
            {
                using (var scene = new FirewallScene())
                {
                    var failedTarget = failReceive ? scene.Receive : scene.Send;
                    scene.Memory.ReadResult = (address, count) => scene.Memory.Blocks[address]
                        .Take(address == failedTarget ? count - 1 : count).ToArray();
                    Throws<InvalidOperationException>(scene.Service.Enable);
                    Assert(!scene.Memory.Writes.Any(write => write.Address == failedTarget) &&
                        scene.Memory.AllocateCalls == 0 && !scene.Service.HasPendingCleanup,
                        "未完整保存原始入口时必须拒绝写补丁，同时回滚已经开启的另一端。");
                    scene.AssertRestored();
                }
            }
            using (var scene = new FirewallScene())
            {
                GreyMagicExternalProcessMemory nextMemory = null;
                Framework.BeforeResolve = () => nextMemory = scene.SwitchCurrentProcess();
                Throws<InvalidOperationException>(scene.Service.Enable);
                Assert(scene.Memory.Writes.Count == 0 && !scene.Service.HasPendingCleanup,
                    "解析入口期间内存实例发生变化时不能把新进程地址写入旧进程。");
                AssertUntouched(nextMemory);
            }
        }

        internal static void FirewallHeartbeatReadStaysBoundToCapturedProcess()
        {
            using (var scene = new FirewallScene())
            {
                GreyMagicExternalProcessMemory nextMemory = null;
                scene.Memory.AllocateResult = () =>
                {
                    nextMemory = new GreyMagicExternalProcessMemory { ProcessHandle = new IntPtr(2) };
                    nextMemory.Blocks[scene.Heartbeat + 4] = new byte[] { 0x78, 0x56 };
                    NamazuInterop.Plugin = new NamazuPlugin { Memory = nextMemory };
                    return new IntPtr(0x4000);
                };
                scene.Service.Enable();
                var cave = scene.Memory.Blocks[scene.Memory.Allocated.Single()];
                Assert(cave[3] == 0x34 && cave[4] == 0x12, "已捕获的补丁不能混入新内存实例中的心跳包编号。");
                AssertUntouched(nextMemory);
                scene.Service.Disable();
                scene.AssertRestored();
            }
        }

        private static void AssertUntouched(GreyMagicExternalProcessMemory memory)
            => Assert(memory != null && memory.ReadCalls == 0 && memory.Writes.Count == 0 &&
                memory.AllocateCalls == 0 && memory.FreeAttempts.Count == 0,
                "旧归属的清理不能读取、写入、分配或释放新进程的内存。");

        internal static void FirewallTimeoutFailureOnlyWarnsAndRebindingRestoresOriginalInstance()
        {
            using (var scene = new FirewallScene())
            {
                var warned = false;
                scene.Host.FirewallLog = message => warned |= message == "禁用解析插件收包计时器失败，如果右下角出现无法接收数据的 ACT 弹窗属于正常现象。";
                XivPluginInterop.plugin = null;
                scene.Service.Enable();
                Assert(scene.Service.IsEnabled && warned, "禁用计时器失败时只能警告，防火墙仍应正常开启。");
                scene.Service.Disable();
                scene.AssertRestored();

                XivPluginInterop.plugin = scene.Parser;
                scene.Service.Enable();
                var replacement = new FFXIV_ACT_Plugin.FFXIV_ACT_Plugin();
                XivPluginInterop.plugin = replacement;
                scene.Service.Disable();
                scene.AssertRestored();
                Assert(replacement.NetworkTimeout == 60, "恢复必须作用于原实例，不能修改重新绑定的解析插件。");
            }
        }

        private static T Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T error) { return error; }
            throw new Exception("预期抛出异常：" + typeof(T).Name);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private sealed class FirewallScene : IDisposable
        {
            internal readonly IntPtr Receive = new IntPtr(0x1000);
            internal readonly IntPtr Send = new IntPtr(0x2000);
            internal readonly IntPtr Heartbeat = new IntPtr(0x3000);
            internal readonly byte[] ReceiveOriginal = { 0x48, 0x89, 0x5C, 0x24, 0x08 };
            internal readonly byte[] SendOriginal =
                { 0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x74, 0x24, 0x10, 0x4C, 0x89, 0x64, 0x24, 0x18 };
            internal readonly GreyMagicExternalProcessMemory Memory = new GreyMagicExternalProcessMemory();
            internal readonly PluginHost Host = new PluginHost();
            internal readonly FFXIV_ACT_Plugin.FFXIV_ACT_Plugin Parser = new FFXIV_ACT_Plugin.FFXIV_ACT_Plugin();
            internal readonly FirewallService Service;

            internal FirewallScene()
            {
                Memory.Blocks[Receive] = (byte[])ReceiveOriginal.Clone();
                Memory.Blocks[Send] = (byte[])SendOriginal.Clone();
                Memory.Blocks[Heartbeat + 4] = new byte[] { 0x34, 0x12 };
                NamazuInterop.Plugin = new NamazuPlugin { Memory = Memory };
                AddressStore.OnSendPacketFuncPtr = Send;
                AddressStore.HeartBeatOpcodeLocatePtr = Heartbeat;
                Framework.ReceiveTarget = Receive;
                Framework.BeforeResolve = null;
                XivPluginInterop.plugin = Parser;
                Service = new FirewallService(Host);
            }

            internal GreyMagicExternalProcessMemory SwitchCurrentProcess()
            {
                var next = new GreyMagicExternalProcessMemory { ProcessHandle = new IntPtr(2) };
                NamazuInterop.Plugin = new NamazuPlugin { Memory = next };
                AddressStore.OnSendPacketFuncPtr = new IntPtr(0x9000);
                AddressStore.HeartBeatOpcodeLocatePtr = new IntPtr(0xA000);
                Framework.ReceiveTarget = new IntPtr(0xB000);
                return next;
            }

            internal void AssertRestored()
                => Assert(Memory.Blocks[Receive].SequenceEqual(ReceiveOriginal) &&
                    Memory.Blocks[Send].SequenceEqual(SendOriginal) && Parser.NetworkTimeout == 60,
                    "两个原函数入口及解析插件诊断阈值都必须恢复到开启前的状态。");

            public void Dispose()
            {
                Memory.WriteResult = null;
                Memory.FreeResult = null;
                Framework.BeforeResolve = null;
                Service.ResetProcess();
                NamazuInterop.Plugin = null;
                XivPluginInterop.plugin = null;
                AddressStore.OnSendPacketFuncPtr = IntPtr.Zero;
                AddressStore.HeartBeatOpcodeLocatePtr = IntPtr.Zero;
                Framework.ReceiveTarget = IntPtr.Zero;
            }
        }
    }
}
