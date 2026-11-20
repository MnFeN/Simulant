using System;

namespace Simulant.Game.FFCS.Client.System.Resource.Handle
{
    // Client::System::Resource::Handle::ModelResourceHandle, FFCS size 0x2A0.
    //   Client::System::Resource::Handle::ResourceHandle
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/System/Resource/Handle/ModelResourceHandle.cs
    internal struct ModelResourceHandle : IMemoryObject
    {
        // FFCS 未声明。读取单独的 MDL 文件。第二参数是 FileDescriptor，第三参数表示打开失败。
        [SigPattern("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 8D 6C 24 ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 ?? 48 8B 72 ?? 4C 8B EA")]
        internal static IntPtr ReadLocalFileFuncPtr { get; set; }

        public IntPtr Ptr { get; set; }

        internal byte ReadLocalFile(IntPtr descriptor)
            => ReadLocalFileFuncPtr.Call<byte>(Ptr, descriptor, false);
    }
}
