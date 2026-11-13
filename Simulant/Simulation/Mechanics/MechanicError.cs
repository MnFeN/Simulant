using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulant.Simulation.Mechanics
{
    /// <summary>一次失误的名单对比与描述。只组织诊断，不改变判定、发送日志或决定是否中断。</summary>
    public sealed class MechanicError
    {
        private readonly bool _isBait;
        private bool _hasAttribution;
        private string _selfHint;
        private string _detail;

        public string Description { get; }
        public string Reason { get; }
        /// <summary>规则类错误可以没有指定名单；空名单则明确表示不应有人参与。</summary>
        public IReadOnlyList<int> Expected { get; }
        public IReadOnlyList<int> Actual { get; }
        public IReadOnlyList<int> Missing { get; }
        public IReadOnlyList<int> Extra { get; }
        public int? Source { get; }
        public int? ExpectedTarget { get; }
        public int? ActualTarget { get; }
        /// <summary>画叉对象，默认留空。只有机制明确确认个人处理条件未满足后，才通过 WithAttribution 指定。</summary>
        public IReadOnlyList<int> Offenders { get; private set; } = Array.AsReadOnly(Array.Empty<int>());

        private MechanicError(string description, IEnumerable<int> expected, IEnumerable<int> actual,
            string reason, int? source = null, bool isBait = false, int? expectedTarget = null, int? actualTarget = null)
        {
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("机制描述不能为空。", nameof(description));
            if (source < 0 || expectedTarget < 0 || actualTarget < 0)
                throw new ArgumentOutOfRangeException(nameof(source), "点名或引导目标下标不能为负数。");
            Description = description.TrimEnd('：', '。', ' ');
            Reason = reason;
            Expected = expected == null ? null : Freeze(expected);
            Actual = Freeze(actual);
            Missing = Freeze(Expected == null ? Enumerable.Empty<int>() : Expected.Except(Actual));
            Extra = Freeze(Expected == null ? Enumerable.Empty<int>() : Actual.Except(Expected));
            Source = source;
            ExpectedTarget = expectedTarget;
            ActualTarget = actualTarget;
            _isBait = isBait;
        }

        public static MechanicError Fixed(string description, IEnumerable<int> expected, IEnumerable<int> actual, string reason = null)
            => new MechanicError(description, expected ?? throw new ArgumentNullException(nameof(expected)), actual, reason);

        public static MechanicError Targeted(string description, IEnumerable<int> expected, IEnumerable<int> actual, int target, string reason = null)
            => new MechanicError(description, expected ?? throw new ArgumentNullException(nameof(expected)), actual, reason,
                source: target, expectedTarget: target, actualTarget: target);

        /// <summary>source 可表示扇形等机制的点名者；方括号始终标记引导目标。没有实际目标时传 null。</summary>
        public static MechanicError Bait(string description, IEnumerable<int> expected, IEnumerable<int> actual,
            int expectedTarget, int? actualTarget, int? source = null, string reason = null)
            => new MechanicError(description, expected ?? throw new ArgumentNullException(nameof(expected)), actual, reason,
                source, true, expectedTarget, actualTarget);

        /// <summary>层数、距离、方向、时机、重复受伤等规则错误；不编造应有名单或自动把受影响者当成责任人。</summary>
        public static MechanicError Rule(string description, IEnumerable<int> affected, string reason)
            => new MechanicError(description, null, affected, reason);

        /// <summary>根据实际范围、状态等明确的个人处理条件指定画叉对象；不能仅凭偏离导航点或被波及归责。</summary>
        public MechanicError WithAttribution(IEnumerable<int> offenders, string selfHint = null)
        {
            Offenders = Freeze(offenders);
            _hasAttribution = true;
            _selfHint = selfHint;
            return this;
        }

        public MechanicError WithDetail(string detail)
        {
            if (!string.IsNullOrWhiteSpace(detail))
                _detail = string.IsNullOrEmpty(_detail) ? detail : _detail.TrimEnd('。', '；') + "；" + detail;
            return this;
        }

        /// <summary>按小队下标去重、排序，名字由调用方提供；日志与聊天应复用同一份输出。</summary>
        public string Format(int? selfSlot, Func<int, string> name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            var lines = new List<string> { Description + "处理错误：" };
            if (Expected != null)
            {
                lines.Add($"· 应有 {Expected.Count} 人：" + Names(Expected, ExpectedTarget, name));
                lines.Add($"· 实际 {Actual.Count} 人：" + Names(Actual, ActualTarget, name));
                if (Missing.Count != 0) lines.Add("· 缺少：" + Names(Missing, null, name));
                if (Extra.Count != 0) lines.Add("· 多余：" + Names(Extra, null, name));
            }
            else lines.Add($"· 涉及 {Actual.Count} 人：" + Names(Actual, null, name));
            if (_isBait && ExpectedTarget != ActualTarget)
                lines.Add($"· 引导目标：应为 {name(ExpectedTarget.Value)}，实际 " + (ActualTarget.HasValue ? name(ActualTarget.Value) : "无"));
            else if (!_isBait && Source.HasValue && (!Expected.Contains(Source.Value) || !Actual.Contains(Source.Value)))
                lines.Add("· 点名：" + name(Source.Value));
            if (!string.IsNullOrWhiteSpace(Reason)) lines.Add("· 判定：" + Reason.TrimEnd('。'));
            if (!string.IsNullOrEmpty(_detail)) lines.Add("· 说明：" + _detail.TrimEnd('。'));
            lines.Add("· 推测：" + InferSelf(selfSlot));
            return string.Join("\n", lines);
        }

        private string InferSelf(int? selfSlot)
        {
            // 默认推测只用于文字提示；画叉名单必须由机制另行提供。
            if (!selfSlot.HasValue || (_hasAttribution && !Offenders.Contains(selfSlot.Value))) return "未知";
            if (!string.IsNullOrWhiteSpace(_selfHint)) return _selfHint.TrimEnd('。');
            if (_isBait && ExpectedTarget != ActualTarget)
            {
                if (ExpectedTarget == selfSlot) return "自身未去引导";
                if (ActualTarget == selfSlot) return "自身不应引导";
            }
            if (Missing.Contains(selfSlot.Value)) return "自身未参与";
            if (Extra.Contains(selfSlot.Value)) return "自身不应参与";
            if (Source == selfSlot) return "自身被点名且站位错误";
            return "未知";
        }

        private static string Names(IReadOnlyList<int> slots, int? target, Func<int, string> name)
            => slots.Count == 0 ? "无" : string.Join(" ", slots.Select(slot => slot == target ? "[" + name(slot) + "]" : name(slot)));

        private static IReadOnlyList<int> Freeze(IEnumerable<int> slots)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            var values = slots.Distinct().OrderBy(slot => slot).ToArray();
            if (values.Any(slot => slot < 0)) throw new ArgumentOutOfRangeException(nameof(slots), "队员下标不能为负数。");
            return Array.AsReadOnly(values);
        }
    }
}
