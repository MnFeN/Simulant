using Simulant.Game.FFCS.Client.Game.Character;
using System;

namespace Simulant.Simulation.Runtime
{
    /// <summary>模拟队员采用职能加职业简称，便于辨认点名与走位。</summary>
    internal static class SimPartyNames
    {
        private static readonly string[] Roles = { "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };
        private static readonly string[] Jobs =
        {
            "冒险者", "剑术", "格斗", "斧术", "枪术", "弓箭", "幻术", "咒术",
            "刻木", "锻铁", "铸甲", "雕金", "制革", "裁衣", "炼金", "烹调",
            "采矿", "园艺", "捕鱼", "骑士", "武僧", "战士", "龙骑", "诗人",
            "白魔", "黑魔", "秘术", "召唤", "学者", "双剑", "忍者", "机工",
            "黑骑", "占星", "武士", "赤魔", "青魔", "绝枪", "舞者", "钐镰",
            "贤者", "蝰蛇", "绘灵"
        };

        internal static string Get(int partyIndex, Job job)
        {
            if (partyIndex < 1 || partyIndex > Roles.Length)
                throw new ArgumentOutOfRangeException(nameof(partyIndex), "模拟职能序号必须在 1 至 8 之间。");
            if ((byte)job >= Jobs.Length)
                throw new ArgumentOutOfRangeException(nameof(job), "尚未定义该职业的中文简称。");
            return Roles[partyIndex - 1] + Jobs[(byte)job];
        }
    }
}
