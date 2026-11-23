using Simulant.Content.U7a.Entities;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Runtime;
using System;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U7a.Abilities
{
    internal sealed class 碎灵一击
    {
        internal const uint ActionId = 40289;
        internal const string Name = "碎灵一击分散";
        internal readonly ActionArea Area = ActionArea.FromAction(ActionId);
        internal readonly PartyMemberSnapshot Target;
        private readonly SimEntityManager _entities;
        private readonly PartySnapshot _snapshot;

        internal 碎灵一击(SimEntityManager entities, int self, 未来的碎片 crystal, Random random)
        {
            _entities = entities;
            _snapshot = PartySnapshot.Capture(entities.Party);
            var positions = Enumerable.Range(0, entities.Party.Count).Select(slot => _snapshot.Get(slot).Position).ToArray();
            Target = _snapshot.Get(SelectTarget(positions, self, Area, crystal?.Pos, crystal?.HitboxRadius ?? 0, random));
        }

        internal static int SelectTarget(Vector2[] positions, int self, ActionArea area,
            Vector2? crystal, float crystalRadius, Random random)
        {
            if (crystal.HasValue && area.IntersectsCircle(crystal.Value, crystalRadius, positions[self]))
                return self;

            var nearby = Enumerable.Range(0, positions.Length)
                .Where(slot => slot != self && area.Contains(positions[self], positions[slot], 0)).ToArray();
            return nearby.Length != 0 ? nearby[random.Next(nearby.Length)] : random.Next(positions.Length);
        }

        internal Action<MechanicResult> KnockbackFrom(Vector2 center)
            => result => SimKnockback.Resolve(_entities.Scheduler, result.HitActors,
                KnockbackEffect.Radial(center, 40, 0.8f), exclude: Target.Actor);

        internal void Resolve(AreaMechanic mechanic)
        {
            // 回放中跳跃演出先于伤害和击退 0.934 秒，期间沿用选人时的站位。
            mechanic.LockedSnapshot = _snapshot;
            mechanic.ResolveAfter = 0.934;
            _entities.RunMechanic(mechanic);
        }
    }
}
