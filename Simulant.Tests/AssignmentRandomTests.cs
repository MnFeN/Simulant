using Simulant.Simulation;
using System;
using System.Linq;

namespace Simulant.Tests
{
    internal static class AssignmentRandomTests
    {
        public static void FixedDuplicateAssignmentsDoNotPreferTheFirstMatchingMember()
        {
            var pool = new[] { 0, 0, 1, 1, 2, 2, 3, 3 };
            var random = new Random(20261117);
            var partners = new int[8];
            const int samples = 7000;
            for (var sample = 0; sample < samples; sample++)
            {
                var values = AssignmentRandom.Draw(pool, random, 1, value => value == 0);
                Check(values[1] == 0 && values.OrderBy(value => value).SequenceEqual(pool), "限制自身点名不能改变各点名人数。");
                var partner = Enumerable.Range(0, 8).Single(slot => slot != 1 && values[slot] == 0);
                partners[partner]++;
            }
            for (var slot = 0; slot < 8; slot++)
                if (slot != 1)
                    Check(Math.Abs(partners[slot] / (double)samples - 1.0 / 7) < 0.03,
                        "同点名队友不应因职能下标而产生概率偏差。");
            Check(pool.SequenceEqual(new[] { 0, 0, 1, 1, 2, 2, 3, 3 }), "抽样不得修改调用方的点名名额池。");
        }

        public static void PrioritySamplingBalancesGroupsAndKeepsPartnersUniformWithinEachSide()
        {
            var pool = new[] { 0, 0, 1, 1, 2, 2, 3, 3 };
            var priority = new[] { 5, 2, 7, 0, 4, 1, 6, 3 };
            foreach (var rank in new[] { 1, 3, 6 })
            {
                var random = new Random(20261117 + rank);
                var self = priority[rank];
                var partners = new int[8];
                const int samples = 14000;
                for (var sample = 0; sample < samples; sample++)
                {
                    var values = AssignmentRandom.Draw(pool, random, self, value => value == 1, priority);
                    Check(values[self] == 1 && values.OrderBy(value => value).SequenceEqual(pool), "均衡分组仍必须保留自身点名及各点名人数。");
                    var partner = Enumerable.Range(0, 8).Single(slot => slot != self && values[slot] == 1);
                    partners[partner]++;
                }
                var higherCount = priority.Take(rank).Sum(slot => partners[slot]);
                Check(Math.Abs(higherCount / (double)samples - 0.5) < 0.02, "自身高低位概率应各占一半。");
                for (var otherRank = 0; otherRank < priority.Length; otherRank++)
                    if (otherRank != rank)
                    {
                        var expected = 0.5 / (otherRank < rank ? rank : priority.Length - rank - 1);
                        Check(Math.Abs(partners[priority[otherRank]] / (double)samples - expected) < 0.02,
                            "同点名队友在选定优先级一侧应等概率，不能偏向某一职能。");
                    }
            }
        }

        public static void FixedMatchesPreserveOccurrenceWeights()
        {
            var pool = new[] { 0, 0, 1, 2 };
            var random = new Random(20261118);
            var counts = new int[2];
            const int samples = 6000;
            for (var sample = 0; sample < samples; sample++)
            {
                var values = AssignmentRandom.Draw(pool, random, 2, value => value < 2);
                Check(values.OrderBy(value => value).SequenceEqual(pool), "固定自身点名时不得增减任何名额。");
                counts[values[2]]++;
            }
            Check(Math.Abs(counts[0] / (double)samples - 2.0 / 3) < 0.03,
                "多个名额满足自身要求时，应保留原名额的概率权重。");
        }

        public static void PriorityExtremesUseOrdinarySampling()
        {
            var pool = new[] { 0, 0, 1, 1, 2, 2, 3, 3 };
            var priority = new[] { 5, 2, 7, 0, 4, 1, 6, 3 };
            foreach (var self in new[] { priority[0], priority[7] })
                for (var seed = 0; seed < 64; seed++)
                {
                    var original = AssignmentRandom.Draw(pool, new Random(seed), self, value => value == 2);
                    var enabled = AssignmentRandom.Draw(pool, new Random(seed), self, value => value == 2, priority);
                    Check(original.SequenceEqual(enabled), "优先级两端不应受均衡分组影响。");
                }
        }

        internal sealed class GroupRandom : Random
        {
            private readonly int _group;
            internal bool GroupChosen { get; private set; }

            internal GroupRandom(int seed, int group) : base(seed)
            {
                _group = group;
            }

            public override int Next(int maxValue)
            {
                if (!GroupChosen)
                {
                    GroupChosen = true;
                    if (_group < 0 || _group >= maxValue)
                        throw new InvalidOperationException("检查指定的组别超出抽样范围。");
                    return _group;
                }
                return base.Next(maxValue);
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }
    }
}
