using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Network;

namespace Simulant.Core.Entity
{
    /// <summary>应用幻境·秘影武器与 99 级职业蓝装的五件护甲；没有对应预设时保留复制的外观。</summary>
    internal static class JobEquipment
    {
        // 来源：2026.07.16.0001.0000 客户端的 Item 表；按 ClassJobCategory 和 EquipSlotCategory 核对职业与部位。
        // 武器为幻境·秘影系列，物品 51000–51021、装等 795；护甲沿用物品 42569–42695 中等级 99 的职业校服蓝装。
        // 共 21 个普通战斗职业、105 件护甲、21 件主手和骑士盾牌；没有饰品，青魔及基础职业没有此套装。
        // 主手物品的 ModelSub 同时包含拳套、箭袋、枪盒、卡牌、刀鞘、双刀和画板等副手模型，不能统一清空。
        private static readonly (Job Job, ushort ArmorId, ulong Mainhand, ulong Offhand)[] Sets =
        {
            (Job.PLD, 772, Weapon(220, 25, 2), Weapon(114, 25, 2)), // 迅骑；幻境利剑·秘影、幻境盾·秘影
            (Job.MNK, 773, Weapon(342, 1, 2), Weapon(392, 1, 2)), // 静修士；幻境旋棍·秘影
            (Job.WAR, 774, Weapon(418, 4, 2), 0), // 苦训；幻境战锤·秘影
            (Job.DRG, 775, Weapon(528, 18, 2), 0), // 腾空；幻境长枪·秘影
            (Job.BRD, 776, Weapon(640, 1, 2), Weapon(698, 150)), // 比胡；幻境琴弓·秘影
            (Job.WHM, 777, Weapon(808, 38, 2), 0), // 虔诚；幻境牧杖·秘影
            (Job.BLM, 778, Weapon(1033, 9, 2), 0), // 大法师；幻境咒杖·秘影
            (Job.SMN, 779, Weapon(1767, 1, 2), 0), // 塑形；幻境魔导书·秘影
            (Job.SCH, 780, Weapon(1766, 1, 2), 0), // 教育；幻境魔导典·秘影
            (Job.NIN, 781, Weapon(1816, 3, 2), Weapon(1866, 3, 2)), // 百地；幻境匕首·秘影
            (Job.DRK, 782, Weapon(1519, 20, 2), 0), // 堕落；幻境断头剑·秘影
            (Job.MCH, 783, Weapon(2027, 15, 2), Weapon(2099, 1)), // 前驱者；幻境左轮枪·秘影
            (Job.AST, 784, Weapon(2155, 1, 2), Weapon(2199, 1, 220)), // 记星士；幻境天球仪·秘影
            (Job.SAM, 785, Weapon(2215, 4, 2), Weapon(2251, 122)), // 左近士；幻境武士刀·秘影
            (Job.RDM, 786, Weapon(2311, 13, 2), Weapon(2351, 97, 2)), // 玫瑰血；幻境刺剑·秘影
            (Job.GNB, 787, Weapon(2501, 87, 2), 0), // 堡垒；幻境枪刃·秘影
            (Job.DNC, 788, Weapon(2616, 1, 2), Weapon(2666, 1, 2)), // 跃舞；幻境折扇·秘影
            (Job.RPR, 790, Weapon(2827, 1, 2), 0), // 暗杀者；幻境战镰·秘影
            (Job.SGE, 789, Weapon(2703, 18, 2), 0), // 蛇杖；幻境振空摆·秘影
            (Job.VPR, 840, Weapon(3101, 32, 2), Weapon(3151, 32, 2)), // 蝰蛇；幻境双牙·秘影
            (Job.PCT, 841, Weapon(2909, 1, 2), Weapon(2951, 53, 2)), // 绘灵法师；幻境圆笔·秘影
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

        // 武器的前三个 ushort 依次为模型编号、类型和变体；保持原色，主副手分别使用表中的变体。
        private static ulong Weapon(ushort id, ushort type, ushort variant = 1) => id | (ulong)type << 16 | (ulong)variant << 32;
    }
}
