using System;
using System.Runtime.InteropServices;

namespace Simulant.Utils
{
    internal static class WindowFocus
    {
        internal static bool IsProcessForeground(int processId)
        {
            if (processId <= 0)
                return false;
            GetWindowThreadProcessId(GetForegroundWindow(), out var foregroundProcessId);
            return foregroundProcessId == processId;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);
    }
}
