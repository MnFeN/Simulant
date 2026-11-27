using Simulant.Core;
using Simulant.Core.Entity;
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
            effects.Insert(0, new ActionEffect
            {
                Type = 0x03,
                Param1 = (byte)(((action.Aspect & 0x0F) << 4) | (damageType & 0x0F)),
                Value = 10000
            });
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
            return TryGetEnemy(player, request, out target) && TryGetSpellCost(player, request, out mpCost);
        }

        internal static bool TryGetSpellCost(Character player, ActionRequest request, out int cost)
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
            effects.Insert(0, new ActionEffect
            {
                Type = 0x0B,
                Param4 = 0x80,
                Value = amount
            });
        }

        internal static bool RequireStatus(Character player, ActionRequest request, ushort status)
        {
            if (Entities.TryGetOwnedStatus(Entities.Attach(player), status, out _, out _))
                return true;
            player.RejectActionResult(request, 572);
            return false;
        }

        internal static void RemoveStatus(Character player, ushort status)
            => Entities.RemoveStatus(Entities.Attach(player), status);

        internal static void ConsumeStatusStack(Character player, ushort status)
        {
            var actor = Entities.Attach(player);
            if (!Entities.TryGetOwnedStatus(actor, status, out var stacks, out var remaining))
                return;
            if (stacks > 1)
                Entities.SetStatus(actor, status, remaining, (ushort)(stacks - 1), actor);
            else
                Entities.RemoveStatus(actor, status);
        }

        internal static void AddSourceStatus(Character player, List<ActionEffect> effects, ushort status,
            float seconds, byte stacks = 0, byte effectParam = 0)
        {
            var actor = Entities.Attach(player);
            Entities.SetStatus(actor, status, seconds, stacks, actor);
            effects.Add(new ActionEffect
            {
                Type = 0x0F,
                Param0 = effectParam,
                Param2 = stacks,
                Param4 = 0x80,
                Value = status
            });
        }

        internal static void RemoveSourceStatus(Character player, List<ActionEffect> effects, ushort status)
        {
            RemoveStatus(player, status);
            effects.Add(new ActionEffect
            {
                Type = 0x11,
                Param4 = 0x80,
                Value = status
            });
        }

        internal static void AddTargetStatus(Character player, Character target, List<ActionEffect> effects,
            ushort status, float seconds, byte effectParam = 0, byte param = 0)
        {
            var source = Entities.Attach(player);
            effects.Add(Entities.SetActionStatus(source, Entities.Attach(target), status, seconds, param, effectParam));
        }

        internal static void AddAreaStatus(Character player, IEnumerable<Character> targets, List<ActionEffect> effects,
            ushort status, float seconds)
        {
            var source = Entities.Attach(player);
            foreach (var target in targets)
                Entities.SetStatus(Entities.Attach(target), status, seconds, 0, source);

            // 结算头只带主目标，其余范围目标已经更新状态。
            effects.Add(new ActionEffect
            {
                Type = 0x0E,
                Value = status
            });
        }

        internal static bool HasCombo(uint prerequisite)
        {
            var combo = ActionManager.Instance.Combo;
            return combo.Action.Get() == prerequisite && combo.Timer.Get() > 0;
        }

        internal static void AddWeaponCombo(List<ActionEffect> effects, ushort action, bool comboSucceeded = false)
        {
            // ActionEffectHandler.Receive 的缓存入口将来源效果 0x1B 的 Value 写到普通连击栏。
            effects.Add(new ActionEffect
            {
                Type = 0x1B,
                Param0 = comboSucceeded ? (byte)1 : (byte)0,
                Param4 = 0x80,
                Value = action
            });
        }
    }
}
