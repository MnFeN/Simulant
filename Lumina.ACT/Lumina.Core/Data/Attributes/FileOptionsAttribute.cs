using System;

namespace Lumina.Data.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class FileOptionsAttribute : Attribute
    {
        public FileCacheBehaviour CacheBehaviour { get; }

        public FileOptionsAttribute(FileCacheBehaviour cacheBehaviour)
        {
            CacheBehaviour = cacheBehaviour;
        }

        public enum FileCacheBehaviour
        {
            None,
            Always,
            Never
        }
    }
}
