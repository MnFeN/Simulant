using System;
using System.Linq;
using System.Runtime.CompilerServices;
using static Simulant.Game.AddressStore;

namespace Simulant.Game
{
    public static class GameFunctions
    {

        #region Common
        internal static void CheckStatus([CallerMemberName] string callerNameAuto = null)
            => CheckStatusCore(callerNameAuto);
        internal static void CheckStatus(IntPtr ptr1, [CallerMemberName] string callerNameAuto = null)
            => CheckStatusCore(callerNameAuto, ptr1);
        internal static void CheckStatus(IntPtr ptr1, IntPtr ptr2, [CallerMemberName] string callerNameAuto = null)
            => CheckStatusCore(callerNameAuto, ptr1, ptr2);
        internal static void CheckStatus(IntPtr ptr1, IntPtr ptr2, IntPtr ptr3, [CallerMemberName] string callerNameAuto = null)
            => CheckStatusCore(callerNameAuto, ptr1, ptr2, ptr3);
        internal static void CheckStatus(IntPtr ptr1, IntPtr ptr2, IntPtr ptr3, IntPtr ptr4, [CallerMemberName] string callerNameAuto = null)
            => CheckStatusCore(callerNameAuto, ptr1, ptr2, ptr3, ptr4);
        private static void CheckStatusCore([CallerMemberName] string callerNameAuto = null, params IntPtr[] ptrs)
        {
            if (!GameInterop.IsReady || GameInterop.Connection?.Memory == null)
                throw new InvalidOperationException($"{callerNameAuto} 函数执行时 GameInterop 未就绪。");

            if (ptrs.Any(ptr => ptr == IntPtr.Zero))
                throw new InvalidOperationException($"{callerNameAuto} 函数执行所需的指针为空。");
        }

        #endregion Common
    }
}
