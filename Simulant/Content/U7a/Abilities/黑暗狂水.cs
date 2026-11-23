using Simulant.Simulation.Geometry;
using Simulant.Simulation.Mechanics;
using Simulant.Simulation.Runtime;
using System.Collections.Generic;

namespace Simulant.Content.U7a.Abilities
{
    internal static class 黑暗狂水
    {
        internal const ushort StatusId = 2461;
        internal const ushort MarkerStatusId = 2458;
        internal const uint ActionId = 40271;

        internal static AreaMechanic CreateStack(SimEntityManager entities, string name, int target, IEnumerable<int> expected)
        {
            entities.RemoveStatus(entities.Party[target], StatusId);
            return new AreaMechanic(name, ActionArea.FromAction(ActionId), centerSlot: target)
            {
                RequiredPlayers = 4,
                Check = members => AreaMechanic.CheckPartyAssignment(members, expected)
            };
        }
    }
}
