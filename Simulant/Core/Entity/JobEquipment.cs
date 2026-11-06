using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Network;

namespace Simulant.Core.Entity
{
    /// <summary>应用已核实的建角预览职业套装；没有对应数据时保留复制的外观。</summary>
    internal static class JobEquipment
    {
        // 来源：SaintCoinach 2026.07.16.0001.0000 导出的 CharaMakeClassEquip.csv，完整的 8 行。
        // 字段定义：https://github.com/xivdev/EXDSchema/blob/latest/CharaMakeClassEquip.yml
        // 原表按基础职业编号关联，这里仅明确映射到对应进阶职业；秘术师预览是召唤师套装，不适用于学者。
        // Item.csv 的上衣模型分别对应英骑、昭武、勇战、腾龙、咏诗、白愈、黑术、唤灵套装。
        private static readonly (Job Job, ushort ArmorId, ulong Mainhand, ulong Offhand)[] Sets =
        {
            (Job.PLD, 42, Weapon(201, 10), Weapon(101, 11)),
            (Job.MNK, 44, Weapon(304, 1), Weapon(354, 1)),
            (Job.WAR, 37, Weapon(401, 7), 0),
            (Job.DRG, 36, Weapon(504, 1), 0),
            (Job.BRD, 41, Weapon(603, 1), Weapon(698, 4)),
            (Job.WHM, 39, Weapon(801, 6), 0),
            (Job.BLM, 40, Weapon(1001, 2), 0),
            (Job.SMN, 132, Weapon(1705, 1), 0)
        };

        internal static bool HasSet(Job job) => FindSet(job) >= 0;

        internal static unsafe bool TryApply(Job job, ref CommonSpawnData common)
        {
            var index = FindSet(job);
            if (index < 0) return false;

            var set = Sets[index];
            common.MainhandWeaponModel = set.Mainhand;
            common.OffhandWeaponModel = set.Offhand;
            // 这 8 套的五件护甲均为同一模型编号、变体 1。旧格式的高字节是第一染色。
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

        // 武器的前三个 ushort 依次为模型编号、类型和变体；当前预览数据的变体均为 1、染色均为 0。
        private static ulong Weapon(ushort id, ushort type) => id | (ulong)type << 16 | 1UL << 32;
    }
}
