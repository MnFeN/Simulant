using Simulant.ACT;
using Simulant.Game;
using Simulant.Game.FFCS.Client.System.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulant.Core.Firewall
{
    internal sealed class FirewallService
    {
        private sealed class HookState
        {
            internal GreyMagicExternalProcessMemory Memory;
            internal IntPtr ProcessHandle;
            internal IntPtr Target;
            internal byte[] Original;
            internal IntPtr Cave;
            internal bool NeedsRestore;
            internal bool Enabled;
        }

        private readonly PluginHost _host;
        private readonly object _gate = new object();
        private HookState _receive;
        private HookState _send;
        // 进程切换清理失败后，后续重试仍需识别已经结束的旧进程。
        private bool _processResetPending;
        private const int SendHookLength = 15;

        public FirewallService(PluginHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public bool IsEnabled
        {
            get { lock (_gate) return _receive?.Enabled == true && _send?.Enabled == true; }
        }

        /// <summary>包括部分写入失败后仍需恢复的入口；不包括仅剩待释放代码洞的情况。</summary>
        public bool HasActiveHooks
        {
            get { lock (_gate) return _receive?.NeedsRestore == true || _send?.NeedsRestore == true; }
        }

        /// <summary>仍持有入口恢复或代码洞释放责任，完整启用期间也为真。</summary>
        public bool HasPendingCleanup
        {
            get { lock (_gate) return _receive != null || _send != null; }
        }

        public void Enable()
        {
            EnableWithRollback(() =>
            {
                ReceiveHookEnableCore();
                SendHookEnableCore();
            });
        }

        public void Disable()
        {
            lock (_gate)
            {
                var errors = DisableCore();
                if (errors != null) throw new AggregateException("关闭防火墙失败，未完成的清理记录已保留。", errors);
            }
        }

        /// <summary>处理进程切换：恢复仍可访问的旧进程，只丢弃已关闭或句柄已变化的旧归属。</summary>
        public void ResetProcess()
        {
            lock (_gate)
            {
                _processResetPending = true;
                var errors = DisableCore();
                if (errors != null) throw new AggregateException("切换进程时未能完整恢复旧防火墙，清理记录已保留。", errors);
            }
        }

        private void EnableWithRollback(Action enable)
        {
            lock (_gate)
            {
                if ((_receive != null && !_receive.Enabled) || (_send != null && !_send.Enabled))
                    throw new InvalidOperationException("防火墙仍有未完成的清理，请先关闭并重试。");
                try { enable(); }
                catch (Exception enableError)
                {
                    var errors = DisableCore();
                    if (errors != null)
                    {
                        errors.Insert(0, enableError);
                        throw new AggregateException("开启防火墙失败，回滚也未完成；清理记录已保留。", errors);
                    }
                    throw;
                }
            }
        }

        private List<Exception> DisableCore()
        {
            List<Exception> errors = null;
            try { ReceiveHookDisableCore(); }
            catch (Exception error) { errors = new List<Exception> { error }; }
            try { SendHookDisableCore(); }
            catch (Exception error)
            {
                if (errors == null) errors = new List<Exception>();
                errors.Add(error);
            }
            ResetProcessModeIfCleared();
            return errors;
        }

        private void ResetProcessModeIfCleared()
        {
            if (_receive == null && _send == null) _processResetPending = false;
        }

        public void ReceiveHookEnable()
            => EnableWithRollback(ReceiveHookEnableCore);

        private void ReceiveHookEnableCore()
        {
            if (_receive?.Enabled == true) return;
            var memory = CaptureMemory();
            var processHandle = memory.ProcessHandle;
            var dispatcher = Framework.Instance.NetworkModuleProxy.NetworkModule.PacketReceiverCallback.PacketDispatcher;
            LogSafely(() => _host.LogVerbose($"PacketDispatcher at {(ulong)dispatcher.Ptr:X}"));

            var vFuncOnReceivePtr = dispatcher.Ptr.GetVFuncPtr(1);
            LogSafely(() => _host.LogVerbose($"OnReceivePacket at {(ulong)vFuncOnReceivePtr:X}"));
            var state = CaptureHook(memory, processHandle, vFuncOnReceivePtr, 5);
            _receive = state;

            // 可以拦截全部收包，恢复后不会掉线
            // patch:
            // xor eax, eax     31 C0
            // ret              C3
            state.NeedsRestore = true;
            WriteExact(state, state.Target, new byte[] { 0x31, 0xC0, 0xC3, 0x90, 0x90 });
            state.Enabled = true;
            LogSafely(() => _host.LogRuntime("收包 Hook 已开启，拦截全部收包。"));
        }

        public void ReceiveHookDisable()
        {
            lock (_gate)
            {
                ReceiveHookDisableCore();
                ResetProcessModeIfCleared();
            }
        }

        private void ReceiveHookDisableCore()
        {
            var state = _receive;
            if (state == null) return;
            if (_processResetPending && !IsCapturedProcessAvailable(state))
            {
                _receive = null;
                return;
            }
            state.Enabled = false;
            if (state.NeedsRestore)
            {
                WriteExact(state, state.Target, state.Original);
                state.NeedsRestore = false;
            }
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

            // 函数开头前 3 条指令：
            // 48 89 5C 24 08
            // 48 89 74 24 10
            // 4C 89 64 24 18
            var state = CaptureHook(memory, processHandle, target, SendHookLength);
            _send = state;

            // Allocate 代码洞
            EnsureCapturedProcess(state);
            state.Cave = state.Memory.AllocateMemory(0x100);
            if (state.Cave == IntPtr.Zero)
                throw new InvalidOperationException("发包 Hook 开启失败，无法分配代码洞。");

            LogSafely(() => _host.LogVerbose($"Send hook cave @ {(ulong)state.Cave:X}"));

            var heartbeatAddress = AddressStore.HeartBeatOpcodeLocatePtr;
            if (heartbeatAddress == IntPtr.Zero)
                throw new InvalidOperationException("心跳包编号地址无效，本次不能开启发包拦截。");
            EnsureCapturedProcess(state);
            var passOpcode = state.Memory.Read<ushort>(heartbeatAddress + 4);
            var returnAddress = (ulong)target + SendHookLength;

            // 代码洞内容：
            // 判断是否为心跳包，是则放行，否则直接返回（跳转至 block 处）：
            // cmp word ptr [rdx], passOpcode
            // jne block
            //
            // 放行路径：执行被覆盖的指令，跳转回补丁后：
            // <原始指令>
            // jmp qword ptr [rip+0]
            // <8 字节回跳地址>
            //
            // block 路径: return 0
            // xor eax, eax
            // ret
            var cave = new List<byte>();

            // cmp word ptr [rdx], imm16
            // 66 81 3A xx xx
            cave.AddRange(new byte[] { 0x66, 0x81, 0x3A }.Concat(BitConverter.GetBytes(passOpcode)));

            // jne block
            var jnePos = cave.Count;
            cave.AddRange(new byte[] { 0x75, 0x00 });

            // 补执行被入口覆盖掉的原始指令
            cave.AddRange(state.Original);

            // jmp qword ptr [rip+0]
            // FF 25 00 00 00 00
            // <8 字节回跳地址>
            cave.AddRange(new byte[] { 0xFF, 0x25, 0x00, 0x00, 0x00, 0x00 });
            cave.AddRange(BitConverter.GetBytes(returnAddress));

            // block:
            var blockPos = cave.Count;
            var blockOffset = blockPos - (jnePos + 2);
            // xor eax, eax
            // ret
            cave.AddRange(new byte[] { 0x31, 0xC0 });
            cave.AddRange(new byte[] { 0xC3 });

            // 回填 jne 的相对偏移
            cave[jnePos + 1] = unchecked((byte)blockOffset);

            WriteExact(state, state.Cave, cave.ToArray());

            // 入口补丁：
            // FF 25 00 00 00 00
            // <8字节 cave 地址>
            // 90
            var patch = new List<byte>();
            patch.AddRange(new byte[] { 0xFF, 0x25, 0x00, 0x00, 0x00, 0x00 });
            patch.AddRange(BitConverter.GetBytes((ulong)state.Cave));
            patch.AddRange(new byte[] { 0x90 });

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
                ResetProcessModeIfCleared();
            }
        }

        private void SendHookDisableCore()
        {
            var state = _send;
            if (state == null) return;
            if (_processResetPending && !IsCapturedProcessAvailable(state))
            {
                _send = null;
                return;
            }
            state.Enabled = false;
            // 恢复原函数入口指令
            if (state.NeedsRestore)
            {
                WriteExact(state, state.Target, state.Original);
                state.NeedsRestore = false;
            }

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

        private GreyMagicExternalProcessMemory CaptureMemory()
        {
            var memory = NamazuInterop.Plugin?.Memory;
            if (memory == null || !memory.IsProcessOpen || memory.ProcessHandle == IntPtr.Zero)
                throw new InvalidOperationException("防火墙无法访问当前游戏进程。");
            var existing = _receive ?? _send;
            if (existing != null && (!ReferenceEquals(existing.Memory.RawMemory, memory.RawMemory) ||
                existing.ProcessHandle != memory.ProcessHandle))
                throw new InvalidOperationException("防火墙仍持有另一进程的记录，请先完成旧进程清理或重置归属。");
            return memory;
        }

        private static HookState CaptureHook(GreyMagicExternalProcessMemory memory, IntPtr processHandle, IntPtr target, int length)
        {
            if (target == IntPtr.Zero) throw new InvalidOperationException("防火墙函数入口地址无效。");
            if (!ReferenceEquals(memory.RawMemory, NamazuInterop.Plugin?.Memory?.RawMemory))
                throw new InvalidOperationException("定位防火墙入口期间内存实例已变化，本次未写入补丁。");
            var state = new HookState { Memory = memory, ProcessHandle = processHandle, Target = target };
            EnsureCapturedProcess(state);
            state.Original = memory.ReadBytes(target, length);
            if (state.Original == null || state.Original.Length != length)
                throw new InvalidOperationException("防火墙未能完整读取函数入口，不能写入补丁。");
            return state;
        }

        private static void EnsureCapturedProcess(HookState state)
        {
            if (!IsCapturedProcessAvailable(state))
                throw new InvalidOperationException("防火墙记录所属的进程已变化，不能对当前进程恢复旧补丁。");
        }

        private static bool IsCapturedProcessAvailable(HookState state)
            => state.Memory.IsProcessOpen && state.Memory.ProcessHandle == state.ProcessHandle;

        private static void WriteExact(HookState state, IntPtr address, byte[] bytes)
        {
            EnsureCapturedProcess(state);
            var written = state.Memory.WriteBytes(address, bytes);
            if (written != bytes.Length)
                throw new InvalidOperationException($"防火墙内存写入不完整：应写入 {bytes.Length} 字节，实际写入 {written} 字节。");
        }

        private static void LogSafely(Action log)
        {
            try { log(); }
            catch { /* 日志失败不能改变补丁状态或掩盖原生操作异常。 */ }
        }
    }
}
