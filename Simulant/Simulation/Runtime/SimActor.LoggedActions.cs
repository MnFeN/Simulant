using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Network;
using Simulant.Game.FFCS.Client.Network;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Simulant.Simulation.Runtime
{
    public sealed partial class SimActor
    {
        internal SimCombatLog CombatLog { get; set; }
        internal Func<uint, Core.Entity.Character> FindLogActor { get; set; }

        /// <summary>播放原生技能。hitTargets 是机制判定的结算目标，与演出所用的目标可以不同。</summary>
        public void ExecuteActionEffect(uint actionId, SimActor target, float? heading = null,
            IEnumerable<SimActor> hitTargets = null)
        {
            Entity.ExecuteActionEffect(actionId, target?.Entity, heading);
            CombatLog?.Ability(Entity, actionId, target?.Entity, null, heading, 0, hitTargets);
        }

        /// <summary>播放指定落点的技能。如果机制已判定命中名单，传入 hitTargets 以生成逐目标日志。</summary>
        public void ExecuteActionEffectAt(uint actionId, Vector3 position, float heading, float animationLock = 0,
            IEnumerable<SimActor> hitTargets = null)
        {
            Entity.ExecuteActionEffectAt(actionId, position, heading, animationLock);
            CombatLog?.Ability(Entity, actionId, null, position, heading, animationLock, hitTargets);
        }

        public void ActorMove(Vector3 position, float heading, byte speed = 20, ActorMoveFlags flags = ActorMoveFlags.None)
        {
            Entity.ActorMove(position, heading, speed, flags);
            CombatLog?.Move(Id, position, heading, speed, flags);
        }

        public void ActorSetPos(Vector3 position, float heading)
        {
            Entity.ActorSetPos(position, heading);
            CombatLog?.SetPosition(Id, position, heading);
        }

        public void SetTargetable(bool targetable)
            => ActorControl(ActorControlCategory.Targetable, targetable ? 1u : 0u);

        /// <summary>向目标显示连线，默认使用第一个连线槽和常用线宽。</summary>
        public void TetherTo(SimActor target, uint tetherId, uint slot = 0, uint width = 15)
            => ActorControl(ActorControlCategory.Tether, slot, tetherId, target.Id, width);

        /// <summary>取消指定槽位的连线。编号为零时取消该槽上的任意连线。</summary>
        public void CancelTether(uint slot = 0, uint tetherId = 0)
            => ActorControl(ActorControlCategory.TetherCancel, slot, tetherId);

        /// <summary>调用普通 ActorControl，按类别生成解析插件或 OverlayPlugin 对应的日志。</summary>
        public IntPtr ActorControl(ActorControlCategory category, uint a1 = 0, uint a2 = 0, uint a3 = 0, uint a4 = 0)
        {
            var result = Entity.ActorControl(category, a1, a2, a3, a4);
            if (CombatLog?.Enabled != true)
                return result;

            switch (category)
            {
                case ActorControlCategory.CancelCast when a3 != 0 && a4 == 1:
                    CombatLog.Cancel(Entity, a3);
                    break;
                case ActorControlCategory.TargetIcon when a1 != 0:
                    CombatLog.Icon(Id, Entity, a1, a2, a3, a4);
                    break;
                case ActorControlCategory.Targetable:
                    CombatLog.Targetable(Id, Entity, a1);
                    break;
                case ActorControlCategory.Tether:
                    CombatLog.Tether(Id, Entity, a3, FindLogActor?.Invoke(a3), a1, a2, a4);
                    break;
            }

            CombatLog.ActorControl(Id, category, a1, a2, a3, a4);
            return result;
        }

        /// <summary>调用 ActorControlSelf 的共享原生处理函数，按包类型生成日志。</summary>
        public IntPtr ActorControlSelf(ActorControlCategory category, uint a1 = 0, uint a2 = 0, uint a3 = 0,
            uint a4 = 0, uint a5 = 0, uint a6 = 0, uint a7 = 0, uint a8 = 0)
        {
            EnsureActive();
            var result = PacketDispatcher.HandleActorControl(Id, category, a1, a2, a3, a4, a5, a6, a7, a8, SimCombatLog.NoTarget, 0);
            CombatLog?.ActorControlSelf(Id, category, a1, a2, a3, a4, a5, a6);
            return result;
        }
    }
}
