using Simulant.Core.Entity;
using Simulant.Game.ExtractedCsv;
using Simulant.Game.ExtractedCsv.Rows;
using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Network;
using Simulant.Game.FFCS.Client.Game.Object;
using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Simulant.Tests
{
    internal static class NetworkSpawnTests
    {
        public static unsafe void NetworkPacketLayoutPreservesNativeOffsets()
        {
            Assert(sizeof(SpawnNpcPacket) == 0x290 && sizeof(CommonSpawnData) == 0x278 &&
                sizeof(DespawnCharacterPacket) == 1, "网络生成或删除包的大小发生了变化。");
            Assert((int)Marshal.OffsetOf(typeof(SpawnNpcPacket), nameof(SpawnNpcPacket.Common)) == 0x10,
                "NPC 生成包的共用数据偏移不正确。");
            var fields = new[] { "MainhandWeaponModel", "OffhandWeaponModel", "Equipment", "Name", "Customize" };
            var offsets = new[] { 0x10, 0x18, 0x1FC, 0x232, 0x252 };
            for (var i = 0; i < fields.Length; i++)
                Assert((int)Marshal.OffsetOf(typeof(CommonSpawnData), fields[i]) == offsets[i],
                    "网络包字段偏移不正确：" + fields[i]);
            Assert((int)Marshal.OffsetOf(typeof(DespawnCharacterPacket), nameof(DespawnCharacterPacket.Index)) == 0,
                "删除包必须以网络槽位编号开头。");
        }

        public static void NetworkSpawnDefaultsUseCsvAndActCoordinates()
        {
            CsvManager.Instance.Get<BNpcBase>().Clear();
            CsvManager.Instance.Get<BNpcName>().Clear();
            CsvManager.Instance.Get<BNpcBase>()[42] = new BNpcBase { ModelCharaId = 321, Battalion = 4 };
            CsvManager.Instance.Get<BNpcName>()[84] = new BNpcName { Name = "测试魔物" };
            var spec = new NetworkNpcSpawnSpec
            {
                BNpcBaseId = 42, BNpcNameId = 84, Level = 90, Pos = new Vector3(1, 2, 3)
            };
            var packet = spec.ToPacket(7);
            var common = packet.Common;
            Assert(common.BaseId == 42 && common.NameId == 84 && common.ModelChara == 321 && common.Battalion == 4,
                "未指定外观和阵营时必须使用当前 CSV 数据。");
            Assert(ReadName(packet) == "测试魔物" && common.Position == new Vector3(1, 3, 2) && common.Rotation == 32768,
                "名字、ACT 坐标轴转换或朝向编码不正确。");
            Assert(common.ObjectKind == ObjectKind.BattleNpc && common.SubKind == 5 &&
                common.CharacterMode == CharacterModes.Normal && common.ClassJobId == 0 && common.Level == 90,
                "默认角色类型、模式、职业或等级不正确。");
            Assert(common.MaxHealthPoints == 10000 && common.HealthPoints == 10000 &&
                common.MaxResourcePoints == 10000 && common.ResourcePoints == 10000,
                "默认当前生命值和资源值必须与最大值一致。");
            Assert(packet.CharacterDataFlags == 3 && packet.LinkRange == 0x14 && common.DisplayFlags == 0x4000B &&
                common.SpawnIndex == 7 && common.EventId == 0,
                "战斗实体默认标志或生成槽位不正确，默认不得关联副本事件。");
            Assert(spec.Targetable, "网络实体默认应在生成后设置为可选中。");
            Assert((ulong)common.TargetId == 0xE0000000 && common.OwnerId == 0xE0000000 &&
                common.TetherTargetId == 0xE0000000 && (ulong)common.CombatTaggerId == 0 && common.ObjectType == 0,
                "无目标哨兵必须零扩展到 64 位，并保留独立对象类型及仇恨归属默认值。");
            Assert(!spec.ModelCharaId.HasValue && !spec.Battalion.HasValue && spec.Name == null &&
                spec.EventId == 0 && !spec.HealthPoints.HasValue && !spec.ResourcePoints.HasValue,
                "解析默认值不得改写调用者的可复用参数。");
        }

        public static void NetworkSpawnOverridesDoNotRequireCsvRows()
        {
            CsvManager.Instance.Get<BNpcBase>().Clear();
            CsvManager.Instance.Get<BNpcName>().Clear();
            var spec = CreateSpec();
            spec.MaxHealthPoints = 765;
            spec.HealthPoints = 432;
            spec.MaxResourcePoints = 222;
            spec.ResourcePoints = 111;
            spec.EventId = 0x80030001;
            spec.Targetable = false;
            spec.LayoutId = 123;
            spec.GimmickId = 456;
            spec.ModelScaleId = 7;
            spec.DisplayFlags = 0x1234;
            spec.CharacterDataFlags = 2;
            spec.LinkRange = 8;
            spec.TargetId = 0x102030405;
            spec.CombatTaggerId = 0x102030406;
            spec.OwnerId = 99;
            spec.TetherTargetId = 100;
            spec.TetherId = 9;
            var packet = spec.ToPacket(3);
            Assert(packet.Common.ModelChara == 321 && packet.Common.Battalion == 4 && ReadName(packet) == "测试单位",
                "显式外观、阵营和名字应允许不依赖 CSV 行构包。");
            Assert(packet.Common.MaxHealthPoints == 765 && packet.Common.HealthPoints == 432 &&
                packet.Common.MaxResourcePoints == 222 && packet.Common.ResourcePoints == 111,
                "生命值或资源值覆盖没有生效。");
            Assert(packet.Common.EventId == 0x80030001 && packet.Common.LayoutId == 123 && packet.GimmickId == 456 &&
                packet.Common.ModelScaleId == 7 && packet.Common.DisplayFlags == 0x1234 &&
                packet.CharacterDataFlags == 2 && packet.LinkRange == 8,
                "显式事件编号、布局、机关或模型索引覆盖没有生效。");
            Assert(!spec.Targetable && packet.Common.DisplayFlags == spec.DisplayFlags,
                "允许生成后保持不可选中，但不能借此改写调用者指定的显示标志。");
            Assert((ulong)packet.Common.TargetId == spec.TargetId && (ulong)packet.Common.CombatTaggerId == spec.CombatTaggerId &&
                packet.Common.OwnerId == 99 && packet.Common.TetherTargetId == 100 && packet.Common.TetherId == 9,
                "目标和归属引用没有保留完整值。");
            Assert(spec.ToPacket(4).Common.EventId == 0x80030001 && spec.ToPacket(5).Common.EventId == 0x80030001 &&
                spec.EventId == 0x80030001,
                "重复构包必须保留调用者显式指定的事件编号，不得替换或改写该关联。");
            spec.EventId = 0;
            Assert(spec.ToPacket(6).Common.EventId == 0, "显式清空事件编号后，构包不得自动恢复副本关联。");
        }

        public static unsafe void NetworkSpawnCopiesAppearanceIntoEachPacket()
        {
            var spec = CreateSpec();
            var equipment = Enumerable.Range(1, 10).Select(value => (uint)value * 0x10001).ToArray();
            var customize = Enumerable.Range(1, 26).Select(value => (byte)value).ToArray();
            spec.Equipment = equipment;
            spec.Customize = customize;
            spec.MainhandWeaponModel = 0x0102030405060708;
            spec.OffhandWeaponModel = 0x1112131415161718;
            var first = spec.ToPacket(1);
            equipment[0] = 999;
            customize[0] = 99;
            spec.Name = "后来名字";
            var second = spec.ToPacket(2);
            Assert(first.Common.Equipment[0] == 0x10001 && first.Common.Customize[0] == 1 && ReadName(first) == "测试单位",
                "构包后调用者修改输入，不得改变已排队包中的外观和名字。");
            Assert(second.Common.Equipment[0] == 999 && second.Common.Customize[0] == 99 && ReadName(second) == "后来名字",
                "下一次构包必须读取本次输入，不得重用旧外观缓存。");
            Assert(first.Common.Equipment[9] == equipment[9] && first.Common.Customize[25] == customize[25] &&
                first.Common.MainhandWeaponModel == spec.MainhandWeaponModel &&
                first.Common.OffhandWeaponModel == spec.OffhandWeaponModel,
                "装备、自定义数组末项或 64 位武器模型丢失。");
            Assert(ReferenceEquals(spec.Equipment, equipment) && ReferenceEquals(spec.Customize, customize),
                "构包不得替换调用者的输入数组。");
        }

        public static unsafe void NetworkSpawnNamesEndAtCompleteUtf8Characters()
        {
            var spec = CreateSpec();
            spec.Name = new string('甲', 11);
            var packet = spec.ToPacket(1);
            Assert(ReadName(packet) == new string('甲', 10) && packet.Common.Name[30] == 0 && packet.Common.Name[31] == 0,
                "中文名字截断必须保留完整字符和结尾空字符。");
            spec.Name = new string('a', 27) + "😀";
            packet = spec.ToPacket(1);
            Assert(ReadName(packet) == spec.Name && packet.Common.Name[31] == 0,
                "31 字节内的补充平面字符必须完整保留。");
            spec.Name = new string('a', 28) + "😀";
            Assert(ReadName(spec.ToPacket(1)) == new string('a', 28),
                "截断不得留下 UTF-16 代理项或不完整的 UTF-8 字节。");
            spec.Name = "名字\0后缀";
            Throws<ArgumentException>(() => spec.ToPacket(1));
            spec.Name = "\uD800";
            Throws<ArgumentException>(() => spec.ToPacket(1));
        }

        public static void NetworkSpawnRejectsInvalidValuesBeforeUsingCsv()
        {
            var spec = CreateSpec();
            foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                foreach (var position in new[] { new Vector3(value, 0, 0), new Vector3(0, value, 0), new Vector3(0, 0, value) })
                {
                    spec.Pos = position;
                    Throws<ArgumentOutOfRangeException>(() => spec.ToPacket(1));
                }
                spec.Pos = Vector3.Zero;
                spec.Heading = value;
                Throws<ArgumentOutOfRangeException>(() => spec.ToPacket(1));
            }
            spec = CreateSpec(); spec.Level = 0;
            Throws<ArgumentOutOfRangeException>(() => spec.ToPacket(1));
            spec = CreateSpec(); spec.BNpcBaseId = 0;
            Throws<ArgumentOutOfRangeException>(() => spec.ToPacket(1));
            spec = CreateSpec(); spec.MaxHealthPoints = 0;
            Throws<ArgumentOutOfRangeException>(() => spec.ToPacket(1));
            spec = CreateSpec(); spec.HealthPoints = 0;
            Throws<ArgumentOutOfRangeException>(() => spec.ToPacket(1));
            spec = CreateSpec(); spec.HealthPoints = spec.MaxHealthPoints + 1;
            Throws<ArgumentOutOfRangeException>(() => spec.ToPacket(1));
            spec = CreateSpec(); spec.MaxResourcePoints = 0; spec.ResourcePoints = 1;
            Throws<ArgumentOutOfRangeException>(() => spec.ToPacket(1));
            spec = CreateSpec(); spec.MaxResourcePoints = 0;
            Assert(spec.ToPacket(1).Common.ResourcePoints == 0, "无资源条的实体应允许当前值和最大值同时为零。");
            foreach (var length in new[] { 0, 9, 11 })
            {
                spec = CreateSpec(); spec.Equipment = new uint[length];
                Throws<ArgumentException>(() => spec.ToPacket(1));
            }
            foreach (var length in new[] { 0, 25, 27 })
            {
                spec = CreateSpec(); spec.Customize = new byte[length];
                Throws<ArgumentException>(() => spec.ToPacket(1));
            }
        }

        public static void NetworkSpawnReportsMissingRowsAndAllowsUnnamedActors()
        {
            CsvManager.Instance.Get<BNpcBase>().Clear();
            CsvManager.Instance.Get<BNpcName>().Clear();
            var spec = CreateSpec();
            spec.ModelCharaId = null;
            Throws<InvalidOperationException>(() => spec.ToPacket(1));
            CsvManager.Instance.Get<BNpcBase>()[42] = new BNpcBase { ModelCharaId = 456, Battalion = 6 };
            Assert(spec.ToPacket(1).Common.ModelChara == 456 && spec.ToPacket(1).Common.Battalion == 4,
                "只覆盖阵营时，模型仍应来自 CSV，阵营不能被表值覆盖。");
            spec.Name = null;
            Throws<InvalidOperationException>(() => spec.ToPacket(1));
            spec.BNpcNameId = 0;
            Assert(ReadName(spec.ToPacket(1)) == string.Empty,
                "未指定名字编号的独立实体应允许空名字。");
        }

        private static NetworkNpcSpawnSpec CreateSpec() => new NetworkNpcSpawnSpec
        {
            BNpcBaseId = 42, BNpcNameId = 84, Level = 90, ModelCharaId = 321, Battalion = 4, Name = "测试单位"
        };

        private static unsafe string ReadName(SpawnNpcPacket packet)
        {
            var length = 0;
            while (length < 32 && packet.Common.Name[length] != 0) length++;
            var bytes = new byte[length];
            for (var i = 0; i < length; i++) bytes[i] = packet.Common.Name[i];
            return new UTF8Encoding(false, true).GetString(bytes);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void Throws<T>(System.Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new Exception("预期抛出 " + typeof(T).Name + "。");
        }
    }
}
