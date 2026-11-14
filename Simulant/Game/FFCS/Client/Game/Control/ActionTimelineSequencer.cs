using System;

namespace Simulant.Game.FFCS.Client.Game.Control
{
    public struct ActionTimelineSequencer : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        // starting from 0x10 is a 0x60-sized struct containing animation request info?! can be passed as a3 to PlayTimeline
        public Character.Character Parent => Ptr.ReadPtr(0x1C8).As<Character.Character>();

        // Determines which slot the timeline belongs in and then plays it on that slot
        [SigPattern("E8 * * * * 4C 8B BC 24 ? ? ? ? 4C 8D 9C 24 ? ? ? ? 49 8B 5B 40")]
        public static IntPtr PlayTimelineFuncPtr { get; set; }

        public void PlayTimeline(ushort actionTimelineId, IntPtr a3 = default)
            => PlayTimelineFuncPtr.Call(Ptr, actionTimelineId, a3);

    }
}
