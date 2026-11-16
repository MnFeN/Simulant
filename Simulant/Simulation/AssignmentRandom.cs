using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulant.Simulation
{
    /// <summary>从点名名额池生成分配，可固定自身点名，或按优先级均衡自身在同点名两人中的高低位。</summary>
    internal static class AssignmentRandom
    {
        /// <summary>
        /// 固定自身点名后洗牌其余名额；selfMatches 匹配多个名额时，按名额等概率选择。
        /// equalGroupPriority 用于每种点名恰好两人的情况：先等概率选择自身高低位，
        /// 再从对应优先级一侧等概率选择同点名队友，最后洗牌其余名额。最高、最低优先级保持普通抽样。
        /// </summary>
        internal static T[] Draw<T>(IReadOnlyList<T> pool, Random random, int self = -1,
            Predicate<T> selfMatches = null, IReadOnlyList<int> equalGroupPriority = null)
        {
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if ((selfMatches != null || equalGroupPriority != null) && (self < 0 || self >= pool.Count))
                throw new ArgumentOutOfRangeException(nameof(self), "自身下标必须位于点名名额池内。");

            var rank = -1;
            if (equalGroupPriority != null)
            {
                if (!equalGroupPriority.OrderBy(slot => slot).SequenceEqual(Enumerable.Range(0, pool.Count)))
                    throw new ArgumentException("优先级必须包含点名池中的每个玩家且不重复。", nameof(equalGroupPriority));
                if (pool.GroupBy(value => value).Any(group => group.Count() != 2))
                    throw new ArgumentException("按优先级均衡分组要求每种点名恰好两人。", nameof(pool));
                rank = Array.IndexOf(equalGroupPriority.ToArray(), self);
            }
            var preferredGroup = rank > 0 && rank < pool.Count - 1 ? random.Next(2) : -1;
            var remaining = pool.ToList();
            var result = new T[pool.Count];
            var assigned = new bool[pool.Count];
            if (selfMatches != null || preferredGroup >= 0)
            {
                var candidates = Enumerable.Range(0, remaining.Count)
                    .Where(index => selfMatches == null || selfMatches(remaining[index])).ToArray();
                if (candidates.Length == 0)
                    throw new InvalidOperationException("点名名额池中没有符合自身要求的点名。");
                var selected = candidates[random.Next(candidates.Length)];
                result[self] = remaining[selected];
                assigned[self] = true;
                remaining.RemoveAt(selected);

                if (preferredGroup >= 0)
                {
                    var partners = preferredGroup == 0 ? equalGroupPriority.Skip(rank + 1).ToArray() : equalGroupPriority.Take(rank).ToArray();
                    var partner = partners[random.Next(partners.Length)];
                    result[partner] = result[self];
                    assigned[partner] = true;
                    remaining.Remove(result[self]);
                }
            }

            for (var i = remaining.Count - 1; i > 0; i--)
            {
                var other = random.Next(i + 1);
                (remaining[i], remaining[other]) = (remaining[other], remaining[i]);
            }
            var next = 0;
            for (var slot = 0; slot < result.Length; slot++)
                if (!assigned[slot])
                    result[slot] = remaining[next++];
            return result;
        }
    }
}
