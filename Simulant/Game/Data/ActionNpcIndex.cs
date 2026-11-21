using System.Collections.Generic;
using System.Linq;

namespace Simulant.Game.Data
{
    /// <summary>Battle NPC base IDs observed casting actions in ACT logs.</summary>
    internal static class ActionNpcIndex
    {
        internal static readonly IReadOnlyDictionary<uint, uint[]> Casters = NpcIndexCsv
            .ReadResource("ActionCasters.csv", NpcIndexCsv.ActionHeader)
            .GroupBy(link => link.First, link => link.Second)
            .ToDictionary(group => group.Key, group => group.Distinct().ToArray());
    }
}
