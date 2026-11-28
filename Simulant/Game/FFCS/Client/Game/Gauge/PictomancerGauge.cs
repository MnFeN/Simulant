using System;

namespace Simulant.Game.FFCS.Client.Game.Gauge
{
    // 原生 Client::Game::Gauge::PictomancerGauge，继承 JobGauge，大小 0x10。
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Game/Gauge/JobGauges.cs
    internal struct PictomancerGauge : IMemoryObject
    {
        public IntPtr Ptr { get; set; }
        internal MemoryField<byte> Palette => Ptr.Field<byte>(0x08);
        internal MemoryField<byte> Paint => Ptr.Field<byte>(0x0A);
        internal MemoryField<byte> Canvases => Ptr.Field<byte>(0x0B);
        internal MemoryField<byte> Depictions => Ptr.Field<byte>(0x0C);
    }
}
