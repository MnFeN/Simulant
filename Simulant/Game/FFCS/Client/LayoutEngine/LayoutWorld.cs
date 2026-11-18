using Simulant.Game;
using System;

namespace Simulant.Game.FFCS.Client.LayoutEngine
{
    // Client::LayoutEngine::LayoutWorld
    //   Client::LayoutEngine::IManagerBase
    //     Client::System::Common::NonCopyable
    // Size = 0x240
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/LayoutEngine/LayoutWorld.cs
    public struct LayoutWorld : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        [SigPattern("48 8B D1 48 8B 0D * * * * 48 85 C9 74 0A")] // isPointer: true
        public static IntPtr InstancePtrPtr { get; set; }

        public static LayoutWorld Instance()
            => InstancePtrPtr.ReadPtr().As<LayoutWorld>();

        public LayoutManager GlobalLayout => Ptr.ReadPtr(0x18).As<LayoutManager>();
        public LayoutManager ActiveLayout => Ptr.ReadPtr(0x20).As<LayoutManager>();
    }
}
