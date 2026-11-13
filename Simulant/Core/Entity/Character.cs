using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Network;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Game.FFCS.Client.Network;
using System;
using System.Numerics;
using NativeCharacter = Simulant.Game.FFCS.Client.Game.Character.Character;

namespace Simulant.Core.Entity
{
    public sealed partial class Character : EntityBase
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

        /// <summary>通过原生收包更新血蓝量，保留全部状态；仅用于网络角色。</summary>
        public void SetNetworkVitals(uint hp, ushort? mp = null)
        {
            if (PacketDispatcher.HandleStatusEffectListDoublePacketFuncPtr == IntPtr.Zero)
                throw new InvalidOperationException("原生角色状态更新函数尚未定位。");
            var native = Native;
            ushort index = native.ObjectIndex;
            if ((ObjectKind != ObjectKind.Pc && ObjectKind != ObjectKind.BattleNpc) || index >= 200 || index % 2 != 0)
                throw new InvalidOperationException("只有战斗实体列表中的网络玩家或战斗 NPC 可以接收血量更新。");
            uint maxHp = native.MaxHealth, maxMp = native.MaxMana;
            uint currentMp = mp.HasValue ? mp.Value : (uint)native.Mana;
            if (hp > maxHp) throw new ArgumentOutOfRangeException(nameof(hp), "当前血量不能超过血量上限。");
            if (currentMp > maxMp) throw new ArgumentOutOfRangeException(nameof(mp), "当前魔力不能超过魔力上限。");
            if (maxMp > ushort.MaxValue)
                throw new InvalidOperationException("角色魔力上限超出状态更新包的范围。");

            var packet = new StatusEffectListDoublePacket
            {
                ClassJob = (byte)(Job)native.ClassJob, Level = native.Level, Level_2 = native.Level,
                CurrentHP = hp, MaxHP = maxHp, CurrentMP = (ushort)currentMp, MaxMP = (ushort)maxMp,
                DamageShield = native.ShieldValue
            };
            // 状态内存每槽 16 字节，包内每槽 12 字节；来源在网络包中使用实体 ID。
            var statuses = native.StatusManager.Ptr.ReadBytes(StatusEffectListDoublePacket.StatusCount * 0x10, 8);
            if (statuses == null || statuses.Length != StatusEffectListDoublePacket.StatusCount * 0x10)
                throw new InvalidOperationException("读取角色完整状态失败，未发送血量更新。");
            for (var i = 0; i < StatusEffectListDoublePacket.StatusCount; i++)
            {
                var offset = i * 0x10;
                packet.SetStatus(i, new StatusEffectListStatus
                {
                    StatusId = BitConverter.ToUInt16(statuses, offset), Param = BitConverter.ToUInt16(statuses, offset + 2),
                    RemainingTime = BitConverter.ToSingle(statuses, offset + 4), SourceEntityId = BitConverter.ToUInt32(statuses, offset + 8)
                });
            }
            PacketDispatcher.HandleStatusEffectListDoublePacket(Native.EntityId, packet);
        }

        public byte Level { get => Native.Level; set => Native.Level.Set(value); }
        public Job Job { get => Native.ClassJob; set => Native.ClassJob.Set(value); }
        public CharacterModes Mode => Native.Mode;
        public byte ModeParam => Native.ModeParam;
        public bool IsWeaponDrawn => Native.Timeline.IsWeaponDrawn;

        /// <summary>通过 ActorControlTarget 更新角色的攻击目标；空目标表示取消选择。</summary>
        public void SetNetworkTarget(Character target)
        {
            var id = target == null ? 0xE0000000u : (uint)target.Native.EntityId;
            // Sapphire Chara::changeTarget：目标位于包尾 targetId，不能填在 a1 中。
            // https://github.com/SapphireServer/Sapphire/blob/master/src/world/Actor/Chara.cpp
            PacketDispatcher.HandleActorControl(Native.EntityId, ActorControlCategory.SetTarget,
                0, 0, 0, 0, 0, 0, 0, 0, id, 0);
        }

        internal void SetCastTarget(Character target)
        {
            var native = target.Native;
            var cast = Native.CastInfo;
            cast.TargetId.Set(new GameObjectId { ObjectId = target.Id });
            cast.TargetLocation.Set(native.Position);
        }
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
