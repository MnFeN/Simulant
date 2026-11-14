using System;
using System.Collections.Generic;

namespace Lumina.Data
{
    /// <summary>Bounded pool with deterministic handle disposal on Framework 4.8.1.</summary>
    public sealed class SqPackStreamPool : IDisposable
    {
        private readonly object gate = new object ();
        private readonly Dictionary<SqPack, Stack<SqPackStream>> streams = new Dictionary<SqPack, Stack<SqPackStream>>();
        private readonly int maximumRetained;
        private bool disposed;
        public SqPackStreamPool(int maximumRetained = 2)
        {
            this.maximumRetained = Math.Max(1, maximumRetained);
        }

        internal StreamHolder RentScoped(SqPack dat)
        {
            lock (gate)
            {
                Stack<SqPackStream> pool;
                if (!disposed && streams.TryGetValue(dat, out pool) && pool.Count != 0)
                    return new StreamHolder(this, dat, pool.Pop());
            }

            return new StreamHolder(this, dat, new SqPackStream(dat.File, dat.SqPackHeader.platformId));
        }

        private void Return(SqPack key, SqPackStream stream)
        {
            lock (gate)
            {
                Stack<SqPackStream> pool;
                if (!disposed)
                {
                    if (!streams.TryGetValue(key, out pool))
                        streams.Add(key, pool = new Stack<SqPackStream>());
                    if (pool.Count < maximumRetained && stream.BaseStream.CanRead)
                    {
                        pool.Push(stream);
                        return;
                    }
                }
            }

            stream.Dispose();
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed)
                    return;
                disposed = true;
                foreach (var pool in streams.Values)
                    foreach (var stream in pool)
                        stream.Dispose();
                streams.Clear();
            }
        }

        internal sealed class StreamHolder : IDisposable
        {
            private readonly SqPackStreamPool owner;
            private readonly SqPack key;
            private SqPackStream stream;
            internal StreamHolder(SqPackStreamPool owner, SqPack key, SqPackStream stream)
            {
                this.owner = owner;
                this.key = key;
                this.stream = stream;
            }

            public StreamHolder(SqPackStream stream)
            {
                this.stream = stream;
            }

            public SqPackStream Stream
            {
                get
                {
                    return stream ?? throw new ObjectDisposedException(nameof(StreamHolder));
                }
            }

            public void Dispose()
            {
                var value = stream;
                if (value == null)
                    return;
                stream = null;
                if (owner == null)
                    value.Dispose();
                else
                    owner.Return(key, value);
            }
        }
    }
}
