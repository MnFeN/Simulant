using Simulant.Simulation;
using Simulant.Simulation.Options;
using Simulant.Simulation.Runtime.PlayerActions;
using System.Collections.Generic;

namespace Simulant.UI
{
    internal sealed partial class ConfigurationForm
    {
        private static List<SimOptionBase> CreatePictomancerOptions(PictomancerStartResources value) => new List<SimOptionBase>
        {
            new CheckBoxOption(nameof(value.Enabled), "自定义绘灵起始资源", value.Enabled,
                "所有模拟预设共用。每次开始或重开时应用，技能与资源受有效等级限制。"),
            new IntegerOption(nameof(value.Mp), "魔力", value.Mp, 10000),
            new IntegerOption(nameof(value.Palette), "调色量谱", value.Palette, 100),
            new IntegerOption(nameof(value.WhitePaint), "白颜料", value.WhitePaint, 5, hint: "白黑颜料合计最多 5 格。"),
            new CheckBoxOption(nameof(value.BlackPaint), "预备黑颜料", value.BlackPaint),
            new IntegerOption(nameof(value.SubtractiveStacks), "减色混合层数", value.SubtractiveStacks, 3),
            new ComboBoxOption<int>(nameof(value.HueStep), "当前色彩连段", value.HueStep,
                new Map<int> { [0] = "第一段", [1] = "第二段", [2] = "第三段" }),
            new IntegerOption(nameof(value.HueSeconds), "色调剩余（秒）", value.HueSeconds, 30),
            new ComboBoxOption<PictomancerMotif>(nameof(value.NextMotif), "动物画布图案", value.NextMotif,
                new Map<PictomancerMotif>
                {
                    [PictomancerMotif.Pom] = "绒球",
                    [PictomancerMotif.Wings] = "翅膀",
                    [PictomancerMotif.Claw] = "兽爪",
                    [PictomancerMotif.Maw] = "尖牙"
                }),
            new CheckBoxOption(nameof(value.CreaturePainted), "动物画布已画好", value.CreaturePainted),
            new CheckBoxOption(nameof(value.WeaponPainted), "重锤画布已画好", value.WeaponPainted),
            new CheckBoxOption(nameof(value.LandscapePainted), "星空画布已画好", value.LandscapePainted),
            new ComboBoxOption<PictomancerPortrait>(nameof(value.Portrait), "可用肖像", value.Portrait,
                new Map<PictomancerPortrait>
                {
                    [PictomancerPortrait.None] = "无",
                    [PictomancerPortrait.Moogle] = "莫古力",
                    [PictomancerPortrait.Madeen] = "马蒂恩"
                }),
            new IntegerOption(nameof(value.HammerStacks), "重锤连击层数", value.HammerStacks, 3,
                hint: "3、2、1 层分别对应重锤敲章、掠刷、抛光。"),
            new IntegerOption(nameof(value.HammerSeconds), "重锤连击剩余（秒）", value.HammerSeconds, 30),
            new IntegerOption(nameof(value.CreatureCharges), "动物构想充能", value.CreatureCharges, 3),
            new IntegerOption(nameof(value.CreatureRecharge), "动物构想下次充能（秒）", value.CreatureRecharge, 40, 1),
            new IntegerOption(nameof(value.HammerCharges), "重锤构想充能", value.HammerCharges, 2),
            new IntegerOption(nameof(value.HammerRecharge), "重锤构想下次充能（秒）", value.HammerRecharge, 60, 1),
            new IntegerOption(nameof(value.StarryCooldown), "星空构想冷却（秒）", value.StarryCooldown, 120),
            new IntegerOption(nameof(value.PortraitCooldown), "肖像技能冷却（秒）", value.PortraitCooldown, 30),
            new IntegerOption(nameof(value.SwiftcastCooldown), "即刻咏唱冷却（秒）", value.SwiftcastCooldown, 60),
            new IntegerOption(nameof(value.LucidCooldown), "醒梦冷却（秒）", value.LucidCooldown, 60),
            new IntegerOption(nameof(value.StarrySeconds), "已展开星空剩余（秒）", value.StarrySeconds, 20,
                hint: "设为 0 时开场没有星空区域。区域中心取点击开始模拟时角色所在的位置。"),
            new IntegerOption(nameof(value.HyperphantasiaStacks), "星空内绘灵幻景层数", value.HyperphantasiaStacks, 5),
            new IntegerOption(nameof(value.FreeSubtractiveSeconds), "减色混合预备剩余（秒）", value.FreeSubtractiveSeconds, 30),
            new IntegerOption(nameof(value.StarPrismSeconds), "天星棱光预备剩余（秒）", value.StarPrismSeconds, 20),
            new IntegerOption(nameof(value.RainbowSeconds), "彩虹点滴瞬发剩余（秒）", value.RainbowSeconds, 30),
            new IntegerOption(nameof(value.SwiftcastSeconds), "即刻咏唱效果剩余（秒）", value.SwiftcastSeconds, 10)
        };
    }
}
