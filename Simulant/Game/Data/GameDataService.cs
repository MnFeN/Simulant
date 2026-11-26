using Lumina;
using Lumina.Data;
using Lumina.Data.Files.Excel;
using Lumina.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

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

        public GameDataService(string gamePath, GameDataLanguage preference = GameDataLanguage.Auto)
            : this(gamePath, preference, null)
        {
        }

        internal GameDataService(string gamePath, GameDataLanguage preference, Action<string> logWarning)
            : this(gamePath, GetLanguageCandidates(preference, logWarning))
        {
        }

        public GameDataService(string gamePath, Language language) : this(gamePath, new[] { language })
        {
        }

        private GameDataService(string gamePath, IEnumerable<Language> candidates)
        {
            SqPackPath = ResolveSqPackPath(gamePath);
            _data = new GameData(SqPackPath);

            try
            {
                Language = SelectLanguage(candidates);
                _data.Options.DefaultExcelLanguage = Language;
            }
            catch
            {
                _data.Dispose();
                throw;
            }
        }

        private static IEnumerable<Language> GetLanguageCandidates(GameDataLanguage preference, Action<string> logWarning = null)
        {
            if (preference != GameDataLanguage.Auto)
            {
                yield return (Language)preference;
                if (preference == GameDataLanguage.CHT)
                    yield return Language.ChineseTraditional;

                // 都没找到时在插件内部显示警告，并回退至默认语言选择
                logWarning?.Invoke($"游戏客户端不包含配置指定的语言 {preference}，回退至自动检测。请在配置中检查游戏路径和语言选项。");
            }

            yield return Language.ChineseSimplified;
            yield return Language.English;
            yield return Language.TraditionalChinese;
            yield return Language.ChineseTraditional;
            yield return Language.Korean;
        }

        private Language SelectLanguage(IEnumerable<Language> candidates)
        {
            var header = _data.GetFile<ExcelHeaderFile>("exd/Action.exh")
                ?? throw new FileNotFoundException("缺少游戏数据表头：Action。");

            // 国际服的表头也会列出简中，按安装包里实际存在的技能表分页选语言。
            foreach (var language in candidates.Distinct())
            {
                if (!header.Languages.Contains(language))
                    continue;

                // 分页路径为 exd/Action_{分页起始行ID}_{语言代码}.exd，起始行 ID 取自 Action.exh。
                // 例如，国服简中页为 exd/Action_0_chs.exd，国际服英语页为 exd/Action_0_en.exd。
                // 无语言差异的表名字如 exd/Xxxx_0.exd。
                var suffix = language == Language.None ? "" : "_" + LanguageUtil.GetLanguageStr(language);
                if (header.DataPages.Length > 0 && header.DataPages.All(page => _data.FileExists($"exd/Action_{page.StartId}{suffix}.exd")))
                    return language;
            }

            throw new FileNotFoundException("当前客户端没有可用的技能表语言分页。请检查游戏路径和语言选项。");
        }

        public ExcelSheet<T> GetSheet<T>()
            where T : struct, IExcelRow<T>
        {
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(GameDataService));

                return _data.GetExcelSheet<T>();
            }
        }

        internal T GetFile<T>(string path)
            where T : FileResource
        {
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(GameDataService));

                return _data.GetFile<T>(path);
            }
        }

        internal bool FileExists(string path)
        {
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(GameDataService));

                return _data.FileExists(path);
            }
        }

        internal void Activate()
        {
            GameDataService previous;

            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(GameDataService));

                previous = Interlocked.Exchange(ref _current, this);
            }

            if (!ReferenceEquals(previous, this))
                previous?.Dispose();
        }

        public static string ResolveSqPackPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("未提供游戏客户端路径。", nameof(path));

            var directory = Path.GetFullPath(File.Exists(path) ? Path.GetDirectoryName(path) : path);

            foreach (var candidate in new[]
            {
                directory,
                Path.Combine(directory, "sqpack"),
                Path.Combine(directory, "game", "sqpack")
            }

            )
                if (string.Equals(new DirectoryInfo(candidate).Name, "sqpack", StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(Path.Combine(candidate, "ffxiv")))
                    return candidate;

            throw new DirectoryNotFoundException("未找到游戏客户端的 sqpack 目录：" + path);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                Interlocked.CompareExchange(ref _current, null, this);
                _data.Dispose();
            }
        }
    }
}
