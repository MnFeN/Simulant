using System;
using System.Collections.Concurrent;

namespace Lumina.Data
{
    public class FileHandleManager
    {
        private readonly GameData _gameData;
        private readonly ConcurrentQueue<WeakReference<BaseFileHandle>> _fileQueue;
        internal FileHandleManager(GameData gameData)
        {
            _gameData = gameData;
            _fileQueue = new ConcurrentQueue<WeakReference<BaseFileHandle>>();
        }

        public FileHandle<T> CreateHandle<T>(string path)
            where T : FileResource
        {
            var handle = new FileHandle<T>(_gameData, path);
            var weakRef = new WeakReference<BaseFileHandle>(handle);
            _fileQueue.Enqueue(weakRef);
            return handle;
        }

        public void ProcessQueue()
        {
            while (HasPendingFileLoads)
            {
                var res = _fileQueue.TryDequeue(out var weakRef);
                if (res && weakRef.TryGetTarget(out var handle))
                {
                    handle.Load();
                }
            }
        }

        public bool HasPendingFileLoads => !_fileQueue.IsEmpty;
    }
}
