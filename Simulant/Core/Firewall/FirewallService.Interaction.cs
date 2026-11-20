using Simulant.Game.FFCS.Client.Game.Control;
using System;
using System.Linq;

namespace Simulant.Core.Firewall
{
    internal sealed partial class FirewallService
    {
        internal const string InteractionWarning =
            "场景交互拦截未能启用，可能与其他插件冲突。防火墙开启期间，请勿与旅馆门、雇员铃等实体交互。";

        // TargetSystem.InteractWithObject starts with mov [rsp+disp8], rbx (5 bytes).
        private static readonly byte[] InteractionEntryPrefix = { 0x48, 0x89, 0x5C, 0x24 };

        internal bool IsInteractionBlocked
        {
            get
            {
                lock (_gate)
                    return _interaction?.Enabled == true;
            }
        }

        private void InteractionHookEnableCore()
        {
            if (_interaction?.Enabled == true)
                return;

            var memory = CaptureMemory();
            try
            {
                var target = TargetSystem.InteractWithObjectFuncPtr;
                if (target == IntPtr.Zero)
                    throw new InvalidOperationException("未找到场景交互入口。");

                var state = CaptureHook(memory, memory.ProcessHandle, target, ReturnZeroPatch.Length);
                if (!state.Original.Take(InteractionEntryPrefix.Length).SequenceEqual(InteractionEntryPrefix))
                    throw new InvalidOperationException("场景交互入口已被修改。");

                // Hyperborea returns zero before starting an interaction that would wait on the server.
                _interaction = state;
                InstallReturnZeroHook(state);
            }
            catch (Exception error)
            {
                if (_interaction != null)
                {
                    // 写入可能只完成了一部分。恢复入口后才能继续开启收发包拦截。
                    try
                    {
                        RestoreEntry(_interaction);
                        _interaction = null;
                    }
                    catch (Exception restoreError)
                    {
                        throw new AggregateException("场景交互拦截写入失败，入口尚未恢复。", error, restoreError);
                    }
                }

                LogSafely(() => _host.LogWarning(error.Message + " " + InteractionWarning));
            }
        }

        private void RestoreInteractionWhenNetworkRestored()
        {
            var state = _interaction;
            if (state == null || _receive?.NeedsRestore == true || _send?.NeedsRestore == true)
                return;

            if ((!_processResetPending || IsCapturedProcessAvailable(state)) && !HasCapturedProcessExited(state))
            {
                try
                {
                    RestoreEntry(state);
                }
                catch (Exception) when (HasCapturedProcessExited(state))
                {
                    // The original process exited while restoring its entry.
                }
            }

            _interaction = null;
        }
    }
}
