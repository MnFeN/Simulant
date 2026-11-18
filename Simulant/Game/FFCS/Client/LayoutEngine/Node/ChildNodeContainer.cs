using Simulant.Game.FFCS.STD;
using System;

namespace Simulant.Game.FFCS.Client.LayoutEngine.Node
{
    // Client::LayoutEngine::Node::ChildNodeContainer
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/LayoutEngine/Node/ChildNodeContainer.cs
    internal struct ChildNodeContainer : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // StdVector<Pointer<ChildNodeInstance>>
        internal MemoryField<StdVector> Instances => Ptr.Field<StdVector>(0x10);
    }
}
