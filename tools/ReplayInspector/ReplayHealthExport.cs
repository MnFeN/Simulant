using Simulant.Replay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using static Simulant.Replay.ReplayConversion;
using static Simulant.Replay.ReplayReader;

namespace Simulant.ReplayTool
{
    internal static class ReplayHealthExport
    {
        private const uint SampleIntervalMs = 10000;

        private sealed class Observation
        {
            internal ReplayPacket Packet;
            internal string Kind;
            internal uint Hp;
            internal uint MaxHp;
        }

        internal static void Export(string inputPath, string outputDirectory, string profile, uint actorId, uint startMs, uint endMs)
        {
            profile = profile ?? ReplayPacketCatalog.China758Profile;
            if (profile != ReplayPacketCatalog.China758Profile)
                throw new ArgumentException("血量提取目前支持已核对的 CN7.58。");
            if (endMs < startMs)
                throw new ArgumentException("区间结束时间必须大于或等于开始时间。");

            var input = Path.GetFullPath(inputPath);
            var output = ReplayInspector.ValidateOutputDirectory(input, outputDirectory);
            using (var stream = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha = SHA256.Create())
            {
                var hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                using (var reader = new ReplayReader(stream, leaveOpen: true))
                {
                    if (reader.Header.GameVersion != AuditedBuild)
                        throw new InvalidDataException($"当前血量布局适用于构建 0x{AuditedBuild:X8}，回放为 0x{reader.Header.GameVersion:X8}。");

                    var observations = Read(reader, profile, actorId, startMs, endMs);
                    Directory.CreateDirectory(output);
                    WriteSamples(Path.Combine(output, "health.tsv"), observations, startMs, endMs);
                    File.WriteAllLines(Path.Combine(output, "summary.md"), new[]
                    {
                        "# 回放血量样本",
                        "",
                        $"- 文件 {Path.GetFileName(input)}，SHA-256 {hash}。",
                        $"- 容器格式 {reader.Header.FormatVersion}，包版本 {profile}，构建 0x{AuditedBuild:X8}。",
                        $"- 实体 0x{actorId:X8}，范围 {startMs}–{endMs} 毫秒。",
                        "- 每 10 秒取一次当时最近的同步值，另取区间终点。相对秒从区间起点算起。",
                        "- 结算确认同时匹配包路由与条目实体。血量、源包时间和偏移保存在 health.tsv。"
                    }, new UTF8Encoding(true));
                }
            }
        }

        private static List<Observation> Read(ReplayReader reader, string profile, uint actorId, uint startMs, uint endMs)
        {
            var observations = new List<Observation>();
            var classifications = new Dictionary<int, string>();
            int? slot = null;
            var lastMs = 0u;
            var maxHp = 0u;
            foreach (var packet in reader.ReadPackets())
            {
                lastMs = Math.Max(lastMs, packet.TimestampMs);
                if (packet.TimestampMs > endMs)
                    continue;

                var body = packet.Payload;
                var key = packet.Opcode << 16 | body.Length;
                if (!classifications.TryGetValue(key, out var kind))
                {
                    var classification = ReplayPacketCatalog.Classify(reader.Header.GameVersion, packet.Opcode, body.Length, profile);
                    kind = classification.Confidence == ReplayPacketConfidence.SelectedProfileMatch ? classification.Name : "";
                    classifications.Add(key, kind);
                }

                if (kind == "DespawnCharacter" && slot.HasValue)
                {
                    var removed = UInt32(body, 4);
                    if (removed == actorId || (removed == 0 || removed == 0xE0000000) && body[0] == slot.Value)
                    {
                        if (packet.TimestampMs > startMs)
                            throw new InvalidDataException("指定区间跨过了该实体的回收，请按一次生成至回收的区间提取。");
                        observations.Clear();
                        slot = null;
                        maxHp = 0;
                    }
                    continue;
                }

                if (kind == "NpcSpawn" || kind == "NpcSpawn2" || kind == "PlayerSpawn")
                {
                    var common = kind == "PlayerSpawn" ? 0x20 : 0x10;
                    var spawnSlot = body[common + 0x6E];
                    if (packet.ObjectId == actorId || slot == spawnSlot)
                    {
                        if (packet.TimestampMs > startMs)
                            throw new InvalidDataException("指定区间内实体或其槽位重新生成，请从目标实体本次生成之后开始提取。");
                        observations.Clear();
                        slot = packet.ObjectId == actorId ? (int?)spawnSlot : null;
                        maxHp = slot.HasValue ? UInt32(body, common + 0x4C) : 0;
                        if (slot.HasValue)
                            Add(UInt32(body, common + 0x50));
                    }
                    continue;
                }

                if (!slot.HasValue)
                    continue;
                if (kind.StartsWith("ActorControl", StringComparison.Ordinal) &&
                    UInt16(body, 0) == 0x58 && UInt32(body, 4) == actorId)
                {
                    Add(UInt32(body, 8));
                    continue;
                }
                if (packet.ObjectId != actorId)
                    continue;

                switch (kind)
                {
                    case "StatusEffectList":
                        Add(UInt32(body, 4), UInt32(body, 8));
                        break;
                    case "BossStatusEffectList":
                    case "StatusEffectListDouble":
                        Add(UInt32(body, 0x16C), UInt32(body, 0x170));
                        break;
                    case "UpdateHpMpTp":
                        Add(UInt32(body, 0));
                        break;
                    default:
                        if (kind.StartsWith("EffectResult", StringComparison.Ordinal))
                        {
                            var basic = kind.StartsWith("EffectResultBasic", StringComparison.Ordinal);
                            var stride = basic ? 16 : 88;
                            if (4 + body[0] * stride > body.Length)
                                throw new InvalidDataException($"记录 {packet.Index} 的结算条目超出包长。");
                            for (var at = 4; at < 4 + body[0] * stride; at += stride)
                                if (UInt32(body, at + 4) == actorId)
                                    Add(UInt32(body, at + 8), basic ? 0 : UInt32(body, at + 12));
                        }
                        break;
                }

                void Add(uint hp, uint observedMaxHp = 0)
                {
                    if (observedMaxHp != 0)
                        maxHp = observedMaxHp;
                    if (maxHp == 0 || hp > maxHp)
                        throw new InvalidDataException($"记录 {packet.Index} 的血量 {hp}/{maxHp} 无法用于取样。");
                    if (observations.Count != 0 && packet.TimestampMs < observations[observations.Count - 1].Packet.TimestampMs)
                        throw new InvalidDataException($"实体的血量记录在第 {packet.Index} 条发生时间回退。");
                    observations.Add(new Observation { Packet = packet, Kind = kind, Hp = hp, MaxHp = maxHp });
                }
            }

            if (endMs > lastMs)
                throw new InvalidDataException("结束时间超出回放记录。");
            if (observations.Count == 0 || observations[0].Packet.TimestampMs > startMs)
                throw new InvalidDataException($"区间开始时找不到实体 0x{actorId:X8} 的血量。请核对原始实体 ID 及时间。");
            return observations;
        }

        private static void WriteSamples(string path, List<Observation> observations, uint startMs, uint endMs)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                writer.WriteLine("相对秒\t回放毫秒\t当前HP\t最大HP\t源包毫秒\t记录\t偏移\t来源");
                var index = 0;
                var time = startMs;
                while (true)
                {
                    while (index + 1 < observations.Count && observations[index + 1].Packet.TimestampMs <= time)
                        index++;

                    var observation = observations[index];
                    var packet = observation.Packet;
                    writer.WriteLine(FormattableString.Invariant($"{(time - startMs) / 1000.0:F3}\t{time}\t{observation.Hp}\t{observation.MaxHp}\t{packet.TimestampMs}\t{packet.Index}\t0x{packet.Offset:X}\t{observation.Kind}"));
                    if (time == endMs)
                        break;
                    time += Math.Min(SampleIntervalMs, endMs - time);
                }
            }
        }
    }
}
