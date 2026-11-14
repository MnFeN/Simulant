using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.IO;
using System.Threading;
using ActionRow = Lumina.Excel.Sheets.Action;

namespace Simulant.Game.Data
{
    /// <summary>读取当前客户端的游戏资源；表按需缓存，切换客户端和卸载时释放。</summary>
    public sealed class GameDataService : IDisposable
    {
        private static GameDataService _current;
        private readonly object _gate = new object();
        private readonly GameData _data;
        private bool _disposed;

        public static GameDataService Instance => Volatile.Read(ref _current)
            ?? throw new InvalidOperationException("游戏数据尚未就绪，请先启动游戏并初始化插件。");

        public string SqPackPath { get; }
        public Language Language { get; }

        public GameDataService(string gamePath, Language language = Language.ChineseSimplified)
        {
            SqPackPath = ResolveSqPackPath(gamePath);
            Language = language;
            _data = new GameData(SqPackPath, new LuminaOptions { DefaultExcelLanguage = language });
            try
            {
                // 先验证模拟所需的表结构，避免运行到机制中途才发现客户端布局不匹配。
                GetSheet<ActionRow>();
                GetSheet<Omen>();
                GetSheet<BNpcBase>();
                GetSheet<BNpcName>();
                GetSheet<TerritoryType>();
                GetSheet<PlaceName>();
                GetSheet<ContentFinderCondition>();
            }
            catch
            {
                _data.Dispose();
                throw;
            }
        }

        public ExcelSheet<T> GetSheet<T>() where T : struct, IExcelRow<T>
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(GameDataService));
                return _data.GetExcelSheet<T>();
            }
        }

        internal void Activate()
        {
            GameDataService previous;
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(GameDataService));
                previous = Interlocked.Exchange(ref _current, this);
            }
            if (!ReferenceEquals(previous, this)) previous?.Dispose();
        }

        public static string ResolveSqPackPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("未提供游戏客户端路径。", nameof(path));
            var directory = Path.GetFullPath(File.Exists(path) ? Path.GetDirectoryName(path) : path);
            foreach (var candidate in new[] { directory, Path.Combine(directory, "sqpack"), Path.Combine(directory, "game", "sqpack") })
                if (string.Equals(new DirectoryInfo(candidate).Name, "sqpack", StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(Path.Combine(candidate, "ffxiv")))
                    return candidate;
            throw new DirectoryNotFoundException("未找到游戏客户端的 sqpack 目录：" + path);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                Interlocked.CompareExchange(ref _current, null, this);
                _data.Dispose();
            }
        }
    }
}
