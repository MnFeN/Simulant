using System;
using System.Collections.Generic;
using System.Numerics;
using Simulant.Simulation.Geometry;

namespace Simulant.Simulation.Runtime
{
    internal sealed partial class SimActorBehaviorController
    {
        private sealed class MovePlan
        {
            public Vector2 Destination;
            public readonly List<Vector2> Points = new List<Vector2>();
            public int Point;
            public double Arrival, NotBefore, Latest, HoldUntil;
            public bool Immediate, StageFirst, Arrived, Running;
        }

        /// <summary>绝对时间均以会话调度器为基准；保持时间覆盖最后一次相关机制判定。</summary>
        internal void MoveForMechanic(SimActor actor, SimPosition area, double arriveBy, double holdUntil,
            bool urgent, double northHeading)
        {
            if (!Finite(arriveBy) || !Finite(holdUntil) || holdUntil < arriveBy)
                throw new ArgumentException("目标时间必须有效，且保持站位的结束时间不能早于目标时间。");
            lock (_gate)
            {
                if (_disposed || !_states.TryGetValue(actor, out var state) || !actor.IsActive)
                    throw new InvalidOperationException("只有本会话生成的有效网络假玩家可以接收机制移动意图。");
                // 取点与相对北只在接收指令时解析一次，后续滑步、转折和截止判断共用同一个终点。
                var destination = area.Sample(_random, northHeading);
                if (Math.Abs(destination.X) > 1000 || Math.Abs(destination.Y) > 1000)
                    throw new ArgumentOutOfRangeException(nameof(area), "移动目的地必须位于网络坐标范围内。");
                var now = _scheduler.ElapsedSeconds;
                var position = actor.Pos;
                // 关闭延迟与紧急意图都直接就位，不让自动读条、滑步、贪刀或绕路推迟移动。
                var immediate = urgent || !state.Options.DelayArrival;
                var plan = new MovePlan { Destination = destination, Latest = arriveBy, HoldUntil = holdUntil, Immediate = immediate,
                    NotBefore = immediate ? now : arriveBy - 1 };
                if (Vector2.Distance(position, destination) > SimMovementController.NetworkArrivalTolerance)
                {
                    var origin = position;
                    if (!immediate && state.Options.AttackTarget && state.Actions?.Melee == true &&
                        _target != null && _target.IsActive && _target.Entity.IsTargetable)
                    {
                        var offset = destination - _target.Pos;
                        var radius = _target.Entity.HitboxRadius + 2;
                        if (offset.Length() > radius + 1)
                        {
                            var staging = _target.Pos + Vector2.Normalize(offset) * radius;
                            // 只在确有时间时靠近目标方向的近战边缘，之后再延后离开。
                            if (Vector2.Distance(origin, staging) > 0.3f &&
                                now + (Vector2.Distance(origin, staging) + Vector2.Distance(staging, destination)) / 6 + 1 < arriveBy)
                            { plan.Points.Add(staging); plan.StageFirst = true; origin = staging; }
                        }
                    }
                    plan.Points.AddRange(CreateRoute(origin, destination, immediate ? 0 : state.Options.RouteAngleDegrees, _random));
                    if (now + TravelTime(position, plan) > arriveBy)
                    { plan.Points.Clear(); plan.Points.Add(destination); plan.StageFirst = false; }
                    if (now + TravelTime(position, plan) > arriveBy)
                        throw new InvalidOperationException($"假玩家 {actor.Job}（{actor.Id:X8}）的移动窗口不足，无法以正常跑速在截止前到达。");
                    // 延迟到达只推迟实体自己的出发安排，仍预留更新余量；关闭或紧急时尽早出发。
                    var earliest = now + TravelTime(position, plan);
                    plan.Arrival = !immediate
                        ? Math.Max(earliest, arriveBy - 0.25 - _random.NextDouble() * 0.35) : earliest;
                }
                else plan.Arrived = true;
                _movement.Stop(actor);
                state.Move = plan;
                state.Anchor = destination;
                state.Initialized = true;
                state.ManualMovement = false;
                if (immediate) CancelOwnCast(state);
                QueueTick();
            }
        }

        private void UpdateMovement(State state, double now)
        {
            var actor = state.Actor;
            var plan = state.Move;
            if (plan == null)
            {
                if (state.ManualMovement && !actor.IsMoving)
                { state.ManualMovement = false; state.Anchor = actor.Pos; state.NextIdle = now + 4; }
                return;
            }
            if (plan.Arrived)
            {
                if (now >= plan.HoldUntil) { state.Move = null; state.NextIdle = now + 3 + _random.NextDouble() * 3; }
                return;
            }
            var position = actor.Pos;
            if (!actor.IsMoving)
            {
                while (plan.Point < plan.Points.Count && Vector2.Distance(position, plan.Points[plan.Point]) <=
                    SimMovementController.NetworkArrivalTolerance) plan.Point++;
                if (plan.Point == plan.Points.Count) { plan.Arrived = true; return; }
            }
            if (now > plan.Latest + 0.25)
                throw new InvalidOperationException($"假玩家 {actor.Job}（{actor.Id:X8}）未按时到位，距目的地 {Vector2.Distance(position, plan.Destination):0.00} 米；已停止模拟，请检查移动窗口。");
            // 延迟或原生插值较慢时，优先删去可选绕路；不加速、不瞬移。
            if (plan.Points.Count - plan.Point > 1 && now + TravelTime(position, plan) > plan.Latest - 0.1)
            {
                _movement.Stop(actor);
                plan.Points.Clear(); plan.Points.Add(plan.Destination); plan.Point = 0; plan.StageFirst = false;
            }
            if (actor.IsMoving) return;
            var casting = _casts.Remaining(actor);
            var run = MustRun(state, now);
            var slide = !run && state.Actions?.Caster == true &&
                state.OwnCast != 0 && casting > 0 && casting <= SlideSeconds;
            if (!run && !slide) return;
            var next = plan.Points[plan.Point];
            // 滑步与提前接近不能绕过延迟到达；最后一段仍由实体等待到合理时刻再完成。
            if (!plan.Immediate && plan.Point == plan.Points.Count - 1 && now + Vector2.Distance(position, next) / 6 < plan.NotBefore)
                return;
            if (casting > 0 && !slide && casting > SlideSeconds)
                CancelOwnCast(state);
            // 显式技能不由自动行为取消；急迫移动仍可出发，避免自动读条占住机制窗口。
            if (slide)
            {
                var delta = next - position;
                var length = delta.Length();
                var step = Math.Min(length, (float)(6 * SlideSeconds));
                if (step < 0.1) return;
                next = position + delta / length * step;
            }
            if (run && !(plan.StageFirst && plan.Point == 0)) plan.Running = true;
            _movement.MoveNetworkTo(actor, next, RunSpeed);
        }

        private bool MustRun(State state, double now)
        {
            var plan = state.Move;
            return plan != null && !plan.Arrived && (plan.Immediate || plan.Running || plan.StageFirst && plan.Point == 0 ||
                now + TravelTime(state.Actor.Pos, plan) >= plan.Arrival);
        }

        private bool CanWait(State state, double now, double duration)
        {
            var plan = state.Move;
            return plan == null || plan.Arrived || !plan.Immediate && !(plan.StageFirst && plan.Point == 0) &&
                now + duration + TravelTime(state.Actor.Pos, plan) < plan.Arrival;
        }

        private static double TravelTime(Vector2 origin, MovePlan plan)
        {
            double distance = 0;
            for (var i = plan.Point; i < plan.Points.Count; i++)
            { distance += Vector2.Distance(origin, plan.Points[i]); origin = plan.Points[i]; }
            // 留出共享更新节点和原生插值的时间；每增加一个转折都多留一次轮询。
            return distance / 6 + 0.2 * (plan.Points.Count - plan.Point) + 0.2;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        /// <summary>先随机初始偏角和左右方向，再逐步回到直达路线；拐点数量随路程增加，偏移始终随距离缩放。</summary>
        internal static Vector2[] CreateRoute(Vector2 origin, Vector2 destination, double angleDegrees, Random random)
        {
            var delta = destination - origin;
            var distance = delta.Length();
            if (angleDegrees <= 0 || distance < 0.5f) return new[] { destination };
            var direction = delta / distance;
            var perpendicular = new Vector2(-direction.Y, direction.X);
            var angle = random.NextDouble() * angleDegrees * Math.PI / 180;
            var side = random.Next(2) == 0 ? -1 : 1;
            var lead = distance * (0.2 + random.NextDouble() * 0.1);
            var lateral = (float)(lead * Math.Sin(angle) * side);
            var points = new List<Vector2>(4)
            {
                origin + direction * (float)(lead * Math.Cos(angle)) + perpendicular * lateral,
            };
            if (distance >= 5) points.Add(origin + direction * (distance * 0.65f) + perpendicular * (lateral * 0.75f));
            if (distance >= 12) points.Add(origin + direction * (distance * 0.85f) + perpendicular * (lateral * 0.25f));
            points.Add(destination);
            // 场地边缘不能因为演出绕路超出网络坐标可表达的范围。
            if (points.Exists(point => Math.Abs(point.X) > 1000 || Math.Abs(point.Y) > 1000)) return new[] { destination };
            return points.ToArray();
        }
    }
}
