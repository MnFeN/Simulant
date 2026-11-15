using Advanced_Combat_Tracker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simulant.ACT
{
    internal static class ActInterop
    {
        /// <summary>
        /// 添加ACT日志行。
        /// </summary>
        /// <param name="type"></param>
        /// <param name="message"></param>
        public static void AddLogLine(string type, string message)
        {
            var logline = FormattableString.Invariant($"00|{DateTime.Now:O}|0|{type}:{message}|");
            ActGlobals.oFormActMain.ParseRawLogLine(isImport: false, DateTime.Now, logline ?? "");
        }
    }
}
