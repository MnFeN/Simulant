using Simulant.UI;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Simulant.Tests
{
    internal static class QuickControlPaintingTests
    {
        public static void CooldownPaintingHandlesTinyArcsAndFullRotation()
        {
            using (var bitmap = new Bitmap(200, 200))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                foreach (var size in new[] { 30f, 48f, 55.04f, 61.44f, 78.24f, 100.3f })
                {
                    var square = new RectangleF(10.88f, 36.81f, size, size);
                    // 旧实现会在 0.001～0.1 度附近抛出 OutOfMemoryException。
                    for (var i = 0; i <= 1000; i++)
                        QuickControlForm.PaintCooldown(graphics, square, i / 10000f);
                    for (var i = 1; i <= 36000; i++)
                        QuickControlForm.PaintCooldown(graphics, square, i / 100f);
                }
                if (bitmap.GetPixel(60, 85).A == 0)
                    throw new Exception("正常冷却必须仍然绘制遮罩。");
            }
        }
    }
}

namespace Simulant.UI
{
    // 绘图回归检查不读取游戏文件；窗口与冷却绘制均使用正式代码。
    internal static class GameIcon
    {
        internal static Bitmap LoadAction(Simulant.Game.Data.GameDataService data, uint action) => new Bitmap(8, 8);
    }
}
