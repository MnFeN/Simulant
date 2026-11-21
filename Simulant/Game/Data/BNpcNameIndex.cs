using System.Collections.Generic;
using System.Linq;

namespace Simulant.Game.Data
{
    /// <summary>The first battle NPC name ID for each base ID.</summary>
    internal static class BNpcNameIndex
    {
        // BNpcNames.csv merges ACT log observations with the GPLv3 BNpcLink table from LuminaSupplemental.
        // https://github.com/Critical-Impact/LuminaSupplemental/blob/a0acea07fcf054799d295001d873b345a1821133/src/LuminaSupplemental.Excel/Generated/BNpcLink.csv
        private static readonly IReadOnlyDictionary<uint, uint> Names = NpcIndexCsv
            .ReadResource("BNpcNames.csv", NpcIndexCsv.NameHeader)
            .ToDictionary(link => link.First, link => link.Second);

        internal static uint GetNameId(uint baseId) => Names.TryGetValue(baseId, out var nameId) ? nameId : 0;
    }
}
