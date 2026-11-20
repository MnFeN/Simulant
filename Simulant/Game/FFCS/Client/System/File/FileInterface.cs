using System;
using System.Text;

namespace Simulant.Game.FFCS.Client.System.File
{
    // Client::System::File::FileInterface, FFCS size 0x28.
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/System/File/FileInterface.cs
    internal struct FileInterface : IMemoryObject
    {
        internal const int Size = 0x28;

        [SigPattern("E8 * * * * 33 F6 45 32 F6")]
        internal static IntPtr CtorFuncPtr { get; set; }

        [SigPattern("E8 * * * * 0F B6 C8 3C 01")]
        internal static IntPtr OpenFileFuncPtr { get; set; }

        [SigPattern("40 53 48 83 EC ?? 48 8B D9 48 8B 49 ?? FF 15 ?? ?? ?? ?? 85 C0")]
        internal static IntPtr CloseFuncPtr { get; set; }

        public IntPtr Ptr { get; set; }
        internal bool IsFileOpen => Ptr.Read<byte>(0x20) != 0;

        internal FileInterface Ctor() => CtorFuncPtr.Call<IntPtr>(Ptr).As<FileInterface>();

        internal byte OpenFile(string path, OpenFileMode mode)
            => OpenFileFuncPtr.Call<byte>(Ptr, RemoteInput.CString(path, Encoding.Unicode), mode);

        internal void Close() => CloseFuncPtr.Call(Ptr);
    }

    internal enum OpenFileMode
    {
        Read = 0,
    }
}
