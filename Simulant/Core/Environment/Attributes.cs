using System;

namespace Simulant.Core.Environment
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SimTerritoryAttribute : Attribute
    {
        public int TerritoryId { get; }

        public SimTerritoryAttribute(int territoryId)
        {
            TerritoryId = territoryId;
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class PhaseAttribute : Attribute
    {
        public int SortIndex { get; }

        public PhaseAttribute(int sortIndex)
        {
            SortIndex = sortIndex;
        }
    }
}
