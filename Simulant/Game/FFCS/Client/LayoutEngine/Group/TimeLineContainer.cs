using Simulant.Game.FFCS.STD;
using System;

namespace Simulant.Game.FFCS.Client.LayoutEngine.Group
{
    // Client::LayoutEngine::Group::TimeLineContainer
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/LayoutEngine/Group/TimeLineContainer.cs
    internal struct TimeLineContainer : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // StdVector<Pointer<TimeLineLayoutInstance>>
        internal MemoryField<StdVector> Instances => Ptr.Field<StdVector>(0x08);
    }
}
