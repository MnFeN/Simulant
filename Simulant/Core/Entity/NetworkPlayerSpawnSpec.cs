using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Network;
using Simulant.Game.FFCS.Client.Game.Object;
using System;
using System.Numerics;

namespace Simulant.Core.Entity
{
    /// <summary>网络模拟玩家的职业、外观来源与初始状态；不需要真实账号或内容编号。</summary>
    public sealed class NetworkPlayerSpawnSpec
    {
        public Job Job;
        /// <summary>未指定时继承外观来源玩家的等级。</summary>
        public byte? Level;
        /// <summary>未指定时继承外观来源玩家的名字。</summary>
        public string Name;
        public Vector3 Pos;
        public float Heading;
        public uint MaxHealthPoints = 10000;
        public uint? HealthPoints;
        public ushort MaxResourcePoints = 10000;
        public ushort? ResourcePoints;
        public bool Targetable = true;
        /// <summary>默认复制自身的外貌、装备与染色；也可指定另一个有效玩家。</summary>
        public Character AppearanceSource;
        /// <summary>优先使用幻境·秘影武器与 99 级职业蓝装护甲；没有对应预设时保留复制的装备。</summary>
        public bool UseJobEquipment = true;

        internal unsafe SpawnPlayerPacket ToPacket(byte spawnIndex, PlayerAppearanceSnapshot appearance)
        {
            if (appearance == null) throw new ArgumentNullException(nameof(appearance));
            if ((byte)Job < (byte)Job.GLA || (byte)Job > (byte)Job.PCT)
                throw new ArgumentOutOfRangeException(nameof(Job), "请选择有效职业，不能使用冒险者或未实现的职业编号。");
            var level = Level ?? appearance.Level;
            if (level == 0) throw new ArgumentOutOfRangeException(nameof(Level), "网络玩家等级必须大于零。");
            if (!IsFinite(Pos.X) || !IsFinite(Pos.Y) || !IsFinite(Pos.Z))
                throw new ArgumentOutOfRangeException(nameof(Pos), "位置的各坐标分量必须为有限数。");
            if (!IsFinite(Heading)) throw new ArgumentOutOfRangeException(nameof(Heading), "朝向必须为有限数。");
            var health = HealthPoints ?? MaxHealthPoints;
            if (MaxHealthPoints == 0 || health == 0 || health > MaxHealthPoints)
                throw new ArgumentOutOfRangeException(nameof(HealthPoints), "当前生命值和最大生命值必须为正数，且当前值不能超过最大值。");
            var resource = ResourcePoints ?? MaxResourcePoints;
            if (resource > MaxResourcePoints)
                throw new ArgumentOutOfRangeException(nameof(ResourcePoints), "当前资源值不能超过最大资源值。");
            if (appearance.Customize == null || appearance.Customize.Length != 26 ||
                appearance.Equipment == null || appearance.Equipment.Length != 10 ||
                appearance.Stain2 == null || appearance.Stain2.Length != 10 ||
                appearance.Glasses == null || appearance.Glasses.Length != 2)
                throw new ArgumentException("玩家外观快照的数据长度不完整。", nameof(appearance));
            var name = PacketCodec.EncodeActorName(Name ?? appearance.Name ?? "模拟玩家");
            var packet = new SpawnPlayerPacket
            {
                HomeWorldId = appearance.HomeWorld,
                CurrentWorldId = appearance.CurrentWorld,
                Common = new CommonSpawnData
                {
                    TargetId = 0xE0000000,
                    OwnerId = 0xE0000000,
                    TetherTargetId = 0xE0000000,
                    ObjectKind = ObjectKind.Pc,
                    SubKind = 4,
                    // 玩家入场显示标志；就绪后还需 ZoneIn 控制，由生成器统一执行。
                    DisplayFlags = 0x40020,
                    CharacterMode = CharacterModes.Normal,
                    SpawnIndex = spawnIndex,
                    ClassJobId = (byte)Job,
                    Level = level,
                    Position = new Vector3(Pos.X, Pos.Z, Pos.Y),
                    Rotation = PacketCodec.EncodeUShortHeading(Heading),
                    MaxHealthPoints = MaxHealthPoints,
                    HealthPoints = health,
                    MaxResourcePoints = MaxResourcePoints,
                    ResourcePoints = resource,
                    MainhandWeaponModel = appearance.MainhandWeaponModel,
                    OffhandWeaponModel = appearance.OffhandWeaponModel
                }
            };
            for (var i = 0; i < name.Length; i++) packet.Common.Name[i] = name[i];
            for (var i = 0; i < 26; i++) packet.Common.Customize[i] = appearance.Customize[i];
            for (var i = 0; i < 10; i++)
            {
                packet.Common.Equipment[i] = appearance.Equipment[i];
                packet.Common.ModelStain2[i] = appearance.Stain2[i];
            }
            for (var i = 0; i < 2; i++) packet.Common.Glasses[i] = appearance.Glasses[i];
            if (UseJobEquipment) JobEquipment.TryApply(Job, ref packet.Common);
            return packet;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
