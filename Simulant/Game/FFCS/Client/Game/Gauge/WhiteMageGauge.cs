using System;

namespace Simulant.Game.FFCS.Client.Game.Gauge
{
    // 原生 Client::Game::Gauge::WhiteMageGauge，继承 JobGauge，大小 0x10。
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Game/Gauge/JobGauges.cs
    internal struct WhiteMageGauge : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        internal MemoryField<short> LilyTimer => Ptr.Field<short>(0x0A);
        internal MemoryField<byte> Lily => Ptr.Field<byte>(0x0C);
        internal MemoryField<byte> BloodLily => Ptr.Field<byte>(0x0D);
    }
}
