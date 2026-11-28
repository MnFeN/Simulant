using Simulant.Game.FFCS.Client.Game.Object;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Simulant.Game.FFCS.Client.Game.Character
{
    // 原生 Client::Game::Character::ActionEffectHandler，无基类，大小 0xF00。
    // https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Game/Character/ActionEffectHandler.cs
    public struct ActionEffectHandler : IMemoryObject
    {
        public IntPtr Ptr { get; set; }

        /// <summary>
        /// A single effect (damage, heal, status, miss, etc) an action had on a single target.
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = 8)]
        public struct Effect
        {
            [FieldOffset(0)] public byte Type;
            [FieldOffset(1)] public byte Param0;
            [FieldOffset(2)] public byte Param1;
            [FieldOffset(3)] public byte Param2;
            [FieldOffset(4)] public byte Param3;
            [FieldOffset(5)] public byte Param4;
            [FieldOffset(6)] public ushort Value;
        }

        /// <summary>
        /// A full set of effects an action had on a single target.
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = 0x40)]
        public unsafe struct TargetEffects
        {
            // C# 固定缓冲区以字节保存连续的八个 Effect。
            [FieldOffset(0)] private fixed byte _effects[0x40];

            /// <summary>项目封送方法，将单个目标的效果复制进定长布局，其余槽位保留为零。</summary>
            internal static TargetEffects FromEffects(ReadOnlySpan<Effect> effects)
            {
                if (effects.Length > 8)
                    throw new ArgumentException("原生技能结算每个目标最多有八个效果槽。", nameof(effects));

                TargetEffects result = default;
                effects.CopyTo(new Span<Effect>(result._effects, 8));
                return result;
            }
        }

        /// <summary>
        /// Global result of a single action. Used in combination with target data: a list of GameObjectId's and sets of Effect's.
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = 0x28)]
        public struct Header
        {
            [FieldOffset(0x00)] public GameObjectId AnimationTargetId; // 'primary' target of the action; animation is played on this object
            [FieldOffset(0x08)] public uint ActionId;
            [FieldOffset(0x0C)] public uint GlobalSequence; // unique id of the action, monotonously increasing as the server is running
            [FieldOffset(0x10)] public float AnimationLock; // caster's animation lock is set to this value, unless this action was not caster-initiated and force-set flag is not set
            [FieldOffset(0x14)] public uint BallistaEntityId; // for Artillery actions - id of the cannon that was used for the action
            [FieldOffset(0x18)] public ushort SourceSequence; // 0 if action was not initiated by a client, otherwise monotonously increasing as the client initiates actions
            [FieldOffset(0x1A)] public ushort RotationInt; // quantized rotation: 0 -> -pi, 65535 -> pi
            [FieldOffset(0x1C)] public ushort SpellId;
            [FieldOffset(0x1E)] public byte AnimationVariation;
            [FieldOffset(0x1F)] public byte ActionType;
            [FieldOffset(0x20)] public byte Flags;
            [FieldOffset(0x21)] public byte NumTargets;
        }

        [SigPattern("E8 * * * * 48 8B 8D ?? ?? ?? ?? 48 33 CC E8 ?? ?? ?? ?? 48 81 C4 00 05 00 00")]
        public static IntPtr ReceiveFuncPtr { get; set; }

        /// <summary>
        /// The main entry point, called when receiving ActionEffectN server packets.
        /// </summary>
        /// <param name="casterEntityId">Caster entity id.</param>
        /// <param name="casterPtr">Caster pointer.</param>
        /// <param name="targetPos">Only relevant for area-targeted actions.</param>
        /// <param name="header">Details of the action.</param>
        /// <param name="effects">Per-target effect list array. Contains header->NumTargets elements.</param>
        /// <param name="targetEntityIds">Per-target effect id array. Contains header->NumTargets elements.</param>
        /// <remarks>项目封送按同一顺序复制效果块和目标 ID。国服回放 ActionEffect1/8/16/24/32 的分发入口传入这两段数组，接收函数按 NumTargets 逐项读取。</remarks>
        internal static void Receive(uint casterEntityId, IntPtr casterPtr, in Vector3 targetPos, in Header header,
            ReadOnlySpan<TargetEffects> effects, ReadOnlySpan<GameObjectId> targetEntityIds)
        {
            if (header.NumTargets > MaxTargets || effects.Length != header.NumTargets || targetEntityIds.Length != header.NumTargets)
                throw new ArgumentException("技能结算的目标数和效果块必须一致，最多支持 32 个目标。");

            var effectInput = effects.IsEmpty
                ? RemoteInput.Struct(default(TargetEffects)) : RemoteInput.FromBytes(MemoryMarshal.AsBytes(effects).ToArray());
            var targetInput = targetEntityIds.IsEmpty
                ? RemoteInput.Struct(default(GameObjectId)) : RemoteInput.FromBytes(MemoryMarshal.AsBytes(targetEntityIds).ToArray());
            ReceiveFuncPtr.Call(casterEntityId, casterPtr, RemoteInput.Struct(targetPos), RemoteInput.Struct(header),
                effectInput, targetInput);
        }

        // 国服 ActionEffect32 负载和原生接收队列均以 32 为容量上限。
        internal const int MaxTargets = 32;
    }
}
