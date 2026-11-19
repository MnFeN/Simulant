using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace Simulant.Replay
{
    internal static class ReplayConversion
    {
        private const uint AuditedBuild = 0x00D975C9;
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        private sealed class Group
        {
            internal ReplayPacket First;
            internal ReplayPacket Last;
            internal readonly long[] Counts = new long[4];

            internal void Track(ReplayPacket packet, ReplayPacketUse use)
            {
                if (First == null)
                    First = packet;
                Last = packet;
                Counts[(int)use]++;
            }
        }

        /// <summary>核对整份录像。JSON 和底稿使用指定区间，区间两端均包含。</summary>
        internal static void Convert(string inputPath, string outputDirectory, string profile, uint startMs, uint endMs)
        {
            if (profile != ReplayPacketCatalog.China758Profile)
                throw new ArgumentException("回放转换目前支持已核对的 CN7.58。");
            if (endMs < startMs)
                throw new ArgumentException("区间结束时间必须大于或等于开始时间。");

            var input = Path.GetFullPath(inputPath);
            var output = ReplayInspector.ValidateOutputDirectory(input, outputDirectory);
            using (var stream = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha = SHA256.Create())
            {
                var hash = Hex(sha.ComputeHash(stream));
                stream.Position = 0;
                using (var reader = new ReplayReader(stream, true))
                {
                    if (reader.Header.GameVersion != AuditedBuild)
                        throw new InvalidDataException($"该录像的构建号为 0x{reader.Header.GameVersion:X8}。当前转换器已核对的构建为 0x{AuditedBuild:X8}。");

                    var document = new ReplayTimelineDocument
                    {
                        Source = new ReplayOrigin
                        {
                            File = Path.GetFileName(input),
                            Sha256 = hash,
                            Profile = profile,
                            Build = reader.Header.GameVersion,
                            Format = reader.Header.FormatVersion
                        },
                        DutyId = reader.Header.ContentFinderConditionId,
                        StartMs = startMs,
                        EndMs = endMs
                    };
                    Export(reader, document, output);
                }
            }
        }

        private static void Export(ReplayReader reader, ReplayTimelineDocument document, string output)
        {
            var decoder = new ReplayTimelineDecoder(document);
            var groups = new Dictionary<int, Group>();
            var controls = new Dictionary<int, Group>();
            var classifications = new Dictionary<int, ReplayPacketClassification>();
            var initialCaptured = false;
            var maxMs = 0u;
            var previousMs = 0u;
            var regressions = 0L;
            var count = 0L;

            Directory.CreateDirectory(output);
            using (var unresolved = Writer(output, "unresolved.tsv"))
            {
                unresolved.WriteLine("记录\t偏移\t时间毫秒\t操作码\t类型\t长度\t对象ID\t正文HEX");
                foreach (var packet in reader.ReadPackets())
                {
                    if (!initialCaptured && packet.TimestampMs >= document.StartMs)
                    {
                        decoder.CaptureInitial();
                        initialCaptured = true;
                    }

                    count++;
                    maxMs = Math.Max(maxMs, packet.TimestampMs);
                    if (packet.TimestampMs < previousMs)
                        regressions++;
                    previousMs = packet.TimestampMs;

                    var key = packet.Opcode << 16 | packet.Payload.Length;
                    if (!classifications.TryGetValue(key, out var classification))
                    {
                        classification = ReplayPacketCatalog.Classify(document.Source.Build, packet.Opcode,
                            packet.Payload.Length, document.Source.Profile);
                        classifications.Add(key, classification);
                    }

                    var use = decoder.Read(packet, classification.Name);
                    Track(groups, key, packet, use);
                    if (classification.Name.StartsWith("ActorControl", StringComparison.Ordinal) && packet.Payload.Length >= 2)
                        Track(controls, packet.Opcode << 16 | ReplayReader.UInt16(packet.Payload, 0), packet, use);
                    if (use == ReplayPacketUse.Unresolved)
                        WriteUnresolved(unresolved, packet, classification.Name);
                }
            }

            if (!initialCaptured || count == 0)
                throw new InvalidDataException("指定区间开始时间超出回放记录的时间范围。");
            document.EndMs = Math.Min(document.EndMs, maxMs);
            decoder.PruneUnusedEntities();

            using (var stream = File.Create(Path.Combine(output, "timeline.json.partial")))
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Utf8, false, true, "  "))
                Serializer().WriteObject(writer, document);

            ReplayTimelineGenerator generator;
            using (var writer = Writer(output, "timeline.g.cs"))
            {
                generator = new ReplayTimelineGenerator(document, writer);
                generator.Write();
            }

            WriteCoverage(output, groups, controls, classifications);
            WriteSummary(output, reader, document, groups, controls, classifications, count, regressions, generator);
            foreach (var file in new[] { "timeline.json", "timeline.g.cs", "packets.tsv", "actor-control.tsv", "unresolved.tsv", "summary.md" })
                File.Move(Path.Combine(output, file + ".partial"), Path.Combine(output, file));
        }

        /// <summary>用已经编辑的 JSON 重新生成 C#，无需重新读取原录像。</summary>
        internal static void Generate(string inputPath, string outputPath)
        {
            ReplayTimelineDocument document;
            using (var input = File.OpenRead(inputPath))
                document = Serializer().ReadObject(input) as ReplayTimelineDocument;
            if (document?.SchemaVersion != ReplayTimelineDocument.CurrentSchemaVersion)
                throw new InvalidDataException("不支持此时间轴 JSON 的 SchemaVersion。");
            if (document.Entities == null || document.InitialState == null || document.Events == null ||
                document.InitialState.Concat(document.Events).Any(e => e == null || string.IsNullOrEmpty(e.Kind) || e.TimeMs < 0))
                throw new InvalidDataException("时间轴 JSON 缺失实体表或事件字段，或包含负时间。");

            var path = Path.GetFullPath(outputPath);
            if (File.Exists(path) || Directory.Exists(path))
                throw new IOException("生成目标已存在，请指定新的 C# 文件。");

            using (var draft = new StringWriter(Culture))
            {
                new ReplayTimelineGenerator(document, draft).Write();
                using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(output, Utf8))
                    writer.Write(draft.ToString());
            }
        }

        private static DataContractJsonSerializer Serializer()
        {
            return new DataContractJsonSerializer(typeof(ReplayTimelineDocument), new DataContractJsonSerializerSettings
            {
                MaxItemsInObjectGraph = int.MaxValue
            });
        }

        private static void Track(Dictionary<int, Group> groups, int key, ReplayPacket packet, ReplayPacketUse use)
        {
            if (!groups.TryGetValue(key, out var group))
            {
                group = new Group();
                groups.Add(key, group);
            }

            group.Track(packet, use);
        }

        private static void WriteUnresolved(TextWriter writer, ReplayPacket p, string name)
        {
            writer.WriteLine(FormattableString.Invariant($"{p.Index}\t0x{p.Offset:X}\t{p.TimestampMs}\t0x{p.Opcode:X4}\t{name}\t{p.Payload.Length}\t0x{p.ObjectId:X8}\t{Hex(p.Payload)}"));
        }

        private static void WriteCoverage(string output, Dictionary<int, Group> groups, Dictionary<int, Group> controls,
            Dictionary<int, ReplayPacketClassification> classifications)
        {
            const string Counts = "时间轴\t参考\t过滤\t待核对\t首次毫秒\t末次毫秒\t第一条偏移";
            using (var writer = Writer(output, "packets.tsv"))
            {
                writer.WriteLine("操作码\t长度\t类型\t识别\t候选\t" + Counts);
                foreach (var entry in groups.OrderBy(pair => pair.Value.First.Opcode).ThenBy(pair => pair.Value.First.Payload.Length))
                {
                    var p = entry.Value.First;
                    var c = classifications[entry.Key];
                    writer.WriteLine(FormattableString.Invariant($"0x{p.Opcode:X4}\t{p.Payload.Length}\t{c.Name}\t{c.Confidence}\t{string.Join(" / ", c.Candidates)}\t{GroupText(entry.Value)}"));
                }
            }

            using (var writer = Writer(output, "actor-control.tsv"))
            {
                writer.WriteLine("操作码\t子类型\t含义\t" + Counts);
                foreach (var entry in controls.OrderBy(pair => pair.Key))
                {
                    var category = (ushort)entry.Key;
                    writer.WriteLine(FormattableString.Invariant($"0x{entry.Value.First.Opcode:X4}\t0x{category:X4}\t{ReplayTimelineDecoder.ControlName(category)}\t{GroupText(entry.Value)}"));
                }
            }
        }

        private static string GroupText(Group group)
        {
            return FormattableString.Invariant($"{string.Join("\t", group.Counts)}\t{group.First.TimestampMs}\t{group.Last.TimestampMs}\t0x{group.First.Offset:X}");
        }

        private static void WriteSummary(string output, ReplayReader reader, ReplayTimelineDocument document,
            Dictionary<int, Group> groups, Dictionary<int, Group> controls, Dictionary<int, ReplayPacketClassification> classifications,
            long count, long regressions, ReplayTimelineGenerator generator)
        {
            using (var writer = Writer(output, "summary.md"))
            {
                writer.WriteLine("# 回放转换报告");
                writer.WriteLine();
                writer.WriteLine($"- 来源 {document.Source.File}，SHA-256 {document.Source.Sha256}。");
                writer.WriteLine($"- 容器格式 {document.Source.Format}，客户端构建 0x{document.Source.Build:X8}，包版本 {document.Source.Profile}。");
                writer.WriteLine($"- 区域 {document.TerritoryId}，副本匹配编号 {document.DutyId}，分析窗口 {document.StartMs}–{document.EndMs} 毫秒。");
                writer.WriteLine($"- 全文件 {count} 条记录，{groups.Count} 个 opcode 与长度组合，{controls.Count} 个 ActorControl 包族与子类型组合，{regressions} 处原始时间回退。");
                writer.WriteLine($"- 全文件用途计数为时间轴 {Total(groups, ReplayPacketUse.Timeline)}，参考 {Total(groups, ReplayPacketUse.Reference)}，过滤 {Total(groups, ReplayPacketUse.Filtered)}，待核对 {Total(groups, ReplayPacketUse.Unresolved)}。");
                writer.WriteLine($"- JSON 含 {document.Entities.Count} 个实体生命周期，{document.InitialState.Count} 条初始状态，{document.Events.Count} 条区间事件。");
                writer.WriteLine($"- C# 写入 {generator.NodeCount} 个调度节点，{generator.Pending.Values.Sum()} 条待核对注释。");
                writer.WriteLine();
                writer.WriteLine("时间轴数据按记录顺序保存。同毫秒的事件保留原顺序。相对时间从区间起点计，坐标使用 Z 高度。");
                writer.WriteLine("unresolved.tsv 保存未归一化记录的完整负载和偏移，packets.tsv 与 actor-control.tsv 覆盖全文件。");
                writer.WriteLine("timeline.g.cs 用于复核后接入现有模拟，点名规则和机制判定由脚本作者定义。");

                WriteEntrySummary(writer, document, groups, classifications);

                if (generator.Pending.Count > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine("| 底稿待核对 | 事件数 |");
                    writer.WriteLine("| --- | ---: |");
                    foreach (var kind in generator.Pending.OrderByDescending(item => item.Value).ThenBy(item => item.Key))
                        writer.WriteLine($"| {kind.Key} | {kind.Value} |");
                }

                if (reader.Chapters.Count > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine("| 章节 | 类型 | 毫秒 | 区内偏移 |");
                    writer.WriteLine("| ---: | ---: | ---: |");
                    foreach (var chapter in reader.Chapters)
                        writer.WriteLine($"| {chapter.Index} | {chapter.Type} | {chapter.TimestampMs} | 0x{chapter.Offset:X} |");
                }
            }
        }

        private static void WriteEntrySummary(TextWriter writer, ReplayTimelineDocument document,
            Dictionary<int, Group> groups, Dictionary<int, ReplayPacketClassification> classifications)
        {
            var packets = groups
                .Select(pair => (Group: pair.Value, Name: classifications[pair.Key].Name))
                .Select(entry => (entry.Group, entry.Name, Handling: DescribeEntries(entry.Name)))
                .Where(entry => entry.Handling != null)
                .OrderBy(entry => entry.Group.First.Opcode)
                .ThenBy(entry => entry.Group.First.Payload.Length)
                .ToArray();
            if (packets.Length == 0)
                return;

            writer.WriteLine();
            writer.WriteLine("| 多条目包与场地效果 | Opcode | 全回放包数 | 条目处理 |");
            writer.WriteLine("| --- | --- | ---: | --- |");
            foreach (var entry in packets)
                writer.WriteLine($"| {entry.Name} | 0x{entry.Group.First.Opcode:X4} | {entry.Group.Counts.Sum()} | {entry.Handling} |");

            if (packets.Any(entry => entry.Name.StartsWith("MapEffect", StringComparison.Ordinal)))
            {
                var initialMapEffects = document.InitialState.Count(e => e.MapSlot.HasValue);
                var mapEffects = document.Events.Count(e => e.Kind == "MapEffect");
                var mapStates = document.Events.Count(e => e.Kind == "MapEffectState");
                writer.WriteLine();
                writer.WriteLine($"所选区间的 JSON 含 {initialMapEffects} 项起始槽位状态、{mapEffects} 条 MapEffect 和 {mapStates} 条 MapEffectState。");
            }
        }

        private static string DescribeEntries(string name)
        {
            if (name.StartsWith("MapEffect", StringComparison.Ordinal))
                return name == "MapEffectStateSnapshot" ? "按变化槽位生成 MapEffectState" : "每个槽位生成一条 MapEffect";
            if (name.StartsWith("ActionEffect", StringComparison.Ordinal))
                return "每次结算保留一个事件，保留逐目标效果数组，C# 传入命中名单";
            if (name.StartsWith("EffectResult", StringComparison.Ordinal))
                return "逐条读取确认和附带的状态效果，关联相应结算";
            if (name == "StatusEffectList2" || name == "StatusEffectList3")
                return "待核对负载布局";
            if (name.IndexOf("StatusEffectList", StringComparison.Ordinal) >= 0)
                return "逐槽比较状态，导出新增、刷新和移除";

            switch (name)
            {
                case "ActorTimelineBatch":
                    return "每个实体和时间轴配对生成一条 PlayTimeline";
                case "PlaceFieldMarkerPreset":
                    return "展开八个标点槽位，保留启用及清除状态";
                case "UpdateParty":
                    return "遍历成员，维护小队槽位并保留可解析的名单";
                case "PlayerSpawn":
                case "NpcSpawn":
                case "NpcSpawn2":
                    return "生成单个实体，逐槽读取附带的初始状态";
                case "UpdateHate":
                case "UpdateHater":
                    return "仇恨列表包，作为调查参考";
                case "UpdatePartyMemberPositions":
                    return "小队位置同步，按过滤规则跳过";
                case "ContentDirectorSlotAssignments":
                    return "副本控制器槽分配，作为调查参考";
                default:
                    return null;
            }
        }

        private static long Total(Dictionary<int, Group> groups, ReplayPacketUse use)
            => groups.Values.Sum(group => group.Counts[(int)use]);

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty);

        private static StreamWriter Writer(string output, string filename)
            => new StreamWriter(Path.Combine(output, filename + ".partial"), false, Utf8) { NewLine = "\r\n" };
    }
}
