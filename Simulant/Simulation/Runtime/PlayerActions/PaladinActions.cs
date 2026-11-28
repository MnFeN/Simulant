using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Game;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Game.FFCS.Client.Game.Gauge;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using ActionEffect = Simulant.Game.FFCS.Client.Game.Character.ActionEffectHandler.Effect;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;
using static Simulant.Simulation.Runtime.PlayerActions.PlayerActionRules;

namespace Simulant.Simulation.Runtime.PlayerActions
{
    internal static class PaladinActions
    {
        private enum ActionId : uint
        {
            先锋剑 = 9,
            暴乱剑 = 15,
            战逃反应 = 20,
            壁垒 = 22,
            厄运流转 = 23,
            保护 = 27,
            钢铁信念 = 28,
            神圣领域 = 30,
            沥血剑 = 3538,
            王权剑 = 3539,
            圣光幕帘 = 3540,
            全蚀斩 = 7381,
            干预 = 7382,
            圣灵 = 7384,
            日珥斩 = 16457,
            圣环 = 16458,
            悔罪 = 16459,
            赎罪剑 = 16460,
            圣盾阵 = 25746,
            偿赎剑 = 25747,
            信念之剑 = 25748,
            真理之剑 = 25749,
            英勇之剑 = 25750,
            解除钢铁信念 = 32065,
            祈告剑 = 36918,
            葬送剑 = 36919,
            极致防御 = 36920,
            绝对统治 = 36921,
            荣耀之剑 = 36922
        }

        private enum StatusId : ushort
        {
            战逃反应 = 76,
            壁垒 = 77,
            钢铁信念 = 79,
            保护 = 80,
            被保护 = 81,
            神圣领域 = 82,
            厄运流转 = 248,
            干预 = 1174,
            圣光幕帘 = 1362,
            安魂祈祷 = 1368,
            赎罪剑预备 = 1902,
            神圣魔法效果提高 = 2673,
            圣盾阵 = 2674,
            骑士的坚守 = 2675,
            骑士的加护 = 2676,
            悔罪预备 = 3019,
            祈告剑预备 = 3827,
            葬送剑预备 = 3828,
            极致防御 = 3829,
            极致护盾 = 3830,
            荣耀之剑预备 = 3831,
            沥血剑预备 = 3847
        }

        // 技能表以 -1 表示采用武器伤害类型，国服回放中的骑士剑技为斩击。
        private const byte WeaponAttackType = 1;
        private const ushort SpellHealing = 10000;
        private static SimScheduledAction _spellComboExpiry;

        internal static bool TryHandleAction(ActionRequest request)
        {
            var player = PluginHost.Instance?.EntityProvider?.GetMyself();
            if (player?.Job != Job.PLD)
                return false;

            var target = player;
            var effects = new List<ActionEffect>(4);
            (Character Target, List<ActionEffect> Effects)[] areaHits = null;
            StatusId consumeAfterResult = 0;
            int mpCost;
            PaladinGauge gauge;
            switch ((ActionId)request.ActionId)
            {
                case ActionId.先锋剑:
                    if (!TryGetEnemy(player, request, out target))
                        return true;

                    AddDamage(effects, request, WeaponAttackType);
                    AddWeaponCombo(effects, ActionId.先锋剑);
                    break;
                case ActionId.暴乱剑:
                    if (!TryGetEnemy(player, request, out target))
                        return true;

                    AddDamage(effects, request, WeaponAttackType);
                    if (HasCombo(ActionId.先锋剑))
                    {
                        AddMpRecovery(player, effects, 1000);
                        AddWeaponCombo(effects, ActionId.暴乱剑, comboSucceeded: true);
                    }
                    break;
                case ActionId.王权剑:
                    if (!TryGetEnemy(player, request, out target))
                        return true;

                    AddDamage(effects, request, WeaponAttackType);
                    if (HasCombo(ActionId.暴乱剑))
                    {
                        AddSourceStatus(player, effects, StatusId.赎罪剑预备, 30);
                        AddSourceStatus(player, effects, StatusId.神圣魔法效果提高, 30);
                        AddWeaponCombo(effects, ActionId.王权剑, comboSucceeded: true);
                    }
                    break;
                case ActionId.全蚀斩:
                    areaHits = AddAreaDamage(player, request, WeaponAttackType);
                    if (areaHits.Length != 0)
                        AddWeaponCombo(areaHits[0].Effects, ActionId.全蚀斩);
                    break;
                case ActionId.日珥斩:
                    areaHits = AddAreaDamage(player, request, WeaponAttackType);
                    if (areaHits.Length != 0 && HasCombo(ActionId.全蚀斩))
                    {
                        AddMpRecovery(player, areaHits[0].Effects, 1000);
                        AddSourceStatus(player, areaHits[0].Effects, StatusId.神圣魔法效果提高, 30);
                        AddWeaponCombo(areaHits[0].Effects, ActionId.日珥斩, comboSucceeded: true);
                    }
                    break;
                case ActionId.战逃反应:
                    // 国服回放中战逃反应直接对自身附加状态，并以来源效果附加沥血剑预备。
                    AddTargetStatus(player, player, effects, StatusId.战逃反应, 20, effectParam: 25);
                    AddSourceStatus(player, effects, StatusId.沥血剑预备, 30);
                    break;
                case ActionId.沥血剑:
                    if (!TryGetEnemy(player, request, out target) || !RequireStatus(player, request, StatusId.沥血剑预备))
                        return true;

                    AddDamage(effects, request, WeaponAttackType);
                    consumeAfterResult = StatusId.沥血剑预备;
                    break;
                case ActionId.赎罪剑:
                    if (!TryGetEnemy(player, request, out target) || !RequireStatus(player, request, StatusId.赎罪剑预备))
                        return true;

                    AddDamage(effects, request, WeaponAttackType);
                    AddMpRecovery(player, effects, 400);
                    AddSourceStatus(player, effects, StatusId.祈告剑预备, 30);
                    consumeAfterResult = StatusId.赎罪剑预备;
                    break;
                case ActionId.祈告剑:
                    if (!TryGetEnemy(player, request, out target) || !RequireStatus(player, request, StatusId.祈告剑预备))
                        return true;

                    AddDamage(effects, request, WeaponAttackType);
                    AddMpRecovery(player, effects, 400);
                    AddSourceStatus(player, effects, StatusId.葬送剑预备, 30);
                    consumeAfterResult = StatusId.祈告剑预备;
                    break;
                case ActionId.葬送剑:
                    if (!TryGetEnemy(player, request, out target) || !RequireStatus(player, request, StatusId.葬送剑预备))
                        return true;

                    AddDamage(effects, request, WeaponAttackType);
                    AddMpRecovery(player, effects, 400);
                    consumeAfterResult = StatusId.葬送剑预备;
                    break;
                case ActionId.偿赎剑:
                    if (!TryGetEnemy(player, request, out target))
                        return true;

                    areaHits = AddAreaDamage(player, request, WeaponAttackType, target);
                    AddMpRecovery(player, areaHits[0].Effects, 500);
                    break;
                case ActionId.厄运流转:
                    areaHits = AddAreaDamage(player, request, WeaponAttackType);
                    foreach (var hit in areaHits)
                        AddTargetStatus(player, hit.Target, hit.Effects, StatusId.厄运流转, 15);
                    break;
                case ActionId.绝对统治:
                    if (!TryGetEnemy(player, request, out target))
                        return true;

                    areaHits = AddAreaDamage(player, request, WeaponAttackType, target);
                    AddSourceStatus(player, areaHits[0].Effects, StatusId.安魂祈祷, 30, param: 4);
                    AddSourceStatus(player, areaHits[0].Effects, StatusId.悔罪预备, 30);
                    break;
                case ActionId.圣灵:
                    if (!TryGetEnemySpell(player, request, out target, out mpCost))
                        return true;

                    AddDamage(effects, request, WeaponAttackType);
                    AddHealing(player, effects, SpellHealing, atSource: true);
                    ChangeMp(player, -mpCost);
                    ConsumeHolySpellProc(player, request);
                    break;
                case ActionId.圣环:
                    if (!TryGetFixedMpCost(player, request, out mpCost))
                        return true;

                    areaHits = AddAreaDamage(player, request, WeaponAttackType);
                    if (areaHits.Length != 0)
                        AddHealing(player, areaHits[0].Effects, SpellHealing, atSource: true);
                    ChangeMp(player, -mpCost);
                    ConsumeHolySpellProc(player, request);
                    break;
                case ActionId.悔罪:
                    if (!TryGetEnemySpell(player, request, out target, out mpCost) ||
                        !TryGetSpellGauge(player, request, out gauge) || !RequireStatus(player, request, StatusId.悔罪预备))
                        return true;

                    areaHits = AddAreaDamage(player, request, WeaponAttackType, target);
                    AddHealing(player, areaHits[0].Effects, SpellHealing, atSource: true);
                    ChangeMp(player, -mpCost);
                    ConsumeStatusStack(player, StatusId.安魂祈祷);
                    RemoveStatus(player, StatusId.悔罪预备);
                    AdvanceSpellCombo(player, gauge, 1);
                    break;
                case ActionId.信念之剑:
                    if (!TryGetEnemySpell(player, request, out target, out mpCost) ||
                        !TryGetSpellGauge(player, request, out gauge, expectedStep: 1))
                        return true;

                    areaHits = AddAreaDamage(player, request, WeaponAttackType, target);
                    AddHealing(player, areaHits[0].Effects, SpellHealing, atSource: true);
                    ChangeMp(player, -mpCost);
                    ConsumeStatusStack(player, StatusId.安魂祈祷);
                    AdvanceSpellCombo(player, gauge, 2);
                    break;
                case ActionId.真理之剑:
                    if (!TryGetEnemySpell(player, request, out target, out mpCost) ||
                        !TryGetSpellGauge(player, request, out gauge, expectedStep: 2))
                        return true;

                    areaHits = AddAreaDamage(player, request, WeaponAttackType, target);
                    AddHealing(player, areaHits[0].Effects, SpellHealing, atSource: true);
                    ChangeMp(player, -mpCost);
                    ConsumeStatusStack(player, StatusId.安魂祈祷);
                    AdvanceSpellCombo(player, gauge, 3);
                    break;
                case ActionId.英勇之剑:
                    if (!TryGetEnemySpell(player, request, out target, out mpCost) ||
                        !TryGetSpellGauge(player, request, out gauge, expectedStep: 3))
                        return true;

                    areaHits = AddAreaDamage(player, request, WeaponAttackType, target);
                    AddHealing(player, areaHits[0].Effects, SpellHealing, atSource: true);
                    ChangeMp(player, -mpCost);
                    ConsumeStatusStack(player, StatusId.安魂祈祷);
                    AddSourceStatus(player, areaHits[0].Effects, StatusId.荣耀之剑预备, 30);
                    AdvanceSpellCombo(player, gauge, 0);
                    break;
                case ActionId.荣耀之剑:
                    if (!TryGetEnemy(player, request, out target) || !RequireStatus(player, request, StatusId.荣耀之剑预备))
                        return true;

                    areaHits = AddAreaDamage(player, request, WeaponAttackType, target);
                    consumeAfterResult = StatusId.荣耀之剑预备;
                    break;
                case ActionId.神圣领域:
                    AddTargetStatus(player, player, effects, StatusId.神圣领域, 10);
                    break;
                case ActionId.壁垒:
                    AddSourceStatus(player, effects, StatusId.壁垒, 10, effectParam: 100);
                    break;
                case ActionId.极致防御:
                    AddTargetStatus(player, player, effects, StatusId.极致防御, 15, effectParam: 0xD8);
                    AddTargetStatus(player, player, effects, StatusId.极致护盾, 15);
                    break;
                case ActionId.圣光幕帘:
                    areaHits = CreateHits(GetPartyInRange(player, request));
                    foreach (var hit in areaHits)
                    {
                        AddTargetStatus(player, hit.Target, hit.Effects, StatusId.圣光幕帘, 30);
                        AddHealing(hit.Target, hit.Effects, SpellHealing);
                    }
                    break;
                case ActionId.钢铁信念:
                    AddTargetStatus(player, player, effects, StatusId.钢铁信念, 0, effectParam: 40);
                    break;
                case ActionId.解除钢铁信念:
                    RemoveSourceStatus(player, effects, StatusId.钢铁信念);
                    break;
                case ActionId.圣盾阵:
                    if (!TrySpendOath(player, request))
                        return true;

                    AddSourceStatus(player, effects, StatusId.圣盾阵, 8, effectParam: 0xF1);
                    AddSourceStatus(player, effects, StatusId.骑士的坚守, 4, effectParam: 0xF1);
                    AddSourceStatus(player, effects, StatusId.骑士的加护, 12);
                    break;
                case ActionId.干预:
                    if (!TryGetPartyTarget(player, request, out target) || !TrySpendOath(player, request))
                        return true;

                    // 铁壁或极致防御生效时，干预减伤由 10% 增至 20%。
                    var enhancedIntervention = 
                        HasStatus(player, GeneralActions.StatusId.铁壁) ||
                        HasStatus(player, StatusId.极致防御);
                    AddTargetStatus(player, target, effects, StatusId.干预, 8,
                        effectParam: enhancedIntervention ? (byte)0xEC : (byte)0xF6);
                    AddTargetStatus(player, target, effects, StatusId.骑士的坚守, 4, effectParam: 0xF6);
                    AddTargetStatus(player, target, effects, StatusId.骑士的加护, 12);
                    break;
                case ActionId.保护:
                    if (!TryGetPartyTarget(player, request, out target) || !TrySpendOath(player, request))
                        return true;

                    AddTargetStatus(player, target, effects, StatusId.被保护, 12);
                    AddSourceStatus(player, effects, StatusId.保护, 12);
                    break;
                default:
                    return false;
            }

            if (areaHits != null)
                Entities.ResolveActionResults(player, request, target, request.ActionId, areaHits);
            else
                Entities.ResolveAction(player, request, target, request.ActionId, effects.ToArray());
            if (consumeAfterResult != 0)
                RemoveStatus(player, consumeAfterResult);
            return true;
        }

        private static void ConsumeHolySpellProc(Character player, ActionRequest request)
        {
            switch (request.CastProc)
            {
                case ActionManager.CastTimeProc.DivineMight:
                    RemoveStatus(player, StatusId.神圣魔法效果提高);
                    break;
                case ActionManager.CastTimeProc.Requiescat:
                    ConsumeStatusStack(player, StatusId.安魂祈祷);
                    break;
            }
        }

        private static bool TryGetSpellGauge(Character player, ActionRequest request, out PaladinGauge gauge, byte? expectedStep = null)
        {
            if (!TryGetGauge(out gauge))
            {
                player.RejectActionResult(request, 582);
                return false;
            }

            if (expectedStep.HasValue && gauge.ConfiteorComboStep.Get() != expectedStep.Value)
            {
                player.RejectActionResult(request, 572);
                return false;
            }

            return true;
        }

        private static bool TryGetGauge(out PaladinGauge gauge)
        {
            gauge = default;
            if (JobGaugeManager.InstancePtr == IntPtr.Zero)
                return false;

            var manager = JobGaugeManager.Instance();
            gauge = manager.Paladin;
            return manager.ClassJobId.Get() == (byte)Job.PLD && manager.CurrentGauge == gauge.Ptr;
        }

        private static void AdvanceSpellCombo(Character player, PaladinGauge gauge, byte next)
        {
            // 国服回放中，悔罪、信念之剑、真理之剑各同步 30 秒及阶段 1、2、3，英勇之剑归零。
            // 从 JobGaugeManager 的骑士对象沿技能可用性虚函数可见只比较连段阶段，忠义值无需修改。
            gauge.ConfiteorComboTimer.Set(next == 0 ? (ushort)0 : ComboDurationMilliseconds);
            gauge.ConfiteorComboStep.Set(next);
            _spellComboExpiry?.Cancel();
            var connection = GameInterop.Connection;
            _spellComboExpiry = next == 0 ? null : Entities.Scheduler.ScheduleAfter(ComboDurationMilliseconds / 1000d, () =>
            {
                if (!ReferenceEquals(connection, GameInterop.Connection) || !player.IsAlive ||
                    !TryGetGauge(out var activeGauge) || activeGauge.ConfiteorComboStep.Get() != next)
                    return;
                activeGauge.ConfiteorComboStep.Set(0);
                activeGauge.ConfiteorComboTimer.Set(0);
            }, "结束悔罪连段");
        }

        private static bool TrySpendOath(Character player, ActionRequest request)
        {
            var row = GetAction(request);
            if (row.PrimaryCostType != 41 || !TryGetGauge(out var gauge))
            {
                player.RejectActionResult(request, 572);
                return false;
            }

            var current = gauge.OathGauge.Get();
            if (current < row.PrimaryCostValue)
            {
                player.RejectActionResult(request, 572);
                return false;
            }

            gauge.OathGauge.Set((byte)(current - row.PrimaryCostValue));
            return true;
        }

        internal static void CancelSpellComboExpiry()
        {
            _spellComboExpiry?.Cancel();
            _spellComboExpiry = null;
        }
    }
}
