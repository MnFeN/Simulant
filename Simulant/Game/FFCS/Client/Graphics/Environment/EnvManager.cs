using System;

namespace Simulant.Game.FFCS.Client.Graphics.Environment
{
    // Client::Graphics::Environment::EnvManager
    public struct EnvManager : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        [SigPattern("0F 28 F2 48 8B 05 * * * *")]
        public static IntPtr InstancePtrPtr { get; set; }
        public static EnvManager Instance
            => InstancePtrPtr.ReadPtr().As<EnvManager>();

        // public EnvScene EnvScene => Ptr.ReadPtr(0x08).As<EnvScene>();
        public MemoryField<float> DayTimeSeconds => Ptr.Field<float>(0x10);
        public MemoryField<float> ActiveTransitionTime => Ptr.Field<float>(0x14);
        public MemoryField<float> CurrentTransitionTime => Ptr.Field<float>(0x18);
        public MemoryField<float> TransitionProgress => Ptr.Field<float>(0x1C);
        public MemoryField<byte> ActiveWeather => Ptr.Field<byte>(0x27);
        public MemoryField<float> TransitionTime => Ptr.Field<float>(0x28);
    }
}
