using Lumina.Data.Files;
using Simulant.Game.Data;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Simulant.UI
{
    /// <summary>从当前客户端资源读取技能图标；返回的位图由使用者释放。</summary>
    internal static class GameIcon
    {
        internal static Bitmap LoadAction(GameDataService data, uint actionId)
        {
            var row = data.GetSheet<Lumina.Excel.Sheets.Action>().GetRow(actionId);
            return Load(data, row.Icon) ?? throw new InvalidOperationException("未找到技能图标：" + actionId);
        }

        internal static Bitmap Load(GameDataService data, int icon)
        {
            if (icon <= 0)
                return null;

            var path = FormattableString.Invariant($"ui/icon/{icon / 1000 * 1000:000000}/{icon:000000}");
            var texture = data.GetFile<TexFile>(path + "_hr1.tex") ?? data.GetFile<TexFile>(path + ".tex");
            if (texture == null)
                return null;

            var bitmap = new Bitmap(texture.Header.Width, texture.Header.Height, PixelFormat.Format32bppArgb);
            try
            {
                var pixels = texture.ImageData;
                var buffer = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    for (var y = 0; y < bitmap.Height; y++)
                        Marshal.Copy(pixels, y * bitmap.Width * 4, buffer.Scan0 + y * buffer.Stride, bitmap.Width * 4);
                }
                finally
                {
                    bitmap.UnlockBits(buffer);
                }
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }
    }
}
