using SimuMagic;
using System;
using System.Diagnostics;
using System.Text;

namespace SimuMagic
{
    public class ExternalProcessMemory : IDisposable
    {
        private static readonly Process CurrentProcess = Process.GetCurrentProcess();

        protected ExternalProcessMemory() { }

        internal ExternalProcessMemory(Process process, bool startupExecutor, bool dx9, IntPtr hookFunc,
            bool defaultCacheValue, int copyBytes, bool veh)
            => throw new NotSupportedException();

        public virtual Process Process => CurrentProcess;
        public virtual IntPtr ProcessHandle => IntPtr.Zero;
        public virtual bool IsProcessOpen => false;

        public virtual T Read<T>(IntPtr address) where T : struct => throw new NotSupportedException();
        public virtual byte[] ReadBytes(IntPtr address, int count) => throw new NotSupportedException();
        public virtual void Write<T>(IntPtr address, T value) where T : struct => throw new NotSupportedException();
        public virtual int WriteBytes(IntPtr address, byte[] bytes) => throw new NotSupportedException();
        public virtual IntPtr AllocateMemory(int size) => throw new NotSupportedException();
        public virtual bool FreeMemory(IntPtr address) => throw new NotSupportedException();
        public virtual bool WriteString(IntPtr address, string value, Encoding encoding) => throw new NotSupportedException();

        internal IDisposable AcquireFrame(bool hardLock) => throw new NotSupportedException();
        internal T CallInjected64<T>(IntPtr address, params object[] args) where T : struct => throw new NotSupportedException();
        public void Dispose() { }
    }
}

namespace Simulant.Game
{
    public interface INativeCalls
    {
        void Call(IntPtr address, params object[] args);
        T Call<T>(IntPtr address, params object[] args) where T : struct;
    }

    public sealed partial class GameConnection
    {
        private readonly INativeCalls _calls;

        public GameConnection(ExternalProcessMemory memory, INativeCalls calls)
        {
            Memory = memory ?? throw new ArgumentNullException(nameof(memory));
            _calls = calls ?? throw new ArgumentNullException(nameof(calls));
        }
    }
}
