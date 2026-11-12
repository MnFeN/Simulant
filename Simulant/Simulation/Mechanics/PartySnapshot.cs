using Simulant.Simulation.Geometry;
using Simulant.Simulation.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Simulation.Mechanics
{
    /// <summary>某次判定中的队员位置；Slot 保留传入候选列表中的原始下标。</summary>
    public readonly struct PartyMemberSnapshot
    {
        public SimActor Actor { get; }
        public Vector2 Position { get; }
        public int Slot { get; }

        public PartyMemberSnapshot(SimActor actor, Vector2 position, int slot)
        {
            if (slot < 0) throw new ArgumentOutOfRangeException(nameof(slot), "候选下标不能为负数。");
            ValidatePosition(position);
            Actor = actor;
            Position = position;
            Slot = slot;
        }

        internal static void ValidatePosition(Vector2 position)
        {
            if (float.IsNaN(position.X) || float.IsInfinity(position.X) || float.IsNaN(position.Y) || float.IsInfinity(position.Y))
                throw new ArgumentOutOfRangeException(nameof(position), "判定坐标必须为有限数。");
        }
    }

    /// <summary>一次读取、重复查询的实际站位；查询期间不会再次访问实体。</summary>
    public sealed class PartySnapshot
    {
        public IReadOnlyList<PartyMemberSnapshot> Members { get; }

        /// <summary>可直接传入纯坐标用于离线测试；此时成员的 Actor 可以为空。</summary>
        public PartySnapshot(IEnumerable<PartyMemberSnapshot> members)
        {
            if (members == null) throw new ArgumentNullException(nameof(members));
            var values = members.ToArray();
            if (values.Select(member => member.Slot).Distinct().Count() != values.Length)
                throw new ArgumentException("站位快照不能包含重复的候选下标。", nameof(members));
            Members = Array.AsReadOnly(values);
        }

        public static PartySnapshot Capture(IEnumerable<SimActor> candidates)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            var actors = candidates.ToArray();
            var members = new List<PartyMemberSnapshot>(actors.Length);
            for (var slot = 0; slot < actors.Length; slot++)
            {
                var actor = actors[slot];
                if (actor == null || !actor.IsActive) continue;
                try { members.Add(new PartyMemberSnapshot(actor, actor.Pos, slot)); }
                catch (InvalidOperationException) when (!actor.IsActive) { }
            }
            return new PartySnapshot(members);
        }

        public IReadOnlyList<PartyMemberSnapshot> InArea(ActionArea area, Vector2 center, float heading = 0, SimActor exclude = null)
        {
            area.Contains(center, center, heading);
            return Array.AsReadOnly(Members.Where(member => !IsExcluded(member, exclude) && area.Contains(member.Position, center, heading)).ToArray());
        }

        public bool AnyInArea(ActionArea area, Vector2 center, float heading = 0, SimActor exclude = null)
        {
            area.Contains(center, center, heading);
            return Members.Any(member => !IsExcluded(member, exclude) && area.Contains(member.Position, center, heading));
        }

        public PartyMemberSnapshot Get(int slot)
        {
            foreach (var member in Members)
                if (member.Slot == slot) return member;
            throw new InvalidOperationException($"站位快照中没有候选下标为 {slot} 的有效角色。");
        }

        public PartyMemberSnapshot? Nearest(Vector2 center, SimActor exclude = null)
        {
            var nearest = Nearest(center, 1, exclude);
            return nearest.Count == 0 ? (PartyMemberSnapshot?)null : nearest[0];
        }

        public IReadOnlyList<PartyMemberSnapshot> Nearest(Vector2 center, int count, SimActor exclude = null)
        {
            PartyMemberSnapshot.ValidatePosition(center);
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "最近成员数量不能为负数。");
            return Array.AsReadOnly(Members.Where(member => !IsExcluded(member, exclude))
                .OrderBy(member => Vector2.DistanceSquared(member.Position, center)).ThenBy(member => member.Slot).Take(count).ToArray());
        }

        private static bool IsExcluded(PartyMemberSnapshot member, SimActor exclude)
            => exclude != null && member.Actor != null && member.Actor.Entity.Identity == exclude.Entity.Identity;
    }
}
