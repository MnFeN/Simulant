using SimuMagic;
using System;

namespace Simulant.Game
{
    /// <summary>持有单个游戏进程的内存访问会话和原生调用调度器。</summary>
    public sealed partial class GameConnection : IDisposable
    {
        private readonly NativeCallScheduler _scheduler;

        public ExternalProcessMemory Memory { get; }

        internal GameConnection(ExternalProcessMemory memory)
        {
            Memory = memory;
            _scheduler = new NativeCallScheduler(memory, frameLeaseMs: 3);
        }

        /// <summary>同步调用游戏原生函数并丢弃其返回值。</summary>
        public void Call(IntPtr address, params object[] args) => CallCore<IntPtr>(address, args, false);

        /// <summary>同步调用游戏原生函数，并以 <typeparamref name="T"/> 类型返回结果。</summary>
        public T Call<T>(IntPtr address, params object[] args) where T : struct => CallCore<T>(address, args, true);

        private T CallCore<T>(IntPtr address, object[] args, bool hasReturnValue) where T : struct
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));

            var normalized = new object[args.Length];
            var bufferSize = 0;
            for (var index = 0; index < args.Length; index++)
            {
                var argument = args[index];
                if (argument is RemoteInput input)
                {
                    bufferSize = checked(AlignArgumentOffset(bufferSize) + input.Bytes.Length);
                    normalized[index] = input;
                    continue;
                }

                var scalar = NormalizeArgument(argument);
#if SIMULANT_NATIVE_TESTS
                if (_calls != null)
                    scalar = argument;
#endif
                normalized[index] = scalar;
            }

            var buffer = IntPtr.Zero;
            try
            {
                if (bufferSize != 0)
                {
                    buffer = Memory.AllocateMemory(bufferSize);
                    if (buffer == IntPtr.Zero)
                        throw new InvalidOperationException("无法分配原生调用参数的内存。");

                    // 同一次调用的 RemoteInput 共用一块远程内存，每份数据的起始地址按 16 字节对齐。
                    var offset = 0;
                    for (var index = 0; index < normalized.Length; index++)
                    {
                        if (!(normalized[index] is RemoteInput input))
                            continue;

                        offset = AlignArgumentOffset(offset);
                        var pointer = buffer + offset;
                        if (Memory.WriteBytes(pointer, input.Bytes) != input.Bytes.Length)
                            throw new InvalidOperationException($"未能完整写入第 {index + 1} 个原生调用参数。");

                        normalized[index] = pointer;
                        offset += input.Bytes.Length;
                    }
                }

#if SIMULANT_NATIVE_TESTS
                if (_calls != null)
                {
                    if (hasReturnValue)
                        return _calls.Call<T>(address, normalized);

                    _calls.Call(address, normalized);
                    return default;
                }
#endif
                T result = default;
                _scheduler.Execute(() => result = Memory.CallInjected64<T>(address, normalized));
                return result;
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                    Memory.FreeMemory(buffer);
            }
        }

        private static int AlignArgumentOffset(int offset) => checked(offset + 15) & ~15;

        private static object NormalizeArgument(object value)
        {
            if (value == null)
                throw new ArgumentException("原生调用参数不能为 null。", nameof(value));

            var type = value.GetType();
            if (type.IsEnum)
                value = Convert.ChangeType(value, Enum.GetUnderlyingType(type));

            switch (value)
            {
                case bool flag: return (byte)(flag ? 1 : 0);
                case sbyte signedByte: return unchecked((byte)signedByte);
                case short signedShort: return unchecked((ushort)signedShort);
                case char character: return (ushort)character;
                case int signedInt: return unchecked((uint)signedInt);
                case long signedLong: return unchecked((ulong)signedLong);
                case UIntPtr unsignedPointer: return unsignedPointer.ToUInt64();
                case byte _:
                case ushort _:
                case uint _:
                case ulong _:
                case IntPtr _:
                case float _:
                case double _: return value;
                default:
                    throw new ArgumentException($"不支持直接传递原生调用参数 {type.Name}。字符串和结构体可用 RemoteInput 包装。", nameof(value));
            }
        }

        public void Dispose()
        {
            // 先让调用队列和帧锁收尾，再释放进程内存访问。
            _scheduler?.Dispose();
            Memory.Dispose();
        }
    }
}
