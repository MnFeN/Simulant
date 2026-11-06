using Simulant.Game;
using Simulant.Game.ExtractedCsv;
using Simulant.Game.ExtractedCsv.Rows;
using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Network;
using Simulant.Game.FFCS.Client.Game.Object;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Simulant.Core.Entity
{
    /// <summary>通过收包处理生成战斗 NPC；位置使用 ACT 的 X/Y 水平面与 Z 高度。</summary>
    public sealed class NetworkNpcSpawnSpec
    {
        private const uint NoTarget = 0xE0000000;

        public uint BNpcBaseId;
        public uint BNpcNameId;
        public byte Level = 1;
        public Vector3 Pos;
        public float Heading;

        /// <summary>未指定时从 BNpcBase 表读取。</summary>
        public ushort? ModelCharaId;
        public byte? Battalion;
        /// <summary>未指定时从 BNpcName 表读取；名字最多占 31 个完整的 UTF-8 字节。</summary>
        public string Name;

        public uint MaxHealthPoints = 10000;
        public uint? HealthPoints;
        public ushort MaxResourcePoints = 10000;
        public ushort? ResourcePoints;
        /// <summary>事件处理器或导演的关联编号。默认 0，不自动绑定当前副本；需要事件联动时显式指定。</summary>
        public uint EventId;
        public uint LayoutId;
        public uint GimmickId;
        public byte ModelScaleId;
        public uint DisplayFlags = 0x4000B;
        /// <summary>角色进入战斗列表后，通过 ActorControl 初始化可选中状态；可见不等于可选中。</summary>
        public bool Targetable = true;
        public byte CharacterDataFlags = 3;
        public byte LinkRange = 0x14;

        public ulong TargetId = NoTarget;
        public ulong CombatTaggerId;
        public uint OwnerId = NoTarget;
        public uint TetherTargetId = NoTarget;
        public ushort TetherId;

        public ulong MainhandWeaponModel;
        public ulong OffhandWeaponModel;
        /// <summary>可选的人形外观；提供时必须恰好包含 10 个装备槽。</summary>
        public uint[] Equipment;
        /// <summary>可选的人形自定义数据；提供时必须恰好包含 26 字节。</summary>
        public byte[] Customize;

        internal unsafe SpawnNpcPacket ToPacket(byte spawnIndex)
        {
            if (BNpcBaseId == 0 || BNpcBaseId > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(BNpcBaseId), "战斗 NPC 基础编号必须为有效的正数表行编号。");
            if (BNpcNameId > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(BNpcNameId), "战斗 NPC 名字编号超出了表行编号范围。");
            if (Level == 0) throw new ArgumentOutOfRangeException(nameof(Level), "网络实体等级必须大于零。");
            if (!IsFinite(Pos.X) || !IsFinite(Pos.Y) || !IsFinite(Pos.Z))
                throw new ArgumentOutOfRangeException(nameof(Pos), "位置的各坐标分量必须为有限数。");
            if (!IsFinite(Heading)) throw new ArgumentOutOfRangeException(nameof(Heading), "朝向必须为有限数。");
            var health = HealthPoints ?? MaxHealthPoints;
            if (MaxHealthPoints == 0 || health == 0 || health > MaxHealthPoints)
                throw new ArgumentOutOfRangeException(nameof(HealthPoints), "当前生命值和最大生命值必须为正数，且当前值不能超过最大值。");
            var resource = ResourcePoints ?? MaxResourcePoints;
            if (resource > MaxResourcePoints)
                throw new ArgumentOutOfRangeException(nameof(ResourcePoints), "当前资源值不能超过最大资源值。");
            var equipment = Equipment;
            var customize = Customize;
            if (equipment != null && equipment.Length != 10)
                throw new ArgumentException("人形装备数据必须恰好包含 10 个槽位。", nameof(Equipment));
            if (customize != null && customize.Length != 26)
                throw new ArgumentException("人形自定义数据必须恰好包含 26 字节。", nameof(Customize));

            BNpcBase baseRow = null;
            if (!ModelCharaId.HasValue || !Battalion.HasValue)
                baseRow = ReadRow<BNpcBase>(BNpcBaseId, "战斗 NPC 基础");
            var name = Name ?? (BNpcNameId == 0 ? string.Empty : ReadRow<BNpcName>(BNpcNameId, "战斗 NPC 名字").Name);
            var nameBytes = PacketCodec.EncodeActorName(name);
            var packet = new SpawnNpcPacket
            {
                GimmickId = GimmickId,
                CharacterDataFlags = CharacterDataFlags,
                LinkRange = LinkRange,
                Common = new CommonSpawnData
                {
                    BaseId = BNpcBaseId,
                    NameId = BNpcNameId,
                    LayoutId = LayoutId,
                    EventId = EventId,
                    TargetId = TargetId,
                    CombatTaggerId = CombatTaggerId,
                    OwnerId = OwnerId,
                    TetherTargetId = TetherTargetId,
                    TetherId = TetherId,
                    MaxHealthPoints = MaxHealthPoints,
                    HealthPoints = health,
                    MaxResourcePoints = MaxResourcePoints,
                    ResourcePoints = resource,
                    ModelChara = ModelCharaId ?? baseRow.ModelCharaId,
                    Battalion = Battalion ?? baseRow.Battalion,
                    Position = new Vector3(Pos.X, Pos.Z, Pos.Y),
                    Rotation = PacketCodec.EncodeUShortHeading(Heading),
                    SpawnIndex = spawnIndex,
                    ObjectKind = ObjectKind.BattleNpc,
                    SubKind = 5,
                    CharacterMode = CharacterModes.Normal,
                    Level = Level,
                    DisplayFlags = DisplayFlags,
                    ModelScaleId = ModelScaleId,
                    MainhandWeaponModel = MainhandWeaponModel,
                    OffhandWeaponModel = OffhandWeaponModel
                }
            };
            // 输入数组只复制到此次包的固定缓冲区，不保存引用，也不改写可复用的生成参数。
            for (var i = 0; i < nameBytes.Length; i++) packet.Common.Name[i] = nameBytes[i];
            if (equipment != null)
                for (var i = 0; i < equipment.Length; i++) packet.Common.Equipment[i] = equipment[i];
            if (customize != null)
                for (var i = 0; i < customize.Length; i++) packet.Common.Customize[i] = customize[i];
            return packet;
        }

        private static T ReadRow<T>(uint id, string description) where T : TypedCsvRow
        {
            try
            {
                if (CsvManager.Instance.Get<T>().TryGetValue((int)id, out var row)) return row;
            }
            catch (KeyNotFoundException error)
            {
                throw new InvalidOperationException($"尚未加载{description}数据表。", error);
            }
            throw new InvalidOperationException($"未找到{description}数据行 #{id}。");
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
