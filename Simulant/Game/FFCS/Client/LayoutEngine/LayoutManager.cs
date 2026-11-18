using Simulant.Game;
using Simulant.Game.FFCS.STD;
using System;

namespace Simulant.Game.FFCS.Client.LayoutEngine
{
    // Client::LayoutEngine::LayoutManager
    //   Client::LayoutEngine::IManagerBase
    //     Client::System::Common::NonCopyable
    // Size = 0xC60
    // https://github.com/aers/FFXIVClientStructs/blob/aad0de66f0a6b49f257d224499dd685ceba567da/FFXIVClientStructs/FFXIV/Client/LayoutEngine/LayoutManager.cs
    public struct LayoutManager : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        public MemoryField<int> InitState => Ptr.Field<int>(0x018); // 7 is fully loaded and ready, <7 are various stages of init
        public MemoryField<uint> TerritoryTypeId => Ptr.Field<uint>(0x020); // TerritoryType row id
        public MemoryField<uint> CfcId => Ptr.Field<uint>(0x024); // ContentFinderCondition row id

        // Maps instance type to the map keyed by (InstanceId << 32) | SubId.
        internal MemoryField<StdMap> InstancesByType => Ptr.Field<StdMap>(0x268);
    }
}
