using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Game;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ActionEffect = Simulant.Game.FFCS.Client.Game.Character.ActionEffectHandler.Effect;
using ActionRow = Lumina.Excel.Sheets.Action;

namespace Simulant.Simulation.Runtime.PlayerActions
{
    internal static class PlayerActionRules
    {
        internal const ushort ComboDurationMilliseconds = 30000;

        internal static SimEntityManager Entities => PluginHost.Instance.PlayerActions.Entities;

        internal static ActionRow GetAction(ActionRequest request)
            => GameDataService.Instance.GetSheet<ActionRow>().GetRow(request.ActionId);

        internal static void AddDamage(List<ActionEffect> effects, ActionRequest request, byte weaponAttackType)
        {
            var action = GetAction(request);
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

        private static bool IsEnemy(Character target)
            => target != null && target.IsAlive && target.HP != 0 && target.Native.IsHostile && target.IsTargetable;

        internal static IEnumerable<Character> GetAreaEnemies(Character player, ActionRequest request)
        {
            var position = player.Pos;
            var range = GetAction(request).EffectRange;
            return PluginHost.Instance.EntityProvider.GetCharacters().Where(character =>
                IsEnemy(character) && Vector2.Distance(position, character.Pos) <= range + character.HitboxRadius);
        }

        internal static IEnumerable<Character> GetPartyInRange(Character player, ActionRequest request)
        {
            yield return player;

            var range = GetAction(request).EffectRange;
            var position = player.Pos;
            var party = PluginHost.Instance.PlayerActions.EncounterEntities?.GetActivePartyCharacters() ?? Array.Empty<Character>();
            foreach (var member in party)
                if (member.Identity != player.Identity && Vector2.Distance(position, member.Pos) <= range)
                    yield return member;
        }

        internal static bool TryGetPartyTarget(Character player, ActionRequest request, out Character target)
        {
            target = FindTarget(request);
            if (target != null && target.Identity != player.Identity)
            {
                var identity = target.Identity;
                var party = PluginHost.Instance.PlayerActions.EncounterEntities?.GetActivePartyCharacters();
                if (party?.Any(member => member.Identity == identity) == true &&
                    Vector2.Distance(player.Pos, target.Pos) <= GetAction(request).Range)
                    return true;
            }

            player.RejectActionResult(request, 582);
            return false;
        }

        internal static bool TryGetEnemySpell(Character player, ActionRequest request, out Character target, out int mpCost)
        {
            mpCost = 0;
            return TryGetEnemy(player, request, out target) && TryGetFixedMpCost(player, request, out mpCost);
        }

        internal static bool TryGetFixedMpCost(Character player, ActionRequest request, out int cost)
        {
            var row = GetAction(request);
            // Action 表的魔力消耗类型为 3，每单位 50 点魔力。
            cost = row.PrimaryCostValue * 50;
            if (row.PrimaryCostType == 3 && player.MP >= cost)
                return true;
            player.RejectActionResult(request, 572);
            return false;
        }

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
            TStatusEnum status, float seconds, byte effectParam = 0, byte param = 0) where TStatusEnum : struct, Enum
        {
            var statusId = Convert.ToUInt16(status);
            var source = Entities.Attach(player);
            effects.Add(Entities.SetActionStatus(source, Entities.Attach(target), statusId, seconds, param, effectParam));
        }

        internal static void AddAreaStatus<TStatusEnum>(Character player, IEnumerable<Character> targets, List<ActionEffect> effects,
            TStatusEnum status, float seconds) where TStatusEnum : struct, Enum
        {
            var statusId = Convert.ToUInt16(status);
            var source = Entities.Attach(player);
            foreach (var target in targets)
                Entities.SetStatus(Entities.Attach(target), statusId, seconds, 0, source);

            // 结算头只带主目标，其余范围目标已经更新状态。
            effects.Add(new ActionEffect
            {
                Type = 0x0E,
                Value = statusId
            });
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
