using Simulant.Game.FFCS.Client.Game.Control;
using System;
using System.Linq;

namespace Simulant.Core.Firewall
{
    internal sealed partial class FirewallService
    {
        // TargetSystem.InteractWithObject starts with mov [rsp+disp8], rbx (5 bytes).
        private static readonly byte[] InteractionEntryPrefix = { 0x48, 0x89, 0x5C, 0x24 };

        private void InteractionHookEnableCore()
        {
            if (_interaction?.Enabled == true)
                return;

            var memory = CaptureMemory();
            var target = TargetSystem.InteractWithObjectFuncPtr;
            if (target == IntPtr.Zero)
                throw new InvalidOperationException("未找到场景交互入口，无法启用防火墙。");

            var state = CaptureHook(memory, memory.ProcessHandle, target, ReturnZeroPatch.Length);
            if (!state.Original.Take(InteractionEntryPrefix.Length).SequenceEqual(InteractionEntryPrefix))
                throw new InvalidOperationException("场景交互入口已被修改，无法启用防火墙。");

            // Hyperborea returns zero before starting an interaction that would wait on the server.
            _interaction = state;
            InstallReturnZeroHook(state);
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
