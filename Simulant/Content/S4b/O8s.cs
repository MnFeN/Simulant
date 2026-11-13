using System.Linq;
using System.Numerics;
using Simulant.Core.Environment;

namespace Simulant.Content.S4b
{
    [SimTerritory(755)]
    public static class O8s
    {
        [Phase(0)]
        public static readonly PhaseData 门神P1;
        [Phase(50)]
        public static readonly PhaseData 门神P2;
        [Phase(100)]
        public static readonly PhaseData 本体P1;
        [Phase(150)]
        public static readonly PhaseData 本体P2;

        static O8s()
        {
            var spawnPos = new Vector3(0, 0, 0);
            var mapEffects = new ushort?[0];

            门神P1 = new PhaseData("门神 P1", 77, 529, spawnPos, mapEffects);
            门神P2 = new PhaseData("门神 P2", 78, 530, spawnPos, mapEffects);

            本体P1 = new PhaseData("本体 P1", 79, 533, spawnPos, mapEffects);
            本体P2 = new PhaseData("本体 P2", 80, 533, spawnPos, mapEffects);
        }
    }
}
