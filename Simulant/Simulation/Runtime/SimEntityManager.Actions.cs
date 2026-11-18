using Simulant.Game.Data;
using System;
using System.Linq;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Simulation.Runtime
{
    public sealed partial class SimEntityManager
    {
        internal static readonly uint[] LocalActions = { 3, 7548, 7559, 30, 43, 3638, 16152 };

        public static uint InvulnerabilityAction(Job job)
        {
            switch (job)
            {
                case Job.PLD:
                    return 30;
                case Job.WAR:
                    return 43;
                case Job.DRK:
                    return 3638;
                case Job.GNB:
                    return 16152;
                default:
                    return 0;
            }
        }

        /// <summary>只演出本职业无敌；伤害判定必须显式允许无敌，不能全局免除机制错误。</summary>
        public bool UseInvulnerability(SimActor actor)
        {
            var action = InvulnerabilityAction(actor.Job);
            return action != 0 && UseLocalAction(actor, action);
        }

        /// <summary>四人水分摊、五人神圣等人数不足时，所有实际参与者获得死亡刻印。</summary>
        public void MarkUnderfilledStack(SimActor[] participants, int required, SimActor source)
        {
            if (participants.Length >= required)
                return;
            lock (_gate)
            {
                foreach (var actor in participants)
                {
                    var status = _statuses.FirstOrDefault(s => s.Actor.Identity == actor.Entity.Identity && s.StatusId == 4372);
                    var stacks = status == null ? 0 : actor.Entity.Native.StatusManager.GetStatus(status.Slot).Param.Get();
                    SetStatus(actor, 4372, 600, (ushort)(stacks + 1), source);
                }
            }
        }

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
            if (actionId != 3 && actionId != 7548 && actionId != 7559 && actionId != InvulnerabilityAction(actor.Job))
                return false;

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
                case 30:
                    id = 82;
                    break;
                case 43:
                    id = 409;
                    break;
                case 3638:
                    id = 810;
                    break;
                case 16152:
                    id = 1836;
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

                var seconds = actionId == 7548 || actionId == 7559 ? 6 : 10;
                // 冲刺的 Param 是加速百分比；回放中的状态 50 使用 30，填 0 只显示图标、不加速。
                var param = actionId == 3 ? (ushort)30 : (ushort)0;
                SetStatus(actor, id, seconds, param, actor);
                actor.ExecuteActionEffect(actionId, actor);
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
