using System;

namespace Simulant.Game.FFCS.Client.LayoutEngine
{
    // Client::LayoutEngine::ILayoutInstance
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/LayoutEngine/ILayoutInstance.cs
    internal struct ILayoutInstance : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        internal MemoryField<uint> SubId => Ptr.Field<uint>(0x20);
        internal MemoryField<byte> Flags1 => Ptr.Field<byte>(0x29);

        // FFCS BitField<int>(NestingLevel, 4, 3).
        internal static int GetNestingLevel(byte flags1) => (flags1 >> 4) & 7;
    }

    internal enum InstanceType : byte
    {
        SharedGroup = 6,
    }
}
