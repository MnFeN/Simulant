using System;

namespace Simulant.Game.FFCS.Client.LayoutEngine
{
    // Client::LayoutEngine::IManagerBase, FFCS size 0x18.
    //   Client::System::Common::NonCopyable
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/LayoutEngine/IManagerBase.cs
    internal struct IManagerBase : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        internal MemoryField<uint> Id => Ptr.Field<uint>(0x10);
    }
}
