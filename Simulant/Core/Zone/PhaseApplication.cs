using System;
using System.Threading;
using System.Threading.Tasks;

namespace Simulant.Core.Zone
{
    // 阶段替换和世界失效都会取消旧等待；恢复后仍在调用方的同步上下文执行。
    internal sealed class PhaseApplication
    {
        private readonly object _gate = new object();
        private readonly Func<int, CancellationToken, Task> _delay;
        private CancellationTokenSource _pending;

        internal PhaseApplication(Func<int, CancellationToken, Task> delay = null)
        {
            _delay = delay ?? Task.Delay;
        }

        internal void Cancel()
        {
            lock (_gate)
            {
                var pending = _pending;
                _pending = null;
                pending?.Cancel();
            }
        }

        internal async Task<bool> ApplyAsync(Action<CancellationToken> prepare,
            Action<CancellationToken> apply, bool waitForZoneInit)
        {
            var source = new CancellationTokenSource();
            lock (_gate)
            {
                var previous = _pending;
                _pending = source;
                previous?.Cancel();
            }
            try
            {
                var token = source.Token;
                token.ThrowIfCancellationRequested();
                prepare(token);
                if (waitForZoneInit)
                    await _delay(3000, token);
                token.ThrowIfCancellationRequested();
                apply(token);
                token.ThrowIfCancellationRequested();
                if (waitForZoneInit)
                {
                    await _delay(2000, token);
                    token.ThrowIfCancellationRequested();
                    apply(token);
                }
                token.ThrowIfCancellationRequested();
                return true;
            }
            catch (OperationCanceledException) when (source.IsCancellationRequested)
            {
                return false;
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_pending, source)) _pending = null;
                    source.Dispose();
                }
            }
        }
    }
}
