using System.Linq;
using System.Numerics;
using Simulant.Core.Environment;

namespace Simulant.Content.U6b
{
    [SimTerritory(1122)]
    public static class U6b
    {
        public static readonly WaymarkLayout Waymarks = WaymarkLayout.Center(100, 100, 0).Circle("A4D3C2B1", true, 13);

        [Phase(10)]
        public static readonly PhaseData P1;
        [Phase(20)]
        public static readonly PhaseData P2;
        [Phase(30)]
        public static readonly PhaseData P3;
        [Phase(40)]
        public static readonly PhaseData P4;
        [Phase(50)]
        public static readonly PhaseData P5;
        [Phase(60)]
        public static readonly PhaseData P6;

        static U6b()
        {
            var spawnPos = new Vector3(100, 100, 0);
            var mapEffects = Enumerable.Repeat<ushort>(0x4, 0x16).ToList();
            mapEffects[0] = 0x1; // 电网

            var p1Effects = mapEffects.ToList();
            p1Effects[0x09] = 0x1; // 北侧隧道特效
            P1 = new PhaseData("P1", 77, 962, spawnPos, p1Effects);

            var p2Effects = mapEffects.ToList();
            p2Effects[0x0A] = 0x1; // 场外赤道面上的符文特效
            P2 = new PhaseData("P2", 78, 963, spawnPos, p2Effects);

            var p3Effects = mapEffects.ToList();
            p3Effects[0x0B] = 0x1; // 包住场地的黑球特效，只出现几秒。似乎只在转入 P3 调用 0x2 的时候有特效，0x1 对应的立刻生成没有任何变化，不过还是写一下
            P3 = new PhaseData("P3", 79, 950, spawnPos, p3Effects);

            var p4Effects = mapEffects.ToList();
            p4Effects[0x14] = 0x1; // 场外赤道面马赛克特效
            P4 = new PhaseData("P4", 89, 950, spawnPos, p4Effects);

            var p5Effects = mapEffects.ToList();
            P5 = new PhaseData("P5", 174, 964, spawnPos, p5Effects);

            var p6Effects = mapEffects.ToList();
            P6 = new PhaseData("P6", 175, 951, spawnPos, p6Effects);
        }
    }
}
