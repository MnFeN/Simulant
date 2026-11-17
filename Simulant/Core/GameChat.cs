using Simulant.Game;
using Simulant.Game.FFCS.Client.System.Framework;
using Simulant.Game.FFCS.Client.System.String;
using Simulant.Game.FFCS.Client.UI;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Simulant.Core
{
    internal static class GameChat
    {
        public static void SendEcho(string text, bool playSound = false)
            => SendCommand((playSound ? "/e <se.11>\n" : "/e ") + text);

        public static void SendCommand(string command)
        {
            if (string.IsNullOrEmpty(command) || command[0] != '/' || command.IndexOf('\0') >= 0)
                throw new ArgumentException("聊天指令必须以 / 开头，且不得含空字符。", nameof(command));

            GameFunctions.CheckStatus(RaptureShellModule.ProcessChatBoxFuncPtr);
            var ui = Framework.Instance.UIModule;
            var shell = ui.ShellModule.ThrowIfNull();
            var memory = GameInterop.Connection.Memory;
            var text = Encoding.UTF8.GetBytes(command + '\0');
            var size = Marshal.SizeOf<Utf8String>();
            var capacity = Math.Max(text.Length, 0x40);
            var buffer = memory.AllocateMemory(size + capacity);
            if (buffer == IntPtr.Zero)
                throw new InvalidOperationException("无法分配聊天指令缓冲区。");

            try
            {
                var textPtr = buffer + size;
                if (memory.WriteBytes(textPtr, text) != text.Length)
                    throw new InvalidOperationException("未能完整写入聊天指令。");
                memory.Write(buffer, new Utf8String
                {
                    Text = textPtr,
                    Capacity = capacity,
                    BytesUsed = text.Length,
                    Length = text.Length - 1
                });
                shell.ProcessChatBox(buffer, ui);
            }
            finally
            {
                memory.FreeMemory(buffer);
            }
        }
    }
}
