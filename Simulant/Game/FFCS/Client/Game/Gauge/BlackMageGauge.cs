using System;

namespace Simulant.Game.FFCS.Client.Game.Gauge
{
    // 原生 Client::Game::Gauge::BlackMageGauge，继承 JobGauge，大小 0x30。
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Game/Gauge/JobGauges.cs
    internal struct BlackMageGauge : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        public MemoryField<sbyte> ElementStance => Ptr.Field<sbyte>(0x0A);
    }
}
