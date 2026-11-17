using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Simulant.Game
{
    /// <summary>在本地保存原生调用的输入字节。调用时将这些字节复制到游戏进程的临时内存，并把地址传给原生函数。</summary>
    public sealed class RemoteInput
    {
        internal byte[] Bytes { get; }

        private RemoteInput(byte[] bytes) => Bytes = bytes;

        /// <summary>创建以空字符结尾的输入字符串，默认使用 UTF-8 编码。</summary>
        public static RemoteInput CString(string text, Encoding encoding = null)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            return new RemoteInput((encoding ?? Encoding.UTF8).GetBytes(text + '\0'));
        }

        /// <summary>创建仅用于输入的非托管值副本。指针字段保留原地址。</summary>
        public static unsafe RemoteInput Struct<T>(in T value) where T : unmanaged
        {
            var bytes = new byte[sizeof(T)];
            fixed (T* address = &value)
                Marshal.Copy((IntPtr)address, bytes, 0, bytes.Length);
            return new RemoteInput(bytes);
        }
    }
}
