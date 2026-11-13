using Simulant.Simulation.Geometry;
using Simulant.Simulation.Mechanics;
using System.Linq;
using System.Numerics;

namespace Simulant.Content.U7b.Presets
{
    internal sealed partial class P2遗弃末世Logic
    {
        private void ReportTowerFailure(int index, int tower, MechanicResult result)
        {
            var round = _plan.Rounds[index];
            var expected = Enumerable.Range(0, 8).Where(slot => round.TowerBySlot[slot] == tower).ToArray();
            var error = result.DescribeFailure($"第 {index + 1} 轮第 {tower + 1} 座塔", expected);
            var noStacks = error.Actual.Where(slot => _stacks[slot] == 0).ToArray();
            // 实际塔范围已经确定；只把快照中存在却未入塔的分配队员视作个人漏踩。
            var missing = error.Missing.Where(slot => result.Snapshot.Members.Any(member => member.Slot == slot)).ToArray();
            if (missing.Length != error.Missing.Count)
                error.WithDetail("部分应踩塔队员不在判定快照中，需检查实体状态，不按个人漏踩标记");
            if (noStacks.Length != 0) error.WithDetail("无层数踩塔：" + string.Join(" ", noStacks.Select(slot => Roles[slot])));
            Fail(error.WithAttribution(missing.Concat(error.Extra).Concat(noStacks),
                noStacks.Contains(_selfSlot) ? "自身无层数时踩塔" : null));
        }

        private MechanicError DescribeMarkerFailure(int index, int source, ForsakenMarker marker, ActionArea area, MechanicResult result)
        {
            var round = _plan.Rounds[index];
            var fan = marker == ForsakenMarker.Fan;
            var expectedHeading = 0f;
            var expectedTarget = source;
            if (fan)
            {
                expectedTarget = Enumerable.Range(0, 8).Where(slot => slot != source)
                    .OrderBy(slot => Vector2.DistanceSquared(round.Positions[source], round.Positions[slot])).ThenBy(slot => slot).First();
                expectedHeading = Heading(round.Positions[source], round.Positions[expectedTarget]);
            }
            var expected = Enumerable.Range(0, 8).Where(slot => (!fan || slot != source) &&
                area.Contains(round.Positions[slot], round.Positions[source], expectedHeading)).ToArray();
            var description = $"第 {index + 1} 轮 {Roles[source]} 的{MarkerName(marker)}点名";
            var error = fan
                ? result.DescribeBaitFailure(description, expected, expectedTarget,
                    result.Snapshot.Nearest(result.Center, Party[source])?.Slot, source)
                : result.DescribeFailure(description, expected, source);
            // 预设点位只用于推导解法名单；名单差异不足以确认是哪位队员移动有误。
            return error;
        }

        private void ReportJumpFailure(ForsakenRoundPlan round, int target, MechanicResult result)
        {
            // 跳跃目标已在更早的节点锁定；这里诊断该目标的圆形伤害，不重新选择近点目标。
            var error = result.DescribeFailure($"第 {round.Number} 轮 {Roles[target]} 的跳跃圆形伤害", new[] { target }, target);
            Fail(error);
        }

        private static string MarkerName(ForsakenMarker marker)
            => marker == ForsakenMarker.Stack ? "分摊" : marker == ForsakenMarker.Fan ? "扇形" : "钢铁";

        private void Fail(MechanicError error)
            => throw new SimMechanicFailedException(RecordMistake(error));

        private void Warn(MechanicError error)
            => _host.LogWarning(RecordMistake(error));

        private string RecordMistake(MechanicError error)
        {
            _mistakes++;
            // 只画机制明确指定的个人失误，不从报错名单或自身推测反推画叉对象。
            foreach (var slot in error.Offenders)
            {
                _failedPlayers.Add(slot);
                ShowResultMarker("m0489trg_b0c", slot);
            }
            return $"[遗弃末世] {Scheduler.ElapsedSeconds - _origin:0.000} 秒：" + error.Format(_selfSlot, slot => Roles[slot]);
        }
    }
}
