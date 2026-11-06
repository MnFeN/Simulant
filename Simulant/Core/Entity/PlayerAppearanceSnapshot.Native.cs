using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Object;
using System;
using System.Text;

namespace Simulant.Core.Entity
{
    internal sealed partial class PlayerAppearanceSnapshot
    {
        internal static PlayerAppearanceSnapshot Capture(Character source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.ObjectKind != ObjectKind.Pc)
                throw new ArgumentException("外观来源必须是玩家角色。", nameof(source));
            var native = source.Native;
            var result = FromDrawData(native.DrawData.ReadAppearanceData());
            result.Level = native.Level;
            result.HomeWorld = native.HomeWorld;
            result.CurrentWorld = native.CurrentWorld;

            // GameObject 的名字缓冲区为 64 字节；生成包稍后按完整 UTF-8 字符截取。
            var nameBytes = native.GetName().ReadBytes(64);
            var nameLength = Array.IndexOf(nameBytes, (byte)0);
            if (nameLength < 0) nameLength = nameBytes.Length;
            try { result.Name = new UTF8Encoding(false, true).GetString(nameBytes, 0, nameLength); }
            catch (DecoderFallbackException error)
            {
                throw new InvalidOperationException("玩家名字不是有效的 UTF-8 文本。", error);
            }
            if (!source.IsAlive)
                throw new InvalidOperationException("复制外观期间来源玩家已经失效，本次未发送生成包。");
            return result;
        }
    }
}
