using System;

namespace Simulant.Game.FFCS.Client.System.Resource.Handle
{
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/System/Resource/Handle/ResourceHandle.cs
    internal struct ResourceHandle : IMemoryObject
    {
        [SigPattern("E8 * * * * 4D 8B B5")]
        public static IntPtr GetAsyncFuncPtr { get; set; }

        [SigPattern("E8 * * * * 48 C7 03 ?? ?? ?? ?? C6 83")]
        public static IntPtr DecRefFuncPtr { get; set; }

        public IntPtr Ptr { get; set; }
        public MemoryField<uint> Id => Ptr.Field<uint>(0x10);
        public MemoryField<byte> ReadState => Ptr.Field<byte>(0xA8);
        public MemoryField<byte> LoadState => Ptr.Field<byte>(0xA9);

        // 上游的 unknown 是原生参数指针。RemoteInput 会把参数复制到游戏进程。
        public static ResourceHandle GetAsync(string path, RemoteInput unknown = null)
            => GetAsyncFuncPtr.Call<IntPtr>(RemoteInput.CString(path), (object)unknown ?? IntPtr.Zero, IntPtr.Zero, 0u)
                .As<ResourceHandle>();

        public bool DecRef() => DecRefFuncPtr.Call<byte>(Ptr, IntPtr.Zero, 0u) != 0;
    }
}
