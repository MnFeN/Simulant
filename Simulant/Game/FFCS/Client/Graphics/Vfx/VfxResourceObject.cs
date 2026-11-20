using Simulant.Game.FFCS.Client.System.Resource.Handle;
using System;

namespace Simulant.Game.FFCS.Client.Graphics.Vfx
{
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Graphics/Vfx/VfxResourceObject.cs
    internal struct VfxResourceObject : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        public ApricotResourceHandle ApricotResourceHandle => Ptr.ReadPtr(0x18).As<ApricotResourceHandle>();
    }
}
