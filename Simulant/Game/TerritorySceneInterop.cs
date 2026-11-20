using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Simulant.Game
{
    internal sealed class TerritorySceneInterop : IDisposable
    {
        private const uint BgOffsetMask = 0xFFFFFF;
        private const uint MemReserveAndCommit = 0x3000;
        private const uint MemRelease = 0x8000;
        private const uint PageReadWrite = 0x04;
        private const int ErrorInvalidAddress = 487;

        // FFCS 未定义。从 GameMain.LoadZone 调用处取得客户端的 TerritoryType 行。
        [SigPattern("48 83 EC 28 48 8B 05 ?? ?? ?? ?? 44 8B C1 BA 5E 00 00 00 48 8B 88 40 2B 00 00 E8 ?? ?? ?? ?? 48 85 C0")]
        private static IntPtr GetTerritoryRowFuncPtr { get; set; }

        private readonly GameConnection _connection;
        private readonly uint _territoryId;
        private readonly IntPtr _field;
        private readonly uint _originalOffset;
        private readonly uint _sceneOffset;
        private IntPtr _storage;

        private TerritorySceneInterop(GameConnection connection, uint territoryId, IntPtr field, uint originalOffset, uint sceneOffset, IntPtr storage)
        {
            _connection = connection;
            _territoryId = territoryId;
            _field = field;
            _originalOffset = originalOffset;
            _sceneOffset = sceneOffset;
            _storage = storage;
        }

        internal static TerritorySceneInterop SetScenePath(GameConnection connection, uint territoryId, string sceneBg)
        {
            var path = Encoding.UTF8.GetBytes(sceneBg + '\0');
            var memory = connection.Memory;
            TerritorySceneInterop scene = null;
            connection.RunInFrame(() =>
            {
                var field = GetBgField(territoryId);
                if (field == IntPtr.Zero)
                    throw new InvalidOperationException($"客户端中找不到区域 {territoryId}。");

                var offset = memory.Read<uint>(field);
                if (memory.Read<byte>(field + (int)(offset & BgOffsetMask)) != 0)
                    throw new InvalidOperationException($"区域 {territoryId} 已有场景路径。");

                var storage = AllocatePathAfter(memory.ProcessHandle, field, path.Length);
                var relative = checked((uint)(storage.ToInt64() - field.ToInt64()));
                var sceneOffset = (offset & ~BgOffsetMask) | relative;
                try
                {
                    if (memory.WriteBytes(storage, path) != path.Length)
                        throw new InvalidOperationException("未完整写入绿幕场景路径。");

                    memory.Write(field, sceneOffset);
                }
                catch
                {
                    memory.Write(field, offset);
                    ReleaseScenePath(memory.ProcessHandle, storage);
                    throw;
                }

                scene = new TerritorySceneInterop(connection, territoryId, field, offset, sceneOffset, storage);
            });
            return scene;
        }

        public void Dispose()
        {
            if (_storage == IntPtr.Zero || GameInterop.Connection != _connection || !GameInterop.IsReady)
                return;

            var memory = _connection.Memory;
            _connection.RunInFrame(() =>
            {
                if (GetBgField(_territoryId) == _field && memory.Read<uint>(_field) == _sceneOffset)
                    memory.Write(_field, _originalOffset);

                ReleaseScenePath(memory.ProcessHandle, _storage);
                _storage = IntPtr.Zero;
            });
        }

        private static void ReleaseScenePath(IntPtr process, IntPtr storage)
        {
            if (!VirtualFreeEx(process, storage, UIntPtr.Zero, MemRelease))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "无法释放绿幕场景路径内存。");
            }
        }

        private static IntPtr GetBgField(uint territoryId)
        {
            GameFunctions.CheckStatus(GetTerritoryRowFuncPtr);
            var row = GetTerritoryRowFuncPtr.Call<IntPtr>(territoryId);
            // TerritoryType.Bg 不在 FFCS 中，客户端读取相对于字段自身的 24 位无符号偏移。
            return row == IntPtr.Zero ? IntPtr.Zero : row + 4;
        }

        private static IntPtr AllocatePathAfter(IntPtr process, IntPtr field, int size)
        {
            GetSystemInfo(out var system);
            var granularity = system.AllocationGranularity;
            var first = (field.ToInt64() / granularity + 1) * granularity;
            var last = Math.Min(field.ToInt64() + BgOffsetMask, system.MaximumApplicationAddress.ToInt64() - size + 1);
            for (var address = first; address <= last; address += granularity)
            {
                var result = VirtualAllocEx(process, new IntPtr(address), (UIntPtr)(uint)size, MemReserveAndCommit, PageReadWrite);
                if (result != IntPtr.Zero)
                    return result;

                var error = Marshal.GetLastWin32Error();
                if (error != ErrorInvalidAddress)
                    throw new Win32Exception(error, "无法分配绿幕场景路径内存。");
            }

            throw new InvalidOperationException("Bg 字段的 24 位偏移范围内没有可用的场景路径内存。");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SystemInfo
        {
            public uint ProcessorArchitectureAndReserved;
            public uint PageSize;
            public IntPtr MinimumApplicationAddress;
            public IntPtr MaximumApplicationAddress;
            public UIntPtr ActiveProcessorMask;
            public uint NumberOfProcessors;
            public uint ProcessorType;
            public uint AllocationGranularity;
            public ushort ProcessorLevel;
            public ushort ProcessorRevision;
        }

        [DllImport("kernel32.dll")]
        private static extern void GetSystemInfo(out SystemInfo info);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, UIntPtr size, uint allocationType, uint protection);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VirtualFreeEx(IntPtr process, IntPtr address, UIntPtr size, uint freeType);
    }
}
