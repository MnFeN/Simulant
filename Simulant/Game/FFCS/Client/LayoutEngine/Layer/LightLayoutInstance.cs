using Simulant.Game.FFCS.Client.Graphics.Scene;
using System;

namespace Simulant.Game.FFCS.Client.LayoutEngine.Layer
{
    // Client::LayoutEngine::Layer::LightLayoutInstance, FFCS size 0x50.
    //   Client::LayoutEngine::ILayoutInstance
    //     Client::System::Common::NonCopyable
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/LayoutEngine/Layer/LightLayoutInstance.cs
    internal struct LightLayoutInstance : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        internal Light GraphicsObject => Ptr.ReadPtr(0x30).As<Light>();
    }
}
