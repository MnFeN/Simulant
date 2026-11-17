using Simulant.Game.FFCS.Client.Game.Object;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.Game.Character
{
    /// <summary>原生技能结算接收入口和参数布局。</summary>
    public static class ActionEffectHandler
    {
        // 来源：FFXIVClientStructs/FFXIV/Client/Game/Character/ActionEffectHandler.cs。
        [StructLayout(LayoutKind.Explicit, Size = 0x28)]
        public struct Header
        {
            [FieldOffset(0x00)]
            public GameObjectId AnimationTargetId;
            [FieldOffset(0x08)]
            public uint ActionId;
            [FieldOffset(0x0C)]
            public uint GlobalSequence;
            [FieldOffset(0x10)]
            public float AnimationLock;
            [FieldOffset(0x14)]
            public uint BallistaEntityId;
            [FieldOffset(0x18)]
            public ushort SourceSequence;
            [FieldOffset(0x1A)]
            public ushort RotationInt;
            [FieldOffset(0x1C)]
            public ushort SpellId;
            [FieldOffset(0x1E)]
            public byte AnimationVariation;
            [FieldOffset(0x1F)]
            public byte ActionType;
            [FieldOffset(0x20)]
            public byte Flags;
            [FieldOffset(0x21)]
            public byte NumTargets;
        }

        [SigPattern("E8 * * * * 48 8B 8D ?? ?? ?? ?? 48 33 CC E8 ?? ?? ?? ?? 48 81 C4 00 05 00 00")]
        public static IntPtr ReceiveFuncPtr { get; set; }

        internal static void Receive(uint casterEntityId, IntPtr casterPtr, Vector3 targetPosition, Header header,
            GameObjectId targetId, ulong[] targetEffects = null)
        {
            if (header.NumTargets > 1)
                throw new ArgumentOutOfRangeException(nameof(header));
            if (targetEffects != null && targetEffects.Length != 8)
                throw new ArgumentException("原生技能结算每个目标有八个效果槽。", nameof(targetEffects));

            var memory = GameInterop.Connection.Memory;
            var buffer = IntPtr.Zero;

            try
            {
                // 一块内存：坐标 0x00，头部 0x10，八项效果 0x38，完整目标身份 0x78。
                buffer = memory.AllocateMemory(0x80);

                if (buffer == IntPtr.Zero)
                    throw new InvalidOperationException("无法分配技能结算的远程参数内存。");

                var bytes = new byte[0x80];
                if (targetEffects != null)
                    Buffer.BlockCopy(targetEffects, 0, bytes, 0x38, targetEffects.Length * sizeof(ulong));

                if (memory.WriteBytes(buffer, bytes) != bytes.Length)
                    throw new InvalidOperationException("技能结算的远程参数内存初始化不完整。");

                memory.Write(buffer, targetPosition);
                memory.Write(buffer + 0x10, header);
                if (header.NumTargets != 0)
                    memory.Write(buffer + 0x78, targetId.Id);
                // 自定义结构体仅写入远程内存，绝不作为 GreyMagic 的按值调用参数。
                ActionEffectHandler.ReceiveFuncPtr.Call(casterEntityId, casterPtr, buffer, buffer + 0x10, buffer + 0x38, buffer + 0x78);
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                    memory.FreeMemory(buffer);
            }
        }
    }
}
