using System;
using System.Numerics;

namespace Simulant.Content.U7b.Presets
{
    internal partial class P5地火Logic
    {
        /// <summary>只计算整组位置；无法满足场地和队友间距时，不返回未经校验的布置。</summary>
        internal static Vector2[] PlanSpreadPositions(Vector2 myPos, int memberCount, float minDistance, Random random)
        {
            var center = new Vector2(100, 100);
            const int maxAttemptsPerMember = 1000;
            var positions = new Vector2[memberCount];

            for (var i = 0; i < positions.Length; i++)
            {
                var found = false;
                for (var attempt = 0; attempt < maxAttemptsPerMember; attempt++)
                {
                    var angle = random.NextDouble() * Math.PI * 2;
                    var distance = Math.Sqrt(random.NextDouble()) * 15;
                    var pos = myPos + new Vector2((float)(Math.Cos(angle) * distance), (float)(Math.Sin(angle) * distance));
                    if (!(Vector2.Distance(center, pos) <= 20f)) continue;

                    var separated = true;
                    for (var previous = 0; previous < i; previous++)
                    {
                        if (Vector2.Distance(positions[previous], pos) > minDistance) continue;
                        separated = false;
                        break;
                    }
                    if (!separated) continue;
                    positions[i] = pos;
                    found = true;
                    break;
                }

                if (!found)
                    throw new InvalidOperationException($"无法在场地内为第 {i + 1} 个模拟队友找到满足间距的位置，请调整自身位置后重新开始。");
            }
            return positions;
        }
    }
}
