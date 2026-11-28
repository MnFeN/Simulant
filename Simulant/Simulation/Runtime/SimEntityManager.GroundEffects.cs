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
            internal bool HasVfx;
            internal SimScheduledAction Expiry;
            internal SimScheduledAction Tick;

            internal GroundEffect(Character source, uint actionId, Vector3 position, float heading, ActionArea area, double expiresAt)
            {
                Source = source;
                ActionId = actionId;
                Position = position;
                Heading = heading;
                Area = area;
                ExpiresAt = expiresAt;
            }

            internal bool Contains(Character target)
                => Area.Contains(target.Pos, new Vector2(Position.X, Position.Y), Heading);
        }

        private readonly Dictionary<(EntityIdentity Source, uint Action), GroundEffect> _groundEffects =
            new Dictionary<(EntityIdentity, uint), GroundEffect>();

        /// <summary>以请求落点创建地面效果。无落点时使用施法者当前位置；重复施放替换同一来源的旧效果。</summary>
        internal void PlaceGroundEffect(Character source, ActionRequest request, double duration, ActionArea? area = null,
            string vfxPath = null, double tickInterval = 0, Action<GroundEffect> onTick = null, Action<GroundEffect> onExpire = null)
        {
            SimTimeline.ValidateSeconds(duration, nameof(duration));
            if (duration == 0)
                throw new ArgumentOutOfRangeException(nameof(duration), "地面效果的持续时间必须大于零。");
            if (onTick != null)
            {
                SimTimeline.ValidateSeconds(tickInterval, nameof(tickInterval));
                if (tickInterval == 0)
                    throw new ArgumentOutOfRangeException(nameof(tickInterval), "地面效果的结算间隔必须大于零。");
            }

            lock (_gate)
            {
                EnsureAcceptingEntities();
                var position = request.GroundPosition ?? source.Pos3D;
                var shape = area ?? ActionArea.FromAction(request.ActionId, source.HitboxRadius);
                var start = _scheduler.ElapsedSeconds;
                var effect = new GroundEffect(source, request.ActionId, position, request.Facing, shape, start + duration);
                RemoveGroundEffect(source, request.ActionId);
                _groundEffects.Add((source.Identity, request.ActionId), effect);

                effect.Expiry = _scheduler.Schedule(effect.ExpiresAt, () =>
                {
                    lock (_gate)
                    {
                        if (!_groundEffects.TryGetValue((source.Identity, request.ActionId), out var current) ||
                            !ReferenceEquals(current, effect))
                            return;

                        RemoveGroundEffect(source, request.ActionId);
                        if (_acceptingEntities && source.IsAlive)
                            onExpire?.Invoke(effect);
                    }
                }, "移除地面效果");
                if (vfxPath != null)
                {
                    effect.HasVfx = true;
                    VfxManager.ShowGroundEffect(vfxPath, effect.Position, effect.Heading, effect);
                }
                if (onTick != null)
                    ScheduleGroundTick(effect, start + tickInterval, tickInterval, onTick);
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
                effect.Tick?.Cancel();
                if (effect.HasVfx)
                    VfxManager.Remove(effect);
                _groundEffects.Remove(key);
            }
        }

        private void ScheduleGroundTick(GroundEffect effect, double at, double interval, Action<GroundEffect> onTick)
        {
            if (at >= effect.ExpiresAt)
                return;

            effect.Tick = _scheduler.Schedule(at, () =>
            {
                lock (_gate)
                {
                    if (!_acceptingEntities || !TryGetGroundEffect(effect.Source, effect.ActionId, out var current) ||
                        !ReferenceEquals(current, effect))
                        return;

                    onTick(effect);
                    if (TryGetGroundEffect(effect.Source, effect.ActionId, out current) && ReferenceEquals(current, effect))
                        ScheduleGroundTick(effect, at + interval, interval, onTick);
                }
            }, "结算地面效果");
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
