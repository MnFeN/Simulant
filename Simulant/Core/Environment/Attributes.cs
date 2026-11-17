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

    /// <summary>在选择阶段的界面中指定“阶段”或“模拟”的显示顺序。</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PhaseSelectionOrderAttribute : Attribute
    {
        public double SortIndex { get; }

        public PhaseSelectionOrderAttribute(double sortIndex)
        {
            SortIndex = sortIndex;
        }
    }
}
