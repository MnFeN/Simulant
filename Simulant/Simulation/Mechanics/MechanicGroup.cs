using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulant.Simulation.Mechanics
{
    /// <summary>同一快照先完成全部子机制的判定，再执行子结果和组结果回调。</summary>
    public sealed class MechanicGroup : SimMechanic
    {
        private readonly IReadOnlyList<SimMechanic> _children;
        private readonly List<SimMechanic> _claimedChildren = new List<SimMechanic>();
        protected override IReadOnlyList<SimMechanic> Children => _claimedChildren;

        public MechanicGroup(params SimMechanic[] children)
        {
            if (children == null) throw new ArgumentNullException(nameof(children));
            if (children.Length == 0 || children.Any(child => child == null) || children.Distinct().Count() != children.Length)
                throw new ArgumentException("机制组必须包含至少一个不重复的有效子机制。", nameof(children));
            _children = Array.AsReadOnly((SimMechanic[])children.Clone());
            Name = "机制组";
        }

        protected override void OnStart()
        {
            // 全部子机制先登记生命周期，再进入任何可能生成实体或显示特效的开始回调。
            foreach (var child in _children)
            {
                if (child.ResolveAfter != 0) throw new InvalidOperationException("子机制的判定延迟必须为零，由机制组统一指定延迟。");
                child.Claim(Context);
                _claimedChildren.Add(child);
            }
            foreach (var child in _children) child.Begin(false);
        }

        protected override MechanicResult Evaluate(PartySnapshot snapshot)
        {
            var results = _children.Select(child => child.EvaluateForGroup(snapshot)).ToArray();
            var participants = results.SelectMany(result => result.Participants).GroupBy(member => member.Slot).Select(group => group.First());
            return new MechanicResult(snapshot, participants, results.All(result => result.Success),
                string.Join("；", results.Where(result => !result.Success).Select(result => result.Reason)), children: results);
        }

        internal override void PublishForGroup(MechanicResult result)
        {
            for (var i = 0; i < _children.Count; i++) _children[i].PublishForGroup(result.Children[i]);
            base.PublishForGroup(result);
        }
    }
}
