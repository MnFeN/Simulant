using System;

namespace Simulant.Game.FFCS.Component.GUI
{
    // FFXIVClientStructs/FFXIV/Component/GUI/AtkModule.cs
    public struct AtkModule : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        public AtkUnitManager AtkUnitManager => Ptr.ReadPtr(0x2C0).As<AtkUnitManager>();
    }
}
