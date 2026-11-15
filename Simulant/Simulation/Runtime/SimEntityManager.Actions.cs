using Simulant.Game.Data;
using System;
using System.Linq;

namespace Simulant.Simulation.Runtime
{
    public sealed partial class SimEntityManager
    {
        /// <summary>模拟职能防击退：施加六秒状态和技能演出，不向服务器使用技能。</summary>
        public void UseKnockbackImmunity(SimActor actor, bool? surecast = null)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));

            UseLocalAction(actor, (surecast ?? (SimJobActions.For(actor.Job)?.Caster == true)) ? 7559u : 7548u);
        }

        /// <summary>本地模拟已支持的职能技能和冲刺；状态和演出共用，冷却不作限制。</summary>
        internal bool UseLocalAction(SimActor actor, uint actionId)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));

            ushort id;
            switch (actionId)
            {
                case 7559:
                    id = 160;
                    break;
                case 7548:
                    id = 1209;
                    break;
                case 3:
                    id = 50;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(actionId), "不支持的模拟技能。");
            }

            lock (_gate)
            {
                EnsureAcceptingEntities();
                EnsureStatusActor(actor);
                var statuses = actor.Entity.Native.StatusManager;
                var sheet = GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Status>();
                // 眩晕可能来自另一个副本会话；判断实际状态，不限于当前管理器施加的状态。
                for (var slot = 0; slot < 60; slot++)
                {
                    var statusId = statuses.GetStatus(slot).StatusId.Get();
                    if (statusId != 0 && sheet.TryGetRow(statusId, out var row) && row.LockActions)
                        return false;
                }

                var seconds = actionId == 3 ? 10 : 6;
                // 冲刺的 Param 是加速百分比；回放中的状态 50 使用 30，填 0 只显示图标、不加速。
                var param = actionId == 3 ? (ushort)30 : (ushort)0;
                SetStatus(actor, id, seconds, param, actor);
                actor.Entity.ExecuteActionEffect(actionId, actor.Entity);
                // 本机没有真正按下技能，需补播角色动作；其他网络角色仍由结算演出驱动。
                if (actor.Entity.Identity.Slot == 0 &&
                    GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Action>().TryGetRow(actionId, out var action))
                    actor.Entity.PlayBlendTimeline((ushort)action.AnimationEnd.RowId);
                _scheduler.ScheduleAfter(seconds, () =>
                {
                    lock (_gate)
                    {
                        var status = _statuses.FirstOrDefault(s => s.Actor.Identity == actor.Entity.Identity && s.StatusId == id);
                        if (status != null && _scheduler.ElapsedSeconds >= status.ExpiresAt)
                            RemoveStatus(actor, id);
                    }
                }, "模拟技能状态结束");
                return true;
            }
        }
    }
}
