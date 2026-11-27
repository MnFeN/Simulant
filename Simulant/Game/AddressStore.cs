using System;

namespace Simulant.Game
{
    internal static class AddressStore
    {
        // Hyperborea
        [SigPattern("48 89 5C 24 ?? 48 89 74 24 ?? 4C 89 64 24 ?? 55 41 56 41 57 48 8B EC 48 83 EC 70")]
        public static IntPtr OnSendPacketFuncPtr { get; set; }

        // Hyperborea
        //                      ↓ ↓ ↓ ↓
        [SigPattern("C7 44 24 ? ? ? ? ? 48 F7 F1")]
        public static IntPtr HeartBeatOpcodeLocatePtr { get; set; }
        public static ushort HeartBeatOpcode => HeartBeatOpcodeLocatePtr.Read<ushort>(0x4);

        // 从 ActionManager.UseActionLocation 沿普通动作分支进入构包函数，跟踪其交给区域发送包装的缓冲区。
        // 确认缓冲头写入 opcode 和长度，正文 +0x10 写完整目标身份，+0x18 写 extraParam。
        // 三条签名都从 mov dword ptr [rsp+20h], imm32 开始，opcode 是指令 +4 的低 16 位，
        // 高两字节为零，随后用长度写入和正文存储指令区分三个构包函数。
        //                       ↓ ↓ ↓  ↓
        [SigPattern("C7 44 24 20 ? ? 00 00 45 33 C0 48 C7 44 24 28 30 00 00 00 89 ? 24 40 0F B6 ?")]
        internal static IntPtr ActionRequestOpcodeLocatePtr { get; set; }

        // 从 ActionManager.UseActionLocation 沿地面选点分支进入构包函数。
        // 核对 location 的三个 float 按原生 XYZ 写入正文 +0x0C，opcode 和长度写入之后是动作类型、请求序号的存储。
        //                       ↓ ↓ ↓  ↓
        [SigPattern("C7 44 24 20 ? ? 00 00 48 C7 44 24 28 28 00 00 00 89 ? 24 40 40 88 ? 24 45 66 89 ? 24 46")]
        internal static IntPtr GroundActionRequestOpcodeLocatePtr { get; set; }

        // 从 ActionManager.UseActionLocation 检查末尾参数 a7 的非零分支，沿调用进入构包函数。
        // 核对解析后的 GameObjectId 按 64 位写在正文 +0x10，+0x18 紧跟 location 的三个 float。
        //                       ↓ ↓ ↓  ↓
        [SigPattern("C7 44 24 20 ? ? 00 00 48 C7 44 24 28 38 00 00 00 89 ? 24 40 40 88 ? 24 45 48 89 ? 24 50")]
        internal static IntPtr TargetedGroundActionRequestOpcodeLocatePtr { get; set; }

        // 从 GameMain.ExecuteCommand 找到写入区域发送描述符的 opcode。
        // 紧接着写入长度和正文，依次保存 command 与四个参数，可与 Hotbar.CancelCast 的调用参数互证。
        //                       ↓ ↓ ↓  ↓
        [SigPattern("C7 44 24 20 ? ? 00 00 48 C7 44 24 28 30 00 00 00 89 6C 24 40 89 74 24 44 89 7C 24 48 89 5C 24 4C 48 C7 44 24 58 00 00 00 00")]
        internal static IntPtr ExecuteCommandOpcodeLocatePtr { get; set; }

    }

}
