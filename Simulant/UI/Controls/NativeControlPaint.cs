using System;
using System.Runtime.InteropServices;

namespace Simulant.UI
{
    internal static class NativeControlPaint
    {
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct PaintStruct
        {
            public IntPtr Dc;
            public int Erase, Left, Top, Right, Bottom, Restore, IncUpdate;
            public fixed byte Reserved[32];
        }

        [DllImport("user32.dll")]
        internal static extern IntPtr BeginPaint(IntPtr window, out PaintStruct paint);

        [DllImport("user32.dll")]
        internal static extern bool EndPaint(IntPtr window, ref PaintStruct paint);
    }
}
