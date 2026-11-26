using System;

namespace Simulant.Game.FFCS.Client.System.Input
{
    // FFXIVClientStructs/FFXIV/Client/System/Input/MouseData.cs
    public struct CursorInputData : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        internal MemoryField<MouseButtonFlags> MouseButtonHeldFlags => Ptr.Field<MouseButtonFlags>(0x0C);
    }

    [Flags]
    internal enum MouseButtonFlags
    {
        None = 0,
        LBUTTON = 1 << 0,
    }
}
