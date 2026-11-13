using Simulant.ACT;
using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Object;
using System;

namespace Simulant.Core.Entity
{
    public sealed partial class Character
    {
        internal byte[] ReadNameBytes()
        {
            var bytes = Native.GetName().ReadBytes(64);
            if (bytes == null || bytes.Length != 64 || Array.IndexOf(bytes, (byte)0) < 0)
                throw new InvalidOperationException("玩家名字读取不完整，不能保存或恢复临时名字。");
            return bytes;
        }

        internal void SetNameBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 64 || Array.IndexOf(bytes, (byte)0) < 0)
                throw new ArgumentException("玩家名字必须是以零结尾的 64 字节缓冲区。", nameof(bytes));
            if (GameObject.SetNameFuncPtr == IntPtr.Zero)
                throw new InvalidOperationException("玩家改名函数尚未定位，请重新初始化。");
            var memory = NamazuInterop.Plugin.Memory;
            var buffer = IntPtr.Zero;
            try
            {
                buffer = memory.AllocateMemory(64);
                if (buffer == IntPtr.Zero) throw new InvalidOperationException("无法分配玩家名字的远程参数内存。");
                if (memory.WriteBytes(buffer, bytes) != 64)
                    throw new InvalidOperationException("玩家名字的远程参数写入不完整。");
                // 写入前重新核对实体身份；只将远程地址传给原生函数。
                Native.Ptr.As<GameObject>().SetName(buffer);
            }
            finally
            {
                if (buffer != IntPtr.Zero) memory.FreeMemory(buffer);
            }
        }
    }
}
