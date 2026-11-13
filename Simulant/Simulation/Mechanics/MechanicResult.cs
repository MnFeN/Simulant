using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Simulation.Mechanics
{
    /// <summary>冻结的一次机制判定；后续演出使用此结果，不重新读取站位。</summary>
    public sealed class MechanicResult
    {
        public PartySnapshot Snapshot { get; }
        public IReadOnlyList<PartyMemberSnapshot> Participants { get; }
        public bool Success { get; }
        public MechanicFailureSeverity FailureSeverity { get; }
        public string Reason { get; }
        public Vector2 Center { get; }
        public float Heading { get; }
        public IReadOnlyList<MechanicResult> Children { get; }

        /// <summary>从本次判定的实际名单生成固定位置或固定点名描述，不重新读取实体，也不改变成功与否。</summary>
        public MechanicError DescribeFailure(string description, IEnumerable<int> expected, int? target = null)
            => target.HasValue
                ? MechanicError.Targeted(description, expected, Participants.Select(member => member.Slot), target.Value, Reason)
                : MechanicError.Fixed(description, expected, Participants.Select(member => member.Slot), Reason);

        /// <summary>实际引导目标应取自锁定时保存的目标或本次 Snapshot，不能重新查询正在移动的实体。</summary>
        public MechanicError DescribeBaitFailure(string description, IEnumerable<int> expected,
            int expectedTarget, int? actualTarget, int? source = null)
            => MechanicError.Bait(description, expected, Participants.Select(member => member.Slot),
                expectedTarget, actualTarget, source, Reason);

        public MechanicResult(PartySnapshot snapshot, IEnumerable<PartyMemberSnapshot> participants, bool success,
            string reason = null, Vector2 center = default(Vector2), float heading = 0,
            IEnumerable<MechanicResult> children = null, MechanicFailureSeverity failureSeverity = MechanicFailureSeverity.Fatal)
        {
            if (failureSeverity != MechanicFailureSeverity.Warning && failureSeverity != MechanicFailureSeverity.Fatal)
                throw new ArgumentOutOfRangeException(nameof(failureSeverity), "机制失败级别无效。");
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            if (participants == null) throw new ArgumentNullException(nameof(participants));
            Participants = Array.AsReadOnly(participants.ToArray());
            Success = success;
            FailureSeverity = failureSeverity;
            Reason = reason;
            Center = center;
            Heading = heading;
            Children = Array.AsReadOnly((children ?? Enumerable.Empty<MechanicResult>()).ToArray());
        }
    }
}
