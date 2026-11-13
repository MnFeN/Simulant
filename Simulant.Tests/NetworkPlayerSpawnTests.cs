using Simulant.Core.Entity;
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
    internal static class NetworkPlayerSpawnTests
    {
        internal static unsafe void PlayerPacketLayoutPreservesNativeOffsets()
        {
            Assert(sizeof(SpawnPlayerPacket) == 0x298 && sizeof(CommonSpawnData) == 0x278,
                "玩家生成包或共用角色数据的大小不正确。");
            Assert((int)Marshal.OffsetOf(typeof(SpawnPlayerPacket), nameof(SpawnPlayerPacket.Common)) == 0x20 &&
                (int)Marshal.OffsetOf(typeof(SpawnPlayerPacket), nameof(SpawnPlayerPacket.AccountId)) == 0 &&
                (int)Marshal.OffsetOf(typeof(SpawnPlayerPacket), nameof(SpawnPlayerPacket.ContentId)) == 8,
                "玩家生成包的共用数据或账号字段偏移不正确。");
            Assert((int)Marshal.OffsetOf(typeof(CommonSpawnData), nameof(CommonSpawnData.ModelStain2)) == 0x224 &&
                (int)Marshal.OffsetOf(typeof(CommonSpawnData), nameof(CommonSpawnData.Glasses)) == 0x22E,
                "第二染色或眼镜字段必须紧接十个装备模型槽。");
        }

        internal static void PlayerAppearanceSnapshotDecodesNativeBytesIndependently()
        {
            var data = new byte[0x268];
            const ulong mainhand = 0xA1B2C3D4E5F60718;
            const ulong offhand = 0x192A3B4C5D6E7F80;
            CopyBytes(BitConverter.GetBytes(mainhand), data, 0x10);
            CopyBytes(BitConverter.GetBytes(offhand), data, 0x80);
            for (var i = 0; i < 10; i++)
            {
                CopyBytes(BitConverter.GetBytes(0x8A030200u + (uint)i), data, 0x1D0 + i * 8);
                data[0x1D4 + i * 8] = (byte)(100 + i);
                data[0x1D5 + i * 8] = 0xEE;
                data[0x1D6 + i * 8] = 0xDD;
                data[0x1D7 + i * 8] = 0xCC;
            }
            for (var i = 0; i < 26; i++) data[0x220 + i] = (byte)(i + 1);
            CopyBytes(BitConverter.GetBytes((ushort)0x1234), data, 0x240);
            CopyBytes(BitConverter.GetBytes((ushort)0xABCD), data, 0x242);
            var snapshot = PlayerAppearanceSnapshot.FromDrawData(data);
            Array.Clear(data, 0, data.Length);
            Assert(snapshot.MainhandWeaponModel == mainhand && snapshot.OffhandWeaponModel == offhand,
                "武器模型必须保留全部八字节，包括两种染色。");
            for (var i = 0; i < 10; i++)
                Assert(snapshot.Equipment[i] == 0x8A030200u + (uint)i && snapshot.Stain2[i] == 100 + i,
                    "装备槽必须按八字节步长解析，且第二染色不能混入模型值。");
            Assert(snapshot.Customize.SequenceEqual(Enumerable.Range(1, 26).Select(value => (byte)value)) &&
                snapshot.Glasses.SequenceEqual(new ushort[] { 0x1234, 0xABCD }),
                "自定义外观与两个眼镜字段必须完整复制，不能引用随后变化的原生字节数组。");
            Throws<ArgumentNullException>(() => PlayerAppearanceSnapshot.FromDrawData(null));
            Throws<ArgumentException>(() => PlayerAppearanceSnapshot.FromDrawData(new byte[0x267]));
            Throws<ArgumentException>(() => PlayerAppearanceSnapshot.FromDrawData(new byte[0x269]));
        }

        internal static void PlayerSpawnDefaultsUseSourceAppearanceAndExplicitJob()
        {
            var appearance = CreateAppearance();
            var spec = new NetworkPlayerSpawnSpec { Job = (Job)24, UseJobEquipment = false, Pos = new Vector3(1, 2, 3) };
            var packet = spec.ToPacket(7, appearance);
            Assert(packet.Common.ObjectKind == ObjectKind.Pc && packet.Common.SubKind == 4 &&
                packet.Common.DisplayFlags == 0x40020 && packet.Common.CharacterMode == CharacterModes.Normal &&
                packet.Common.SpawnIndex == 7 && packet.Common.ClassJobId == 24,
                "网络玩家必须使用玩家类型和显示标志，并保留输入职业。");
            Assert(packet.AccountId == 0 && packet.ContentId == 0 && packet.GMRank == 0 && packet.TitleId == 0,
                "模拟玩家不得继承真实玩家的账号、内容 ID 或管理员身份。");
            Assert(packet.CurrentWorldId == 17 && packet.HomeWorldId == 23 && packet.Common.Level == 90 &&
                ReadName(packet) == "外观来源", "服务器、等级和默认名字必须来自本次外观快照。");
            Assert(packet.Common.MaxHealthPoints == 10000 && packet.Common.HealthPoints == 10000 &&
                packet.Common.MaxResourcePoints == 10000 && packet.Common.ResourcePoints == 10000,
                "玩家默认当前生命和资源必须与其最大值一致。");
            Assert(packet.Common.Position == new Vector3(1, 3, 2) && packet.Common.Rotation == 32768 &&
                (ulong)packet.Common.TargetId == 0xE0000000 && packet.Common.OwnerId == 0xE0000000 &&
                packet.Common.TetherTargetId == 0xE0000000 && packet.Common.EventId == 0,
                "玩家坐标、朝向和无目标哨兵必须正确，且默认不得绑定副本事件。");
            Assert(spec.Targetable && !spec.Level.HasValue && spec.Name == null,
                "玩家应默认可选中；构包不得把快照默认值写回可复用参数。");
        }

        internal static unsafe void PlayerSpawnPacketsOwnTheirAppearanceBytes()
        {
            var appearance = CreateAppearance();
            var spec = new NetworkPlayerSpawnSpec { Job = (Job)19, UseJobEquipment = false };
            var first = spec.ToPacket(1, appearance);
            appearance.Equipment[0] = 42;
            appearance.Stain2[0] = 43;
            appearance.Customize[0] = 44;
            appearance.Glasses[0] = 45;
            appearance.MainhandWeaponModel = 46;
            appearance.Name = "后来名字";
            var second = spec.ToPacket(2, appearance);
            Assert(first.Common.Equipment[0] == 0x7F010200 && first.Common.ModelStain2[0] == 80 &&
                first.Common.Customize[0] == 1 && first.Common.Glasses[0] == 300 &&
                first.Common.MainhandWeaponModel == 0x0102030405060708 && ReadName(first) == "外观来源",
                "构包后修改来源快照，不得污染已排队的玩家外观。");
            Assert(second.Common.Equipment[0] == 42 && second.Common.ModelStain2[0] == 43 &&
                second.Common.Customize[0] == 44 && second.Common.Glasses[0] == 45 &&
                second.Common.MainhandWeaponModel == 46 && ReadName(second) == "后来名字",
                "下次构包必须重新读取本次快照数据。");
            Assert(first.Common.Equipment[9] == appearance.Equipment[9] && first.Common.ModelStain2[9] == appearance.Stain2[9] &&
                first.Common.Customize[25] == appearance.Customize[25] && first.Common.Glasses[1] == appearance.Glasses[1] &&
                first.Common.OffhandWeaponModel == appearance.OffhandWeaponModel,
                "外观数组末项、第二个眼镜或副手模型不得丢失。");
        }

        internal static void PlayerSpawnOverridesAndNamesAreIndependentOfSource()
        {
            var appearance = CreateAppearance();
            var spec = new NetworkPlayerSpawnSpec
            {
                Job = Job.PLD, UseJobEquipment = false, Level = 50, Name = "模拟角色",
                MaxHealthPoints = 12345, HealthPoints = 6789, MaxResourcePoints = 9876, ResourcePoints = 5432,
                Targetable = false
            };
            var packet = spec.ToPacket(3, appearance);
            Assert(packet.Common.Level == 50 && ReadName(packet) == "模拟角色" &&
                packet.Common.MaxHealthPoints == 12345 && packet.Common.HealthPoints == 6789 &&
                packet.Common.MaxResourcePoints == 9876 && packet.Common.ResourcePoints == 5432,
                "显式名字、等级、生命和资源必须覆盖默认值。");
            Assert(!spec.Targetable && packet.Common.DisplayFlags == 0x40020 && appearance.Level == 90 && appearance.Name == "外观来源",
                "不可选中设置不得更改显示标志；覆盖值不得改写来源快照。");
            spec.Name = new string('甲', 11);
            Assert(ReadName(spec.ToPacket(4, appearance)) == new string('甲', 10),
                "玩家中文名字必须按完整 UTF-8 字符截断至 31 字节内。");
            spec.Name = new string('a', 27) + "😀";
            Assert(ReadName(spec.ToPacket(4, appearance)) == spec.Name, "四字节字符应完整占用剩余名字空间。");
            spec.Name = new string('a', 28) + "😀";
            Assert(ReadName(spec.ToPacket(4, appearance)) == new string('a', 28), "名字尾部不得截断四字节字符。");
            spec.Name = null;
            appearance.Name = null;
            Assert(ReadName(spec.ToPacket(4, appearance)) == "模拟玩家", "无来源名字时必须有明确的玩家默认名字。");
        }

        internal static unsafe void PlayerJobEquipmentPreservesFaceAccessoriesAndFallback()
        {
            var appearance = CreateAppearance();
            var jobs = new[] { Job.PLD, Job.MNK, Job.WAR, Job.DRG, Job.BRD, Job.WHM, Job.BLM, Job.SMN };
            var armors = new uint[] { 0x10304, 0x10305, 0x10306, 0x10307, 0x10308, 0x10309, 0x1030A, 0x1030B };
            var mainhands = new ulong[] { 0x2001900DC, 0x200010156, 0x2000401A2, 0x200120210, 0x200010280, 0x200260328, 0x200090409, 0x2000106E7 };
            var offhands = new ulong[] { 0x200190072, 0x200010188, 0, 0, 0x1009602BA, 0, 0, 0 };
            for (var index = 0; index < jobs.Length; index++)
            {
                var spec = new NetworkPlayerSpawnSpec { Job = jobs[index] };
                var packet = spec.ToPacket(1, appearance);
                Assert(JobEquipment.HasSet(jobs[index]) && packet.Common.MainhandWeaponModel == mainhands[index] &&
                    packet.Common.OffhandWeaponModel == offhands[index] && packet.Common.ClassJobId == (byte)jobs[index],
                    "已知职业套装的武器、染色或职业编号与核实数据不符：" + jobs[index]);
                for (var slot = 0; slot < 10; slot++)
                    Assert(packet.Common.Equipment[slot] == (slot < 5 ? armors[index] : appearance.Equipment[slot]) &&
                        packet.Common.ModelStain2[slot] == (slot < 5 ? 0 : appearance.Stain2[slot]),
                        "校服只应替换五个护甲槽并清理其两种染色，必须保留饰品：" + slot);
                for (var slot = 0; slot < 26; slot++)
                    Assert(packet.Common.Customize[slot] == appearance.Customize[slot], "换职业校服不得改变肤色、种族或面貌。");
                Assert(packet.Common.Glasses[0] == 300 && packet.Common.Glasses[1] == 400,
                    "换职业校服不得移除复制的眼镜。");
            }
            foreach (var job in new[] { Job.GLA, Job.ACN, Job.BLU, Job.CRP })
            {
                var packet = new NetworkPlayerSpawnSpec { Job = job }.ToPacket(2, appearance);
                Assert(!JobEquipment.HasSet(job) && !JobEquipment.TryApply(job, ref packet.Common) &&
                    packet.Common.MainhandWeaponModel == appearance.MainhandWeaponModel &&
                    packet.Common.OffhandWeaponModel == appearance.OffhandWeaponModel && packet.Common.ClassJobId == (byte)job,
                    "没有 99 级职业套装的基础职业、青魔及生产采集职业必须保留来源武器。");
                for (var slot = 0; slot < 10; slot++)
                    Assert(packet.Common.Equipment[slot] == appearance.Equipment[slot] &&
                        packet.Common.ModelStain2[slot] == appearance.Stain2[slot], "校服回退不得改写任何装备槽。");
            }
            Assert(appearance.Equipment[0] == 0x7F010200 && appearance.Stain2[0] == 80 &&
                appearance.MainhandWeaponModel == 0x0102030405060708, "应用校服不得修改供其他生成请求共用的外观快照。");
        }

        internal static void PlayerSpawnRejectsInvalidVitalsJobsAndAppearance()
        {
            var appearance = CreateAppearance();
            var spec = new NetworkPlayerSpawnSpec { Job = Job.PLD };
            Throws<ArgumentNullException>(() => spec.ToPacket(1, null));
            foreach (var job in new[] { (Job)0, (Job)43, (Job)255 })
            {
                spec.Job = job;
                Throws<ArgumentOutOfRangeException>(() => spec.ToPacket(1, appearance));
            }
            spec.Job = Job.PLD;
            foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                foreach (var pos in new[] { new Vector3(value, 0, 0), new Vector3(0, value, 0), new Vector3(0, 0, value) })
                {
                    spec.Pos = pos;
                    Throws<ArgumentOutOfRangeException>(() => spec.ToPacket(1, appearance));
                }
                spec.Pos = Vector3.Zero;
                spec.Heading = value;
                Throws<ArgumentOutOfRangeException>(() => spec.ToPacket(1, appearance));
            }
            spec.Heading = 0;
            spec.Level = 0;
            Throws<ArgumentOutOfRangeException>(() => spec.ToPacket(1, appearance));
            spec.Level = null;
            appearance.Level = 0;
            Throws<ArgumentOutOfRangeException>(() => spec.ToPacket(1, appearance));
            appearance.Level = 90;
            spec.MaxHealthPoints = 0;
            Throws<ArgumentOutOfRangeException>(() => spec.ToPacket(1, appearance));
            spec.MaxHealthPoints = 100;
            foreach (var health in new[] { 0u, 101u })
            {
                spec.HealthPoints = health;
                Throws<ArgumentOutOfRangeException>(() => spec.ToPacket(1, appearance));
            }
            spec.HealthPoints = 100;
            spec.MaxResourcePoints = 1;
            spec.ResourcePoints = 2;
            Throws<ArgumentOutOfRangeException>(() => spec.ToPacket(1, appearance));
            spec.ResourcePoints = 0;
            spec.MaxResourcePoints = 0;
            Assert(spec.ToPacket(1, appearance).Common.ResourcePoints == 0, "零资源上限与当前值应允许生成。");
            foreach (var name in new[] { "名字\0后缀", "\uD800" })
            {
                spec.Name = name;
                Throws<ArgumentException>(() => spec.ToPacket(1, appearance));
            }
            spec.Name = null;
            var invalidSnapshots = new[]
            {
                new PlayerAppearanceSnapshot { Level = 1, Equipment = null },
                new PlayerAppearanceSnapshot { Level = 1, Equipment = new uint[9] },
                new PlayerAppearanceSnapshot { Level = 1, Stain2 = null },
                new PlayerAppearanceSnapshot { Level = 1, Stain2 = new byte[11] },
                new PlayerAppearanceSnapshot { Level = 1, Customize = null },
                new PlayerAppearanceSnapshot { Level = 1, Customize = new byte[25] },
                new PlayerAppearanceSnapshot { Level = 1, Glasses = null },
                new PlayerAppearanceSnapshot { Level = 1, Glasses = new ushort[3] }
            };
            foreach (var invalid in invalidSnapshots)
                Throws<ArgumentException>(() => spec.ToPacket(1, invalid));
        }

        private static PlayerAppearanceSnapshot CreateAppearance()
        {
            return new PlayerAppearanceSnapshot
            {
                Name = "外观来源", Level = 90, HomeWorld = 23, CurrentWorld = 17,
                MainhandWeaponModel = 0x0102030405060708, OffhandWeaponModel = 0x1112131415161718,
                Equipment = Enumerable.Range(0, 10).Select(value => 0x7F010200u + (uint)value).ToArray(),
                Stain2 = Enumerable.Range(80, 10).Select(value => (byte)value).ToArray(),
                Customize = Enumerable.Range(1, 26).Select(value => (byte)value).ToArray(),
                Glasses = new ushort[] { 300, 400 }
            };
        }

        private static unsafe string ReadName(SpawnPlayerPacket packet)
        {
            var bytes = new byte[32];
            for (var i = 0; i < bytes.Length; i++) bytes[i] = packet.Common.Name[i];
            return new UTF8Encoding(false, true).GetString(bytes, 0, Array.IndexOf(bytes, (byte)0));
        }

        private static void CopyBytes(byte[] source, byte[] destination, int offset)
            => Array.Copy(source, 0, destination, offset, source.Length);

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new Exception("预期抛出 " + typeof(T).Name + "。");
        }
    }
}
