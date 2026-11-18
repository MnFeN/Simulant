using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Simulant.Game
{
    public static class IntPtrExtensions
    {
        public static bool IsZero(this IntPtr ptr) => ptr == IntPtr.Zero;

        public static IntPtr ThrowIfZero(this IntPtr ptr, string callerName, int offset, string opDesc)
        {
            if (ptr == IntPtr.Zero)
                throw new InvalidOperationException($"{callerName} 在 0x{offset:X} 处{opDesc}时指针为空。");

            return ptr;
        }

        public static string Hex(this IntPtr ptr) => $"{(ulong)ptr:X}";

        public static T Read<T>(this IntPtr ptr, int offset = 0,
            [CallerMemberName] string callerName = "")
            where T : struct
        {
            ptr.ThrowIfZero(callerName, offset, "读取");
            return GameInterop.Connection.Memory.Read<T>(ptr + offset);
        }

        public static IntPtr ReadPtr(this IntPtr ptr, int offset = 0,
            [CallerMemberName] string callerName = "")
        {
            ptr.ThrowIfZero(callerName, offset, "读取 IntPtr ");
            return GameInterop.Connection.Memory.Read<IntPtr>(ptr + offset);
        }

        public static byte[] ReadBytes(this IntPtr ptr, int count, int offset = 0,
            [CallerMemberName] string callerName = "")
        {
            ptr.ThrowIfZero(callerName, offset, "读取字节数组");
            return GameInterop.Connection.Memory.ReadBytes(ptr + offset, count);
        }

        public static MemoryField<T> Field<T>(this IntPtr ptr, int offset)
            where T : struct
            => new MemoryField<T>(ptr, offset);

        public static MemoryArray<T> ArrayField<T>(this IntPtr ptr, int offset, int length)
            where T : unmanaged
            => new MemoryArray<T>(ptr, offset, length);

        public static MemoryBitField BitField(this IntPtr ptr, int offset, int bit)
            => new MemoryBitField(ptr, offset, bit);

        public static TMemoryObject As<TMemoryObject>(this IntPtr ptr, int offset = 0,
            [CallerMemberName] string callerName = "")
            where TMemoryObject : struct, IMemoryObject
        {
            // when the base pointer is null, always return a TMemoryObject which Ptr is 0.
            if (ptr.IsZero())
                return new TMemoryObject();

            return new TMemoryObject
            {
                Ptr = ptr + offset
            };
        }

        public static void Write<T>(this IntPtr ptr, T value, int offset = 0,
            [CallerMemberName] string callerName = "")
            where T : struct
        {
            ptr.ThrowIfZero(callerName, offset, "写入");
            GameInterop.Connection.Memory.Write<T>(ptr + offset, value);
        }

        public static void WriteBytes(this IntPtr ptr, byte[] data, int offset = 0,
            [CallerMemberName] string callerName = "")
        {
            ptr.ThrowIfZero(callerName, offset, "写入字节数组");
            GameInterop.Connection.Memory.WriteBytes(ptr + offset, data);
        }

        /// <summary>调用此地址处的游戏函数并等待执行完成。</summary>
        public static void Call(this IntPtr ptr, params object[] args)
        {
            ptr.ThrowIfZero("Call", 0, "调用函数");

            GameInterop.Connection.Call(ptr, args);
        }

        /// <summary>调用此地址处的游戏函数，并以 <typeparamref name="T"/> 类型返回结果。</summary>
        public static T Call<T>(this IntPtr ptr, params object[] args)
            where T : struct
        {
            ptr.ThrowIfZero("Call", 0, "调用函数");

            return GameInterop.Connection.Call<T>(ptr, args);
        }

        public static IntPtr GetVFuncPtr(this IntPtr objectPtr, int idx, [CallerMemberName] string callerName = "")
        {
            objectPtr.ThrowIfZero(callerName, 0, "获取虚函数指针");
            var vTablePtr = objectPtr.Read<IntPtr>().ThrowIfZero(callerName, 0, "读取 vTable 指针");
            return vTablePtr.As<VTable>()[idx];
        }

        public static void CallVFunc(this IntPtr objectPtr, int idx, params object[] args)
        {
            var vFuncPtr = GetVFuncPtr(objectPtr, idx);
            vFuncPtr.Call(new object[]
            {
                objectPtr
            }.Concat(args).ToArray());
        }

        public static T CallVFunc<T>(this IntPtr objectPtr, int idx, params object[] args)
            where T : struct
        {
            var vFuncPtr = GetVFuncPtr(objectPtr, idx);
            return vFuncPtr.Call<T>(new object[]
            {
                objectPtr
            }.Concat(args).ToArray());
        }
    }
}
