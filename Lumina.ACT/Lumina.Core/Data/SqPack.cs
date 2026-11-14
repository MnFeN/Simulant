using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Lumina.Data.Attributes;
using Lumina.Data.Structs;

namespace Lumina.Data
{
    public class SqPackInflateException : Exception
    {
        public SqPackInflateException(string message) : base(message)
        {
        }
    }

    public class SqPack
    {
        private readonly GameData _gameData;
        private readonly ConcurrentDictionary<long, WeakReference<FileResource>> _cache;
        public FileInfo File { get; }
        public string Name => File.Name;
        public string FullName => File.FullName;
        public SqPackHeader SqPackHeader { get; private set; }

        [Obsolete("Use property \"ConvertEndianness\" from \"LuminaBinaryReader\" instead")]
        public bool ShouldConvertEndianness { // todo: what about reading LE files on BE device? do we even care?
 get => BitConverter.IsLittleEndian && SqPackHeader.platformId == PlatformId.PS3 || !BitConverter.IsLittleEndian && SqPackHeader.platformId != PlatformId.PS3; }

        [Obsolete("Not used.")]
        protected readonly Dictionary<long, WeakReference<FileResource>> FileCache = new global::System.Collections.Generic.Dictionary<long, global::System.WeakReference<global::Lumina.Data.FileResource>>();
        [Obsolete("Not used.")]
        protected readonly object CacheLock = new object ();
        internal SqPack(FileInfo file, GameData gameData)
        {
            if (!file.Exists)
            {
                throw new FileNotFoundException($"SqPack file {file.FullName} could not be found.");
            }

            _gameData = gameData;
            // always init the cache just in case the should cache setting is changed later
            _cache = new global::System.Collections.Concurrent.ConcurrentDictionary<long, global::System.WeakReference<global::Lumina.Data.FileResource>>();
            File = file;
            using (var sh = GetStreamHolder())
            {
                SqPackHeader = sh.Stream.GetSqPackHeader();
            }
        }

        public SqPackFileInfo GetFileMetadata(long offset)
        {
            using (var sh = GetStreamHolder())
            {
                return sh.Stream.GetFileMetadata(offset);
            }
        }

        public T ReadFile<T>(long offset)
            where T : FileResource
        {
            var cacheBehaviour = FileOptionsAttribute.FileCacheBehaviour.None;
            if (typeof(T).GetCustomAttribute<FileOptionsAttribute>()is global::Lumina.Data.Attributes.FileOptionsAttribute fileOpts)
                cacheBehaviour = fileOpts.CacheBehaviour;
            if (!_gameData.Options.CacheFileResources || cacheBehaviour == FileOptionsAttribute.FileCacheBehaviour.Never)
            {
                using (var rsh = GetStreamHolder())
                {
                    return rsh.Stream.ReadFile<T>(offset);
                }
            }

            // WeakReference ctor accepts null.
            var weakRef = _cache.GetOrAdd(offset, _ => new global::System.WeakReference<global::Lumina.Data.FileResource>(null));
            if (weakRef.TryGetTarget(out var valueUntyped) && valueUntyped is T value)
                return value;
            lock (weakRef)
            {
                if (weakRef.TryGetTarget(out valueUntyped) && valueUntyped is T value2)
                    return value2;
                using (var sh = GetStreamHolder())
                {
                    var f = sh.Stream.ReadFile<T>(offset);
                    weakRef.SetTarget(f);
                    return f;
                }
            }
        }

        protected T GetCachedFile<T>(long offset)
            where T : FileResource => _cache.TryGetValue(offset, out var cached) && cached.TryGetTarget(out var valueUntyped) ? valueUntyped as T : null;
        private SqPackStreamPool.StreamHolder GetStreamHolder() => _gameData.StreamPool is global::Lumina.Data.SqPackStreamPool streamPool ? streamPool.RentScoped(this) : new global::Lumina.Data.SqPackStreamPool.StreamHolder(new SqPackStream(File, SqPackHeader.platformId));
    }
}
