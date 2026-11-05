using Simulant.ACT;
using Simulant.Game;
using Simulant.Game.ExtractedCsv;
using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Network;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Game.FFCS.Client.Graphics.Scene;
using Simulant.Game.FFCS.Client.Network;
using Simulant.Simulation.Timeline;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using ActionRow = Simulant.Game.ExtractedCsv.Rows.Action;
using NativeCharacter = Simulant.Game.FFCS.Client.Game.Character.Character;

namespace Simulant.Core.Entity
{
    public class Character : EntityBase
    {
        private readonly PluginHost _host;
        private readonly object _castGate = new object();
        private SimScheduledAction _castCompletion;
        private SimScheduledAction _pendingOmen;
        private long _castGeneration;
        private bool _deleted;
        internal SimScheduler Scheduler { get; set; }
        private SimScheduler ActionScheduler => Scheduler ?? _host.EntitySpawner.StandaloneScheduler;

        internal void Invalidate()
        {
            lock (_castGate)
            {
                _deleted = true;
                CancelPendingCast();
            }
        }

        private void CancelPendingCast()
        {
            _castGeneration++;
            _castCompletion?.Cancel();
            _pendingOmen?.Cancel();
            _castCompletion = null;
            _pendingOmen = null;
        }

        protected override GameObject NativeGameObject => Native.Ptr.As<GameObject>();
        public new NativeCharacter Native { get; }
        public Character(NativeCharacter native, PluginHost host)
        {
            Native = native;
            _host = host;
        }

        public uint HP
        {
            get => Native.Health;
            set => Native.Health.Set(value);
        }

        public uint MaxHP
        {
            get => Native.MaxHealth;
            set => Native.MaxHealth.Set(value);
        }

        public uint MP
        {
            get => Native.Mana;
            set => Native.Mana.Set(value);
        }

        public uint MaxMp
        {
            get => Native.MaxMana;
            set => Native.MaxMana.Set(value);
        }

        public byte Level
        {
            get => Native.Level;
            set => Native.Level.Set(value);
        }

        public Job Job
        {
            get => Native.ClassJob;
            set => Native.ClassJob.Set(value);
        }

        public CharacterModes Mode
        {
            get => Native.Mode;
            // set => Native.Mode.Set(value); should set by SetMode method?
        }

        public byte ModeParam
        {
            get => Native.ModeParam;
            // set => Native.ModeParam.Set(value);
        }

        public void SetMode(CharacterModes mode, byte modeParam = 0)
            => Native.SetMode(mode, modeParam);

        public bool HasStatus(uint statusId)
            => Native.HasStatus(statusId);

        public void Knockback(float angle, float distance, float duration, byte a5 = 0, int a6 = 0)
            => Native.Knockback(angle, distance, duration, a5, a6);

        public void PlayBlendTimeline(ushort timelineId)
        {
            if (timelineId == 0) return;
            Native.Timeline.TimelineSequencer.PlayTimeline(timelineId);
            _host.LogVerbose($"PlayTimeline: {timelineId}");
        }

        public void PlayBaseTimeline(ushort timelineId, bool interrupt = true)
        {
            if (timelineId == 0) return;

            Native.SetMode(CharacterModes.AnimLock, 0);
            Native.Timeline.BaseOverride.Set(timelineId);

            if (!interrupt) return;
            
            Native.Timeline.TimelineSequencer.PlayTimeline(timelineId);
            _host.LogVerbose($"PlayTimeline: {timelineId}");
        }

        private readonly struct BaseTimelineState
        {
            public readonly CharacterModes Mode;
            public readonly byte ModeParam;
            public readonly ushort BaseOverride;

            public BaseTimelineState(CharacterModes mode, byte modeParam, ushort baseOverride)
            {
                Mode = mode;
                ModeParam = modeParam;
                BaseOverride = baseOverride;
            }
        }

        private static ActionRow ResolveActionData(uint actionId)
            => CsvManager.Instance.Get<ActionRow>().TryGetValue((int)actionId, out var action) ? action : null;

        public void Cast(uint actionId, float? omenDelay = 0)
        {
            lock (_castGate)
            {
                if (_deleted) return;
                var action = ResolveActionData(actionId);
                if (action == null) return;
                CancelPendingCast();

                var castTime = action.CastTime;

                // 用 CastInfo 控制读条开始，可以自动创建特效、显示咏唱进度条
                SetCastInfo(actionId, castTime);

                // 如果技能表里有 omen：
                var omenId = action.OmenId;
                if (omenId > 0 && omenDelay.HasValue)
                    PlayOmen(action, omenDelay.Value);

                // 读条结束时播放对应 timeline
                QueueExecute(action, castTime);

                _host.LogVerbose($"Casting action: {actionId} ({castTime:0.0} s)");
            }
        }

        public void SetCastInfo(uint actionId, float castTime, byte actionType = 1)
        {
            var castInfo = Native.CastInfo;
            castInfo.IsCasting.Set(true);
            castInfo.ActionType.Set(actionType); // 普通技能
            castInfo.ActionId.Set(actionId);
            //castInfo.TargetId.Set(0xE000_0000);
            castInfo.CurrentCastTime.Set(0);
            castInfo.BaseCastTime.Set(castTime);
            castInfo.TotalCastTime.Set(castTime);
        }

        private bool FinalizeCast()
        {
            var native = Native;
            if (native.IsNull())
                return false;

            var castInfo = native.CastInfo;
            if (castInfo.IsNull())
                return false;

            bool isCasting = castInfo.IsCasting;
            byte actionType = castInfo.ActionType;
            uint actionId = castInfo.ActionId;

            if (isCasting && actionId != 0)
            {
                native.ClearCastAnimationAndState(actionType, actionId);
                return true;
            }

            return false;
        }

        public void PlayOmen(ActionRow action, float omenDelay)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (float.IsNaN(omenDelay) || float.IsInfinity(omenDelay))
                throw new ArgumentOutOfRangeException(nameof(omenDelay));
            lock (_castGate)
            {
                if (_deleted) return;
                PlayOmenCore(action, omenDelay);
            }
        }

        private void PlayOmenCore(ActionRow action, float omenDelay)
        {
            omenDelay = Math.Max(0, omenDelay);
            var sustain = action.CastTime - omenDelay - 0.3; // 实际读条比表里短 0.3 s
            if (sustain < 0) return;

            var rawX = action.ScaleX;
            var rawY = action.ScaleY;
            var center = Pos3D;
            var heading = Heading;
            float x, y;
            switch (action.ShapeType)
            {
                case 2: // 圆
                case 10: // 圆环
                case 13: // 扇形
                    x = y = rawY;
                    break;
                case 3: // 扇形 + R
                case 5: // 圆形 + R
                    x = y = rawY + Native.HitboxRadius;
                    break;
                case 12: // 矩形
                case 11: // 十字
                case 14: // 三角
                    x = rawX;
                    y = rawY;
                    break;
                case 4: // 矩形 + R
                    x = rawX;
                    y = rawY + Native.HitboxRadius;
                    break;
                default:
                    return;
            }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Tag: Simulant");
            sb.AppendLine($"Omen: {action.Omen.Name}");
            sb.AppendLine($"t: {sustain}"); 
            sb.AppendLine($"O: {center.X}, {center.Y}, {center.Z}");
            sb.AppendLine($"θ: {heading}");
            sb.AppendLine($"Scale: {x}, {y}, 1");
            var command = sb.ToString();
            _pendingOmen?.Cancel();
            if (omenDelay <= 0)
            {
                TriggernometryInterop.InvokeNamedCallback("PictoACT", command);
                return;
            }
            var generation = _castGeneration;
            _pendingOmen = ActionScheduler.ScheduleAfter(omenDelay, () =>
            {
                lock (_castGate)
                {
                    if (!_deleted && generation == _castGeneration)
                        TriggernometryInterop.InvokeNamedCallback("PictoACT", command);
                }
            }, $"技能 {action.Index} 预兆");
        }

        public void Execute(uint actionId)
        {
            lock (_castGate)
            {
                if (_deleted) return;
                var action = ResolveActionData(actionId);
                if (action == null) return;
                Execute(action);
            }
        }

        private void Execute(ActionRow action)
        {
            var timelineEndId = action.AnimationEndId;
            if (timelineEndId >= 0)
            {
                Native.Timeline.BaseOverride.Set(0);
                Native.SetMode(CharacterModes.Normal, 0);
                PlayBlendTimeline((ushort)timelineEndId);
            }

            _host.LogVerbose($"Executed action: {action.Index}; TimelineEnd: {timelineEndId}");
        }

        private void QueueExecute(ActionRow action, float delay)
        {
            var generation = _castGeneration;
            _castCompletion = ActionScheduler.ScheduleAfter(Math.Max(0, delay), () =>
            {
                lock (_castGate)
                {
                    if (_deleted || generation != _castGeneration) return;
                    Execute(action);
                    FinalizeCast();
                    _castCompletion = null;
                }
            }, $"技能 {action.Index} 读条完成");
        }

        public void ActorMove(Vector3 pos, float heading, byte speedParam = 20)
        {
            var payload = new ActorMovePacket
            {
                RawX = PacketCodec.EncodeUShortCoord(pos.X),
                RawY = PacketCodec.EncodeUShortCoord(pos.Z), // y ↔ z
                RawZ = PacketCodec.EncodeUShortCoord(pos.Y),
                RawHeading = PacketCodec.EncodeUShortHeading(heading),
                SpeedParam = speedParam
            };

            PacketDispatcher.HandleActorMovePacket(Native.EntityId, payload);
        }
    }
}
