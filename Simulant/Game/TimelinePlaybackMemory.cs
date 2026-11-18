using System;
using System.Runtime.InteropServices;

namespace Simulant.Game
{
    // 本机客户端 2026.11.03.0000.0000 反汇编补充。
    // RVA 0x706280 把 SchedulerObject 字段的地址传给 0x18CA5D0。
    // RVA 0x19916A0 从该字段取引用对象，再读引用对象首指针，得到实际播放实例。
    [StructLayout(LayoutKind.Explicit)]
    internal struct TimelinePlaybackReference
    {
        [FieldOffset(0x00)] internal IntPtr Instance;
    }

    // 本机客户端 2026.11.03.0000.0000 反汇编补充，仅描述读状态所需的前缀。
    // RVA 0x18CA5D0 先检查 +0x10 的 bit 5，再调用 vtable +0x20 判断结束。
    // 该虚函数（RVA 0x1BFB610）先检查 +0x11 的 bit 0，后续分支可能更新播放状态。
    [StructLayout(LayoutKind.Explicit, Size = 0x12)]
    internal struct TimelinePlaybackState
    {
        [FieldOffset(0x10)] private byte _flags;
        [FieldOffset(0x11)] private byte _completionFlags;

        internal bool IsPlaying => (_flags & 0x20) != 0 && (_completionFlags & 1) == 0;
    }
}
