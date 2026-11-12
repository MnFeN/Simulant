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
        public string Reason { get; }
        public Vector2 Center { get; }
        public float Heading { get; }
        public IReadOnlyList<MechanicResult> Children { get; }

        public MechanicResult(PartySnapshot snapshot, IEnumerable<PartyMemberSnapshot> participants, bool success,
            string reason = null, Vector2 center = default(Vector2), float heading = 0,
            IEnumerable<MechanicResult> children = null)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            if (participants == null) throw new ArgumentNullException(nameof(participants));
            Participants = Array.AsReadOnly(participants.ToArray());
            Success = success;
            Reason = reason;
            Center = center;
            Heading = heading;
            Children = Array.AsReadOnly((children ?? Enumerable.Empty<MechanicResult>()).ToArray());
        }
    }
}
