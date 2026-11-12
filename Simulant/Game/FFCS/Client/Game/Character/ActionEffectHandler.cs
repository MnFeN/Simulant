using Simulant.ACT;
using Simulant.Game.FFCS.Client.Game.Object;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Simulant.Game.FFCS.Client.Game.Character
{
    /// <summary>FFCS 的技能结算接收入口；当前只封装一个目标、没有伤害效果的演出调用。</summary>
    public static class ActionEffectHandler
    {
        // 来源：FFXIVClientStructs/FFXIV/Client/Game/Character/ActionEffectHandler.cs。
        [StructLayout(LayoutKind.Explicit, Size = 0x28)]
        public struct Header
        {
            [FieldOffset(0x00)] public GameObjectId AnimationTargetId;
            [FieldOffset(0x08)] public uint ActionId;
            [FieldOffset(0x0C)] public uint GlobalSequence;
            [FieldOffset(0x10)] public float AnimationLock;
            [FieldOffset(0x14)] public uint BallistaEntityId;
            [FieldOffset(0x18)] public ushort SourceSequence;
            [FieldOffset(0x1A)] public ushort RotationInt;
            [FieldOffset(0x1C)] public ushort SpellId;
            [FieldOffset(0x1E)] public byte AnimationVariation;
            [FieldOffset(0x1F)] public byte ActionType;
            [FieldOffset(0x20)] public byte Flags;
            [FieldOffset(0x21)] public byte NumTargets;
        }

        [SigPattern("E8 * * * * 48 8B 8D ?? ?? ?? ?? 48 33 CC E8 ?? ?? ?? ?? 48 81 C4 00 05 00 00")]
        public static IntPtr ReceiveFuncPtr { get; set; }

        private static int _globalSequence;

        /// <summary>向原生入口提供完整主目标及空的八项效果。坐标采用游戏原生 XYZ（Y 为高度）。</summary>
        public static void ReceiveVisual(uint casterEntityId, IntPtr casterPtr, uint actionId, float heading,
            Vector3 targetPosition, GameObjectId targetId)
        {
            if (ReceiveFuncPtr == IntPtr.Zero)
                throw new InvalidOperationException("原生技能结算接收函数尚未定位。");
            if (casterPtr == IntPtr.Zero || casterEntityId == 0 || casterEntityId == 0xE0000000)
                throw new ArgumentException("技能演出的施法者身份或地址无效。", nameof(casterEntityId));
            if (targetId.ObjectId == 0 || targetId.ObjectId == 0xE0000000)
                throw new ArgumentException("技能演出的目标身份无效。", nameof(targetId));
            if (actionId == 0 || actionId > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(actionId), "技能编号必须能够写入演出技能字段。");
            if (!IsFinite(targetPosition.X) || !IsFinite(targetPosition.Y) || !IsFinite(targetPosition.Z))
                throw new ArgumentOutOfRangeException(nameof(targetPosition), "技能目标坐标必须为有限数。");

            var rotation = PacketCodec.EncodeUShortHeading(heading);
            var sequence = unchecked((uint)Interlocked.Increment(ref _globalSequence));
            if (sequence == 0) sequence = unchecked((uint)Interlocked.Increment(ref _globalSequence));
            var header = new Header
            {
                AnimationTargetId = targetId,
                ActionId = actionId,
                GlobalSequence = sequence,
                BallistaEntityId = 0xE0000000,
                RotationInt = rotation,
                SpellId = (ushort)actionId,
                ActionType = 1,
                NumTargets = 1,
            };

            var memory = NamazuInterop.Plugin.Memory;
            var buffer = IntPtr.Zero;
            try
            {
                // 一块内存：坐标 0x00，头部 0x10，八项效果 0x38，完整目标身份 0x78。
                buffer = memory.AllocateMemory(0x80);
                if (buffer == IntPtr.Zero) throw new InvalidOperationException("无法分配技能演出的远程参数内存。");
                if (memory.WriteBytes(buffer, new byte[0x80]) != 0x80)
                    throw new InvalidOperationException("技能演出的远程参数内存清零不完整。");
                memory.Write(buffer, targetPosition);
                memory.Write(buffer + 0x10, header);
                memory.Write(buffer + 0x78, targetId.Id);
                // 自定义结构体仅写入远程内存，绝不作为 GreyMagic 的按值调用参数。
                ReceiveFuncPtr.Call(casterEntityId, casterPtr, buffer, buffer + 0x10, buffer + 0x38, buffer + 0x78);
            }
            finally
            {
                if (buffer != IntPtr.Zero) memory.FreeMemory(buffer);
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
