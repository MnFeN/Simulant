using Simulant.Game.Data;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using ActionRow = Lumina.Excel.Sheets.Action;

namespace Simulant.Simulation.Geometry
{
    public readonly partial struct ActionArea
    {
        private static readonly Regex OmenFan = new Regex(@"(?:^|[/\\_])fan([0-9]{3})(?![0-9])", RegexOptions.CultureInvariant);
        private static readonly Regex OmenDonut = new Regex(@"(?:^|[/\\_])(?:sircle_|sicle_|circle_?)([0-9]{2})([0-9]{2})(?![0-9])", RegexOptions.CultureInvariant);

        /// <summary>
        /// 从技能表创建范围。扇形完整张角和圆环内半径优先使用显式参数，缺省时只识别标准预兆命名。
        /// 原点由调用方提供；目标型技能应传入目标位置，不能一律使用施法者位置。
        /// </summary>
        public static ActionArea FromAction(uint actionId, float casterHitboxRadius = 0,
            float? coneAngleDegrees = null, float? innerRadius = null)
        {
            RequireNonNegative(casterHitboxRadius, nameof(casterHitboxRadius), "施法者碰撞半径");
            if (!GameDataService.Instance.GetSheet<ActionRow>().TryGetRow(actionId, out var action))
                throw new InvalidOperationException($"当前客户端的 Action 表中不存在技能 {actionId}，请确认客户端版本，或显式指定技能范围。");

            // CastType 的范围和碰撞半径规则参考 BossMod 的 AbilityInfo。
            // https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Replay/Analysis/AbilityInfo.cs
            var range = (float)action.EffectRange;
            switch (action.CastType)
            {
                case 2: return Circle(range);
                case 3: return Cone(range + casterHitboxRadius, ResolveConeAngle(actionId, action, coneAngleDegrees));
                case 4: return Rectangle(range + casterHitboxRadius, action.XAxisModifier);
                case 5: return Circle(range + casterHitboxRadius);
                case 10: return Donut(ResolveInnerRadius(actionId, action, innerRadius), range);
                case 12: return Rectangle(range, action.XAxisModifier);
                case 13: return Cone(range, ResolveConeAngle(actionId, action, coneAngleDegrees));
                default:
                    throw new NotSupportedException($"技能 {actionId} 的 CastType={action.CastType} 尚无已确认的范围定义，请显式使用 Circle、Cone、Rectangle、Donut，或补充该类型的规则。");
            }
        }

        private static float ResolveConeAngle(uint actionId, ActionRow action, float? explicitAngle)
        {
            if (explicitAngle.HasValue) return explicitAngle.Value;
            var matches = OmenFan.Matches(GetOmenName(action));
            if (matches.Count == 1 && int.TryParse(matches[0].Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var angle)
                && angle > 0 && angle <= 360)
                return angle;
            throw new InvalidOperationException($"技能 {actionId} 的扇形张角无法从预兆名称确定，请向 FromAction 传入 coneAngleDegrees（完整张角，单位为度）。");
        }

        private static float ResolveInnerRadius(uint actionId, ActionRow action, float? explicitInner)
        {
            if (explicitInner.HasValue) return explicitInner.Value;
            // 标准圆环名的四位数字依次为外、内半径比例，实际外半径仍以 EffectRange 为准。
            // https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Framework/Utils.cs
            var matches = OmenDonut.Matches(GetOmenName(action));
            if (matches.Count == 1
                && int.TryParse(matches[0].Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var outer)
                && int.TryParse(matches[0].Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var inner)
                && outer > 0 && inner > 0 && inner < outer)
                return action.EffectRange * ((float)inner / outer);
            throw new InvalidOperationException($"技能 {actionId} 的圆环内半径无法从预兆名称确定，请向 FromAction 传入 innerRadius（世界距离）。");
        }

        private static string GetOmenName(ActionRow action)
        {
            if (action.Omen.RowId == 0) return string.Empty;
            return action.Omen.ValueNullable?.Path.ExtractText() ?? string.Empty;
        }
    }
}
