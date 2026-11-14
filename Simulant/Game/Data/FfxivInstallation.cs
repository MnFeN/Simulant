using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Simulant.Game.Data
{
    /// <summary>优先使用配置的客户端文件；自动检测只枚举当前进程，不依赖解析插件。</summary>
    internal static class FfxivInstallation
    {
        internal static string Resolve(string configuredPath, Func<IEnumerable<string>> findProcesses = null)
        {
            var path = configuredPath?.Trim();
            if (string.IsNullOrEmpty(path))
            {
                var paths = (findProcesses ?? FindProcessPaths)().Select(Path.GetFullPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (paths.Length == 0) return null;
                if (paths.Length > 1)
                    throw new InvalidOperationException("检测到正在运行多个不同目录的 FFXIV 客户端，请在配置页面中指定 FFXIV 安装路径。");
                path = paths[0];
            }
            if (!File.Exists(path))
                throw new FileNotFoundException($"配置中的游戏路径（{path}）不存在，请在配置页面中检查路径。", path);
            if (!string.Equals(Path.GetFileName(path), "ffxiv_dx11.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("游戏路径必须指向 ffxiv_dx11.exe 文件，请在配置页面中重新选择。");
            return Path.GetFullPath(path);
        }

        private static IEnumerable<string> FindProcessPaths()
        {
            var paths = new List<string>();
            Exception accessError = null;
            foreach (var process in Process.GetProcessesByName("ffxiv_dx11"))
            {
                using (process)
                {
                    try { paths.Add(process.MainModule.FileName); }
                    catch (InvalidOperationException) { } // 枚举期间进程已经退出。
                    catch (Win32Exception error) { accessError = error; }
                }
            }
            // 不忽略无法读取的进程，以免多开时误选另一个安装目录。
            if (accessError != null)
                throw new InvalidOperationException("无法读取 FFXIV 进程路径，请在配置页面中手动设置 FFXIV 安装路径。", accessError);
            return paths;
        }
    }
}
