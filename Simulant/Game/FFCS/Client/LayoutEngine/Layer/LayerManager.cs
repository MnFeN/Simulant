using System;

namespace Simulant.Game.FFCS.Client.LayoutEngine.Layer
{
    // Client::LayoutEngine::Layer::LayerManager
    //   Client::LayoutEngine::IManagerBase
    //     Client::System::Common::NonCopyable
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/LayoutEngine/Layer/LayerManager.cs
    /// <summary>
    /// Layer is a flat list of instances that are always loaded together.
    /// </summary>
    internal struct LayerManager : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        internal IManagerBase ManagerBase => Ptr.As<IManagerBase>();
    }
}
