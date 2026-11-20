using System;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.System.File
{
    // Client::System::File::FileDescriptor，只声明加载散装资源用到的字段。
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/System/File/FileDescriptor.cs
    [StructLayout(LayoutKind.Explicit, Size = Size)]
    internal unsafe struct FileDescriptor
    {
        internal const int Size = 0x278;
        private const int PathCapacity = 260;

        [FieldOffset(0x00)] internal FileMode FileMode;
        [FieldOffset(0x30)] internal IntPtr FileInterface;
        [FieldOffset(0x50)] internal IntPtr ResourceHandle;
        [FieldOffset(0x70)] private fixed char _filePath[PathCapacity];

        // 项目辅助方法。其余字段置零，读取范围为整个文件。
        internal static FileDescriptor ForUnpackedFile(IntPtr file, IntPtr resource, string path)
        {
            if (path.Length >= PathCapacity)
            {
                throw new ArgumentException("测试文件路径超过客户端的长度限制。", nameof(path));
            }

            var descriptor = new FileDescriptor
            {
                FileMode = FileMode.LoadUnpackedResource,
                FileInterface = file,
                ResourceHandle = resource,
            };
            for (var index = 0; index < path.Length; index++)
            {
                descriptor._filePath[index] = path[index];
            }

            return descriptor;
        }
    }

    internal enum FileMode : byte
    {
        LoadUnpackedResource = 0,
    }
}
