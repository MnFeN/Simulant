namespace Lumina.Data
{
    public abstract class BaseFileHandle
    {
        public enum FileState
        {
            None,
            Loading,
            Loaded,
            Error
        }

        internal BaseFileHandle(GameData gameData, string path)
        {
            GameData = gameData;
            Path = path;
        }

        public FileState State { get; protected set; } = FileState.None;

        protected readonly GameData GameData;
        protected readonly string Path;
        protected object Instance;
        public bool HasValue => State == FileState.Loaded && Instance != null;

        public virtual void Load()
        {
            throw new System.NotImplementedException();
        }
    }

    public class FileHandle<T> : BaseFileHandle where T : FileResource
    {
        internal FileHandle(GameData gameData, string path) : base(gameData, path)
        {
        }

        public override void Load()
        {
            State = FileState.Loading;
            var file = GameData.GetFile<T>(Path);
            if (file == null)
            {
                State = FileState.Error;
                return;
            }

            State = FileState.Loaded;
            Instance = file;
        }

        public T Value
        {
            get
            {
                if (HasValue)
                {
                    return (T)Instance;
                }

                return null;
            }
        }
    }
}
