using System.Linq;
using Simulant.Core.Environment;

namespace Simulant.Content.U7b
{
    [SimTerritory(1363)]
    public static class U7b
    {
        public static readonly WaymarkLayout Waymarks = WaymarkLayout.Center(100, 100, 0)
            .Circle("A1D4C3B2", true, 12, 12 * System.Math.Sqrt(2));

        [PhaseSelectionOrder(1)]
        public static readonly PhaseData P1_1;
        [PhaseSelectionOrder(1.5)]
        public static readonly PhaseData P1_2;
        [PhaseSelectionOrder(2)]
        public static readonly PhaseData P2_1;
        [PhaseSelectionOrder(2.5)]
        public static readonly PhaseData P2_2;
        [PhaseSelectionOrder(3)]
        public static readonly PhaseData P34;
        [PhaseSelectionOrder(5)]
        public static readonly PhaseData P5;
        [PhaseSelectionOrder(99.9)]
        public static readonly PhaseData Complete;

        static U7b()
        {
            var mapEffects = Enumerable.Repeat<ushort?>(0x4, 0x24).ToList();
            mapEffects[0x11] = 0x1;
            mapEffects[0x12] = 0x1;

            var p1Effects = mapEffects.ToList();
            P1_1 = new PhaseData("P1 前半", 77, 20293, p1Effects);
            P1_2 = new PhaseData("P1 后半", 78, 20289, p1Effects); // 20289-20291

            var p21Effects = mapEffects.ToList();
            P2_1 = new PhaseData("P2 金色场地", 79, 20292, p21Effects);

            var p22Effects = mapEffects.ToList();
            p22Effects[0x11] = 0x4;
            p22Effects[0x12] = 0x4;
            p22Effects[0x0] = 0x40;
            P2_2 = new PhaseData("P2 遗弃末世", 79, 20292, p22Effects);

            var p34Effects = mapEffects.ToList();
            P34 = new PhaseData("P3-4", 174, 20293, p34Effects);

            var p5Effects = mapEffects.ToList();
            p5Effects[0x0] = 0x40;

            // 补上九个洞
            p5Effects[0x14] = 0x200;

            for (var i = 0x15; i <= 0x1C; i++)
                p5Effects[i] = 0x1;

            // 瓦砾之塔
            for (var i = 0x1D; i <= 0x21; i++)
                p5Effects[i] = 0x2;

            P5 = new PhaseData("P5", 175, 20294, p5Effects);

            var completeEffects = p5Effects.ToList();
            completeEffects[0x23] = 0x1; // 像素小人
            Complete = new PhaseData("过本", 175, 20294, completeEffects);
        }
    }
}
