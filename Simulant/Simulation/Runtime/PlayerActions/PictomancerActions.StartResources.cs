using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Game;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.Game;
using System;
using System.Diagnostics;
using ActionRow = Lumina.Excel.Sheets.Action;
using static Simulant.Simulation.Runtime.PlayerActions.PlayerActionRules;

namespace Simulant.Simulation.Runtime.PlayerActions
{
    internal sealed partial class PictomancerActions
    {
        private Character _combatOwner;
        private (Character Player, uint? Mp, uint ComboAction, float ComboSeconds, long StartedAt)? _opening;

        private static byte HammerCapacity(byte level) => level >= 86 ? (byte)3 : (byte)1;

        internal override void OnSimStart()
        {
            var options = PluginHost.Instance.Configuration.PictomancerResources?.Copy();
            var player = PluginHost.Instance.EntityProvider.GetMyself();
            var manager = ActionManager.Instance.ThrowIfNull();
            var combo = manager.Combo;
            _opening = (player, options?.Enabled == true ? (uint?)player.MP : null,
                combo.Action.Get(), combo.Timer.Get(), Stopwatch.GetTimestamp());
            if (options?.Enabled != true)
                return;

            var gauge = GetGauge();
            var before = ReadGauge(gauge);
            var actor = Entities.Attach(player);

            var level = player.Level;
            var hasStarryField = level >= 70 && options.StarrySeconds > 0;
            if (hasStarryField)
                EnterSimulatedCombat(player);
            var motif = (int)options.NextMotif;
            if (level < 96 && motif >= (int)PictomancerMotif.Claw)
                motif = (int)PictomancerMotif.Pom;
            var blackPaint = level >= 90 && options.BlackPaint;
            var portrait = PortraitFlag(options.Portrait, level);
            var nextGauge = (
                Palette: level >= 60 ? (byte)options.Palette : (byte)0,
                Paint: level >= 80 ? (byte)(options.WhitePaint + (blackPaint ? 1 : 0)) : (byte)0,
                Canvases: (byte)((options.CreaturePainted && level >= 30 ? 1 << motif : 0) |
                    (options.WeaponPainted && level >= 50 ? WeaponCanvas : 0) |
                    (options.LandscapePainted && level >= 70 && !hasStarryField ? LandscapeCanvas : 0)),
                Depictions: (byte)((1 << motif) - 1 | portrait));

            var hammerStacks = options.HammerSeconds > 0 && level >= 50
                ? Math.Min(options.HammerStacks, HammerCapacity(level)) : 0;
            if (level >= 60 && options.SubtractiveStacks > 0)
                SetOpeningStatus(actor, StatusId.减色混合, 9999, (ushort)options.SubtractiveStacks);
            if (level >= 90 && (blackPaint || options.Monochrome))
                SetOpeningStatus(actor, StatusId.色调反转, 9999);
            if (options.HueStep != 0)
                SetOpeningStatus(actor, options.HueStep == 1 ? StatusId.以太色调 : StatusId.以太色调II, options.HueSeconds);
            if (hammerStacks > 0)
                SetOpeningStatus(actor, StatusId.重锤连击, options.HammerSeconds, (ushort)hammerStacks);
            if (level >= 70)
            {
                SetOpeningStatus(actor, StatusId.减色混合预备, options.FreeSubtractiveSeconds);
                if (hasStarryField)
                {
                    SetOpeningStatus(actor, StatusId.星空构想, options.StarrySeconds);
                    if (level >= 82 && options.HyperphantasiaStacks > 0)
                        SetOpeningStatus(actor, StatusId.绘灵幻景, options.StarrySeconds, (ushort)options.HyperphantasiaStacks);
                    PlaceStarryField(player, player.Pos3D, player.Heading, options.StarrySeconds);
                }
            }
            if (level >= 92)
                SetOpeningStatus(actor, StatusId.彩虹点滴效果提高, options.RainbowSeconds);
            if (level >= 100)
                SetOpeningStatus(actor, StatusId.天星棱光预备, options.StarPrismSeconds);
            if (level >= 18 && options.SwiftcastSeconds > 0)
                Entities.SetStatus(actor, (ushort)GeneralActions.StatusId.即刻咏唱, options.SwiftcastSeconds, 0, actor);

            ChangeMp(player, options.Mp - (int)player.MP);
            var cooldowns = GameDataService.Instance.GetSheet<ActionRow>();
            var creature = cooldowns.GetRow((uint)ActionId.绒球构想 + (uint)motif);
            var hammer = cooldowns.GetRow((uint)ActionId.重锤构想);
            var starry = cooldowns.GetRow((uint)ActionId.星空构想);
            var portraitAction = cooldowns.GetRow((uint)(portrait == MadeenPortrait ? ActionId.马蒂恩惩罚 : ActionId.莫古力激流));
            var swiftcast = cooldowns.GetRow((uint)GeneralActions.ActionId.即刻咏唱);
            var lucid = cooldowns.GetRow((uint)ActionId.醒梦);
            GameInterop.Connection.RunInFrame(() =>
            {
                WriteGauge(player, gauge, before, nextGauge);
                SetHammerCombo(manager.Combo, level, hammerStacks, options.HammerSeconds);
                if (level >= 30)
                {
                    SetChargeCooldown(manager, creature, options.CreatureCharges, options.CreatureRecharge, level >= 96 ? 3 : 2, 3);
                    SetCooldown(manager, portraitAction, options.PortraitCooldown, portraitAction.Recast100ms / 10f);
                }
                if (level >= 50)
                    SetChargeCooldown(manager, hammer, options.HammerCharges, options.HammerRecharge, level >= 86 ? 2 : 1, 2);
                if (level >= 70)
                    SetCooldown(manager, starry, options.StarryCooldown, starry.Recast100ms / 10f);
                if (level >= 18)
                    SetCooldown(manager, swiftcast, options.SwiftcastCooldown, level >= 94 ? 40 : 60);
                if (level >= 14)
                    SetCooldown(manager, lucid, options.LucidCooldown, lucid.Recast100ms / 10f);
            });
        }

        internal override void OnSimEnd(bool canWritePlayerState)
        {
            var gaugeOwner = _gaugeOwner;
            var combatOwner = _combatOwner;
            var opening = _opening;
            _gaugeOwner = null;
            _combatOwner = null;
            _opening = null;

            if (!canWritePlayerState)
                return;

            var player = PluginHost.Instance.EntityProvider.GetMyself();
            if (player?.IsAlive != true)
                return;

            if (combatOwner?.Identity == player.Identity)
                player.ActorControl(ActorControlCategory.SetBattle, 0);
            var sameRound = opening.HasValue && opening.Value.Player.Identity == player.Identity;
            if (sameRound && opening.Value.Mp.HasValue)
                ChangeMp(player, (int)opening.Value.Mp.Value - (int)player.MP);

            GameInterop.Connection.RunInFrame(() =>
            {
                if (gaugeOwner?.Identity == player.Identity)
                {
                    var gauge = GetGauge();
                    var current = ReadGauge(gauge);
                    if (current == _lastGauge)
                        SetGauge(gauge, current, _originalGauge);
                }

                if (sameRound)
                {
                    var snapshot = opening.Value;
                    var elapsed = (Stopwatch.GetTimestamp() - snapshot.StartedAt) / (float)Stopwatch.Frequency;
                    var remaining = Math.Max(0, snapshot.ComboSeconds - elapsed);
                    var combo = ActionManager.Instance.Combo;
                    combo.Timer.Set(remaining);
                    combo.Action.Set(remaining > 0 ? snapshot.ComboAction : 0);
                }
            });
        }

        private void EnterCombatOnAttack(Character player, Character target)
        {
            if (_combatOwner == null && PluginHost.Instance.PlayerActions.EncounterEntities?.OwnsHostileTarget(target) == true)
                EnterSimulatedCombat(player);
        }

        private void EnterSimulatedCombat(Character player)
        {
            if (_combatOwner != null || player.Native.InCombat.Get())
                return;

            _combatOwner = player;
            player.ActorControl(ActorControlCategory.SetBattle, 1);
        }

        private static void SetOpeningStatus(SimActor actor, StatusId status, float duration, ushort param = 0)
        {
            if (duration > 0)
                Entities.SetStatus(actor, (ushort)status, duration, param, actor);
        }

        private static byte PortraitFlag(PictomancerPortrait portrait, byte level)
        {
            if (portrait == PictomancerPortrait.Moogle && level >= 30)
                return MooglePortrait;
            if (portrait == PictomancerPortrait.Madeen && level >= 96)
                return MadeenPortrait;
            return 0;
        }

        private static void SetHammerCombo(ComboDetail combo, byte level, int stacks, int seconds)
        {
            // 重锤替换同时读取层数和原生连段记录。每一级记录上一发成功动作的编号。
            var last = stacks == 0 ? 0u : (uint)ActionId.重锤构想;
            if (stacks > 0 && level >= 86 && stacks < HammerCapacity(level))
                last = (uint)ActionId.重锤敲章 + (uint)(2 - stacks);
            combo.Action.Set(last);
            combo.Timer.Set(stacks > 0 ? seconds : 0);
        }

        private static void SetChargeCooldown(ActionManager manager, ActionRow action, int charges,
            int recharge, int capacity, int capacityAtMaxLevel)
        {
            if (charges >= capacity)
                return;

            // 客户端在整条充能计时上按一格时长扣减。Total 始终按满级充能数计算。
            var cycle = action.Recast100ms / 10f;
            manager.SetCooldown(ActionType.Action, action.RowId, Math.Max(0, cycle * (charges + 1) - recharge), cycle * capacityAtMaxLevel);
        }

        private static void SetCooldown(ActionManager manager, ActionRow action, int remaining, float full)
        {
            if (remaining > 0)
                manager.SetCooldown(ActionType.Action, action.RowId, Math.Max(0, full - remaining), full);
        }
    }
}
