using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Lumina.Data;
using Lumina.Data.Structs;
using Lumina.Excel;
using Lumina.Misc;

namespace Lumina
{
    /// <summary>ACT/.NET Framework facade over Lumina 7.7.2's resource readers.</summary>
    public sealed class GameData : IDisposable
    {
        [ThreadStatic] private static WeakReference<GameData> context;
        private bool disposed;
        public DirectoryInfo DataPath { get; private set; }
        public Dictionary<string, Repository> Repositories { get; private set; }
        public LuminaOptions Options { get; private set; }
        public ExcelModule Excel { get; private set; }
        public ExcelSheet<T> GetExcelSheet<T>(Language? language = null, string name = null) where T : struct, IExcelRow<T>
        { if (disposed) throw new ObjectDisposedException(nameof(GameData)); return Excel.GetSheet<T>(language, name); }
        public SubrowExcelSheet<T> GetSubrowExcelSheet<T>(Language? language = null, string name = null) where T : struct, IExcelSubrow<T>
        { if (disposed) throw new ObjectDisposedException(nameof(GameData)); return Excel.GetSubrowSheet<T>(language, name); }
        public FileHandleManager FileHandleManager { get; private set; }
        public SqPackStreamPool StreamPool { get; set; }
        internal ILogger Logger { get; private set; }

        public static GameData CurrentContext { get { GameData value; return context != null && context.TryGetTarget(out value) ? value : null; } }

        public GameData(string dataPath, LuminaOptions options = null)
        {
            Options = options ?? new LuminaOptions();
            DataPath = new DirectoryInfo(dataPath);
            if (!DataPath.Exists)
                throw new DirectoryNotFoundException(DataPath.FullName);
            if (!DataPath.Name.Equals("sqpack", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose the game's sqpack directory.", nameof(dataPath));
            Repositories = new Dictionary<string, Repository>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var directory in DataPath.GetDirectories())
                    Repositories.Add(directory.Name, new Repository(directory, this));
                FileHandleManager = new FileHandleManager(this);
                Excel = new ExcelModule(this);
                StreamPool = new SqPackStreamPool();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public GameData(string path, ILogger logger, LuminaOptions options = null) : this(path, options)
        {
            Logger = logger;
        }

        public static ParsedFilePath ParseFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            path = path.Trim().Replace('\\', '/').ToLowerInvariant();
            int split = path.LastIndexOf('/');
            if (path.Length >= 260 || split < 1 || split == path.Length - 1)
                return null;
            var parts = path.Split('/');
            var repository = parts[1];
            if (repository.Length < 3 || !repository.StartsWith("ex", StringComparison.Ordinal) || !char.IsDigit(repository[2]))
                repository = "ffxiv";
            return new ParsedFilePath
            {
                Category = parts[0],
                Repository = repository,
                Path = path,
                IndexHash = ((ulong)Crc32.Get(path.Substring(0, split)) << 32) | Crc32.Get(path.Substring(split + 1)),
                Index2Hash = Crc32.Get(path)
            };
        }

        public FileResource GetFile(string path)
        {
            return GetFile<FileResource>(path);
        }

        public T GetFile<T>(string path)
            where T : FileResource
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(GameData));
            SetCurrentContext();
            var parsed = ParseFilePath(path);
            Repository repo;
            return parsed != null && Repositories.TryGetValue(parsed.Repository, out repo) ? repo.GetFile<T>(parsed.Category, parsed) : null;
        }

        public T GetFileFromDisk<T>(string path, string origPath = null)
            where T : FileResource
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(GameData));
            SetCurrentContext();
            var file = Activator.CreateInstance<T>();
            file.Data = File.ReadAllBytes(path);
            file.FilePath = origPath == null ? null : ParseFilePath(origPath);
            file.Reader = new LuminaBinaryReader(file.Data, Options.CurrentPlatform);
            file.LoadFile();
            return file;
        }

        public SqPackFileInfo? GetFileMetadata(string path)
        {
            var parsed = ParseFilePath(path);
            Repository repo;
            return parsed != null && Repositories.TryGetValue(parsed.Repository, out repo) ? repo.GetFileMetadata(parsed.Category, parsed) : null;
        }

        public bool FileExists(string path)
        {
            var parsed = ParseFilePath(path);
            Repository repo;
            return parsed != null && Repositories.TryGetValue(parsed.Repository, out repo) && repo.FileExists(parsed.Category, parsed);
        }

        public static ulong GetFileHash(string path)
        {
            return ParseFilePath(path).IndexHash;
        }

        public FileHandle<T> GetFileHandle<T>(string path)
            where T : FileResource
        {
            return FileHandleManager.CreateHandle<T>(path);
        }

        public void ProcessFileHandleQueue()
        {
            FileHandleManager.ProcessQueue();
        }

        internal void SetCurrentContext()
        {
            context = new WeakReference<GameData>(this);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (StreamPool != null)
            {
                StreamPool.Dispose();
                StreamPool = null;
            }

            if (ReferenceEquals(CurrentContext, this)) context = null;
        }
    }

    public sealed class ParsedFilePath
    {
        public static implicit operator string (ParsedFilePath value)
        {
            return value == null ? null : value.Path;
        }

        public override string ToString()
        {
            return Path;
        }

        public string Category { get; set; }
        public string Repository { get; set; }
        public string Path { get; set; }
        public ulong IndexHash { get; set; }
        public uint Index2Hash { get; set; }
    }

    public sealed class LuminaOptions
    {
        public PlatformId CurrentPlatform { get; set; } = PlatformId.Win32;
        public bool CacheFileResources { get; set; } = true;
        public Language DefaultExcelLanguage { get; set; } = Language.English;
        public bool LoadMultithreaded { get; set; }
        public Func<string, string> RsvResolver { get; set; }
    }
}
