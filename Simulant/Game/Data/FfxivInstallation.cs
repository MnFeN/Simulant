using System;
using System.IO;

namespace Simulant.Game.Data
{
    /// <summary>解析用于离线准备的客户端配置路径。运行中的目标由 FFXIV 解析插件提供。</summary>
    internal static class FfxivInstallation
    {
        internal static string Resolve(string configuredPath)
        {
            var path = configuredPath?.Trim();
            if (string.IsNullOrEmpty(path))
                return null;

            if (!File.Exists(path))
                throw new FileNotFoundException($"配置中的游戏路径（{path}）不存在，请在配置页面中检查路径。", path);

            if (!string.Equals(Path.GetFileName(path), "ffxiv_dx11.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("游戏路径必须指向 ffxiv_dx11.exe 文件，请在配置页面中重新选择。");

            return Path.GetFullPath(path);
        }
    }
}
