using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Game;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Game.FFCS.Client.Game.Gauge;
using Simulant.Simulation.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ActionEffect = Simulant.Game.FFCS.Client.Game.Character.ActionEffectHandler.Effect;
using ActionRow = Lumina.Excel.Sheets.Action;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;
using static Simulant.Simulation.Runtime.PlayerActions.PlayerActionRules;

namespace Simulant.Simulation.Runtime.PlayerActions
{
    internal sealed partial class PictomancerActions : JobActions<PictomancerActions, PictomancerGauge>
    {
        private enum ActionId : uint
        {
            昏乱 = 7560,
            醒梦 = 7562,
            催眠 = 25880,
            火炎之红 = 34650, 疾风之绿, 流水之蓝,
            冰结之蓝青, 飞石之纯黄, 闪雷之品红,
            烈炎之红, 烈风之绿, 激水之蓝,
            冰冻之蓝青, 坚石之纯黄, 震雷之品红,
            神圣之白, 彗星之黑,
            绒球彩绘, 翅膀彩绘, 兽爪彩绘, 尖牙彩绘, 重锤彩绘, 星空彩绘,
            绒球构想, 翅膀构想, 兽爪构想, 尖牙构想, 重锤构想, 星空构想,
            莫古力激流, 马蒂恩惩罚,
            重锤敲章, 重锤掠刷, 重锤抛光,
            天星棱光, 天星棱光治疗, 减色混合, 速涂, 坦培拉涂层, 油性坦培拉涂层,
            彩虹点滴 = 34688
        }

        private enum StatusId : ushort
        {
            昏乱 = 1203,
            醒梦 = 1204,
            睡眠 = 3,
            减色混合 = 3674,
            以太色调 = 3675,
            以太色调II = 3676,
            彩虹点滴效果提高 = 3679,
            重锤连击 = 3680,
            天星棱光预备 = 3681,
            速涂 = 3684,
            星空构想 = 3685,
            坦培拉涂层 = 3686,
            油性坦培拉涂层 = 3687,
            绘灵幻景 = 3688,
            绘画装置 = 3689,
            减色混合预备 = 3690,
            色调反转 = 3691
        }

        private const byte CreatureCanvases = 0x0F;
        private const byte WeaponCanvas = 0x10;
        private const byte LandscapeCanvas = 0x20;
        private const byte MooglePortrait = 0x10;
        private const byte MadeenPortrait = 0x20;
        private const byte CreatureDepictions = 0x07;
        private const float StarryFieldRadius = 8f;
        private const float StarryFieldDuration = 20f;
        private const string StarryFieldCircle = "s5p5_general_blue_01e1";
        private const byte MagicAttack = 5;

        private Character _gaugeOwner;
        private (byte Palette, byte Paint, byte Canvases, byte Depictions) _originalGauge, _lastGauge;
        private PictomancerActions() : base(Job.PCT) => AddSpecialAnimationLock(ActionId.速涂, 0.8f);

        internal override bool SupportsLevel(byte level) => true;

        internal override bool TryHandleAction(ActionRequest request)
        {
            var action = (ActionId)request.ActionId;
            if (!IsSupported(action))
                return false;

            var player = PluginHost.Instance.EntityProvider.GetMyself();
            return player?.Job == Job.PCT && Resolve(player, request, action);
        }

        private bool Resolve(Character player, ActionRequest request, ActionId action)
        {
            var row = GetAction(request);
            var target = player;
            if (row.Range > 0 && !TryGetEnemy(player, request, out target))
                return true;
            if (row.Range > 0 && Vector2.Distance(player.Pos, target.Pos) >
                row.Range + target.HitboxRadius + player.HitboxRadius)
                return Reject(player, request, 566);

            var gauge = GetGauge();
            var before = ReadGauge(gauge);
            var after = before;
            int mpCost;
            // 国服 Action 表以类型 96 或 92 保存绘灵色魔法的魔力消耗，每单位为 100 MP。
            if (row.PrimaryCostType == 96 || row.PrimaryCostType == 92)
            {
                mpCost = row.PrimaryCostValue * 100;
                if (player.MP < mpCost)
                    return Reject(player, request, 568);
            }
            else if (!TryGetMpCost(player, request, out mpCost))
                return true;

            var hits = GetHits(player, request, target, row);
            var effects = hits[0].Effects;
            var subtractive = HasStatus(player, StatusId.减色混合);

            if (action >= ActionId.火炎之红 && action <= ActionId.震雷之品红)
            {
                var color = (int)(action - ActionId.火炎之红) % 6;
                var step = color % 3;
                var hues = HasStatus(player, StatusId.以太色调II) ? 2 : HasStatus(player, StatusId.以太色调) ? 1 : 0;
                if (subtractive != (color >= 3) || hues != step)
                    return Reject(player, request, 572);

                if (step == 0)
                    AddSourceStatus(player, effects, StatusId.以太色调, 30);
                else if (step == 1)
                {
                    RemoveStatus(player, StatusId.以太色调);
                    AddSourceStatus(player, effects, StatusId.以太色调II, 30);
                }
                else
                {
                    RemoveStatus(player, StatusId.以太色调II);
                    if (player.Level >= 80)
                        after.Paint = (byte)Math.Min(5, before.Paint + 1);
                    if (!subtractive && player.Level >= 60)
                        after.Palette = (byte)Math.Min(100, before.Palette + 25);
                }

                if (subtractive)
                    ConsumeStatusStack(player, StatusId.减色混合);
                ConsumeInspiration(player, effects);
            }
            else
            {
                switch (action)
                {
                    case ActionId.神圣之白:
                    case ActionId.彗星之黑:
                        var black = action == ActionId.彗星之黑;
                        if (before.Paint == 0 || black != HasStatus(player, StatusId.色调反转))
                            return Reject(player, request, 572);
                        after.Paint--;
                        if (black)
                            RemoveStatus(player, StatusId.色调反转);
                        ConsumeInspiration(player, effects);
                        break;
                    case ActionId.减色混合:
                        var free = HasStatus(player, StatusId.减色混合预备);
                        if (subtractive || (!free && before.Palette < row.PrimaryCostValue))
                            return Reject(player, request, 572);
                        if (free)
                            RemoveStatus(player, StatusId.减色混合预备);
                        else
                            after.Palette = (byte)(before.Palette - row.PrimaryCostValue);
                        AddSourceStatus(player, effects, StatusId.减色混合, 9999, param: 3);
                        if (player.Level >= 90 && !HasStatus(player, StatusId.色调反转))
                            AddSourceStatus(player, effects, StatusId.色调反转, 9999);
                        break;
                    case ActionId.绒球彩绘:
                    case ActionId.翅膀彩绘:
                    case ActionId.兽爪彩绘:
                    case ActionId.尖牙彩绘:
                        var motif = (byte)(1 << (int)(action - ActionId.绒球彩绘));
                        if ((before.Canvases & CreatureCanvases) != 0)
                            return Reject(player, request, 572);
                        after.Canvases |= motif;
                        break;
                    case ActionId.重锤彩绘:
                        if ((before.Canvases & WeaponCanvas) != 0)
                            return Reject(player, request, 572);
                        after.Canvases |= WeaponCanvas;
                        break;
                    case ActionId.星空彩绘:
                        if ((before.Canvases & LandscapeCanvas) != 0 || HasStatus(player, StatusId.星空构想))
                            return Reject(player, request, 572);
                        after.Canvases |= LandscapeCanvas;
                        break;
                    case ActionId.绒球构想:
                    case ActionId.翅膀构想:
                    case ActionId.兽爪构想:
                    case ActionId.尖牙构想:
                        var creature = (byte)(1 << (int)(action - ActionId.绒球构想));
                        if ((before.Canvases & creature) == 0)
                            return Reject(player, request, 572);
                        after.Canvases &= (byte)~creature;
                        if (action == ActionId.尖牙构想)
                            after.Depictions = MadeenPortrait;
                        else
                        {
                            after.Depictions |= creature;
                            if (action == ActionId.翅膀构想)
                                after.Depictions = player.Level >= 96
                                    ? (byte)((after.Depictions & CreatureDepictions) | MooglePortrait) : MooglePortrait;
                        }
                        break;
                    case ActionId.莫古力激流:
                    case ActionId.马蒂恩惩罚:
                        var portrait = action == ActionId.莫古力激流 ? MooglePortrait : MadeenPortrait;
                        if ((before.Depictions & portrait) == 0)
                            return Reject(player, request, 572);
                        after.Depictions &= (byte)~portrait;
                        break;
                    case ActionId.重锤构想:
                        if ((before.Canvases & WeaponCanvas) == 0 || HasStatus(player, StatusId.重锤连击))
                            return Reject(player, request, 572);
                        after.Canvases = (byte)(before.Canvases & ~WeaponCanvas);
                        AddSourceStatus(player, effects, StatusId.重锤连击, 30, param: HammerCapacity(player.Level));
                        AddWeaponCombo(effects, action);
                        break;
                    case ActionId.重锤敲章:
                    case ActionId.重锤掠刷:
                    case ActionId.重锤抛光:
                        var hammerStep = (int)(action - ActionId.重锤敲章);
                        var expectedStacks = HammerCapacity(player.Level) - hammerStep;
                        if (expectedStacks <= 0 || GetStacks(player, StatusId.重锤连击) != expectedStacks)
                            return Reject(player, request, 572);
                        ConsumeStatusStack(player, StatusId.重锤连击);
                        AddWeaponCombo(effects, action, comboSucceeded: hammerStep > 0);
                        break;
                    case ActionId.星空构想:
                        if ((before.Canvases & LandscapeCanvas) == 0 || !player.Native.InCombat.Get())
                            return Reject(player, request, 572);
                        after.Canvases = (byte)(before.Canvases & ~LandscapeCanvas);
                        ConferStarryMuse(player, request, hits);
                        break;
                    case ActionId.天星棱光:
                        if (!RequireStatus(player, request, StatusId.天星棱光预备))
                            return true;
                        RemoveStatus(player, StatusId.天星棱光预备);
                        ConsumeInspiration(player, effects);
                        break;
                    case ActionId.彩虹点滴:
                        after.Paint = (byte)Math.Min(5, before.Paint + 1);
                        if (request.CastProc == ActionManager.CastTimeProc.RainbowBright)
                            RemoveStatus(player, StatusId.彩虹点滴效果提高);
                        break;
                    case ActionId.速涂:
                        if (player.Level >= 84)
                            AddSourceStatus(player, effects, StatusId.速涂, 5, param: 30);
                        break;
                    case ActionId.坦培拉涂层:
                        AddSourceStatus(player, effects, StatusId.坦培拉涂层, 10);
                        break;
                    case ActionId.油性坦培拉涂层:
                        if (!RequireStatus(player, request, StatusId.坦培拉涂层))
                            return true;
                        RemoveStatus(player, StatusId.坦培拉涂层);
                        foreach (var hit in hits)
                            AddTargetStatus(player, hit.Target, hit.Effects, StatusId.油性坦培拉涂层, 10);
                        break;
                    case ActionId.昏乱:
                        AddTargetStatus(player, target, effects, StatusId.昏乱, player.Level >= 98 ? 15 : 10);
                        break;
                    case ActionId.醒梦:
                        AddSourceStatus(player, effects, StatusId.醒梦, 21);
                        ObserveStatus(player, player, StatusId.醒梦, onTick: targetWithLucid =>
                        {
                            if (targetWithLucid.HP != 0)
                                ChangeMp(targetWithLucid, 550);
                        });
                        break;
                    case ActionId.催眠:
                        foreach (var hit in hits)
                            hit.Effects.Add(new ActionEffect { Type = 0x37, Value = (ushort)StatusId.睡眠 });
                        break;
                }
            }

            WriteGauge(player, gauge, before, after);
            ChangeMp(player, -mpCost);
            Entities.ResolveActionResults(player, request, target, request.ActionId, hits);
            if (row.Range > 0 && action != ActionId.催眠 && action != ActionId.昏乱)
                EnterCombatOnAttack(player, target);
            if (action == ActionId.天星棱光)
                HealWithStarPrism(player, request, target);
            return true;
        }

        private static bool IsSupported(ActionId action) =>
            (action >= ActionId.火炎之红 && action <= ActionId.油性坦培拉涂层 && action != ActionId.天星棱光治疗) ||
            action == ActionId.彩虹点滴 || action == ActionId.昏乱 || action == ActionId.醒梦 || action == ActionId.催眠;

        private static (Character Target, List<ActionEffect> Effects)[] GetHits(
            Character player, ActionRequest request, Character target, ActionRow row)
        {
            if (target.Identity == player.Identity && row.EffectRange > 0)
            {
                // 来源自己始终是第一项，以便合并星空构想的职业触发状态。
                var others = GetPartyInRange(player, request).Where(member => member.Identity != player.Identity);
                return CreateHits(new[] { player }.Concat(others));
            }

            var action = (ActionId)request.ActionId;
            var targets = new[] { target }.AsEnumerable();
            if (row.EffectRange > 0)
            {
                // 直线技能以施法者为原点。目标圆形技能以主目标为圆心。
                var center = row.CastType == 4 ? player : target;
                targets = targets.Concat(GetAreaEnemies(player, request, center).Where(enemy => enemy.Identity != target.Identity));
            }

            var hits = CreateHits(targets);
            if (row.Range > 0 && action != ActionId.昏乱 && action != ActionId.催眠)
            {
                foreach (var hit in hits)
                {
                    AddDamage(hit.Effects, request, MagicAttack);
                    if (action >= ActionId.重锤敲章 && action <= ActionId.重锤抛光)
                    {
                        // 国服技能记录的伤害标志 0x60 表示暴击且直击。
                        var damage = hit.Effects[0];
                        damage.Param0 = 0x60;
                        hit.Effects[0] = damage;
                    }
                }
            }
            return hits;
        }

        private static void ConferStarryMuse(Character player, ActionRequest request,
            (Character Target, List<ActionEffect> Effects)[] hits)
        {
            foreach (var hit in hits)
                AddTargetStatus(player, hit.Target, hit.Effects, StatusId.星空构想, StarryFieldDuration, effectParam: 5);
            var sourceEffects = hits[0].Effects;
            AddSourceStatus(player, sourceEffects, StatusId.减色混合预备, 30);
            if (player.Level >= 82)
                AddSourceStatus(player, sourceEffects, StatusId.绘灵幻景, 30, param: 5);
            if (player.Level >= 100)
                AddSourceStatus(player, sourceEffects, StatusId.天星棱光预备, 20);
            PlaceStarryField(player, request.GroundPosition ?? player.Pos3D, request.Facing, StarryFieldDuration);
        }

        private static void PlaceStarryField(Character player, Vector3 position, float heading, float duration)
        {
            const uint starry = (uint)ActionId.星空构想;
            var entities = Entities;
            entities.PlaceGroundEffect(player, starry, position, heading, duration, ActionArea.Circle(StarryFieldRadius),
                onExpire: effect => RemoveInspiration(effect.Source), showVisual: effect =>
                {
                    try
                    {
                        VfxManager.ShowOmen(StarryFieldCircle, effect.Position, effect.Heading,
                            new Vector3(StarryFieldRadius, StarryFieldRadius, 1), duration, effect);
                    }
                    catch (InvalidOperationException error)
                    {
                        PluginHost.Instance?.LogWarning("星空构想范围特效未能播放：" + error.Message);
                    }
                });
            if (!entities.TryGetGroundEffect(player, starry, out var field))
                return;

            // 星空进出范围会切换咏唱加速，使用独立于三秒周期效果的检查。
            void UpdateInspiration()
            {
                if (!entities.TryGetGroundEffect(player, starry, out var current) || !ReferenceEquals(current, field))
                    return;
                UpdateInspirationForPlayer(player, player.Job == Job.PCT && player.HP > 0 && field.Contains(player));
                entities.Scheduler.ScheduleAfter(0.25, UpdateInspiration, "检查星空构想范围");
            }

            UpdateInspiration();
        }

        private static void UpdateInspirationForPlayer(Character player, bool inside)
        {
            if (!inside || !HasStatus(player, StatusId.绘灵幻景))
            {
                RemoveInspiration(player);
                return;
            }

            var actor = Entities.Attach(player);
            if (!Entities.TryGetOwnedStatus(actor, (ushort)StatusId.绘画装置, out _, out var seconds) || seconds < 0.5f)
                Entities.SetStatus(actor, (ushort)StatusId.绘画装置, 5, 0, actor);
        }

        private static void RemoveInspiration(Character player)
            => Entities.RemoveStatus(Entities.Attach(player), (ushort)StatusId.绘画装置);

        private static void ConsumeInspiration(Character player, List<ActionEffect> effects)
        {
            if (!Entities.TryGetGroundEffect(player, (uint)ActionId.星空构想, out var field) || !field.Contains(player))
                return;

            var stacks = GetStacks(player, StatusId.绘灵幻景);
            if (stacks == 0)
                return;
            ConsumeStatusStack(player, StatusId.绘灵幻景);
            if (stacks == 1)
            {
                RemoveInspiration(player);
                if (player.Level >= 92)
                    AddSourceStatus(player, effects, StatusId.彩虹点滴效果提高, 30);
            }
        }

        private static void HealWithStarPrism(Character player, ActionRequest request, Character target)
        {
            var hits = CreateHits(GetPartyInRange(player, request, target, ActionArea.Circle(30)));
            if (hits.Length == 0)
                return;
            foreach (var hit in hits)
                AddHealing(hit.Target, hit.Effects, 10000);
            Entities.ResolveActionResults(player, null, target, (uint)ActionId.天星棱光治疗, hits);
        }

        private static ushort GetStacks(Character player, StatusId status)
        {
            var statuses = player.Native.StatusManager;
            var index = statuses.GetStatusIndex((ushort)status, player.Id);
            return index < 0 ? (ushort)0 : statuses.GetStatus(index).Param.Get();
        }

        private static (byte Palette, byte Paint, byte Canvases, byte Depictions) ReadGauge(PictomancerGauge gauge)
            => (gauge.Palette.Get(), gauge.Paint.Get(), gauge.Canvases.Get(), gauge.Depictions.Get());

        private void WriteGauge(Character player, PictomancerGauge gauge,
            (byte Palette, byte Paint, byte Canvases, byte Depictions) before,
            (byte Palette, byte Paint, byte Canvases, byte Depictions) after)
        {
            if (before == after)
                return;
            if (_gaugeOwner?.Identity != player.Identity || before != _lastGauge)
            {
                _gaugeOwner = player;
                _originalGauge = before;
            }
            _lastGauge = after;
            SetGauge(gauge, before, after);
        }

        private static void SetGauge(PictomancerGauge gauge,
            (byte Palette, byte Paint, byte Canvases, byte Depictions) before,
            (byte Palette, byte Paint, byte Canvases, byte Depictions) after)
        {
            if (before.Palette != after.Palette)
                gauge.Palette.Set(after.Palette);
            if (before.Paint != after.Paint)
                gauge.Paint.Set(after.Paint);
            if (before.Canvases != after.Canvases)
                gauge.Canvases.Set(after.Canvases);
            if (before.Depictions != after.Depictions)
                gauge.Depictions.Set(after.Depictions);
        }

        private static bool Reject(Character player, ActionRequest request, uint message)
        {
            player.RejectActionResult(request, message);
            return true;
        }
    }
}
