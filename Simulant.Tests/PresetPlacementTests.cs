using Simulant.Content.U7b.Presets;
using System;
using System.Numerics;

namespace Simulant.Tests
{
    internal static class PresetPlacementTests
    {
        public static void SpreadPlacementKeepsEveryMemberInsideArenaAndSeparated()
        {
            var center = new Vector2(100, 100);
            foreach (var minDistance in new[] { 3f, 5.1f })
            {
                var positions = P5地火Logic.PlanSpreadPositions(center, 7, minDistance, new Random(12345));
                Assert(positions.Length == 7, "分散布局必须包含全部七名模拟队友。");
                for (var i = 0; i < positions.Length; i++)
                {
                    Assert(Vector2.Distance(center, positions[i]) <= 20f,
                        "每个队友的位置必须在场地范围内。");
                    Assert(Vector2.Distance(center, positions[i]) <= 15f,
                        "候选位置必须保留原先距离自身十五个世界单位以内的采样范围。");
                    for (var previous = 0; previous < i; previous++)
                        Assert(Vector2.Distance(positions[previous], positions[i]) > minDistance,
                            "任意两名模拟队友之间的距离必须大于指定间距。");
                }
            }
        }

        public static void SpreadPlacementRejectsImpossibleLayoutInsteadOfReturningFallback()
        {
            foreach (var scenario in new[]
            {
                (Origin: new Vector2(140, 100), Spacing: 5.1f),
                (Origin: new Vector2(100, 100), Spacing: 31f)
            })
            {
                try
                {
                    P5地火Logic.PlanSpreadPositions(scenario.Origin, 7, scenario.Spacing, new Random(12345));
                }
                catch (InvalidOperationException error)
                {
                    Assert(error.Message.Contains("无法在场地内") && error.Message.Contains("重新开始"),
                        "布置失败必须说明原因和恢复方法。");
                    continue;
                }
                throw new Exception("没有合法候选点或中途无法满足间距时，必须拒绝整组布局，不能返回兜底位置。");
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
