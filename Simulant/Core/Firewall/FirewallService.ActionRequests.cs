using Simulant.Game;
using Simulant.Game.FFCS.Client.Game;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Simulant.Core.Firewall
{
    internal sealed partial class FirewallService
    {
        // 从 AddressStore 的 opcode 写入签名往后读，紧跟的 64 位写入给出各描述符的长度。
        // 从 ZoneClient.SendPacket 追到 Hook 所在的区域发送函数，可以复核它以描述符 +0x08 的
        // 64 位长度建包，并把 +0x20 开始的“长度减 0x10”字节复制到 IPC 头之后。
        private const byte ActionRequestLength = 0x30;
        private const byte GroundActionRequestLength = 0x28;
        private const byte TargetedGroundActionRequestLength = 0x38;
        private const byte ExecuteCommandLength = 0x30;
        private const byte CancelCastCommand = 0x69;

        // 仿生石定义的共享内存布局。远程分配开头是跳板和采集代码，后面是队列头与 64 个槽位。
        // 游标和计数都是 32 位。头部 +0x00 为已提交的写游标，其余字段见下列常量。
        // 槽位前两个 DWORD 保存 opcode、描述符长度，随后保存对应长度的请求正文。
        private const int ActionRequestQueueOffset = 0x100;
        private const int QueueReadOffset = 4;
        private const int QueueWriterLockOffset = 8;
        private const int QueueDroppedOffset = 12;
        private const int QueueHeaderSize = 16;
        private const int QueueSlotShift = 6;
        private const int QueueSlotSize = 1 << QueueSlotShift;
        private const int QueueSlots = 64;
        private const int SendCaveSize = ActionRequestQueueOffset + QueueHeaderSize + QueueSlots * QueueSlotSize;

        private void AppendActionRequestCapture(List<byte> code, HookState state, ushort heartbeatOpcode)
        {
            var matches = new List<int>(4);
            AppendScannedRequestMatch(code, state, AddressStore.ActionRequestOpcodeLocatePtr,
                ActionRequestLength, heartbeatOpcode, "普通技能请求", matches);
            AppendScannedRequestMatch(code, state, AddressStore.GroundActionRequestOpcodeLocatePtr,
                GroundActionRequestLength, heartbeatOpcode, "地面技能请求", matches);
            AppendScannedRequestMatch(code, state, AddressStore.TargetedGroundActionRequestOpcodeLocatePtr,
                TargetedGroundActionRequestLength, heartbeatOpcode, "带目标地面技能请求", matches);
            state.CancelCastOpcode = AppendScannedRequestMatch(code, state, AddressStore.ExecuteCommandOpcodeLocatePtr,
                ExecuteCommandLength, heartbeatOpcode, "取消咏唱请求", matches, CancelCastCommand);
            code.AddRange(new byte[] { 0x31, 0xC0, 0xC3 }); // xor eax,eax; ret
            if (matches.Count == 0)
                return;

            foreach (var match in matches)
                PatchShortJump(code, match);

            // 按 Windows x64 调用约定，拦截分支用 rdx 读参数，只修改易失寄存器 rax、r8–r11 和标志位。
            // 这里不建栈帧，不调用其他函数，最后直接向原调用者返回区域发送函数的失败值 0。
            var queue = state.Cave + ActionRequestQueueOffset;
            code.AddRange(new byte[] { 0x49, 0xBB }); // mov r11,队列地址
            code.AddRange(BitConverter.GetBytes(queue.ToInt64()));

            // cmpxchg 比较 eax 的 0 和写锁，成功才把 r10d 的 1 写入锁。忙时直接计入丢弃。
            code.AddRange(new byte[]
            {
                0x31, 0xC0,                         // xor eax,eax
                0x41, 0xBA, 1, 0, 0, 0,             // mov r10d,1
                0xF0, 0x45, 0x0F, 0xB1, 0x53, QueueWriterLockOffset // lock cmpxchg [r11+写锁],r10d
            });
            var busy = AppendShortJump(code, 0x75); // jne 忙碌

            // ACT 更新读游标，游戏侧在写锁内读取它。无符号的写减读等于未消费槽位数，支持游标回绕。
            code.AddRange(new byte[]
            {
                0x41, 0x8B, 0x03,                   // mov eax,[r11]；已提交的写游标
                0x41, 0x89, 0xC0,                   // mov r8d,eax
                0x45, 0x2B, 0x43, QueueReadOffset,   // sub r8d,[r11+读游标]
                0x41, 0x83, 0xF8, QueueSlots         // cmp r8d,槽位数
            });
            var full = AppendShortJump(code, 0x73); // jae 已满

            // 以写游标的低 6 位选槽，左移 6 位得到字节偏移。r8 指向槽位，eax 保留待发布的写游标。
            code.AddRange(new byte[]
            {
                0x41, 0x89, 0xC0,                   // mov r8d,eax
                0x41, 0x83, 0xE0, QueueSlots - 1,    // and r8d,槽位掩码
                0x41, 0xC1, 0xE0, QueueSlotShift,    // shl r8d,槽位大小的位移
                0x4F, 0x8D, 0x44, 0x03, QueueHeaderSize, // lea r8,[r11+r8+队列头大小]
                0x44, 0x8B, 0x0A,                   // mov r9d,[rdx]；opcode
                0x45, 0x89, 0x08,                   // mov [r8],r9d
                0x44, 0x8B, 0x4A, 0x08,             // mov r9d,[rdx+8]；原生长度
                0x45, 0x89, 0x48, 0x04              // mov [r8+4],r9d
            });

            // 匹配分支已验证完整 64 位长度。按正文的 24、32、40 字节边界逐段复制，避免越过短包。
            AppendRequestQwordCopy(code, 0);
            AppendRequestQwordCopy(code, 8);
            AppendRequestQwordCopy(code, 16);
            code.AddRange(new byte[] { 0x80, 0x7A, 0x08, GroundActionRequestLength }); // cmp byte ptr [rdx+8],地面请求长度
            var publishGround = AppendShortJump(code, 0x74); // je 发布
            AppendRequestQwordCopy(code, 24);
            code.AddRange(new byte[] { 0x80, 0x7A, 0x08, TargetedGroundActionRequestLength }); // cmp byte ptr [rdx+8],带目标地面请求长度
            var publishTarget = AppendShortJump(code, 0x75); // jne 发布
            AppendRequestQwordCopy(code, 32);

            // 槽位写完再发布写游标，再释放写锁。ACT 看到新游标时该槽已写完整。
            PatchShortJump(code, publishGround);
            PatchShortJump(code, publishTarget);
            code.AddRange(new byte[]
            {
                0xFF, 0xC0,                         // inc eax
                0x41, 0x89, 0x03                    // mov [r11],eax；正文完整后发布写游标
            });
            var release = AppendShortJump(code, 0xEB); // jmp 释放写锁
            // 队列满时当前线程持有写锁，需要释放。抢锁失败的路径只增加同一个原子丢弃计数。
            PatchShortJump(code, full);
            code.AddRange(new byte[] { 0xF0, 0x41, 0xFF, 0x43, QueueDroppedOffset }); // lock inc [r11+丢弃数]
            PatchShortJump(code, release);
            code.AddRange(new byte[]
            {
                0x31, 0xC0,                         // xor eax,eax
                0x41, 0x89, 0x43, QueueWriterLockOffset, // mov [r11+写锁],eax
                0xC3                                // ret
            });
            PatchShortJump(code, busy);
            code.AddRange(new byte[]
            {
                0xF0, 0x41, 0xFF, 0x43, QueueDroppedOffset, // lock inc [r11+丢弃数]
                0x31, 0xC0, 0xC3                   // xor eax,eax; ret
            });
            state.CapturesActionRequests = true;
        }

        private ushort AppendScannedRequestMatch(List<byte> code, HookState state, IntPtr instruction,
            byte length, ushort heartbeatOpcode, string name, List<int> matches, byte? command = null)
        {
            ushort opcode;
            try
            {
                // 和心跳定位一样，从 C7 /0 指令的 imm32 读取低 16 位，使用安装 Hook 时捕获的游戏进程。
                opcode = instruction == IntPtr.Zero ? (ushort)0 : state.Memory.Read<ushort>(instruction + 4);
            }
            catch (Exception error)
            {
                LogSafely(() => _host.LogWarning($"读取{name} opcode 失败，已跳过该类采集：{error.Message}"));
                return 0;
            }

            if (opcode == 0 || opcode == heartbeatOpcode)
            {
                LogSafely(() => _host.LogWarning($"{name} opcode 无效，已跳过该类采集。"));
                return 0;
            }

            matches.Add(AppendRequestMatch(code, opcode, length, command));
            LogSafely(() => _host.LogVerbose($"{name} opcode 为 0x{opcode:X4}。"));
            return opcode;
        }

        private static int AppendRequestMatch(List<byte> code, ushort opcode, byte length, byte? command)
        {
            // opcode 决定请求族，描述符长度保护读取范围。每个匹配分支跳到同一段队列写入代码。
            code.AddRange(new byte[] { 0x66, 0x81, 0x3A }); // cmp word ptr [rdx],opcode
            code.AddRange(BitConverter.GetBytes(opcode));
            var next = AppendShortJump(code, 0x75); // jne 下一个请求类型
            code.AddRange(new byte[] { 0x48, 0x83, 0x7A, 0x08, length }); // cmp qword ptr [rdx+8],长度
            var wrongLength = -1;
            if (command.HasValue)
            {
                wrongLength = AppendShortJump(code, 0x75); // jne 下一个请求类型
                // 沿 Hotbar.CancelCast 到 GameMain.ExecuteCommand，首个正文 DWORD 保存命令号。
                // 0x69 可由 imm8 符号扩展成 DWORD，因此这里可以直接比较完整的命令字段。
                code.AddRange(new byte[] { 0x83, 0x7A, 0x20, command.Value }); // cmp dword ptr [rdx+20h],命令
            }
            var match = AppendShortJump(code, 0x74); // je 采集
            PatchShortJump(code, next);
            if (wrongLength >= 0)
                PatchShortJump(code, wrongLength);
            return match;
        }

        private static void AppendRequestQwordCopy(List<byte> code, byte offset)
        {
            code.AddRange(new byte[]
            {
                0x4C, 0x8B, 0x4A, (byte)(0x20 + offset), // mov r9,[rdx+正文偏移]
                0x4D, 0x89, 0x48, (byte)(8 + offset)     // mov [r8+槽位偏移],r9
            });
        }

        private static int AppendShortJump(List<byte> code, byte opcode)
        {
            code.Add(opcode);
            var offset = code.Count;
            code.Add(0);
            return offset;
        }

        // rel8 从跳转的下一条指令算起。超出 sbyte 范围时在安装前报错。
        private static void PatchShortJump(List<byte> code, int offset)
            => code[offset] = unchecked((byte)checked((sbyte)(code.Count - offset - 1)));

        internal ActionRequest[] DrainActionRequests(out uint droppedCount)
        {
            lock (_gate)
            {
                droppedCount = 0;
                var state = _send;
                if (state?.Enabled != true || !state.CapturesActionRequests || _receive?.Enabled != true ||
                    !ReferenceEquals(state.Memory, GameInterop.Connection?.Memory) || !IsCapturedProcessAvailable(state))
                    return Array.Empty<ActionRequest>();

                var queue = state.Cave + ActionRequestQueueOffset;
                // 先快照已提交的写游标，再读它之前的槽位。读完才写回读游标，游戏侧此时才可复用这些槽。
                var header = state.Memory.ReadBytes(queue, QueueHeaderSize);
                var write = BitConverter.ToUInt32(header, 0);
                var dropped = BitConverter.ToUInt32(header, QueueDroppedOffset);
                var pending = unchecked(write - state.ActionRequestsRead);
                if (pending > QueueSlots)
                    return Array.Empty<ActionRequest>();

                var requests = Array.Empty<ActionRequest>();
                if (pending != 0)
                {
                    requests = new ActionRequest[(int)pending];
                    var index = (int)(state.ActionRequestsRead & (QueueSlots - 1));
                    var first = Math.Min(requests.Length, QueueSlots - index);
                    ReadActionRequestSlots(state, queue, index, first, requests, 0);
                    if (first < requests.Length)
                        ReadActionRequestSlots(state, queue, 0, requests.Length - first, requests, first);

                    WriteExact(state, queue + QueueReadOffset, BitConverter.GetBytes(write));
                    state.ActionRequestsRead = write;
                }
                droppedCount = unchecked(dropped - state.ActionRequestsDropped);
                state.ActionRequestsDropped = dropped;
                return requests;
            }
        }

        private static void ReadActionRequestSlots(HookState state, IntPtr queue, int index, int count,
            ActionRequest[] requests, int start)
        {
            var slots = state.Memory.ReadBytes(queue + QueueHeaderSize + index * QueueSlotSize, count * QueueSlotSize);
            for (var i = 0; i < count; ++i)
                requests[start + i] = ReadActionRequest(slots, i * QueueSlotSize, state.CancelCastOpcode);
        }

        private static ActionRequest ReadActionRequest(byte[] data, int slot, ushort cancelCastOpcode)
        {
            var opcode = BitConverter.ToUInt16(data, slot);
            var length = BitConverter.ToUInt32(data, slot + 4);
            var body = slot + 8;
            if (opcode == cancelCastOpcode && length == ExecuteCommandLength && BitConverter.ToUInt32(data, body) == CancelCastCommand)
                return new ActionRequest(opcode, length);

            // 国服 2026.11.03.0000.0000：
            // 从 ActionManager.UseActionLocation 跟踪 GetAdjustedCastTime 的 outOptProc，
            // 三种构包函数都把这个字节复制到正文 +4。
            // 骑士的安魂祈祷与神圣魔法效果提高分支会先写入对应枚举值，再返回 0 毫秒。
            // 正文 +5 起是动作类型、请求序号和两项压缩朝向。
            var action = BitConverter.ToUInt32(data, body);
            var type = (ActionType)data[body + 5];
            var sequence = BitConverter.ToUInt16(data, body + 6);
            var rotation = PacketCodec.DecodeUShortHeading(BitConverter.ToUInt16(data, body + 8));
            var facing = PacketCodec.DecodeUShortHeading(BitConverter.ToUInt16(data, body + 10));
            ActionRequestKind kind;
            ulong? targetId = null;
            Vector3? groundPosition = null;
            uint? extraParam = null;
            // 三种技能请求的长度各自唯一，用它选择正文布局。
            switch (length)
            {
                case GroundActionRequestLength:
                    kind = ActionRequestKind.Ground;
                    groundPosition = ReadRequestPosition(data, body + 12);
                    break;
                case TargetedGroundActionRequestLength:
                    kind = ActionRequestKind.TargetedGround;
                    targetId = BitConverter.ToUInt64(data, body + 16);
                    groundPosition = ReadRequestPosition(data, body + 24);
                    break;
                default:
                    kind = ActionRequestKind.Normal;
                    targetId = BitConverter.ToUInt64(data, body + 16);
                    extraParam = BitConverter.ToUInt32(data, body + 24);
                    break;
            }

            return new ActionRequest(kind, opcode, length, action, type, sequence, data[body + 4], rotation, facing,
                targetId, groundPosition, extraParam);
        }

        private static Vector3 ReadRequestPosition(byte[] data, int offset)
        {
            // 构包函数依次写入原生 X、高度、Z。将高度放到 ACT 坐标的 Z 分量。
            var x = BitConverter.ToSingle(data, offset);
            var height = BitConverter.ToSingle(data, offset + 4);
            var y = BitConverter.ToSingle(data, offset + 8);
            return new Vector3(x, y, height);
        }
    }
}
