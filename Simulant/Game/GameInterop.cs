using SimuMagic;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Simulant.Game
{
    /// <summary>管理游戏进程连接，目标进程由宿主提供。</summary>
    public static class GameInterop
    {
        public static GameConnection Connection { get; private set; }
        public static bool IsReady
        {
            get
            {
                var memory = Connection?.Memory;
                return memory?.IsProcessOpen == true && memory.Process?.HasExited == false;
            }
        }

        internal static bool IsConnectedTo(Process process)
        {
            var memory = Connection?.Memory;
            var connectedProcess = memory?.Process;
            return process != null && connectedProcess?.Id == process.Id &&
                memory.IsProcessOpen && !connectedProcess.HasExited;
        }

        /// <summary>连接指定的游戏进程。新建连接时返回 true，复用已有连接时返回 false。</summary>
        internal static bool Connect(Process process, Action<string> logWarning)
        {
            if (IsConnectedTo(process))
                return false;

            Disconnect();
            var scanner = GameImageScanner.Open(process);
            var entry = scanner.FindFrameEntry(out var original);
            if (RestoreFrameEntry(process, entry, original))
                logWarning("检测到疑似 ACT 异常退出留下的内存跳转，已自动恢复框架 UI 更新入口的原始指令。");

            // SimuMagic 使用已校验的 Framework UI 更新回调作为原生调用执行入口。
            var native = new ExternalProcessMemory(process, true, false, entry, false, GameImageScanner.FrameEntryCopyBytes, true);
            try
            {
                Connection = new GameConnection(native, scanner);
                return true;
            }
            catch
            {
                native.Dispose();
                throw;
            }
        }

        /// <summary>校验运行中的入口字节，并恢复符合 SimuMagic 入口跳转格式的内存残留。</summary>
        private static bool RestoreFrameEntry(Process process, IntPtr entry, byte[] original)
        {
            const int copyBytes = GameImageScanner.FrameEntryCopyBytes;
            var actual = ReadFrameEntry(process.Handle, entry, original.Length);
            if (actual.SequenceEqual(original))
                return false;

            if (!actual.Skip(copyBytes).SequenceEqual(original.Skip(copyBytes)))
                throw new InvalidOperationException("框架 UI 更新入口后 5 字节与磁盘不一致。");
            // SimuMagic 在入口写入 5 字节的 E9 rel32 跳转，后续字节仍应与 EXE 相同。
            if (actual[0] != 0xE9)
                throw new InvalidOperationException("框架 UI 更新入口前 5 字节已变更，未识别到可恢复的入口跳转。");

            using (var memory = new ExternalProcessMemory(process, false, false, IntPtr.Zero, false, 0, false))
            {
                var handle = memory.ProcessHandle;
                if (!ReadFrameEntry(handle, entry, original.Length).SequenceEqual(actual))
                    throw new InvalidOperationException("框架 UI 更新入口在恢复前发生变化，请重新连接。");

                var instruction = original.Take(copyBytes).ToArray();
                if (memory.WriteBytes(entry, instruction) != copyBytes ||
                    !FlushInstructionCache(handle, entry, new UIntPtr(copyBytes)) ||
                    !ReadFrameEntry(handle, entry, original.Length).SequenceEqual(original))
                    throw new InvalidOperationException("框架 UI 更新入口恢复失败。");
            }

            return true;
        }

        private static byte[] ReadFrameEntry(IntPtr process, IntPtr entry, int length)
        {
            var bytes = new byte[length];
            if (!ReadProcessMemory(process, entry, bytes, new UIntPtr((uint)length), out var read) ||
                read.ToUInt64() != (uint)length)
                throw new InvalidOperationException("框架 UI 更新入口不可读。");

            return bytes;
        }

        internal static void Disconnect()
        {
            Connection?.Dispose();
            Connection = null;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] buffer, UIntPtr size, out UIntPtr read);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
    }
}
