using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Game.FFCS.Client.Network;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using ActionEffect = Simulant.Game.FFCS.Client.Game.Character.ActionEffectHandler.Effect;

namespace Simulant.Core.Entity
{
    public sealed partial class Character
    {
        /// <summary>
        /// 播放无目标、带落点的原生技能演出。坐标采用 ACT XYZ（Z 为高度），不修改实体位置或面向。
        /// heading 仅写入技能包；依赖施法者面向的动画，调用前还需通过 ActorMove 同步实体面向。
        /// </summary>
        public void ExecuteActionEffectAt(uint actionId, Vector3 position, float heading, float animationLock = 0)
        {
            var native = Native;
            var index = native.ObjectIndex;
            if ((ObjectKind != ObjectKind.Pc && ObjectKind != ObjectKind.BattleNpc) || index >= 200 || index % 2 != 0)
                throw new InvalidOperationException("指定位置的技能演出只支持战斗实体列表中的网络角色。");

            ReceiveVisual(Id, native.Ptr, actionId, heading,
                new Vector3(position.X, position.Z, position.Y), new GameObjectId
                {
                    ObjectId = 0xE0000000
                }, animationLock);
        }

        /// <summary>播放带目标的原生技能演出，不产生伤害或状态。调用前应结束对应读条；瞬发演出可以与移动并行。</summary>
        public void ExecuteActionEffect(uint actionId, Character target)
            => ExecuteActionEffect(actionId, target, null);

        /// <summary>显式指定演出面向，避免转向包尚在插值时，把上一刻的面向再次写入技能包；不代替 ActorMove。</summary>
        public void ExecuteActionEffect(uint actionId, Character target, float? heading)
            => ExecuteActionEffect(actionId, target, heading, 0);

        internal void ExecuteActionEffect(uint actionId, Character target, float? heading, float animationLock)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            // 两次 Native 访问都会核对注册身份，不能向地址已复用的旧实体发送演出。
            var sourceNative = Native;
            var targetNative = target.Native;
            ushort sourceIndex = sourceNative.ObjectIndex, targetIndex = targetNative.ObjectIndex;

            if ((ObjectKind != ObjectKind.Pc && ObjectKind != ObjectKind.BattleNpc) ||
                (target.ObjectKind != ObjectKind.Pc && target.ObjectKind != ObjectKind.BattleNpc) ||
                sourceIndex >= 200 || sourceIndex % 2 != 0 || targetIndex >= 200 || targetIndex % 2 != 0)
                throw new InvalidOperationException("带目标的技能演出只支持战斗实体列表中的网络角色。");

            ReceiveVisual(Id, sourceNative.Ptr, actionId, heading ?? sourceNative.Rotation,
                targetNative.Position, new GameObjectId
                {
                    ObjectId = target.Id
                }, animationLock);
        }

        private static int _globalSequence;

        /// <summary>向原生入口提供演出坐标及空的八项效果；目标 0xE0000000 表示无目标。坐标采用游戏原生 XYZ（Y 为高度）。</summary>
        internal static void ReceiveVisual(uint casterEntityId, IntPtr casterPtr, uint actionId, float heading,
            Vector3 targetPosition, GameObjectId targetId, float animationLock = 0)
            => ReceiveSingleTargetEffect(casterEntityId, casterPtr, actionId, heading, targetPosition,
                targetId, targetId, animationLock, 0, null);

        /// <summary>沿原生收包入口确认本机请求，携带该请求的 SourceSequence 和实际效果项。</summary>
        internal void ReceiveActionResult(ActionRequest request, uint actionId, Character target, Vector3 position,
            float animationLock, params ActionEffect[] effects)
        {
            var source = Native;
            GameObjectId targetId = target?.Id ?? 0xE0000000u;
            // 范围技能打空时仍以施法者为动画目标，受击目标数为零。
            var animationTargetId = target == null && request.Kind == ActionRequestKind.Normal
                ? (GameObjectId)Id : targetId;
            ReceiveSingleTargetEffect(Id, source.Ptr, actionId, request.Facing,
                new Vector3(position.X, position.Z, position.Y), targetId, animationTargetId,
                animationLock, request.SourceSequence, effects);
        }

        /// <summary>一次确认原请求，按相同下标传入各目标的身份和效果。</summary>
        internal void ReceiveActionResults(ActionRequest request, uint actionId, Character animationTarget, Vector3 position,
            float animationLock, IReadOnlyList<(Character Target, List<ActionEffect> Effects)> hits)
        {
            var targetIds = new GameObjectId[hits.Count];
            var targetEffects = new ActionEffectHandler.TargetEffects[hits.Count];
            for (var index = 0; index < hits.Count; index++)
            {
                var hit = hits[index];
                targetIds[index] = hit.Target.Id;
                targetEffects[index] = ActionEffectHandler.TargetEffects.FromEffects(hit.Effects.ToArray());
            }

            ReceiveActionEffect(Id, Native.Ptr, actionId, request?.Facing ?? Heading,
                new Vector3(position.X, position.Z, position.Y), animationTarget?.Id ?? 0xE0000000u,
                animationLock, request?.SourceSequence ?? 0, targetEffects, targetIds);
        }

        internal void RejectActionResult(ActionRequest request, uint messageId)
            => PacketDispatcher.HandleActorControl(Native.EntityId, ActorControlCategory.ActionRejected,
                messageId, (uint)request.ActionType, request.ActionId, 0, 0, request.SourceSequence, 0, 0, 0xE0000000, 0);

        private static void ReceiveSingleTargetEffect(uint casterEntityId, IntPtr casterPtr, uint actionId, float heading,
            Vector3 targetPosition, GameObjectId targetId, GameObjectId animationTargetId, float animationLock,
            ushort sourceSequence, ActionEffect[] effects)
        {
            var targetEffects = ActionEffectHandler.TargetEffects.FromEffects(effects);
            if (targetId.ObjectId == 0xE0000000)
            {
                ReceiveActionEffect(casterEntityId, casterPtr, actionId, heading, targetPosition, animationTargetId,
                    animationLock, sourceSequence, default, default);
                return;
            }

            ReceiveActionEffect(casterEntityId, casterPtr, actionId, heading, targetPosition, animationTargetId,
                animationLock, sourceSequence, new[] { targetEffects }, new[] { targetId });
        }

        private static void ReceiveActionEffect(uint casterEntityId, IntPtr casterPtr, uint actionId, float heading,
            Vector3 targetPosition, GameObjectId animationTargetId, float animationLock, ushort sourceSequence,
            ReadOnlySpan<ActionEffectHandler.TargetEffects> targetEffects, ReadOnlySpan<GameObjectId> targetIds)
        {
            if (ActionEffectHandler.ReceiveFuncPtr == IntPtr.Zero)
                throw new InvalidOperationException("原生技能结算接收函数尚未定位。");

            if (casterPtr == IntPtr.Zero || casterEntityId == 0 || casterEntityId == 0xE0000000)
                throw new ArgumentException("技能演出的施法者身份或地址无效。", nameof(casterEntityId));

            if (animationTargetId.ObjectId == 0)
                throw new ArgumentException("技能演出的动画目标身份无效。", nameof(animationTargetId));

            foreach (var targetId in targetIds)
                if (targetId.ObjectId == 0 || targetId.ObjectId == 0xE0000000)
                    throw new ArgumentException("技能结算的受击目标身份无效。", nameof(targetIds));

            if (actionId == 0 || actionId > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(actionId), "技能编号必须能够写入演出技能字段。");

            if (!IsFinite(targetPosition.X) || !IsFinite(targetPosition.Y) || !IsFinite(targetPosition.Z))
                throw new ArgumentOutOfRangeException(nameof(targetPosition), "技能目标坐标必须为有限数。");

            if (!IsFinite(animationLock) || animationLock < 0)
                throw new ArgumentOutOfRangeException(nameof(animationLock), "技能动画锁时长必须为非负有限数。");

            var rotation = PacketCodec.EncodeUShortHeading(heading);
            var sequence = unchecked((uint)Interlocked.Increment(ref _globalSequence));

            if (sequence == 0)
                sequence = unchecked((uint)Interlocked.Increment(ref _globalSequence));

            var header = new ActionEffectHandler.Header
            {
                AnimationTargetId = animationTargetId,
                ActionId = actionId,
                GlobalSequence = sequence,
                AnimationLock = animationLock,
                BallistaEntityId = 0xE0000000,
                SourceSequence = sourceSequence,
                RotationInt = rotation,
                SpellId = (ushort)actionId,
                ActionType = 1,
                NumTargets = checked((byte)targetIds.Length),
            };

            ActionEffectHandler.Receive(casterEntityId, casterPtr, targetPosition, header, targetEffects, targetIds);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
