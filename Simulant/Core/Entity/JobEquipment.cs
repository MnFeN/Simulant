using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Network;

namespace Simulant.Core.Entity
{
    /// <summary>应用 99 级职业蓝装的武器与五件护甲；没有对应套装时保留复制的外观。</summary>
    internal static class JobEquipment
    {
        // 来源：SaintCoinach 2026.07.16.0001.0000 导出的 Item.csv，物品 42569–42695。
        // 筛选条件为装备等级 99、装等 690、稀有度 3；按 ClassJobCategory 和 EquipSlotCategory 核对职业与部位。
        // 共 21 个普通战斗职业、105 件护甲、21 件主手和骑士盾牌；没有饰品，青魔及基础职业没有此套装。
        // 主手物品的 ModelSub 同时包含拳套、箭袋、枪盒、卡牌、刀鞘、双刀和画板等副手模型，不能统一清空。
        private static readonly (Job Job, ushort ArmorId, ulong Mainhand, ulong Offhand)[] Sets =
        {
            (Job.PLD, 772, Weapon(220, 2), Weapon(114, 2)), // 迅骑
            (Job.MNK, 773, Weapon(338, 1), Weapon(388, 1)), // 静修士
            (Job.WAR, 774, Weapon(417, 1), 0), // 苦训
            (Job.DRG, 775, Weapon(528, 1), 0), // 腾空
            (Job.BRD, 776, Weapon(635, 1), Weapon(698, 130)), // 比胡
            (Job.WHM, 777, Weapon(831, 2), 0), // 虔诚
            (Job.BLM, 778, Weapon(1033, 2), 0), // 大法师
            (Job.SMN, 779, Weapon(1752, 1), 0), // 塑形
            (Job.SCH, 780, Weapon(1753, 1), 0), // 教育
            (Job.NIN, 781, Weapon(1801, 128), Weapon(1851, 128)), // 百地
            (Job.DRK, 782, Weapon(1519, 2), 0), // 堕落
            (Job.MCH, 783, Weapon(2026, 2), Weapon(2099, 1)), // 前驱者
            (Job.AST, 784, Weapon(2136, 1), Weapon(2199, 1, 188)), // 记星士
            (Job.SAM, 785, Weapon(2218, 1), Weapon(2259, 3)), // 左近士
            (Job.RDM, 786, Weapon(2301, 97), Weapon(2380, 1)), // 玫瑰血
            (Job.GNB, 787, Weapon(2501, 64), 0), // 堡垒
            (Job.DNC, 788, Weapon(2611, 2), Weapon(2661, 2)), // 跃舞
            (Job.RPR, 790, Weapon(2816, 1), 0), // 暗杀者
            (Job.SGE, 789, Weapon(2701, 26), 0), // 蛇杖
            (Job.VPR, 840, Weapon(3101, 1), Weapon(3151, 1)), // 蝰蛇
            (Job.PCT, 841, Weapon(2901, 1), Weapon(2951, 1)), // 绘灵法师
        };

        internal static bool HasSet(Job job) => FindSet(job) >= 0;

        internal static unsafe bool TryApply(Job job, ref CommonSpawnData common)
        {
            var index = FindSet(job);
            if (index < 0) return false;

            var set = Sets[index];
            common.MainhandWeaponModel = set.Mainhand;
            common.OffhandWeaponModel = set.Offhand;
            // 已逐件核对：每套五件护甲均使用相同模型编号、变体 1。旧格式的高字节是第一染色。
            var armor = (uint)set.ArmorId | 1u << 16;
            fixed (uint* equipment = common.Equipment)
            fixed (byte* stain2 = common.ModelStain2)
            {
                for (var i = 0; i < 5; i++)
                {
                    equipment[i] = armor;
                    stain2[i] = 0;
                }
            }
            return true;
        }

        private static int FindSet(Job job)
        {
            for (var i = 0; i < Sets.Length; i++)
                if (Sets[i].Job == job) return i;
            return -1;
        }

        // 武器的前三个 ushort 依次为模型编号、类型和变体；保持原色，占星副手的变体为 188。
        private static ulong Weapon(ushort id, ushort type, ushort variant = 1) => id | (ulong)type << 16 | (ulong)variant << 32;
    }
}
