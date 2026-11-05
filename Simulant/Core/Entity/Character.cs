using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Network;
using Simulant.Game.FFCS.Client.Network;
using System.Numerics;
using NativeCharacter = Simulant.Game.FFCS.Client.Game.Character.Character;

namespace Simulant.Core.Entity
{
    public sealed class Character : EntityBase
    {
        private readonly object _baseTimelineGate = new object();
        private long _baseTimelineVersion;
        internal new NativeCharacter Native => base.Native.Ptr.As<NativeCharacter>();
        internal Character(EntityRegistry registry, EntityIdentity identity, EntityObservation observation)
            : base(registry, identity, observation) { }

        public uint HP { get => Native.Health; set => Native.Health.Set(value); }
        public uint MaxHP { get => Native.MaxHealth; set => Native.MaxHealth.Set(value); }
        public uint MP { get => Native.Mana; set => Native.Mana.Set(value); }
        public uint MaxMp { get => Native.MaxMana; set => Native.MaxMana.Set(value); }
        public byte Level { get => Native.Level; set => Native.Level.Set(value); }
        public Job Job { get => Native.ClassJob; set => Native.ClassJob.Set(value); }
        public CharacterModes Mode => Native.Mode;
        public byte ModeParam => Native.ModeParam;
        public uint CastingActionId
        {
            get
            {
                var castInfo = Native.CastInfo;
                return castInfo.IsCasting ? (uint)castInfo.ActionId : 0;
            }
        }

        public void SetMode(CharacterModes mode, byte modeParam = 0) => Native.SetMode(mode, modeParam);
        public bool HasStatus(uint statusId) => Native.HasStatus(statusId);
        public void Knockback(float angle, float distance, float duration, byte a5 = 0, int a6 = 0)
            => Native.Knockback(angle, distance, duration, a5, a6);

        public void PlayBlendTimeline(ushort timelineId)
        {
            if (timelineId != 0) Native.Timeline.TimelineSequencer.PlayTimeline(timelineId);
        }

        public void PlayBaseTimeline(ushort timelineId, bool interrupt = true)
        {
            if (timelineId == 0) return;
            lock (_baseTimelineGate)
            {
                var native = Native;
                _baseTimelineVersion++;
                native.SetMode(CharacterModes.AnimLock, 0);
                native.Timeline.BaseOverride.Set(timelineId);
                if (interrupt) native.Timeline.TimelineSequencer.PlayTimeline(timelineId);
            }
        }

        public void ResetBaseTimeline()
        {
            lock (_baseTimelineGate)
            {
                var native = Native;
                _baseTimelineVersion++;
                native.Timeline.BaseOverride.Set(0);
                native.SetMode(CharacterModes.Normal, 0);
            }
        }

        /// <summary>返回本次动画的版本，供行为结束时确认基础动画仍属于自己。</summary>
        internal long PlayOwnedBaseTimeline(ushort timelineId)
        {
            lock (_baseTimelineGate)
            {
                PlayBaseTimeline(timelineId);
                return _baseTimelineVersion;
            }
        }

        /// <summary>只释放本次设置的覆盖，并恢复仍匹配的动画锁定模式，不修改读条状态。</summary>
        internal bool ResetOwnedBaseTimeline(ushort timelineId, long version)
        {
            lock (_baseTimelineGate)
            {
                var native = Native;
                if (version != _baseTimelineVersion || native.Timeline.BaseOverride != timelineId)
                    return false;
                _baseTimelineVersion++;
                native.Timeline.BaseOverride.Set(0);
                if (native.Mode == CharacterModes.AnimLock && native.ModeParam == 0)
                    native.SetMode(CharacterModes.Normal, 0);
                return true;
            }
        }

        public void SetCastInfo(uint actionId, float castTime, byte actionType = 1)
        {
            var castInfo = Native.CastInfo;
            castInfo.IsCasting.Set(true);
            castInfo.ActionType.Set(actionType);
            castInfo.ActionId.Set(actionId);
            castInfo.CurrentCastTime.Set(0);
            castInfo.BaseCastTime.Set(castTime);
            castInfo.TotalCastTime.Set(castTime);
        }

        /// <summary>只结束调用者开始的技能，避免旧完成节点打断另一条读条。</summary>
        public bool FinalizeCast(uint expectedActionId)
        {
            var native = Native;
            var castInfo = native.CastInfo;
            if (expectedActionId == 0 || !castInfo.IsCasting || castInfo.ActionId != expectedActionId)
                return false;
            native.ClearCastAnimationAndState(castInfo.ActionType, expectedActionId);
            return true;
        }

        public void ActorMove(Vector3 pos, float heading, byte speedParam = 20)
        {
            var payload = new ActorMovePacket
            {
                RawX = PacketCodec.EncodeUShortCoord(pos.X),
                RawY = PacketCodec.EncodeUShortCoord(pos.Z),
                RawZ = PacketCodec.EncodeUShortCoord(pos.Y),
                RawHeading = PacketCodec.EncodeUShortHeading(heading),
                SpeedParam = speedParam
            };
            PacketDispatcher.HandleActorMovePacket(Native.EntityId, payload);
        }
    }
}
