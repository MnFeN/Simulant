using System;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.System.Resource.Handle
{
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/System/Resource/Handle/ResourceHandle.cs
    internal struct ResourceHandle : IMemoryObject
    {
        [SigPattern("E8 * * * * 4D 8B B5")]
        public static IntPtr GetAsyncFuncPtr { get; set; }

        [SigPattern("E8 * * * * 48 C7 03 ?? ?? ?? ?? C6 83")]
        public static IntPtr DecRefFuncPtr { get; set; }

        [SigPattern("E8 * * * * 80 3F 0B 75 28")]
        internal static IntPtr FinishLoadFuncPtr { get; set; }

        // FFCS 未声明。国服资源加载流程用此入口设置读取状态，再调用 ResourceHandle.Read 虚函数。
        [SigPattern("48 89 5C 24 18 56 57 41 57 48 83 EC 20 33 F6 45 0F B6 F8 8B C6 48 8B FA 48 8B D9 F0 0F C0 81 A8 00 00 00")]
        internal static IntPtr ReadUnpackedFuncPtr { get; set; }

        // FFCS 未声明。国服 ResourceHandle 同步读取流程在 FinishLoad 后调用的入口。
        [SigPattern("48 89 6C 24 18 48 89 74 24 20 57 48 83 EC 20 33 ED 48 8B F9 8B C5 F0 0F C0 81 AA 00 00 00")]
        internal static IntPtr CompleteSyncLoadFuncPtr { get; set; }

        public IntPtr Ptr { get; set; }
        public MemoryField<uint> Id => Ptr.Field<uint>(0x10);
        public MemoryField<uint> FileSize => Ptr.Field<uint>(0x28);
        public MemoryField<byte> ReadState => Ptr.Field<byte>(0xA8);
        public MemoryField<byte> LoadState => Ptr.Field<byte>(0xA9);

        // FFCS 未声明。ReadResult 由 FinishLoad 设置，0x80 为跳过自动读取标志。
        private const byte SkipSqPackReadFlag = 0x80;
        internal MemoryField<byte> ReadResult => Ptr.Field<byte>(0x69);
        private MemoryField<byte> ReadFlags => Ptr.Field<byte>(0x6A);
        internal bool SkipsSqPackRead => (ReadFlags.Get() & SkipSqPackReadFlag) != 0;

        internal void ClearSkipSqPackRead()
            => ReadFlags.Set((byte)(ReadFlags.Get() & ~SkipSqPackReadFlag));

        // FFCS 将 GetAsync 的这个参数声明为 nint。这里只映射国服加载流程已确认用到的字段。
        [StructLayout(LayoutKind.Explicit, Size = 0x34)]
        internal struct GetResourceParameters
        {
            [FieldOffset(0x18)] internal byte Priority;
            [FieldOffset(0x19)] internal byte ListenerIndex;
            [FieldOffset(0x26)] internal byte SkipSqPackRead;
        }

        internal bool FinishLoad(IntPtr fileDescriptor, byte openFileResult, byte a4)
            => FinishLoadFuncPtr.Call<bool>(Ptr, fileDescriptor, openFileResult, a4);

        internal byte ReadUnpacked(IntPtr fileDescriptor, bool failedToOpen)
            => ReadUnpackedFuncPtr.Call<byte>(Ptr, fileDescriptor, failedToOpen);

        internal void CompleteSyncLoad(bool a2) => CompleteSyncLoadFuncPtr.Call(Ptr, a2);

        // 上游的 unknown 是原生参数指针。RemoteInput 会把参数复制到游戏进程。
        public static ResourceHandle GetAsync(string path, RemoteInput unknown = null)
            => GetAsyncFuncPtr.Call<IntPtr>(RemoteInput.CString(path), (object)unknown ?? IntPtr.Zero, IntPtr.Zero, 0u)
                .As<ResourceHandle>();

        public bool DecRef() => DecRefFuncPtr.Call<byte>(Ptr, IntPtr.Zero, 0u) != 0;
    }
}
