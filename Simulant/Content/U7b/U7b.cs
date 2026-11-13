using System.Linq;
using System.Numerics;
using Simulant.Core.Environment;

namespace Simulant.Content.U7b
{
    [SimTerritory(1363)]
    public static class U7b
    {
        public static readonly WaymarkLayout Waymarks = WaymarkLayout.Center(100, 100, 0)
            .Circle("A1D4C3B2", true, 12, 12 * System.Math.Sqrt(2));

        [Phase(10)]
        public static readonly PhaseData P1_1;
        [Phase(15)]
        public static readonly PhaseData P1_2;
        [Phase(20)]
        public static readonly PhaseData P2_1;
        [Phase(25)]
        public static readonly PhaseData P2_2;
        [Phase(30)]
        public static readonly PhaseData P34;
        [Phase(50)]
        public static readonly PhaseData P5;
        [Phase(999)]
        public static readonly PhaseData Complete;

        static U7b()
        {
            var spawnPos = new Vector3(100, 100, 0);
            var mapEffects = Enumerable.Repeat<ushort>(0x4, 0x24).ToList();
            mapEffects[0x11] = 0x1;
            mapEffects[0x12] = 0x1;

            var p1Effects = mapEffects.ToList();
            P1_1 = new PhaseData("P1 前半", 77, 20293, spawnPos, p1Effects);
            P1_2 = new PhaseData("P1 后半", 78, 20289, spawnPos, p1Effects); // 20289-20291

            var p21Effects = mapEffects.ToList();
            P2_1 = new PhaseData("P2 金色场地", 79, 20292, spawnPos, p21Effects);

            var p22Effects = mapEffects.ToList();
            p22Effects[0x11] = 0x4;
            p22Effects[0x12] = 0x4;
            p22Effects[0x0] = 0x40;
            P2_2 = new PhaseData("P2 遗弃末世", 79, 20292, spawnPos, p22Effects);

            var p34Effects = mapEffects.ToList();
            P34 = new PhaseData("P3-4", 174, 20293, spawnPos, p34Effects);

            var p5Effects = mapEffects.ToList();
            p5Effects[0x0] = 0x40;

            // 补上九个洞
            p5Effects[0x14] = 0x200;
            for (var i = 0x15; i <= 0x1C; i++)
                p5Effects[i] = 0x1;
            
            // 瓦砾之塔
            for (var i = 0x1D; i <= 0x21; i++)
                p5Effects[i] = 0x2;

            P5 = new PhaseData("P5", 175, 20294, spawnPos, p5Effects);

            var completeEffects = p5Effects.ToList();
            completeEffects[0x23] = 0x1; // 像素小人
            Complete = new PhaseData("过本", 175, 20294, spawnPos, completeEffects);
        }
    }
}
