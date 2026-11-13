using Simulant.Game;
using NativeEventObject = Simulant.Game.FFCS.Client.Game.Object.EventObject;

namespace Simulant.Core.Entity
{
    public sealed class EventObject : EntityBase
    {
        internal new NativeEventObject Native => base.Native.Ptr.As<NativeEventObject>();
        internal EventObject(EntityRegistry registry, EntityIdentity identity, EntityObservation observation)
            : base(registry, identity, observation) { }

        public void SetSharedTimelineState(ushort sharedTimelineState)
            => ActorControl(ActorControlCategory.SetSharedGroupTimelineState, sharedTimelineState);
        public void PlayAnimation(ushort bitMask) => PlayAnimation(0, bitMask, 0);
        public void PlayAnimation(ushort sharedTimelineState, ushort bitMask, ulong context = 0)
            => Native.PlayAnimation(sharedTimelineState, bitMask, context);
    }
}
