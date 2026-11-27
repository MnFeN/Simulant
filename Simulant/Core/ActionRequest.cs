using Simulant.Game.FFCS.Client.Game;
using System;
using System.Numerics;
using CastTimeProc = Simulant.Game.FFCS.Client.Game.ActionManager.CastTimeProc;

namespace Simulant.Core
{
    internal enum ActionRequestKind
    {
        Normal,
        Ground,
        TargetedGround,
        CancelCast
    }

    /// <summary>客户端提交的一次技能请求或主动取消。朝向使用弧度，落点使用 ACT 坐标。</summary>
    internal sealed class ActionRequest
    {
        /// <summary>请求的布局类型，分为普通、地面、带目标地面和主动取消。</summary>
        internal ActionRequestKind Kind { get; }

        /// <summary>请求描述符开头记录的 16 位网络 opcode。</summary>
        private ushort Opcode { get; }

        /// <summary>发送描述符中的包长，单位为字节，包含 16 字节 IPC 头和请求正文。</summary>
        private uint DescriptorLength { get; }

        /// <summary>客户端构包时写入的动作 ID。主动取消请求未携带动作 ID，此时为 0。</summary>
        internal uint ActionId { get; }

        /// <summary>请求中的动作类型，用于区分技能、道具等动作类别。适用于技能请求。</summary>
        internal ActionType ActionType { get; }

        /// <summary>客户端分配的 16 位技能请求序号。主动取消请求未携带序号，此时为 0。</summary>
        internal ushort SourceSequence { get; }

        /// <summary>客户端计算本次咏唱时间时采用的修正类型。请求正文偏移 +4。</summary>
        internal CastTimeProc CastProc { get; }

        /// <summary>请求记录的修正类型会直接把咏唱时间归零。</summary>
        internal bool HasInstantCastProc => CastProc == CastTimeProc.Requiescat || CastProc == CastTimeProc.DivineMight;

        /// <summary>构包时施法者的当前朝向，单位为弧度。</summary>
        internal float Rotation { get; }

        /// <summary>客户端为本次动作计算的朝向，单位为弧度。</summary>
        internal float Facing { get; }

        /// <summary>普通及带目标地面请求中的完整 64 位目标身份。纯地面请求为 <see langword="null"/>。</summary>
        internal ulong? TargetId { get; }

        /// <summary>地面请求的 ACT 落点，X/Y 为水平面，Z 为高度。普通请求为 <see langword="null"/>。</summary>
        internal Vector3? GroundPosition { get; }

        /// <summary>普通请求携带的原生附加参数，含义随动作类型而异。地面请求为 <see langword="null"/>。</summary>
        private uint? ExtraParam { get; }

        internal ActionRequest(ushort opcode, uint descriptorLength)
        {
            Kind = ActionRequestKind.CancelCast;
            Opcode = opcode;
            DescriptorLength = descriptorLength;
        }

        internal ActionRequest(ActionRequestKind kind, ushort opcode, uint descriptorLength, uint actionId,
            ActionType actionType, ushort sourceSequence, byte castProc, float rotation, float facing,
            ulong? targetId, Vector3? groundPosition, uint? extraParam)
        {
            Kind = kind;
            Opcode = opcode;
            DescriptorLength = descriptorLength;
            ActionId = actionId;
            ActionType = actionType;
            SourceSequence = sourceSequence;
            CastProc = (CastTimeProc)castProc;
            Rotation = rotation;
            Facing = facing;
            TargetId = targetId;
            GroundPosition = groundPosition;
            ExtraParam = extraParam;
        }

        public override string ToString()
        {
            if (Kind == ActionRequestKind.CancelCast)
                return $"[取消咏唱] 0x{Opcode:X4} 长度=0x{DescriptorLength:X}";

            string label;
            switch (Kind)
            {
                case ActionRequestKind.Ground:
                    label = "地面技能";
                    break;
                case ActionRequestKind.TargetedGround:
                    label = "带目标地面技能";
                    break;
                default:
                    label = "普通技能";
                    break;
            }

            var message = $"[{label}] 0x{Opcode:X4} 长度=0x{DescriptorLength:X} 技能={ActionId} (0x{ActionId:X}) 类型={(uint)ActionType} 序号={SourceSequence}" +
                FormattableString.Invariant($" 咏唱修正=0x{(byte)CastProc:X2} 施法者朝向={Rotation:F3} 动作朝向={Facing:F3}（弧度）");
            if (TargetId is ulong targetId)
                message += $" 目标=0x{targetId:X16}";
            if (GroundPosition is Vector3 position)
                message += FormattableString.Invariant($" ACT 落点=({position.X:F3}, {position.Y:F3}, {position.Z:F3})");
            if (ExtraParam is uint extraParam)
                message += $" 参数=0x{extraParam:X8}";

            return message;
        }
    }
}
