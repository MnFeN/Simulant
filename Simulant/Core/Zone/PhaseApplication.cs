using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        internal Task<bool> ApplyAsync(Action<CancellationToken> prepare,
            Action<CancellationToken> apply, Func<CancellationToken, bool> isReady, Func<CancellationToken, Task> complete = null)
            => ApplyAsync(async token =>
        {
            prepare(token);
            await WaitUntilAsync(isReady, token, "地图就绪");
            token.ThrowIfCancellationRequested();
            apply(token);
            if (complete != null)
                await complete(token);
        });

        internal async Task ApplyMapEffectsAsync(IReadOnlyList<ushort?> flags, Func<uint, ushort, bool> play, CancellationToken token)
        {
            var pending = new List<(uint Slot, ushort Mask)>();
            for (var slot = 0; slot < flags.Count; slot++)
                if (flags[slot] is ushort mask)
                    pending.Add(((uint)slot, mask));

            try
            {
                await WaitUntilAsync(current =>
                {
                    pending.RemoveAll(effect =>
                    {
                        current.ThrowIfCancellationRequested();
                        return play(effect.Slot, effect.Mask);
                    });
                    return pending.Count == 0;
                }, token, "场地动画就绪");
            }
            catch (TimeoutException error)
            {
                throw new TimeoutException($"{error.Message}尚未播放的槽位：{string.Join("、", pending.ConvertAll(effect => effect.Slot))}。", error);
            }
        }

        internal async Task WaitUntilAsync(Func<CancellationToken, bool> isReady,
            CancellationToken token, string stage)
        {
            var clock = Stopwatch.StartNew();

            while (true)
            {
                token.ThrowIfCancellationRequested();

                if (isReady(token))
                    return;

                if (clock.Elapsed.TotalSeconds >= 30)
                    throw new TimeoutException($"等待{stage}超过 30 秒。");

                await _delay(50, token);
            }
        }

        // 就绪条件决定执行时机的操作，也复用同一套替换与取消处理。
        internal async Task<bool> ApplyAsync(Func<CancellationToken, Task> apply)
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
                await apply(token);
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
                    if (ReferenceEquals(_pending, source))
                        _pending = null;

                    source.Dispose();
                }
            }
        }
    }
}
