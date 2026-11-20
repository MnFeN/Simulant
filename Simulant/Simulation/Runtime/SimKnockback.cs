using Simulant.Game.Data;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Simulation.Runtime
{
    /// <summary>Defines the direction and movement parameters of a knockback.</summary>
    public readonly struct KnockbackEffect
    {
        internal Vector2? Center { get; }
        internal float Heading { get; }
        private readonly float _distance, _duration;
        private readonly ushort? _rowId;
        private readonly byte _additionalDistance;

        private KnockbackEffect(Vector2? center, float heading, float distance, float duration,
            ushort? rowId = null, byte additionalDistance = 0)
        {
            Center = center;
            Heading = heading;
            _distance = distance;
            _duration = duration;
            _rowId = rowId;
            _additionalDistance = additionalDistance;
        }

        public static KnockbackEffect Radial(Vector2 center, float distance, float duration)
            => new KnockbackEffect(center, 0, distance, duration);

        public static KnockbackEffect Directional(float heading, float distance, float duration)
            => new KnockbackEffect(null, heading, distance, duration);

        public static KnockbackEffect RadialFromRow(Vector2 center, ushort rowId, byte additionalDistance = 0)
            => new KnockbackEffect(center, 0, 0, 0, rowId, additionalDistance);

        public static KnockbackEffect DirectionalFromRow(float heading, ushort rowId, byte additionalDistance = 0)
            => new KnockbackEffect(null, heading, 0, 0, rowId, additionalDistance);

        internal (float Distance, float Duration) GetMovement()
        {
            if (!_rowId.HasValue)
                return (_distance, _duration);

            if (!GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Knockback>().TryGetRow(_rowId.Value, out var row))
                throw new InvalidOperationException(FormattableString.Invariant($"击退效果引用的 Knockback 行 {_rowId.Value} 不存在。"));

            var distance = row.Distance + _additionalDistance;
            if (row.Speed == 0 || distance == 0)
                throw new InvalidOperationException(FormattableString.Invariant($"Knockback 行 {_rowId.Value} 的击退距离或速度为零。"));

            return (distance, distance / (float)row.Speed);
        }
    }

    public static class SimKnockback
    {
        /// <summary>
        /// Resolves immunity at skill hit time and applies knockback from live positions.<br />
        /// · hitActors: actors hit by the skill, or the whole party for a raid-wide knockback.<br />
        /// · movementAt: absolute session time for movement. Omit to apply immediately.<br />
        /// · ignoreImmunity: enable for effects that bypass knockback immunity.<br />
        /// · suspendFor: optional behavior pause in seconds for all included hit actors.<br />
        /// · beforeMovement / afterMovement: callbacks around the movement step.
        /// </summary>
        public static void Resolve(SimScheduler scheduler, IEnumerable<SimActor> hitActors, KnockbackEffect effect,
            double? movementAt = null, SimActor exclude = null, bool ignoreImmunity = false, double suspendFor = 0,
            Action beforeMovement = null, Action afterMovement = null, string name = null)
        {
            if (scheduler == null)
                throw new ArgumentNullException(nameof(scheduler));
            if (hitActors == null)
                throw new ArgumentNullException(nameof(hitActors));

            SimTimeline.ValidateSeconds(suspendFor, nameof(suspendFor));
            var (distance, duration) = effect.GetMovement();
            var hits = hitActors.Where(actor => actor?.IsActive == true && actor != exclude).Distinct()
                .Select(actor => (Actor: actor, KnockedBack: ignoreImmunity || !actor.IsKnockbackImmune)).ToArray();

            void Apply()
            {
                beforeMovement?.Invoke();
                foreach (var hit in hits)
                {
                    if (!hit.Actor.IsActive)
                        continue;
                    if (suspendFor > 0)
                        hit.Actor.SuspendBehavior(suspendFor);
                    if (hit.KnockedBack)
                        hit.Actor.ApplyKnockback(effect.Heading, effect.Center, distance, duration);
                }
                afterMovement?.Invoke();
            }

            if (movementAt.HasValue)
                scheduler.Schedule(movementAt.Value, Apply, name ?? "击退位移开始");
            else
                Apply();
        }
    }
}
