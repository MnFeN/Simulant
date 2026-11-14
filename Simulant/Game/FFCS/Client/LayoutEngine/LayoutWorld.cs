using Simulant.Game;
using System;

namespace Simulant.Game.FFCS.Client.LayoutEngine
{
    // Client::LayoutEngine::LayoutWorld
    //   Client::LayoutEngine::IManagerBase
    //     Client::System::Common::NonCopyable
    // Size = 0x240
    public struct LayoutWorld : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        [SigPattern("48 8B D1 48 8B 0D * * * * 48 85 C9 74 0A")] // isPointer: true
        public static IntPtr InstancePtrPtr { get; set; }

        public static LayoutWorld Instance()
            => InstancePtrPtr.ReadPtr().As<LayoutWorld>();

        public LayoutManager ActiveLayout
            => Ptr.ReadPtr(0x020).As<LayoutManager>();
    }
}
