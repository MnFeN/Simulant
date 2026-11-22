using Simulant.Core.Entity;
using Simulant.Core.Party;
using Simulant.Game.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Simulation.Runtime
{
    internal enum SimLocalAction : uint
    {
        Sprint = 3,
        ArmsLength = 7548,
        Surecast = 7559,
        HallowedGround = 30,
        Holmgang = 43,
        LivingDead = 3638,
        Superbolide = 16152,
        Provoke = 7533,
        Shirk = 7537
    }

    public sealed partial class SimEntityManager
    {
        internal static readonly uint[] LocalActions = Enum.GetValues(typeof(SimLocalAction))
            .Cast<SimLocalAction>().Select(action => (uint)action).ToArray();

        internal static bool IsEnmityAction(uint action) => action == (uint)SimLocalAction.Provoke || action == (uint)SimLocalAction.Shirk;

        internal bool CanUseEnmityAction(uint actionId)
        {
            lock (_gate)
            {
                if (!_acceptingEntities || !_scheduler.IsRunning || Party.Count == 0)
                    return false;

                if (actionId == (uint)SimLocalAction.Shirk)
                    return Party.Exists(IsShirkTarget);

                return actionId == (uint)SimLocalAction.Provoke && _partyEnmity.Count != 0 &&
                    (Party.Count == 1 || _partyService?.IsOwnedBy(this) == true);
            }
        }

        private static bool IsShirkTarget(SimActor actor)
            => actor != null && actor.Entity.Identity.Slot != 0 && actor.IsActive && actor.Entity.HP != 0;

        internal bool TryUseEnmityAction(Character player, uint actionId)
        {
            lock (_gate)
            {
                if (!CanUseEnmityAction(actionId) || InvulnerabilityAction(player.Job) == 0 ||
                    !_actors.TryGetValue(player.Identity, out var source) || !source.IsActive || source.Entity.HP == 0 ||
                    !Party.Contains(source) || ActionsLocked(source))
                    return false;

                var provoke = actionId == (uint)SimLocalAction.Provoke;
                var candidates = (provoke
                    ? _partyEnmity.Keys.Where(actor => actor.IsActive && actor.Entity.HP != 0 && actor.Entity.IsTargetable)
                    : Party.Where(IsShirkTarget)).Where(actor => actor != source).ToArray();
                SimActor FindTarget(IntPtr address) => candidates.FirstOrDefault(actor => actor.Address == address);

                var target = FindTarget(SimPartyService.GetSelectedTarget(preferSoftTarget: true)) ??
                    FindTarget(SimPartyService.GetSelectedTarget());
                // 没有选中队友时可保留当前目标，优先退避给另一名坦克。
                if (!provoke && target == null)
                    target = candidates.FirstOrDefault(actor => InvulnerabilityAction(actor.Job) != 0) ?? candidates.FirstOrDefault();
                if (target == null)
                    return false;

                var bosses = provoke ? new[] { target } : _partyEnmity.Keys.Where(boss => boss.IsActive && boss.Entity.HP != 0).ToArray();
                foreach (var boss in bosses)
                {
                    var scores = new Dictionary<SimActor, float>(_partyEnmity[boss]);
                    if (provoke)
                    {
                        var first = OrderedPartyEnmity(scores).FirstOrDefault(actor => actor.Entity.HP != 0);
                        scores[source] = (first == null ? 0 : scores[first]) + 20;
                    }
                    else
                    {
                        if (!scores.TryGetValue(source, out var before) || before == 0)
                            continue;
                        scores.TryGetValue(target, out var received);
                        var amount = before * 0.25f;
                        scores[source] = before - amount;
                        scores[target] = received + amount;
                    }

                    var leader = OrderedPartyEnmity(scores).First(actor => actor.Entity.HP != 0);
                    var percentages = Party.Select(actor => actor == leader ? 100 : actor.IsActive && actor.Entity.HP != 0 &&
                        scores.TryGetValue(actor, out var value) ? Math.Min(99, (int)(100 * value / scores[leader])) : 0).ToArray();
                    PublishPartyEnmity(boss, scores, percentages);
                }

                PlayLocalActionEffect(source, actionId, target);
                return true;
            }
        }

        public static uint InvulnerabilityAction(Job job)
        {
            switch (job)
            {
                case Job.PLD:
                    return (uint)SimLocalAction.HallowedGround;
                case Job.WAR:
                    return (uint)SimLocalAction.Holmgang;
                case Job.DRK:
                    return (uint)SimLocalAction.LivingDead;
                case Job.GNB:
                    return (uint)SimLocalAction.Superbolide;
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

            UseLocalAction(actor, (uint)((surecast ?? (SimJobActions.For(actor.Job)?.Caster == true))
                ? SimLocalAction.Surecast : SimLocalAction.ArmsLength));
        }

        /// <summary>本地模拟已支持的职能技能和冲刺；状态和演出共用，冷却不作限制。</summary>
        internal bool UseLocalAction(SimActor actor, uint actionId)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));
            var action = (SimLocalAction)actionId;
            var knockbackImmunity = action == SimLocalAction.ArmsLength || action == SimLocalAction.Surecast;
            if (action != SimLocalAction.Sprint && !knockbackImmunity && actionId != InvulnerabilityAction(actor.Job))
                return false;

            ushort id;
            switch (action)
            {
                case SimLocalAction.Surecast:
                    id = 160;
                    break;
                case SimLocalAction.ArmsLength:
                    id = 1209;
                    break;
                case SimLocalAction.Sprint:
                    id = 50;
                    break;
                case SimLocalAction.HallowedGround:
                    id = 82;
                    break;
                case SimLocalAction.Holmgang:
                    id = 409;
                    break;
                case SimLocalAction.LivingDead:
                    id = 810;
                    break;
                case SimLocalAction.Superbolide:
                    id = 1836;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(actionId), "不支持的模拟技能。");
            }

            lock (_gate)
            {
                EnsureAcceptingEntities();
                EnsureStatusActor(actor);
                if (ActionsLocked(actor))
                    return false;

                var seconds = knockbackImmunity ? 6 : 10;
                // 冲刺的 Param 是加速百分比；回放中的状态 50 使用 30，填 0 只显示图标、不加速。
                var param = action == SimLocalAction.Sprint ? (ushort)30 : (ushort)0;
                SetStatus(actor, id, seconds, param, actor);
                PlayLocalActionEffect(actor, actionId, actor);
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

        private static bool ActionsLocked(SimActor actor)
        {
            var statuses = actor.Entity.Native.StatusManager;
            var sheet = GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Status>();
            // 眩晕可能来自另一个会话，因此从角色当前状态读取。
            for (var slot = 0; slot < 60; slot++)
            {
                var statusId = statuses.GetStatus(slot).StatusId.Get();
                if (statusId != 0 && sheet.TryGetRow(statusId, out var row) && row.LockActions)
                    return true;
            }

            return false;
        }

        private static void PlayLocalActionEffect(SimActor actor, uint actionId, SimActor target)
        {
            actor.ExecuteActionEffect(actionId, target);
            // 本机模拟技能需要补播动作，网络假角色由结算演出驱动。
            if (actor.Entity.Identity.Slot == 0 &&
                GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Action>().TryGetRow(actionId, out var action))
                actor.Entity.PlayBlendTimeline((ushort)action.AnimationEnd.RowId);
        }
    }
}
