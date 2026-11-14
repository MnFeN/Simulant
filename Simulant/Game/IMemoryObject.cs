using System;

namespace Simulant.Game
{
    public interface IMemoryObject
    {
        IntPtr Ptr { get; set; }
    }

    public static class MemoryObjectExtensions
    {
        public static bool IsNull<TMemoryObject>(this TMemoryObject obj)
            where TMemoryObject : struct, IMemoryObject
        {
            return obj.Ptr == IntPtr.Zero;
        }

        public static TMemoryObject ThrowIfNull<TMemoryObject>(this TMemoryObject obj, string error = null)
           where TMemoryObject : struct, IMemoryObject
        {
            if (obj.Ptr == IntPtr.Zero)
                throw new InvalidOperationException(error ?? $"MemoryObject {typeof(TMemoryObject).Name} is null.");
            return obj;
        }
    }
}