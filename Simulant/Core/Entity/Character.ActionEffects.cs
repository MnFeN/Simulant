using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Object;
using System;
using System.Numerics;

namespace Simulant.Core.Entity
{
    public sealed partial class Character
    {
        /// <summary>播放无目标、带落点的原生技能演出。坐标采用 ACT XYZ（Z 为高度），不额外修改实体位置。</summary>
        public void ExecuteActionEffectAt(uint actionId, Vector3 position, float heading, float animationLock = 0)
        {
            var native = Native;
            var index = native.ObjectIndex;
            if ((ObjectKind != ObjectKind.Pc && ObjectKind != ObjectKind.BattleNpc) || index >= 200 || index % 2 != 0)
                throw new InvalidOperationException("指定位置的技能演出只支持战斗实体列表中的网络角色。");

            ActionEffectHandler.ReceiveVisual(Id, native.Ptr, actionId, heading,
                new Vector3(position.X, position.Z, position.Y), new GameObjectId
                {
                    ObjectId = 0xE0000000
                }, animationLock);
        }

        /// <summary>播放带目标的原生技能演出，不产生伤害或状态。调用前应结束对应读条；瞬发演出可以与移动并行。</summary>
        public void ExecuteActionEffect(uint actionId, Character target)
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

            ActionEffectHandler.ReceiveVisual(Id, sourceNative.Ptr, actionId, sourceNative.Rotation,
                targetNative.Position, new GameObjectId
                {
                    ObjectId = target.Id
                });
        }
    }
}
