using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Game;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Simulation.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ActionEffect = Simulant.Game.FFCS.Client.Game.Character.ActionEffectHandler.Effect;
using ActionEffectHandler = Simulant.Game.FFCS.Client.Game.Character.ActionEffectHandler;
using ActionRow = Lumina.Excel.Sheets.Action;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Simulation.Runtime.PlayerActions
{
    /// <summary>本机一次成功结算，目标列表以发给客户端的效果为准。</summary>
    internal readonly struct PlayerActionResult
    {
        internal Character Source { get; }
        internal uint ActionId { get; }
        internal ActionRequest Request { get; }
        internal IReadOnlyList<Character> Targets { get; }
        internal IReadOnlyList<Character> DamagedTargets { get; }
        internal bool IsAutoAttack => Request == null;
        internal bool IsSpell => !IsAutoAttack && Category == 2;
        internal bool IsWeaponskill => !IsAutoAttack && Category == 3;

        private uint Category => GameDataService.Instance.GetSheet<ActionRow>().GetRowOrDefault(ActionId)?.ActionCategory.RowId ?? 0;

        internal PlayerActionResult(Character source, uint actionId, ActionRequest request,
            IReadOnlyList<Character> targets, IReadOnlyList<Character> damagedTargets)
        {
            Source = source;
            ActionId = actionId;
            Request = request;
            Targets = targets;
            DamagedTargets = damagedTargets;
        }
    }

    internal static class PlayerActionRules
    {
        internal const ushort ComboDurationMilliseconds = 30000;

        /// <summary>按结算技能编号保存特殊瞬发动画锁，单位为秒。</summary>
        private static readonly Dictionary<uint, float> SpecialAnimationLocks = new Dictionary<uint, float>();

        /// <summary>由职业单例的构造函数按技能枚举登记特殊瞬发动画锁。</summary>
        /// <remarks>职业初始化时登记，每个技能编号登记一次。</remarks>
        internal static void AddSpecialAnimationLock<TActionId>(TActionId actionId, float animationLock)
            where TActionId : struct, Enum
            => SpecialAnimationLocks.Add(Convert.ToUInt32(actionId), animationLock);

        /// <summary>为本机技能回包选择动画锁。读条用 0.1 秒，瞬发优先查特殊值，默认 0.6 秒。</summary>
        /// <remarks>
        /// ActionEffectHandler.Receive 的本机技能结算分支读取 Header.AnimationLock 并更新本机计时器。
        /// 默认值取自实战回包，例如圣灵读条和瞬发。特殊值可用 ACT 21/22 行或原生回放的技能结算核对。
        /// </remarks>
        internal static float GetAnimationLock(ActionRequest request, uint actionId)
        {
            if (request == null)
                return 0f;
            if (request.RequiresCast)
                return 0.1f;

            return SpecialAnimationLocks.TryGetValue(actionId, out var animationLock) ? animationLock : 0.6f;
        }

        internal static SimEntityManager Entities => PluginHost.Instance.PlayerActions.Entities;

        /// <summary>读取本机当前职业的量谱。职业与量谱类型由调用方对应。</summary>
        internal static TGauge GetGauge<TGauge>(Job job) where TGauge : struct, IMemoryObject
        {
            var manager = JobGaugeManager.Instance().ThrowIfNull("未定位本机职业量谱管理器。");
            var currentJob = (Job)manager.ClassJobId.Get();
            var gauge = manager.CurrentGauge;
            if (currentJob != job || gauge == IntPtr.Zero)
                throw new InvalidOperationException($"未能取得本机职业 {job} 的量谱。当前量谱职业为 {currentJob}。");

            return gauge.As<TGauge>();
        }

        /// <summary>提交本机技能的单目标或范围结算。范围打空时传入空的目标列表。</summary>
        internal static void ResolvePlayerAction(Character player, ActionRequest request, Character target,
            List<ActionEffect> effects, IReadOnlyList<(Character Target, List<ActionEffect> Effects)> areaHits = null,
            uint? actionId = null)
        {
            var resolvedActionId = actionId ?? request.ActionId;
            if (areaHits == null)
                Entities.ResolveAction(player, request, target, resolvedActionId, effects.ToArray());
            else
                Entities.ResolveActionResults(player, request, target, resolvedActionId, areaHits);
        }

        internal static ActionRow GetAction(ActionRequest request)
            => GameDataService.Instance.GetSheet<ActionRow>().GetRow(request.ActionId);

        internal static void AddDamage(List<ActionEffect> effects, ActionRequest request, byte weaponAttackType)
            => AddDamage(effects, request.ActionId, weaponAttackType);

        internal static void AddDamage(List<ActionEffect> effects, uint actionId, byte weaponAttackType)
        {
            var action = GameDataService.Instance.GetSheet<ActionRow>().GetRow(actionId);
            var attackType = unchecked((sbyte)action.AttackType.RowId);
            var damageType = attackType == -1 ? weaponAttackType : (int)attackType;
            // ActionEffect 的 Param1 低四位为伤害类型，高四位为属性。Param0 的直击和暴击位保持为零。
            effects.Add(new ActionEffect
            {
                Type = 0x03,
                Param1 = (byte)(((action.Aspect & 0x0F) << 4) | (damageType & 0x0F)),
                Value = 10000
            });
        }

        internal static void AddHealing(Character recipient, List<ActionEffect> effects, ushort amount, bool atSource = false)
        {
            RestoreHp(recipient, amount);
            effects.Add(new ActionEffect
            {
                Type = 0x04,
                Param4 = atSource ? (byte)0x80 : (byte)0,
                Value = amount
            });
        }

        /// <summary>恢复施法者的生命值，并在当前目标的效果列表中添加来源治疗。</summary>
        internal static void AddSourceHealing(Character player, List<ActionEffect> effects, ushort amount)
            => AddHealing(player, effects, amount, atSource: true);

        internal static void RestoreHp(Character recipient, ushort amount)
        {
            var current = recipient.HP;
            var next = (uint)Math.Min(recipient.MaxHP, (ulong)current + amount);
            if (current != 0 && next > current)
                recipient.SetNetworkVitals(next);
        }

        private static Character FindTarget(ActionRequest request)
            => request.TargetId is ulong id && id <= uint.MaxValue
                ? PluginHost.Instance.EntityProvider.GetCharacters().FirstOrDefault(character => character.Id == id)
                : null;

        internal static bool TryGetEnemy(Character player, ActionRequest request, out Character target)
        {
            target = FindTarget(request);
            if (IsEnemy(target))
                return true;
            player.RejectActionResult(request, 582);
            return false;
        }

        internal static bool IsEnemy(Character target)
            => target != null && target.IsAlive && target.HP != 0 && target.Native.IsHostile && target.IsTargetable;

        /// <summary>优先使用请求落点，其次使用指定的范围中心，默认以施法者为中心。</summary>
        internal static IEnumerable<Character> GetAreaEnemies(Character player, ActionRequest request,
            Character center = null, ActionArea? shape = null)
        {
            var area = shape ?? ActionArea.FromAction(request.ActionId, player.HitboxRadius);
            var origin = GetAreaOrigin(player, request, center);
            return PluginHost.Instance.EntityProvider.GetCharacters().Where(character =>
                IsEnemy(character) && area.IntersectsCircle(character.Pos, character.HitboxRadius, origin, request.Facing));
        }

        internal static IEnumerable<Character> GetPartyInRange(Character player, ActionRequest request,
            Character center = null, ActionArea? shape = null)
        {
            var area = shape ?? ActionArea.FromAction(request.ActionId, player.HitboxRadius);
            var origin = GetAreaOrigin(player, request, center);
            return GetPartyCandidates(player)
                .Where(member => area.Contains(member.Pos, origin, request.Facing));
        }

        private static Vector2 GetAreaOrigin(Character player, ActionRequest request, Character center)
            => request.GroundPosition is Vector3 point ? new Vector2(point.X, point.Y) : center?.Pos ?? player.Pos;

        private static IEnumerable<Character> GetPartyCandidates(Character player, bool includeDead = false)
        {
            yield return player;

            var party = PluginHost.Instance.PlayerActions.EncounterEntities?.GetActivePartyCharacters(includeDead)
                ?? Array.Empty<Character>();
            foreach (var member in party)
                if (member.Identity != player.Identity)
                    yield return member;
        }

        internal static bool TryGetPartyTarget(Character player, ActionRequest request, out Character target,
            bool allowSelf = false, bool dead = false)
        {
            target = FindTarget(request);
            if (target != null && target.IsAlive && (target.HP == 0) == dead &&
                (allowSelf || target.Identity != player.Identity) &&
                Vector2.Distance(player.Pos, target.Pos) <= GetAction(request).Range)
            {
                var identity = target.Identity;
                if (GetPartyCandidates(player, includeDead: dead).Any(member => member.Identity == identity))
                    return true;
            }

            player.RejectActionResult(request, 582);
            return false;
        }

        internal static (Character Target, List<ActionEffect> Effects)[] CreateHits(IEnumerable<Character> targets)
            => targets.Take(ActionEffectHandler.MaxTargets).Select(target => (target, new List<ActionEffect>())).ToArray();

        /// <summary>对范围内的敌人添加伤害。指定已判定命中的主目标时，以它为中心并将它放在首位。</summary>
        internal static (Character Target, List<ActionEffect> Effects)[] AddAreaDamage(Character player,
            ActionRequest request, byte weaponAttackType, Character primaryTarget = null, ActionArea? shape = null)
        {
            var targets = GetAreaEnemies(player, request, primaryTarget, shape);
            if (primaryTarget != null)
                targets = new[] { primaryTarget }.Concat(targets.Where(target => target.Identity != primaryTarget.Identity));

            var hits = CreateHits(targets);
            foreach (var hit in hits)
                AddDamage(hit.Effects, request, weaponAttackType);
            return hits;
        }

        internal static bool TryGetEnemySpell(Character player, ActionRequest request, out Character target, out int mpCost)
        {
            mpCost = 0;
            return TryGetEnemy(player, request, out target) && TryGetMpCost(player, request, out mpCost);
        }

        /// <summary>按原生规则检查魔力并给出实际扣费。技能成功结算时用 <see cref="ChangeMp"/> 扣除。</summary>
        /// <remarks>
        /// Action.PrimaryCostType 为 3、4、12、51 时查询魔力消耗。类型 4 单独查询最低魔力，
        /// 再用当前魔力比较门槛与实际扣费中的较大值。量谱和状态的消耗由职业结算负责。
        /// 国服 7.58（2026.11.03.0000.0000）：沿 GetActionCost 的类型 4 分支读取
        /// Action.PrimaryCostValue 和 ParamGrow.MpModifier，核爆（技能 162）的 100 级基础门槛为
        /// 8 × 10000 ÷ 100 = 800 MP。无灵极心时 1000 MP 可以释放并耗尽，600 MP 低于门槛。
        /// 该分支对核爆读取黑魔量谱的灵极心。原生用整数计算扣去 34% 后的门槛，并按同一比例减少
        /// 实际消耗，最低仍扣调整后的门槛。有灵极心时门槛为 528 MP，1000 MP 扣 660，600 MP 扣 528。
        /// </remarks>
        internal static bool TryGetMpCost(Character player, ActionRequest request, out int cost)
        {
            var costType = GetAction(request).PrimaryCostType;
            var minimumMp = costType == 4
                ? ActionManager.GetActionCost(request.ActionType, request.ActionId, 1, 1, 1, 0)
                : 0;
            cost = costType == 3 || costType == 4 || costType == 12 || costType == 51
                ? ActionManager.GetActionCost(request.ActionType, request.ActionId, 1, 0, 1, 0)
                : 0;
            if (player.MP >= Math.Max(minimumMp, cost))
                return true;

            player.RejectActionResult(request, 568);
            return false;
        }

        /// <summary>通过原生角色血蓝更新应用魔力变化，结果限制在零到最大魔力之间。</summary>
        internal static void ChangeMp(Character player, int delta)
        {
            var current = player.MP;
            var next = (uint)Math.Min(player.MaxMp, Math.Max(0L, (long)current + delta));
            if (current == next)
                return;
            player.SetNetworkVitals(player.HP, checked((ushort)next));
        }

        internal static void AddMpRecovery(Character player, List<ActionEffect> effects, ushort amount)
        {
            ChangeMp(player, amount);
            effects.Add(new ActionEffect
            {
                Type = 0x0B,
                Param4 = 0x80,
                Value = amount
            });
        }

        internal static void ReduceCooldown<TActionEnum>(TActionEnum action, float seconds)
            where TActionEnum : struct, Enum
            => AdvanceCooldown(Convert.ToUInt32(action), seconds, charges: 0);

        internal static void RestoreCharges<TActionEnum>(TActionEnum action, int count = 1)
            where TActionEnum : struct, Enum
            => AdvanceCooldown(Convert.ToUInt32(action), 0, count);

        private static void AdvanceCooldown(uint actionId, float seconds, int charges)
        {
            var connection = GameInterop.Connection;
            connection.RunInFrame(() =>
            {
                var manager = ActionManager.Instance;
                var group = manager.GetRecastGroup((int)ActionType.Action, actionId);
                var cooldowns = manager._cooldowns;
                if ((uint)group >= (uint)cooldowns.Length)
                    return;

                var recast = (cooldowns.Ptr + group * RecastDetail.Size).Read<RecastDetail>();
                if (!recast.IsActive || recast.Elapsed >= recast.Total)
                    return;

                // 同组动作共享进度。继续使用当前记录的动作，避免覆盖原生冷却组的动作标记。
                var trackedAction = recast.ActionId == 0 ? actionId : recast.ActionId;
                if (charges > 0)
                    seconds += recast.Total / Math.Max(1, (int)ActionManager.GetMaxCharges(trackedAction, 0)) * charges;
                if (seconds > 0)
                    manager.SetCooldown(ActionType.Action, trackedAction, Math.Min(recast.Total, recast.Elapsed + seconds), recast.Total);
            });
        }

        internal static bool HasStatus<TStatusEnum>(Character player, TStatusEnum status)
            where TStatusEnum : struct, Enum
            => player.HasStatus(Convert.ToUInt16(status));

        internal static bool RequireStatus<TStatusEnum>(Character player, ActionRequest request, TStatusEnum status)
            where TStatusEnum : struct, Enum
        {
            if (HasStatus(player, status))
                return true;
            player.RejectActionResult(request, 572);
            return false;
        }

        internal static void RemoveStatus<TStatusEnum>(Character player, TStatusEnum status)
            where TStatusEnum : struct, Enum
        {
            var statusId = Convert.ToUInt16(status);
            // 状态可能在开启防火墙前已生效。移除本机角色自身施加的状态后清理模拟记录。
            player.ActorControl(ActorControlCategory.LoseEffect, a1: statusId, a3: player.Id);
            Entities.RemoveStatus(Entities.Attach(player), statusId);
        }

        internal static void ConsumeStatusStack<TStatusEnum>(Character player, TStatusEnum status)
            where TStatusEnum : struct, Enum
        {
            var statusId = Convert.ToUInt16(status);
            var statuses = player.Native.StatusManager;
            var slot = statuses.GetStatusIndex(statusId, player.Id);
            if (slot < 0)
                return;

            var stacks = statuses.GetStatus(slot).Param.Get();
            if (stacks <= 1)
            {
                RemoveStatus(player, status);
                return;
            }

            var actor = Entities.Attach(player);
            var next = (ushort)(stacks - 1);
            if (Entities.TryGetOwnedStatus(actor, statusId, out _, out var remaining))
                Entities.SetStatus(actor, statusId, remaining, next, actor);
            else
                player.ActorControl(ActorControlCategory.UpdateEffect, (uint)slot, statusId, next);
        }

        internal static void AddSourceStatus<TStatusEnum>(Character player, List<ActionEffect> effects, TStatusEnum status,
            float seconds, byte param = 0, byte effectParam = 0) where TStatusEnum : struct, Enum
        {
            var statusId = Convert.ToUInt16(status);
            var actor = Entities.Attach(player);
            Entities.SetStatus(actor, statusId, seconds, param, actor);
            effects.Add(new ActionEffect
            {
                Type = 0x0F,
                Param0 = effectParam,
                Param2 = param,
                Param4 = 0x80,
                Value = statusId
            });
        }

        internal static void RemoveSourceStatus<TStatusEnum>(Character player, List<ActionEffect> effects, TStatusEnum status)
            where TStatusEnum : struct, Enum
        {
            RemoveStatus(player, status);
            effects.Add(new ActionEffect
            {
                Type = 0x11,
                Param4 = 0x80,
                Value = Convert.ToUInt16(status)
            });
        }

        internal static void AddTargetStatus<TStatusEnum>(Character player, Character target, List<ActionEffect> effects,
            TStatusEnum status, float seconds, byte effectParam = 0, byte param = 0, bool breaksOnDamage = false)
            where TStatusEnum : struct, Enum
        {
            var statusId = Convert.ToUInt16(status);
            var source = Entities.Attach(player);
            effects.Add(Entities.SetActionStatus(source, Entities.Attach(target), statusId, seconds, param,
                effectParam, breaksOnDamage));
        }

        /// <summary>以施法者为目标附加状态，使用目标状态效果。</summary>
        internal static void AddSelfStatus<TStatusEnum>(Character player, List<ActionEffect> effects, TStatusEnum status,
            float seconds, byte effectParam = 0, byte param = 0, bool breaksOnDamage = false)
            where TStatusEnum : struct, Enum
            => AddTargetStatus(player, player, effects, status, seconds, effectParam, param, breaksOnDamage);

        /// <summary>观察自己施加的模拟状态。每次成功结算只调用一次 onAction，目标是该状态的持有者。</summary>
        internal static bool ObserveStatus<TStatusEnum>(Character player, Character target, TStatusEnum status,
            Action<Character, PlayerActionResult> onAction = null, Action<Character> onBreak = null,
            Action<Character> onExpire = null, Action<Character> onTick = null)
            where TStatusEnum : struct, Enum
            => Entities.ObserveStatus(player, target, Convert.ToUInt16(status), onAction, onBreak, onExpire, onTick);

        /// <summary>仅模拟指定护盾被击破，可用于不扣本机血量的练习。</summary>
        internal static bool BreakShield<TStatusEnum>(Character target, TStatusEnum status)
            where TStatusEnum : struct, Enum
            => Entities.BreakShields(target, Convert.ToUInt16(status));

        /// <summary>添加随统一三秒刻度结算的持续伤害或治疗。治疗更新实际生命值。</summary>
        internal static void AddHoTDoT<TStatusEnum>(Character source, Character target, List<ActionEffect> effects,
            TStatusEnum status, float seconds, ushort amount, bool isHealing = false)
            where TStatusEnum : struct, Enum
        {
            AddTargetStatus(source, target, effects, status, seconds);
            var statusId = Convert.ToUInt16(status);
            ObserveStatus(source, target, status,
                onTick: statusTarget => ApplyPeriodicEffect(source, statusTarget, statusId, amount, isHealing));
        }

        /// <summary>向单个目标结算一次持续伤害或治疗，状态和地面效果共用。</summary>
        internal static void ApplyPeriodicEffect(Character source, Character target, ushort statusId, ushort amount, bool isHealing)
        {
            if (target.HP == 0)
                return;
            if (isHealing)
                RestoreHp(target, amount);
            target.ActorControl(ActorControlCategory.HotDot, statusId, isHealing ? 4u : 3u, amount, source.Id);
            Entities.CombatLog?.PeriodicEffect(source, target, statusId, amount, isHealing);
        }

        /// <summary>为倒地目标加复活状态，并按国服结算顺序写入复活与技能确认效果。</summary>
        internal static void OfferRaise<TActionEnum>(Character player, Character target, List<ActionEffect> effects,
            TActionEnum action) where TActionEnum : struct, Enum
        {
            const ushort status = (ushort)GeneralActions.StatusId.复活;
            // 国服复活结算先附加 60 秒状态，效果槽记录 Param1 = 20、Param2 = 8。
            Entities.SetStatus(Entities.Attach(target), status, 60, 8, Entities.Attach(player));
            effects.Add(new ActionEffect
            {
                Type = 0x0E,
                Param1 = 20,
                Param2 = 8,
                Value = status
            });
            AddWeaponCombo(effects, action);
        }

        internal static bool HasCombo<TActionEnum>(TActionEnum prerequisite) where TActionEnum : struct, Enum
        {
            var combo = ActionManager.Instance.Combo;
            return combo.Action.Get() == Convert.ToUInt32(prerequisite) && combo.Timer.Get() > 0;
        }

        internal static void AddWeaponCombo<TActionEnum>(List<ActionEffect> effects, TActionEnum action,
            bool comboSucceeded = false) where TActionEnum : struct, Enum
        {
            // ActionEffectHandler.Receive 的缓存入口将来源效果 0x1B 的 Value 写到普通连击栏。
            effects.Add(new ActionEffect
            {
                Type = 0x1B,
                Param0 = comboSucceeded ? (byte)1 : (byte)0,
                Param4 = 0x80,
                Value = Convert.ToUInt16(action)
            });
        }
    }
}
