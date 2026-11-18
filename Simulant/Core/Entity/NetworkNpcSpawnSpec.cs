using Simulant.Game;
using Simulant.Game.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Network;
using Simulant.Game.FFCS.Client.Game.Object;
using System;
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
        public byte ModelState;
        public byte AnimationState;
        /// <summary>模型部件显隐标志；按实体生成包设置，与武器装备编号无关。</summary>
        public byte ModelAttributeFlags;
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

        /// <summary>显式武器模型；为 0 且使用表内默认装备时，继承装备表中的武器。</summary>
        public ulong MainhandWeaponModel;
        public ulong OffhandWeaponModel;
        /// <summary>提供时必须恰好包含 10 个装备槽；使用表内模型且未提供时，读取 BNpcBase 关联的 NpcEquip。</summary>
        public uint[] Equipment;
        /// <summary>提供时必须恰好包含 26 字节；使用表内模型且未提供时，读取 BNpcCustomize。</summary>
        public byte[] Customize;
        /// <summary>随生成包初始化的状态，最多 30 条；永久状态的 RemainingTime 可以为 0。</summary>
        public CommonSpawnData.StatusEffect[] InitialStatuses;

        internal unsafe SpawnNpcPacket ToPacket(byte spawnIndex)
        {
            if (BNpcBaseId == 0 || BNpcBaseId > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(BNpcBaseId), "战斗 NPC 基础编号必须为有效的正数表行编号。");

            if (BNpcNameId > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(BNpcNameId), "战斗 NPC 名字编号超出了表行编号范围。");

            if (Level == 0)
                throw new ArgumentOutOfRangeException(nameof(Level), "网络实体等级必须大于零。");

            if (!IsFinite(Pos.X) || !IsFinite(Pos.Y) || !IsFinite(Pos.Z))
                throw new ArgumentOutOfRangeException(nameof(Pos), "位置的各坐标分量必须为有限数。");

            if (!IsFinite(Heading))
                throw new ArgumentOutOfRangeException(nameof(Heading), "朝向必须为有限数。");

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

            if (InitialStatuses != null && InitialStatuses.Length > CommonSpawnData.StatusCapacity)
                throw new ArgumentException("网络 NPC 的初始状态不能超过 30 条。", nameof(InitialStatuses));

            BNpcBase baseRow = default(BNpcBase);

            if (!ModelCharaId.HasValue || !Battalion.HasValue)
                baseRow = ReadRow<BNpcBase>(BNpcBaseId, "战斗 NPC 基础");

            var name = Name ?? (BNpcNameId == 0 ? string.Empty : ReadRow<BNpcName>(BNpcNameId, "战斗 NPC 名字").Singular.ExtractText());
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
                    ModelChara = ModelCharaId ?? checked((ushort)baseRow.ModelChara.RowId),
                    Battalion = Battalion ?? checked((byte)baseRow.Battalion.RowId),
                    Position = new Vector3(Pos.X, Pos.Z, Pos.Y),
                    Rotation = PacketCodec.EncodeUShortHeading(Heading),
                    SpawnIndex = spawnIndex,
                    ObjectKind = ObjectKind.BattleNpc,
                    SubKind = 5,
                    CharacterMode = CharacterModes.Normal,
                    Level = Level,
                    DisplayFlags = DisplayFlags,
                    ModelScaleId = ModelScaleId,
                    ModelState = ModelState,
                    AnimationState = AnimationState,
                    ModelAttributeFlags = ModelAttributeFlags,
                    MainhandWeaponModel = MainhandWeaponModel,
                    OffhandWeaponModel = OffhandWeaponModel
                }
            };

            // 人形、亚人模型的身体也可能属于装备部件；只传 ModelChara 会生成看不见的空模型。
            if (!ModelCharaId.HasValue && equipment == null && baseRow.NpcEquip.RowId != 0)
                ApplyNpcEquipment(ref packet.Common, ReadRow<NpcEquip>(baseRow.NpcEquip.RowId, "战斗 NPC 装备"));

            if (!ModelCharaId.HasValue && customize == null && baseRow.BNpcCustomize.RowId != 0)
            {
                var row = ReadRow<BNpcCustomize>(baseRow.BNpcCustomize.RowId, "战斗 NPC 外观");
                customize = new byte[]
                {
                    checked((byte)row.Race.RowId), row.Gender, row.BodyType, row.Height,
                    checked((byte)row.Tribe.RowId), row.Face, row.HairStyle, row.HairHighlight,
                    row.SkinColor, row.EyeHeterochromia, row.HairColor, row.HairHighlightColor,
                    row.FacialFeature, row.FacialFeatureColor, row.Eyebrows, row.EyeColor,
                    row.EyeShape, row.Nose, row.Jaw, row.Mouth, row.LipColor, row.BustOrTone1,
                    row.ExtraFeature1, row.ExtraFeature2OrBust, row.FacePaint, row.FacePaintColor
                };
            }

            // 输入数组只复制到此次包的固定缓冲区，不保存引用，也不改写可复用的生成参数。
            for (var i = 0; i < nameBytes.Length; i++)
                packet.Common.Name[i] = nameBytes[i];

            if (equipment != null)
                for (var i = 0; i < equipment.Length; i++)
                    packet.Common.Equipment[i] = equipment[i];

            if (customize != null)
                for (var i = 0; i < customize.Length; i++)
                    packet.Common.Customize[i] = customize[i];

            if (InitialStatuses != null)
                for (var i = 0; i < InitialStatuses.Length; i++)
                    packet.Common.SetStatus(i, InitialStatuses[i]);

            return packet;
        }

        private static unsafe void ApplyNpcEquipment(ref CommonSpawnData common, NpcEquip row)
        {
            if (common.MainhandWeaponModel == 0)
                common.MainhandWeaponModel = row.ModelMainHand | (ulong)row.DyeMainHand.RowId << 48 | (ulong)row.Dye2MainHand.RowId << 56;

            if (common.OffhandWeaponModel == 0)
                common.OffhandWeaponModel = row.ModelOffHand | (ulong)row.DyeOffHand.RowId << 48 | (ulong)row.Dye2OffHand.RowId << 56;

            var slots = new (uint Model, uint Stain, uint Stain2)[]
            {
                (row.ModelHead, row.DyeHead.RowId, row.Dye2Head.RowId),
                (row.ModelBody, row.DyeBody.RowId, row.Dye2Body.RowId),
                (row.ModelHands, row.DyeHands.RowId, row.Dye2Hands.RowId),
                (row.ModelLegs, row.DyeLegs.RowId, row.Dye2Legs.RowId),
                (row.ModelFeet, row.DyeFeet.RowId, row.Dye2Feet.RowId),
                (row.ModelEars, row.DyeEars.RowId, row.Dye2Ears.RowId),
                (row.ModelNeck, row.DyeNeck.RowId, row.Dye2Neck.RowId),
                (row.ModelWrists, row.DyeWrists.RowId, row.Dye2Wrists.RowId),
                (row.ModelRightRing, row.DyeRightRing.RowId, row.Dye2RightRing.RowId),
                (row.ModelLeftRing, row.DyeLeftRing.RowId, row.Dye2LeftRing.RowId)
            };
            fixed (uint* equipment = common.Equipment)
            fixed (byte* stain2 = common.ModelStain2)
                for (var i = 0; i < slots.Length; i++)
                {
                    equipment[i] = slots[i].Model | slots[i].Stain << 24;
                    stain2[i] = checked((byte)slots[i].Stain2);
                }
        }

        private static T ReadRow<T>(uint id, string description)
            where T : struct, IExcelRow<T>
        {
            if (GameDataService.Instance.GetSheet<T>().TryGetRow(id, out var row))
                return row;

            throw new InvalidOperationException($"未找到{description}数据行 #{id}。");
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
