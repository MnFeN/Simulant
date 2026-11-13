using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
namespace Simulant.Core.Environment
{
    public readonly struct PhaseData
    {
        public readonly string Name;
        public readonly byte Weather;
        public readonly ushort BGM;
        public readonly Vector3? Spawn;
        /// <summary>下标为地图效果槽位；值为 null 时跳过该槽位。</summary>
        public readonly IReadOnlyList<ushort?> MapEffectFlags;

        public PhaseData(string name, byte weather, ushort bgm, Vector3? spawn, IEnumerable<ushort?> mapEffectFlags)
        {
            Name = name;
            Weather = weather;
            BGM = bgm;
            Spawn = spawn;
            MapEffectFlags = mapEffectFlags.ToList();
        }

        public static PhaseData Empty => new PhaseData("无数据", 0, 0, null, Enumerable.Empty<ushort?>());

        public override string ToString()
            => $"[阶段] {Name}";

        private static Dictionary<int, Type> _territoryCache = null;
        public static List<PhaseData> GetPhases(int territoryId)
        {
            _territoryCache = _territoryCache ?? Assembly.GetExecutingAssembly()
                .GetTypes()
                .Select(t => new
                {
                    Type = t,
                    Attr = t.GetCustomAttribute<SimTerritoryAttribute>()
                })
                .Where(x => x.Attr != null)
                .ToDictionary(x => x.Attr.TerritoryId, x => x.Type); // does not allow duplicated ids for now

            var result = new List<PhaseData>();
            if (_territoryCache.TryGetValue(territoryId, out var typeOfTerritory))
            {
                RuntimeHelpers.RunClassConstructor(typeOfTerritory.TypeHandle);

                result.AddRange(typeOfTerritory
                    .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(phaseField => new
                    {
                        Field = phaseField,
                        Attr = phaseField.GetCustomAttribute<PhaseAttribute>()
                    })
                    .Where(x => x.Attr != null)
                    .OrderBy(x => x.Attr.SortIndex)
                    .Select(x => (PhaseData)x.Field.GetValue(null))
                );
            }
            if (result.Count == 0)
                result.Add(Empty);
            return result;
        }
    }
}
