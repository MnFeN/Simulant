using System;
using System.Diagnostics;
using System.Text;

namespace Simulant.ACT
{
    /// <summary>
    /// Wrapper for GreyMagic.MemoryBase
    /// </summary>
    public class GreyMagicMemoryBase
    {
        private readonly dynamic _memory;

        public GreyMagicMemoryBase(object memory)
        {
            _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        }

        public Process Process => _memory.Process;
        public IntPtr ProcessHandle => _memory.ProcessHandle;
        public bool IsProcessOpen => _memory.IsProcessOpen;
        public IntPtr ImageBase => _memory.ImageBase;

        public byte[] ReadBytes<T>(IntPtr addr)
            where T : struct
            => _memory.ReadBytes<T>(addr);

        public byte[] ReadBytes(IntPtr addr, int count, bool isRelative)
            => _memory.ReadBytes(addr, count, isRelative);

        public byte[] ReadBytes(IntPtr addr, int count)
            => _memory.ReadBytes(addr, count);

        public T Read<T>(bool isRelative, params IntPtr[] addrs)
            where T : struct
            => _memory.Read<T>(isRelative, addrs);

        public T Read<T>(IntPtr addr, bool isRelative)
            where T : struct
            => _memory.Read<T>(addr, isRelative);

        public T Read<T>(IntPtr addr)
            where T : struct
            => _memory.Read<T>(addr);

        public T[] ReadArray<T>(IntPtr addr, int count, bool isRelative)
            where T : struct
            => _memory.ReadArray<T>(addr, count, isRelative);

        public T[] ReadArray<T>(IntPtr addr, int count)
            where T : struct
            => _memory.ReadArray<T>(addr, count);

        public string ReadString(IntPtr address, Encoding encoding)
            => _memory.ReadString(address, encoding);

        public string ReadString(IntPtr address, Encoding encoding, int maxLength)
            => _memory.ReadString(address, encoding, maxLength);

        public string ReadString(IntPtr address, Encoding encoding, int maxLength, bool isRelative)
            => _memory.ReadString(address, encoding, maxLength, isRelative);

        public string ReadStringUTF8(IntPtr address)
            => _memory.ReadStringUTF8(address);

        public int WriteBytes<T>(IntPtr addr, byte[] bytes, bool isRelative)
            => _memory.WriteBytes<T>(addr, bytes, isRelative);

        public int WriteBytes(IntPtr addr, byte[] bytes)
            => _memory.WriteBytes(addr, bytes);

        public void Write<T>(IntPtr addr, T value, bool isRelative)
            where T : struct
            => _memory.Write<T>(addr, value, isRelative);

        public void Write<T>(IntPtr addr, T value)
            where T : struct
            => _memory.Write<T>(addr, value);

        public bool WriteString(IntPtr addr, string value, Encoding encoding)
            => _memory.WriteString(addr, value, encoding);

        public IntPtr AllocateMemory(int size)
            => _memory.AllocateMemory(size);

        public IntPtr AllocateMemory(int size, uint allocationType, uint protect)
            => _memory.AllocateMemory(size, allocationType, protect);

        public bool FreeMemory(IntPtr memory, int size, uint freeType)
            => _memory.FreeMemory(memory, size, freeType);

        public bool FreeMemory(IntPtr memory)
            => _memory.FreeMemory(memory);

        public IntPtr GetProcAddress(string module, string function)
            => _memory.GetProcAddress(module, function);

        public IntPtr GetVFTableEntry(IntPtr address, int index)
            => _memory.GetVFTableEntry(address, index);

        public IntPtr GetAbsolute(IntPtr relative)
            => _memory.GetAbsolute(relative);

        public IntPtr GetRelative(IntPtr absolute)
            => _memory.GetRelative(absolute);

        public GreyMagicFrameLock AcquireFrame(bool hardLock)
            => new GreyMagicFrameLock(_memory.AcquireFrame(hardLock));

        public GreyMagicFrameLock AcquireFrame()
            => new GreyMagicFrameLock(_memory.AcquireFrame());

        public GreyMagicFrameLockRelease ReleaseFrame(bool reacquireAsHardLock)
            => new GreyMagicFrameLockRelease(_memory.ReleaseFrame(reacquireAsHardLock));

        public GreyMagicFrameLockRelease ReleaseFrame()
            => new GreyMagicFrameLockRelease(_memory.ReleaseFrame());
    }
}
