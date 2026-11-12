using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Object;
using System;

namespace Simulant.Core.Entity
{
    public sealed partial class Character
    {
        /// <summary>播放带目标的原生技能演出。效果列表为空，不产生伤害或状态；调用前应结束会话管理的读条和移动。</summary>
        public void ExecuteActionEffect(uint actionId, Character target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            // 两次 Native 访问都会核对注册身份，不能向地址已复用的旧实体发送演出。
            var sourceNative = Native;
            var targetNative = target.Native;
            ushort sourceIndex = sourceNative.ObjectIndex, targetIndex = targetNative.ObjectIndex;
            if ((ObjectKind != ObjectKind.Pc && ObjectKind != ObjectKind.BattleNpc) ||
                (target.ObjectKind != ObjectKind.Pc && target.ObjectKind != ObjectKind.BattleNpc) ||
                sourceIndex >= 200 || sourceIndex % 2 != 0 || targetIndex >= 200 || targetIndex % 2 != 0)
                throw new InvalidOperationException("带目标的技能演出只支持战斗实体列表中的网络角色。");

            ActionEffectHandler.ReceiveVisual(Id, sourceNative.Ptr, actionId, sourceNative.Rotation,
                targetNative.Position, new GameObjectId { ObjectId = target.Id });
        }
    }
}
