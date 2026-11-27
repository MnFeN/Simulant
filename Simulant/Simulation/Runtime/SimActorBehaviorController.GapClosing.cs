using Simulant.Game.FFCS.Client.Game.Character;
using System;
using System.Numerics;

namespace Simulant.Simulation.Runtime
{
    internal sealed partial class SimActorBehaviorController
    {
        private bool TryGapClose(State state, MovePlan plan, double now)
        {
            if (!plan.GapClose)
                return false;

            var action = state.Actions?.GapCloser ?? 0;
            var targetReady = action != 0 && _target?.IsActive == true && _target.Entity.IsTargetable && _target.Entity.HP != 0;
            // 只有单独的突进意图会原地等复现的首领；普通机制走位仍按原方式回跑。
            if (plan.GapCloseOnly && action != 0 && !targetReady && now < plan.HoldUntil)
                return true;
            plan.GapClose = false;
            if (!targetReady || plan.GapCloseOnly && now >= plan.HoldUntil)
                return false;

            var actor = state.Actor;
            var offset = actor.Pos - _target.Pos;
            var radius = _target.Entity.HitboxRadius + 0.5f;
            if (offset.Length() <= radius + SimMovementController.NetworkArrivalTolerance)
                return false;

            var destination = _target.Pos + Vector2.Normalize(offset) * radius;
            var heading = (float)Math.Atan2(-offset.X, -offset.Y);
            CancelOwnCast(state);
            _movement.Stop(actor);
            SetSprintEffect(state, false);
            actor.ActorMove(actor.Pos3D, heading, 0);
            if (actor.Job == Job.NIN || actor.Job == Job.ROG)
                actor.ExecuteActionEffectAt(action, new Vector3(destination, actor.Pos3D.Z), heading);
            else
                actor.ExecuteActionEffect(action, actor.Job == Job.RPR ? actor : _target, heading);

            // 技能演出不负责更新逻辑位置；显式同步到目标圈外，并保留已选定的机制目的地。
            const float duration = 0.3f;
            _movement.DashTo(actor, destination, duration);
            state.SuspendedUntil = now + duration + 0.1;
            state.NextAction = Math.Max(state.NextAction, state.SuspendedUntil);
            plan.Speed = RunSpeed;
            plan.StageFirst = false;
            plan.Points.Clear();
            plan.Point = 0;
            plan.Arrived = plan.GapCloseOnly;
            if (plan.GapCloseOnly)
            {
                plan.Destination = destination;
                state.Anchor = destination;
            }
            else
                plan.Points.Add(plan.Destination);
            return true;
        }
    }
}
