using System;

namespace Simulant.Game.FFCS.Client.Game.Gauge
{
    // 原生 Client::Game::Gauge::PaladinGauge，继承 JobGauge，大小 0x10。
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Game/Gauge/JobGauges.cs
    internal struct PaladinGauge : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        internal MemoryField<byte> OathGauge => Ptr.Field<byte>(0x08);
        internal MemoryField<ushort> ConfiteorComboTimer => Ptr.Field<ushort>(0x0A); //that only updates when you generate/spend oath
        internal MemoryField<byte> ConfiteorComboStep => Ptr.Field<byte>(0x0C);
    }
}
