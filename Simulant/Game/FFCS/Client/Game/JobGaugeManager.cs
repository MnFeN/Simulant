using Simulant.Game.FFCS.Client.Game.Gauge;
using System;

namespace Simulant.Game.FFCS.Client.Game
{
    // 原生 Client::Game::JobGaugeManager，无基类，大小 0x60。
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Game/JobGaugeManager.cs
    internal struct JobGaugeManager : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        [SigPattern("48 8D 0D * * * * E8 ?? ?? ?? ?? B8 ?? ?? ?? ?? 66 3B F0")]
        internal static IntPtr InstancePtr { get; set; }
        internal static JobGaugeManager Instance() => InstancePtr.As<JobGaugeManager>();

        internal IntPtr CurrentGauge => Ptr.ReadPtr();
        internal PaladinGauge Paladin => Ptr.As<PaladinGauge>(0x08);
        internal BlackMageGauge BlackMage => Ptr.As<BlackMageGauge>(0x08);
        internal MemoryField<byte> ClassJobId => Ptr.Field<byte>(0x58);
    }
}
