using System;

namespace Simulant.Game
{
    public struct VTable : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        public IntPtr this[int idx]
        {
            get
            {
                if (idx < 0)
                    throw new ArgumentOutOfRangeException(nameof(idx));

                return Ptr.Read<IntPtr>(idx * IntPtr.Size);
            }
        }
    }
}
