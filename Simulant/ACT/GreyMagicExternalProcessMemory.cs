using System;
using System.Linq;
using System.Text;
using System.Threading;

namespace Simulant.ACT
{
    /// <summary>
    /// Wrapper for GreyMagic.ExternalProcessMemory
    /// </summary>
    public class GreyMagicExternalProcessMemory : GreyMagicMemoryBase
    {
        private readonly dynamic _externalMem;

        public object RawMemory
        {
            get
            {
                return _externalMem;
            }
        }

        public GreyMagicExternalProcessMemory(object memory)
            : base(memory)
        {
            _externalMem = memory ?? throw new ArgumentNullException(nameof(memory));
        }

        public dynamic Patches => _externalMem.Patches;
        public bool CacheEnabled => _externalMem.CacheEnabled;
        public bool IsThreadOpen => _externalMem.IsThreadOpen;

        public object GetLock() => _externalMem.GetLock();
        public void ClearCache() => _externalMem.ClearCache();
        public void DisableCache() => _externalMem.DisableCache();
        public void EnableCache() => _externalMem.EnableCache();
        public void ClearCallCache() => _externalMem.ClearCallCache();
        /*
        public T CallInjected64<T>(IntPtr address, params object[] args) where T : struct
        {
            return WrapInjectedCall(() => (T)_externalMem.CallInjected64<T>(address, args));
        }

        public void CallInjected64(IntPtr address, params object[] args)
        {
            _ = CallInjected64<IntPtr>(address, args);
        }

        public T CallInjected64ROP<T>(IntPtr address, params object[] args) where T : struct
        {
            return WrapInjectedCall(() => (T)_externalMem.CallInjected64ROP<T>(address, args));
        }

        public void CallInjected64ROP(IntPtr address, params object[] args)
        {
            _ = CallInjected64ROP<IntPtr>(address, args);
        }

        private T WrapInjectedCall<T>(Func<T> func)
        {
            try
            {
                return func();
            }
            catch (Exception ex) when (ex.Message.Contains("InjectionFinishedEvent was never fired"))
            {
                throw new Exception("调用函数时游戏无响应，疑似崩溃，可能是开启了冲突的卫月插件等原因所致。建议重启 ACT 以防再次崩溃。\n\n", ex);
            }
        }

        public void CallVirtualFunction(IntPtr objAddress, int vFuncIndex, params object[] args)
        {
            CallVirtualFunction<IntPtr>(objAddress, vFuncIndex, args);
        }

        public T CallVirtualFunction<T>(IntPtr objAddress, int vFuncIndex, params object[] args) where T : struct
        {
            IntPtr vTablePtr = Read<IntPtr>(objAddress);
            IntPtr vFuncPtr = Read<IntPtr>(vTablePtr + 8 * vFuncIndex);
            return CallInjected64<T>(vFuncPtr, new object[] { objAddress }.Concat(args).ToArray());
        }

        public void ExecuteWithLock(Action action)
        {
            bool lockTaken = false;
            object assmLock = _externalMem.Executor.AssemblyLock;
            try
            {
                Monitor.Enter(assmLock, ref lockTaken);
                action();
            }
            finally
            {
                if (lockTaken)
                    Monitor.Exit(assmLock);
            }
        }

        public T ExecuteWithLock<T>(Func<T> function)
        {
            bool lockTaken = false;
            object assmLock = _externalMem.Executor.AssemblyLock;
            try
            {
                Monitor.Enter(assmLock, ref lockTaken);
                return function();
            }
            finally
            {
                if (lockTaken)
                    Monitor.Exit(assmLock);
            }
        }
        */
        public void WithAllocatedString(string text, Encoding encoding, Action<IntPtr> action)
        {
            IntPtr stringPtr = IntPtr.Zero;

            try
            {
                int byteCount = encoding.GetByteCount(text) + encoding.GetByteCount("\0") + 10;
                stringPtr = AllocateMemory(byteCount);
                WriteString(stringPtr, text, encoding);
                action(stringPtr);
            }
            finally
            {
                if (stringPtr != IntPtr.Zero)
                    FreeMemory(stringPtr);
            }
        }

        public T WithAllocatedString<T>(string text, Encoding encoding, Func<IntPtr, T> func)
        {
            IntPtr stringPtr = IntPtr.Zero;

            try
            {
                int byteCount = encoding.GetByteCount(text) + encoding.GetByteCount("\0") + 10;
                stringPtr = AllocateMemory(byteCount);
                WriteString(stringPtr, text, encoding);
                return func(stringPtr);
            }
            finally
            {
                if (stringPtr != IntPtr.Zero)
                    FreeMemory(stringPtr);
            }
        }

        public void WithAllocatedString2(string text1, string text2, Encoding encoding, Action<IntPtr, IntPtr> action)
        {
            IntPtr stringPtr1 = IntPtr.Zero;
            IntPtr stringPtr2 = IntPtr.Zero;

            try
            {
                int byteCount1 = encoding.GetByteCount(text1) + encoding.GetByteCount("\0") + 10;
                int byteCount2 = encoding.GetByteCount(text2) + encoding.GetByteCount("\0") + 10;
                stringPtr1 = AllocateMemory(byteCount1);
                stringPtr2 = AllocateMemory(byteCount2);
                WriteString(stringPtr1, text1, encoding);
                WriteString(stringPtr2, text2, encoding);
                action(stringPtr1, stringPtr2);
            }
            finally
            {
                if (stringPtr1 != IntPtr.Zero)
                    FreeMemory(stringPtr1);

                if (stringPtr2 != IntPtr.Zero)
                    FreeMemory(stringPtr2);
            }
        }

        public T WithAllocatedString2<T>(string text1, string text2, Encoding encoding, Func<IntPtr, IntPtr, T> func)
        {
            IntPtr stringPtr1 = IntPtr.Zero;
            IntPtr stringPtr2 = IntPtr.Zero;

            try
            {
                int byteCount1 = encoding.GetByteCount(text1) + encoding.GetByteCount("\0") + 10;
                int byteCount2 = encoding.GetByteCount(text2) + encoding.GetByteCount("\0") + 10;
                stringPtr1 = AllocateMemory(byteCount1);
                stringPtr2 = AllocateMemory(byteCount2);
                WriteString(stringPtr1, text1, encoding);
                WriteString(stringPtr2, text2, encoding);
                return func(stringPtr1, stringPtr2);
            }
            finally
            {
                if (stringPtr1 != IntPtr.Zero)
                    FreeMemory(stringPtr1);

                if (stringPtr2 != IntPtr.Zero)
                    FreeMemory(stringPtr2);
            }
        }

        public void WithAllocatedStrings(Encoding encoding, Action<IntPtr[]> action, params string[] texts)
        {
            var ptrs = new IntPtr[texts.Length];

            try
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    int byteCount = encoding.GetByteCount(texts[i]) + encoding.GetByteCount("\0") + 10;
                    ptrs[i] = AllocateMemory(byteCount);
                    WriteString(ptrs[i], texts[i], encoding);
                }

                action(ptrs);
            }
            finally
            {
                foreach (var ptr in ptrs)
                {
                    if (ptr != IntPtr.Zero)
                        FreeMemory(ptr);
                }
            }
        }

        public T WithAllocatedStrings<T>(Encoding encoding, Func<IntPtr[], T> func, params string[] texts)
        {
            var ptrs = new IntPtr[texts.Length];

            try
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    int byteCount = encoding.GetByteCount(texts[i]) + encoding.GetByteCount("\0") + 10;
                    ptrs[i] = AllocateMemory(byteCount);
                    WriteString(ptrs[i], texts[i], encoding);
                }

                return func(ptrs);
            }
            finally
            {
                foreach (var ptr in ptrs)
                {
                    if (ptr != IntPtr.Zero)
                        FreeMemory(ptr);
                }
            }
        }
    }
}
