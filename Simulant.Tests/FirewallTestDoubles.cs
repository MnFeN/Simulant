using System;
using System.Collections.Generic;
using Simulant.Game.FFCS.Client.System.Framework;

// 保留真实解析插件的私有字段结构，验证反射修改与原实例恢复。
namespace FFXIV_ACT_Plugin
{
    public sealed class FFXIV_ACT_Plugin
    {
        private readonly TestDataCollection _dataCollection = new TestDataCollection();
        public TestDataRepository DataRepository { get; } = new TestDataRepository();
        internal int NetworkTimeout => _dataCollection.NetworkTimeout;

        private sealed class TestDataCollection
        {
            private readonly TestMonitor _monitor = new TestMonitor();
            internal int NetworkTimeout => _monitor.CurrentTimeout;
        }

        private sealed class TestMonitor
        {
            private int Timeout { get; } = 60;
            internal int CurrentTimeout => Timeout;
        }
    }

    public sealed class TestDataRepository
    {
        public int GetSelectedLanguageID() => 5;
    }
}

// 防火墙测试链接实际服务，仅替换外部进程内存、入口地址解析和日志宿主。
namespace Simulant.ACT
{
    public sealed class GreyMagicExternalProcessMemory
    {
        internal sealed class WriteAttempt
        {
            internal IntPtr Address;
            internal byte[] Bytes;
        }

        internal readonly Dictionary<IntPtr, byte[]> Blocks = new Dictionary<IntPtr, byte[]>();
        internal readonly List<WriteAttempt> Writes = new List<WriteAttempt>();
        internal readonly List<IntPtr> FreeAttempts = new List<IntPtr>();
        internal readonly List<IntPtr> Freed = new List<IntPtr>();
        internal readonly List<IntPtr> Allocated = new List<IntPtr>();
        internal Func<IntPtr, byte[], int?> WriteResult { get; set; }
        internal Func<IntPtr, int, byte[]> ReadResult { get; set; }
        internal Func<IntPtr> AllocateResult { get; set; }
        internal Func<IntPtr, bool> FreeResult { get; set; }
        internal int AllocateCalls;
        internal int ReadCalls;
        private long _nextAllocation = 0x4000;

        public object RawMemory => this;
        public IntPtr ProcessHandle { get; set; } = new IntPtr(1);
        public bool IsProcessOpen { get; set; } = true;

        public byte[] ReadBytes(IntPtr address, int count)
        {
            ReadCalls++;
            if (ReadResult != null) return ReadResult(address, count);
            if (!Blocks.TryGetValue(address, out var data) || data.Length < count)
                throw new InvalidOperationException("测试内存不存在完整的读取区域。");
            var result = new byte[count];
            Array.Copy(data, result, count);
            return result;
        }

        public T Read<T>(IntPtr address) where T : struct
        {
            if (typeof(T) == typeof(ushort)) return (T)(object)BitConverter.ToUInt16(ReadBytes(address, 2), 0);
            throw new InvalidOperationException("测试未声明该内存读取类型。");
        }

        public int WriteBytes(IntPtr address, byte[] bytes)
        {
            Writes.Add(new WriteAttempt { Address = address, Bytes = (byte[])bytes.Clone() });
            var count = WriteResult?.Invoke(address, bytes) ?? bytes.Length;
            if (!Blocks.TryGetValue(address, out var existing) || existing.Length < bytes.Length)
                existing = Blocks[address] = new byte[bytes.Length];
            Array.Copy(bytes, existing, Math.Min(bytes.Length, Math.Max(0, count)));
            return count;
        }

        public IntPtr AllocateMemory(int size)
        {
            AllocateCalls++;
            var address = AllocateResult != null ? AllocateResult() : new IntPtr(_nextAllocation);
            _nextAllocation += 0x1000;
            if (address != IntPtr.Zero)
            {
                Allocated.Add(address);
                Blocks[address] = new byte[size];
            }
            return address;
        }

        public bool FreeMemory(IntPtr address)
        {
            FreeAttempts.Add(address);
            if (FreeResult != null && !FreeResult(address)) return false;
            if (!Blocks.Remove(address)) throw new InvalidOperationException("重复释放或释放了未持有的测试内存。");
            Freed.Add(address);
            return true;
        }
    }

    public sealed class NamazuPlugin
    {
        public GreyMagicExternalProcessMemory Memory { get; set; }
    }

    public static class NamazuInterop
    {
        public static NamazuPlugin Plugin { get; set; }
    }
}

namespace Simulant.Game
{
    internal static class AddressStore
    {
        public static IntPtr OnSendPacketFuncPtr { get; set; }
        public static IntPtr HeartBeatOpcodeLocatePtr { get; set; }
    }

    internal static class FirewallPointerExtensions
    {
        internal static IntPtr GetVFuncPtr(this IntPtr dispatcher, int index)
        {
            if (dispatcher == IntPtr.Zero || index != 1)
                throw new InvalidOperationException("测试收包入口解析参数不正确。");
            return Framework.ReceiveTarget;
        }
    }
}

namespace Simulant.Game.FFCS.Client.System.Framework
{
    public struct Framework
    {
        internal static IntPtr ReceiveTarget;
        internal static Action BeforeResolve { get; set; }
        public static Framework Instance
        {
            get { BeforeResolve?.Invoke(); return new Framework(); }
        }
        public NetworkProxy NetworkModuleProxy => new NetworkProxy();
    }

    public struct NetworkProxy
    {
        public NetworkModule NetworkModule => new NetworkModule();
    }

    public struct NetworkModule
    {
        public PacketReceiver PacketReceiverCallback => new PacketReceiver();
    }

    public struct PacketReceiver
    {
        public Dispatcher PacketDispatcher => new Dispatcher();
    }

    public struct Dispatcher
    {
        public IntPtr Ptr => new IntPtr(0x800);
    }
}
