using Simulant.Game;

namespace Simulant.Core.Entity
{
    public sealed class EventObject : EntityBase
    {
        internal EventObject(EntityRegistry registry, EntityIdentity identity, EntityObservation observation)
            : base(registry, identity, observation)
        {
        }

        public void SetSharedTimelineState(ushort sharedTimelineState)
            => ActorControl(ActorControlCategory.SetSharedGroupTimelineState, sharedTimelineState);
    }
}
