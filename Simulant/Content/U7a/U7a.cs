using System.Linq;
using System.Numerics;
using Simulant.Core.Environment;

namespace Simulant.Content.U7a
{
    [SimTerritory(1238)]
    public static class U7a
    {
        public static readonly WaymarkLayout Waymarks = WaymarkLayout.Center(100, 100, 0).Circle("A1D4C3B2", true, 10);

        [Phase(10)]
        public static readonly PhaseData P1;
        [Phase(15)]
        public static readonly PhaseData P1_Fog;
        [Phase(20)]
        public static readonly PhaseData P2;
        [Phase(25)]
        public static readonly PhaseData P25;
        [Phase(30)]
        public static readonly PhaseData P3;
        [Phase(40)]
        public static readonly PhaseData P4;
        [Phase(50)]
        public static readonly PhaseData P5;
        [Phase(999)]
        public static readonly PhaseData Complete;

        static U7a()
        {
            // 电网属于天气

            var spawnPos = new Vector3(100, 100, 0);
            var mapEffects = Enumerable.Repeat<ushort>(0x4, 0x35).ToList();

            var p1Effects = mapEffects.ToList();
            P1 = new PhaseData("P1", 2, 20134, spawnPos, p1Effects);
            P1_Fog = new PhaseData("P1 雾龙", 4, 20134, spawnPos, p1Effects);

            var p2Effects = mapEffects.ToList();
            p2Effects[0x17] = 0x2; // p2 普通地板
            P2 = new PhaseData("P2", 35, 20135, spawnPos, p2Effects);

            var p25Effects = mapEffects.ToList();
            p25Effects[0x17] = 0x20; // p2 结冰地板
            p25Effects[0x18] = 0x2; // 场中冰块
            P25 = new PhaseData("P2.5", 35, 20136, spawnPos, p25Effects);

            var p3Effects = mapEffects.ToList();
            p3Effects[0x28] = 0x2; // 紫色地板（阶段中会变绿）
            p3Effects[0x29] = 0x2; // 六重对称魔法阵花纹（阶段中会变化）
            P3 = new PhaseData("P3", 105, 20105, spawnPos, p3Effects); 
            // 天气没有实测，可能是 105 106 12 141 142，下同

            var p4Effects = mapEffects.ToList();
            p4Effects[0x28] = 0x2; // 紫色地板（阶段中会变绿）
            p4Effects[0x29] = 0x2; // 六重对称魔法阵花纹（阶段中会变化）
            p4Effects[0x2E] = 0x2; // 正北水晶
            P4 = new PhaseData("P4", 106, 20137, spawnPos, p4Effects); // 天气没有实测

            var p5Effects = mapEffects.ToList();
            p5Effects[0x2F] = 0x2; // 场外回忆画面
            P5 = new PhaseData("P5", 108, 20099, spawnPos, p5Effects);

            var completeEffects = mapEffects.ToList();
            Complete = new PhaseData("过本", 1, 0, spawnPos, completeEffects);
        }
    }
}
