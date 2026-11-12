using Simulant.Simulation.Geometry;
using Simulant.Simulation.Runtime;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Simulant.Simulation.Mechanics
{
    /// <summary>通用范围判定：可用于塔、分摊和范围伤害；额外规则不改变实际入内人数。</summary>
    public sealed class AreaMechanic : SimMechanic
    {
        public ActionArea Area { get; set; }
        public Vector2 Center { get; set; }
        public float Heading { get; set; }
        public SimActor Exclude { get; set; }
        public int? RequiredPlayers { get; set; }
        public int? MinPlayers { get; set; }
        public int? MaxPlayers { get; set; }
        public Func<PartySnapshot, Vector2> CenterAtResolve { get; set; }
        public Func<PartySnapshot, float> HeadingAtResolve { get; set; }
        /// <summary>返回空值表示满足规则；否则返回失败原因，可检查层数、颜色等条件。</summary>
        public Func<IReadOnlyList<PartyMemberSnapshot>, string> Check { get; set; }

        protected override void Validate()
        {
            if (RequiredPlayers.HasValue && (MinPlayers.HasValue || MaxPlayers.HasValue))
                throw new InvalidOperationException("指定人数不能与最少或最多人数同时设置。");
            if (RequiredPlayers < 0 || MinPlayers < 0 || MaxPlayers < 0 || MinPlayers > MaxPlayers)
                throw new InvalidOperationException("机制人数范围无效。");
            // 即使候选列表为空，也应在开始前发现未配置的范围或无效坐标。
            Area.Contains(Center, Center, Heading);
        }

        protected override MechanicResult Evaluate(PartySnapshot snapshot)
        {
            var center = CenterAtResolve?.Invoke(snapshot) ?? Center;
            var heading = HeadingAtResolve?.Invoke(snapshot) ?? Heading;
            var participants = snapshot.InArea(Area, center, heading, Exclude);
            var minimum = RequiredPlayers ?? MinPlayers;
            var maximum = RequiredPlayers ?? MaxPlayers;
            string reason = null;
            if (minimum.HasValue && participants.Count < minimum.Value)
                reason = $"需要至少 {minimum.Value} 人，实际 {participants.Count} 人。";
            else if (maximum.HasValue && participants.Count > maximum.Value)
                reason = $"允许最多 {maximum.Value} 人，实际 {participants.Count} 人。";
            if (reason == null) reason = Check?.Invoke(participants);
            return new MechanicResult(snapshot, participants, reason == null, reason, center, heading);
        }
    }
}
