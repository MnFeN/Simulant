using System.Numerics;
using Simulant.Core.Environment;

namespace Simulant.Content.U5a
{
    [SimTerritory(887)]
    public static class U5a
    {
        [Phase(10)]
        public static readonly PhaseData P1;
        [Phase(20)]
        public static readonly PhaseData P2;
        [Phase(30)]
        public static readonly PhaseData P3;
        [Phase(31)]
        public static readonly PhaseData P3时停;
        [Phase(40)]
        public static readonly PhaseData P4;

        static U5a()
        {
            var spawnPos = new Vector3(100, 100, 0);

            // MapEffects:
            // 0: 无效
            // 1: 电网
            // 2: P3 地板 + 背景
            // 3: P3 结尾北侧超大亚历山大
            // 4: P4 场外旋转齿轮
            // 5: P4 地板 + 背景
            // 6: P4 结尾北侧超大亚历山大
            // 7: 场地整体发光，瞬时特效

            P1 = new PhaseData(
                "P1", 2, 0x2C2, spawnPos, 
                new ushort[8] { 0x0, 0x1, 0x4, 0x1, 0x4, 0x4, 0x4, 0x1 }
            );
            P2 = new PhaseData(
                "P2", 105, 0x2D3, spawnPos, 
                new ushort[8] { 0x0, 0x1, 0x4, 0x1, 0x4, 0x4, 0x4, 0x1 }
            );
            P3 = new PhaseData(
                "P3", 71, 0x2D5, spawnPos, 
                new ushort[8] { 0x0, 0x1, 0x1, 0x1, 0x4, 0x4, 0x4, 0x1 }
            );
            P3时停 = new PhaseData(
                "P3 时停", 72, 0x2D4, spawnPos, 
                new ushort[8] { 0x0, 0x1, 0x1, 0x1, 0x4, 0x4, 0x4, 0x1 }
            );
            P4 = new PhaseData(
                "P4", 108, 0x2D6, spawnPos, 
                new ushort[8] { 0x0, 0x40, 0x4, 0x1, 0x1, 0x1, 0x4, 0x4 }
            );
        }
    }
}
