using Simulant.Game.FFCS.Client.Game.Character;
using System.Collections.Generic;

namespace Simulant.Simulation.Runtime
{
    /// <summary>只用于演出的简短循环，不模拟职业资源、连击条件或实际伤害。编号取自项目内 Action 表。</summary>
    internal sealed class SimJobActions
    {
        /// <summary>循环中的一次演出；Instant 可跳过技能表的读条，不需要模拟对应增益。</summary>
        internal readonly struct Step
        {
            public readonly uint Id;
            public readonly bool Instant;

            public Step(uint id, bool instant = false) { Id = id; Instant = instant; }
            public static implicit operator Step(uint id) => new Step(id);
        }

        public readonly bool Melee;
        public readonly Step[] Loop;
        public readonly uint MovingAction;
        public readonly uint DistantAction;
        public bool Caster => MovingAction != 0;

        private SimJobActions(bool melee, uint movingAction, uint distantAction, params Step[] loop)
        { Melee = melee; MovingAction = movingAction; DistantAction = distantAction; Loop = loop; }

        private static readonly Dictionary<Job, SimJobActions> Jobs = new Dictionary<Job, SimJobActions>
        {
            [Job.PLD] = new SimJobActions(true, 0, 24, 9, 15, 3539), // 先锋剑、暴乱剑、王权剑；远处投盾
            [Job.WAR] = new SimJobActions(true, 0, 46, 31, 37, 42), // 重劈、凶残裂、暴风斩
            [Job.DRK] = new SimJobActions(true, 0, 3624, 3617, 3623, 3632), // 重斩、吸收斩、噬魂斩
            [Job.GNB] = new SimJobActions(true, 0, 16143, 16137, 16139, 16145), // 利刃斩、残暴弹、迅连斩
            [Job.MNK] = new SimJobActions(true, 0, 36949, 53, 54, 56), // 连击、正拳、崩拳；远处绝空拳
            [Job.DRG] = new SimJobActions(true, 0, 90, 75, 78, 84), // 精准刺、贯通刺、直刺
            [Job.NIN] = new SimJobActions(true, 0, 2247, 2240, 2242, 2255), // 双刃旋、绝风、旋风刃
            [Job.SAM] = new SimJobActions(true, 0, 7486, 7477, 7478, 7481), // 刃风、阵风、月光
            [Job.RPR] = new SimJobActions(true, 0, 24386, 24373, 24374, 24375), // 切割、增盈切割、地狱切割；远处勾刃
            [Job.VPR] = new SimJobActions(true, 0, 34632, 34606, 34608, 34610, 34607, 34609, 34612), // 两套三连交替
            [Job.BRD] = new SimJobActions(false, 0, 0, 16495, 7409, 16495), // 爆发射击、辉煌箭
            [Job.MCH] = new SimJobActions(false, 0, 0, 7411, 7412, 7413), // 热分裂弹、热独头弹、热狙击弹
            [Job.DNC] = new SimJobActions(false, 0, 0, 15989, 15990, 15991, 15992), // 瀑泻、喷泉、逆瀑泻、坠喷泉
            [Job.WHM] = new SimJobActions(false, 16532, 0, 25859), // 闪灼／天辉
            [Job.SCH] = new SimJobActions(false, 17864, 0, 25865), // 极炎法／毒菌
            [Job.AST] = new SimJobActions(false, 16554, 0, 25871), // 落陷凶星／焚灼
            [Job.SGE] = new SimJobActions(false, 24314, 0, 24312), // 注药 III／均衡注药 III
            [Job.BLM] = new SimJobActions(false, 7420, 0, 3577, 3577, 3577, 3576), // 炽炎、冰澈／霹雷
            [Job.SMN] = new SimJobActions(false, 7426, 0, 3579), // 毁荡／毁绝
            [Job.RDM] = new SimJobActions(false, 7517, 0,
                37004, new Step(25855, instant: true), 37004, new Step(25856, instant: true)), // 激荡交替接瞬发赤暴雷、赤暴风；移动时飞刺
            [Job.PCT] = new SimJobActions(false, 34662, 0, 34650, 34651, 34652), // 火炎之红、疾风之绿、流水之蓝／神圣之白
            [Job.BLU] = new SimJobActions(false, 11423, 0, 18308), // 音爆／投掷沙丁鱼
        };

        public static SimJobActions For(Job job)
        {
            switch (job)
            {
                case Job.GLA: job = Job.PLD; break;
                case Job.MRD: job = Job.WAR; break;
                case Job.PGL: job = Job.MNK; break;
                case Job.LNC: job = Job.DRG; break;
                case Job.ROG: job = Job.NIN; break;
                case Job.ARC: job = Job.BRD; break;
                case Job.CNJ: job = Job.WHM; break;
                case Job.THM: job = Job.BLM; break;
                case Job.ACN: job = Job.SMN; break;
            }
            return Jobs.TryGetValue(job, out var actions) ? actions : null;
        }
    }
}
