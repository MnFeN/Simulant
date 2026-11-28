using Simulant.ACT;
using Simulant.Game;
using Simulant.Game.FFCS.Client.System.Framework;
using SimuMagic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Simulant.Core.Firewall
{
    internal sealed partial class FirewallService
    {
        private sealed class HookState
        {
            internal ExternalProcessMemory Memory;
            internal IntPtr ProcessHandle;
            internal Process Process;
            internal IntPtr Target;
            internal byte[] Original;
            internal IntPtr Cave;
            internal bool CapturesActionRequests;
            internal ushort CancelCastOpcode;
            internal uint ActionRequestsRead;
            internal uint ActionRequestsDropped;
            internal bool NeedsRestore;
            internal bool Enabled;
            internal Action RestoreNetworkTimeout;
        }

        private readonly PluginHost _host;
        private readonly object _gate = new object();
        private HookState _receive;
        private HookState _send;
        private HookState _interaction;
        // 进程切换清理失败后，后续重试仍需识别已经结束的旧进程。
        private bool _processResetPending;
        private const int SendHookLength = 15;
        // xor eax, eax; ret; nop; nop
        private static readonly byte[] ReturnZeroPatch = { 0x31, 0xC0, 0xC3, 0x90, 0x90 };

        public FirewallService(PluginHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public bool IsEnabled
        {
            get
            {
                lock (_gate)
                    return _receive?.Enabled == true && _send?.Enabled == true;
            }
        }

        /// <summary>包括部分写入失败后仍需恢复的入口；不包括仅剩待释放代码洞的情况。</summary>
        public bool HasActiveHooks
        {
            get
            {
                lock (_gate)
                    return _receive?.NeedsRestore == true || _send?.NeedsRestore == true || _interaction?.NeedsRestore == true;
            }
        }

        /// <summary>仍持有入口恢复或代码洞释放责任，完整启用期间也为真。</summary>
        public bool HasPendingCleanup
        {
            get
            {
                lock (_gate)
                    return _receive != null || _send != null || _interaction != null;
            }
        }

        public void Enable()
        {
            EnableWithRollback(() =>
            {
                InteractionHookEnableCore();
                ReceiveHookEnableCore();
                SendHookEnableCore();
            });
        }

        public void Disable()
        {
            _host.PlayerActions?.RestoreSimPlayer();
            try
            {
                lock (_gate)
                {
                    var errors = DisableCore();

                    if (errors != null)
                        throw new AggregateException("关闭防火墙失败，未完成的清理记录已保留。", errors);
                }
            }
            finally
            {
                LogSafely(() => _host.PlayerActions?.Reset());
            }
        }

        /// <summary>退出模拟等前置清理失败时，仅在确认绑定进程已退出后重置防火墙。</summary>
        public bool TryResetExitedProcess()
        {
            lock (_gate)
            {
                var states = new[] { _receive, _send, _interaction }.Where(state => state != null).ToArray();
                if (states.Length == 0 || states.Any(state => !HasCapturedProcessExited(state)))
                    return false;

                foreach (var state in states)
                    ResetExitedHook(state);

                _receive = _send = _interaction = null;
                ResetProcessModeIfCleared();
                _host.PlayerActions?.ForgetSimPlayer();
                LogSafely(() => _host.PlayerActions?.Reset());
                return true;
            }
        }

        /// <summary>处理进程切换：恢复仍可访问的旧进程，只丢弃已关闭或句柄已变化的旧归属。</summary>
        public void ResetProcess()
        {
            try
            {
                lock (_gate)
                {
                    _processResetPending = true;
                    var errors = DisableCore();

                    if (errors != null)
                        throw new AggregateException("切换进程时未能完整恢复旧防火墙，清理记录已保留。", errors);
                }
            }
            finally
            {
                LogSafely(() => _host.PlayerActions?.Reset());
            }
        }

        private void EnableWithRollback(Action enable)
        {
            lock (_gate)
            {
                if ((_receive != null && !_receive.Enabled) || (_send != null && !_send.Enabled) ||
                    (_interaction != null && !_interaction.Enabled))
                    throw new InvalidOperationException("防火墙仍有未完成的清理，请先关闭并重试。");

                if (!IsEnabled)
                    _host.EnsureFirewallAllowed();

                if (_receive == null && _send == null && _interaction == null)
                {
                    _host.CancelCastBeforeFirewall();
                    _host.PlayerActions?.RecordPlayerLevel();
                }

                try
                {
                    enable();
                }
                catch (Exception enableError)
                {
                    var errors = DisableCore();

                    if (errors != null)
                    {
                        errors.Insert(0, enableError);
                        throw new AggregateException("开启防火墙失败，回滚也未完成；清理记录已保留。", errors);
                    }

                    _host.PlayerActions?.ForgetSimPlayer();
                    throw;
                }
            }
        }

        private List<Exception> DisableCore()
        {
            List<Exception> errors = null;

            void Restore(Action restore)
            {
                try
                {
                    restore();
                }
                catch (Exception error)
                {
                    if (errors == null)
                        errors = new List<Exception>();

                    errors.Add(error);
                }
            }

            Restore(ReceiveHookDisableCore);
            Restore(SendHookDisableCore);
            Restore(RestoreInteractionWhenNetworkRestored);
            ResetProcessModeIfCleared();
            return errors;
        }

        private void ResetProcessModeIfCleared()
        {
            if (_receive == null && _send == null && _interaction == null)
                _processResetPending = false;
        }

        public void ReceiveHookEnable()
            => EnableWithRollback(ReceiveHookEnableCore);

        private void ReceiveHookEnableCore()
        {
            if (_receive?.Enabled == true)
                return;

            var memory = CaptureMemory();
            var processHandle = memory.ProcessHandle;
            var dispatcher = Framework.Instance.NetworkModuleProxy.NetworkModule.PacketReceiverCallback.PacketDispatcher;
            LogSafely(() => _host.LogVerbose($"PacketDispatcher at {(ulong)dispatcher.Ptr:X}"));

            var vFuncOnReceivePtr = dispatcher.Ptr.GetVFuncPtr(1);
            LogSafely(() => _host.LogVerbose($"OnReceivePacket at {(ulong)vFuncOnReceivePtr:X}"));
            var state = CaptureHook(memory, processHandle, vFuncOnReceivePtr, ReturnZeroPatch.Length);
            _receive = state;

            try
            {
                state.RestoreNetworkTimeout = XivPluginInterop.SuspendNetworkTimeout();
            }
            catch
            {
                LogSafely(() => _host.LogWarning("禁用解析插件收包计时器失败，如果右下角出现无法接收数据的 ACT 弹窗属于正常现象。"));
            }

            // 可以拦截全部收包，恢复后不会掉线
            InstallReturnZeroHook(state);
            LogSafely(() => _host.LogRuntime("收包 Hook 已开启，拦截全部收包。"));
        }

        public void ReceiveHookDisable()
        {
            lock (_gate)
            {
                ReceiveHookDisableCore();
                RestoreInteractionWhenNetworkRestored();
                ResetProcessModeIfCleared();
            }
        }

        private void ReceiveHookDisableCore()
        {
            var state = _receive;
            try
            {
                ReceiveHookRestoreCore();
            }
            catch (Exception) when (HasCapturedProcessExited(state))
            {
                ResetExitedHook(state);
                _receive = null;
            }
        }

        private void ReceiveHookRestoreCore()
        {
            var state = _receive;

            if (state == null)
                return;

            if (_processResetPending && !IsCapturedProcessAvailable(state))
            {
                state.RestoreNetworkTimeout?.Invoke();
                _receive = null;
                return;
            }

            RestoreEntry(state);
            state.RestoreNetworkTimeout?.Invoke();
            _receive = null;
            LogSafely(() => _host.LogRuntime("收包 Hook 已关闭，恢复正常收包。"));
        }

        public void SendHookEnable()
            => EnableWithRollback(SendHookEnableCore);

        private void SendHookEnableCore()
        {
            if (_send?.Enabled == true)
            {
                LogSafely(() => _host.LogWarning("发包 Hook 已经开启，跳过操作。"));
                return;
            }

            var memory = CaptureMemory();
            var processHandle = memory.ProcessHandle;
            var target = AddressStore.OnSendPacketFuncPtr;

            if (target == IntPtr.Zero)
            {
                throw new InvalidOperationException("发包 Hook 开启失败，OnSendPacketFuncPtr 无效。");
            }

            LogSafely(() => _host.LogVerbose($"OnSendPacketFuncPtr at {(ulong)target:X}"));

            // 从 ZoneClient.SendPacket 沿转发调用进入 AddressStore.OnSendPacketFuncPtr 所定位的函数。
            // 入口签名的前三条 mov 将 rbx、rsi、r12 保存到调用者预留的栈槽，合计 15 字节。
            // 这三条完整指令只用 rsp 寻址，原样搬入放行分支后能在同一个栈帧中执行。
            var state = CaptureHook(memory, processHandle, target, SendHookLength);
            _send = state;

            // 同一次远程分配保存入口跳板、请求采集代码和队列，随本次发包 Hook 一起回收。
            EnsureCapturedProcess(state);
            state.Cave = state.Memory.AllocateMemory(SendCaveSize);

            if (state.Cave == IntPtr.Zero)
                throw new InvalidOperationException("发包 Hook 开启失败，无法分配代码洞。");

            LogSafely(() => _host.LogVerbose($"Send hook cave @ {(ulong)state.Cave:X}"));

            var heartbeatAddress = AddressStore.HeartBeatOpcodeLocatePtr;

            if (heartbeatAddress == IntPtr.Zero)
                throw new InvalidOperationException("心跳包编号地址无效，本次不能开启发包拦截。");

            EnsureCapturedProcess(state);
            var passOpcode = state.Memory.Read<ushort>(heartbeatAddress + 4);
            var returnAddress = (ulong)target + SendHookLength;

            // 从 ZoneClient.SendPacket 的转发参数确认 rdx 是发送描述缓冲区，开头是 opcode。
            // 心跳分支保留所有参数和栈指针，补执行入口指令后继续原函数。
            // 其余包跳到拦截分支，其中命中技能请求签名和长度的包会先复制到队列。
            var cave = new List<byte>();

            // cmp word ptr [rdx], imm16
            // 66 81 3A xx xx
            cave.AddRange(new byte[]
            {
                0x66, 0x81, 0x3A
            }.Concat(BitConverter.GetBytes(passOpcode)));

            var blocked = AppendShortJump(cave, 0x75); // jne 拦截分支

            // 补执行被入口覆盖掉的原始指令
            cave.AddRange(state.Original);

            // FF 25 00 00 00 00 是 jmp qword ptr [rip+0]。紧跟指令的 8 字节存放完整回跳地址，
            // 回到原函数被覆盖的三条指令之后。间接跳转无需代码洞位于 rel32 可达范围内。
            cave.AddRange(new byte[]
            {
                0xFF, 0x25, 0x00, 0x00, 0x00, 0x00
            });
            cave.AddRange(BitConverter.GetBytes(returnAddress));

            PatchShortJump(cave, blocked);
            AppendActionRequestCapture(cave, state, passOpcode);

            // 生成的指令只能占用队列之前的区域，队列头和槽位留给运行时读写。
            if (cave.Count > ActionRequestQueueOffset)
                throw new InvalidOperationException("发包代码超过预留空间。");

            WriteExact(state, state.Cave, cave.ToArray());

            // 原入口同样用 jmp qword ptr [rip+0] 读取紧随指令的代码洞地址，最后补一个 nop，
            // 使补丁正好覆盖三条完整的入口指令。恢复时写回 CaptureHook 保存的原始字节。
            var patch = new List<byte>();
            patch.AddRange(new byte[]
            {
                0xFF, 0x25, 0x00, 0x00, 0x00, 0x00
            });
            patch.AddRange(BitConverter.GetBytes((ulong)state.Cave));
            patch.AddRange(new byte[]
            {
                0x90
            });

            state.NeedsRestore = true;
            WriteExact(state, target, patch.ToArray());

            state.Enabled = true;
            LogSafely(() => _host.LogRuntime($"发包 Hook 已开启，仅放行心跳包 0x{passOpcode:X4}。"));
        }

        public void SendHookDisable()
        {
            lock (_gate)
            {
                SendHookDisableCore();
                RestoreInteractionWhenNetworkRestored();
                ResetProcessModeIfCleared();
            }
        }

        private void SendHookDisableCore()
        {
            var state = _send;
            try
            {
                SendHookRestoreCore();
            }
            catch (Exception) when (HasCapturedProcessExited(state))
            {
                ResetExitedHook(state);
                _send = null;
            }
        }

        private void SendHookRestoreCore()
        {
            var state = _send;

            if (state == null)
                return;

            if (_processResetPending && !IsCapturedProcessAvailable(state))
            {
                _send = null;
                return;
            }

            RestoreEntry(state);

            // 释放代码洞
            if (state.Cave != IntPtr.Zero)
            {
                EnsureCapturedProcess(state);

                if (!state.Memory.FreeMemory(state.Cave))
                    throw new InvalidOperationException("发包入口已恢复，但代码洞释放失败；已保留记录供重试。");

                state.Cave = IntPtr.Zero;
            }

            _send = null;
            LogSafely(() => _host.LogRuntime("发包 Hook 已关闭，恢复正常发包。"));
        }

        private ExternalProcessMemory CaptureMemory()
        {
            var memory = GameInterop.Connection?.Memory;

            if (memory == null || !memory.IsProcessOpen || memory.ProcessHandle == IntPtr.Zero)
                throw new InvalidOperationException("防火墙无法访问当前游戏进程。");

            var existing = _receive ?? _send ?? _interaction;

            if (existing != null && (!ReferenceEquals(existing.Memory, memory) ||
                existing.ProcessHandle != memory.ProcessHandle))
                throw new InvalidOperationException("防火墙仍持有另一进程的记录，请先完成旧进程清理或重置归属。");

            return memory;
        }

        private static HookState CaptureHook(ExternalProcessMemory memory, IntPtr processHandle, IntPtr target, int length)
        {
            if (target == IntPtr.Zero)
                throw new InvalidOperationException("防火墙函数入口地址无效。");

            if (!ReferenceEquals(memory, GameInterop.Connection?.Memory))
                throw new InvalidOperationException("定位防火墙入口期间内存实例已变化，本次未写入补丁。");

            var state = new HookState
            {
                Memory = memory,
                ProcessHandle = processHandle,
                Process = memory.Process,
                Target = target
            };
            EnsureCapturedProcess(state);
            state.Original = memory.ReadBytes(target, length);

            if (state.Original == null || state.Original.Length != length)
                throw new InvalidOperationException("防火墙未能完整读取函数入口，不能写入补丁。");

            return state;
        }

        private static void InstallReturnZeroHook(HookState state)
        {
            state.NeedsRestore = true;
            WriteExact(state, state.Target, ReturnZeroPatch);
            state.Enabled = true;
        }

        private static void RestoreEntry(HookState state)
        {
            state.Enabled = false;
            if (!state.NeedsRestore)
                return;

            WriteExact(state, state.Target, state.Original);
            state.NeedsRestore = false;
        }

        private static void EnsureCapturedProcess(HookState state)
        {
            if (!IsCapturedProcessAvailable(state))
                throw new InvalidOperationException("防火墙记录所属的进程已变化，不能对当前进程恢复旧补丁。");
        }

        private static bool IsCapturedProcessAvailable(HookState state)
            => state.Memory.IsProcessOpen && state.Memory.ProcessHandle == state.ProcessHandle;

        private static bool HasCapturedProcessExited(HookState state)
        {
            try
            {
                return state?.Process?.HasExited == true;
            }
            catch
            {
                // 无法确认进程退出时仍保留清理记录，避免遗留运行中的补丁。
                return false;
            }
        }

        private void ResetExitedHook(HookState state)
        {
            try
            {
                state.RestoreNetworkTimeout?.Invoke();
            }
            catch (Exception error)
            {
                LogSafely(() => _host.LogWarning($"游戏进程已退出，恢复解析插件收包计时器失败：{error.Message}"));
            }
            LogSafely(() => _host.LogWarning("关闭防火墙时清理失败，但原绑定的 FF14 进程已退出，已重置防火墙记录。"));
        }

        private static void WriteExact(HookState state, IntPtr address, byte[] bytes)
        {
            EnsureCapturedProcess(state);
            var written = state.Memory.WriteBytes(address, bytes);

            if (written != bytes.Length)
                throw new InvalidOperationException($"防火墙内存写入不完整：应写入 {bytes.Length} 字节，实际写入 {written} 字节。");
        }

        private static void LogSafely(Action log)
        {
            try
            {
                log();
            }
            catch
            { /* 日志失败不能改变补丁状态或掩盖原生操作异常。 */
            }
        }
    }
}
