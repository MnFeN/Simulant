using Simulant.Game;
using System;

namespace Simulant.Game.FFCS.Client.LayoutEngine
{
    // Client::LayoutEngine::LayoutManager
    //   Client::LayoutEngine::IManagerBase
    //     Client::System::Common::NonCopyable
    // Size = 0xC60
    public struct LayoutManager : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        public MemoryField<int> InitState => Ptr.Field<int>(0x018); // 7 is fully loaded and ready, <7 are various stages of init
        public MemoryField<uint> TerritoryTypeId => Ptr.Field<uint>(0x020); // TerritoryType row id
        public MemoryField<uint> CfcId => Ptr.Field<uint>(0x024); // ContentFinderCondition row id
    }
}
