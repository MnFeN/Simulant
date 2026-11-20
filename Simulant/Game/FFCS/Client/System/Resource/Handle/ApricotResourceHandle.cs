using Simulant.Game.FFCS.Client.Graphics.Vfx;
using System;

namespace Simulant.Game.FFCS.Client.System.Resource.Handle
{
    // Inherits<DefaultResourceHandle>
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/System/Resource/Handle/ApricotResourceHandle.cs
    internal struct ApricotResourceHandle : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        public VfxResourceObject VfxResourceObject => Ptr.ReadPtr(0xC0).As<VfxResourceObject>();
    }
}
