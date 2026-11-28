using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Simulation.Geometry;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Simulant.Simulation.Runtime
{
    public sealed partial class SimEntityManager
    {
        internal sealed class GroundEffect
        {
            internal Character Source { get; }
            internal uint ActionId { get; }
            internal Vector3 Position { get; }
            internal float Heading { get; }
            internal ActionArea Area { get; }
            internal double ExpiresAt { get; }
            internal Action<GroundEffect> OnTick { get; }
            internal bool HasVfx;
            internal SimScheduledAction Expiry;

            internal GroundEffect(Character source, uint actionId, Vector3 position, float heading,
                ActionArea area, double expiresAt, Action<GroundEffect> onTick)
            {
                Source = source;
                ActionId = actionId;
                Position = position;
                Heading = heading;
                Area = area;
                ExpiresAt = expiresAt;
                OnTick = onTick;
            }

            internal bool Contains(Character target)
                => Area.Contains(target.Pos, new Vector2(Position.X, Position.Y), Heading);
        }

        private readonly Dictionary<(EntityIdentity Source, uint Action), GroundEffect> _groundEffects =
            new Dictionary<(EntityIdentity, uint), GroundEffect>();

        /// <summary>以请求落点创建地面效果。重复施放替换同一来源的旧效果，onTick 随统一三秒刻度结算。</summary>
        internal void PlaceGroundEffect(Character source, ActionRequest request, double duration, ActionArea? area = null,
            string vfxPath = null, Action<GroundEffect> onTick = null, Action<GroundEffect> onExpire = null)
            => PlaceGroundEffect(source, request.ActionId, request.GroundPosition ?? source.Pos3D, request.Facing, duration,
                area, onTick, onExpire, vfxPath == null ? (Action<GroundEffect>)null :
                    effect => VfxManager.ShowGroundEffect(vfxPath, effect.Position, effect.Heading, effect));

        internal void PlaceGroundEffect(Character source, uint actionId, Vector3 position, float heading, double duration,
            ActionArea? area = null, Action<GroundEffect> onTick = null, Action<GroundEffect> onExpire = null,
            Action<GroundEffect> showVisual = null)
        {
            SimTimeline.ValidateSeconds(duration, nameof(duration));
            if (duration == 0)
                throw new ArgumentOutOfRangeException(nameof(duration), "地面效果的持续时间必须大于零。");

            lock (_gate)
            {
                EnsureAcceptingEntities();
                var shape = area ?? ActionArea.FromAction(actionId, source.HitboxRadius);
                var effect = new GroundEffect(source, actionId, position, heading,
                    shape, _scheduler.ElapsedSeconds + duration, onTick);
                RemoveGroundEffect(source, actionId);
                _groundEffects.Add((source.Identity, actionId), effect);

                effect.Expiry = _scheduler.Schedule(effect.ExpiresAt, () =>
                {
                    lock (_gate)
                    {
                        if (!_groundEffects.TryGetValue((source.Identity, actionId), out var current) ||
                            !ReferenceEquals(current, effect))
                            return;

                        RemoveGroundEffect(source, actionId);
                        if (_acceptingEntities && source.IsAlive)
                            onExpire?.Invoke(effect);
                    }
                }, "移除地面效果");
                if (showVisual != null)
                {
                    effect.HasVfx = true;
                    showVisual(effect);
                }
            }
        }

        internal bool TryGetGroundEffect(Character source, uint actionId, out GroundEffect effect)
        {
            lock (_gate)
                return _groundEffects.TryGetValue((source.Identity, actionId), out effect) &&
                    source.IsAlive && _scheduler.ElapsedSeconds < effect.ExpiresAt;
        }

        internal void RemoveGroundEffect(Character source, uint actionId)
        {
            lock (_gate)
            {
                var key = (source.Identity, actionId);
                if (!_groundEffects.TryGetValue(key, out var effect))
                    return;

                effect.Expiry?.Cancel();
                if (effect.HasVfx)
                    VfxManager.Remove(effect);
                _groundEffects.Remove(key);
            }
        }

        private bool ClearGroundEffects()
        {
            lock (_gate)
            {
                var cleared = true;
                foreach (var effect in _groundEffects.Values.ToArray())
                    if (!TryCleanup(() => RemoveGroundEffect(effect.Source, effect.ActionId)))
                        cleared = false;
                return cleared;
            }
        }
    }
}
