using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Simulant.Replay
{
    /// <summary>离线导出回放包清单；不连接客户端，也不执行记录中的任何动作。</summary>
    public static class ReplayInspector
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        private sealed class PacketGroup
        {
            internal ushort Opcode;
            internal int Length;
            internal long Count;
            internal uint FirstMs, LastMs;
            internal ReplayPacketClassification Classification;
        }

        public static ReplayInspectionResult Export(string inputPath, string outputDirectory, string profile = null)
        {
            if (string.IsNullOrWhiteSpace(inputPath)) throw new ArgumentException("请指定回放文件。", nameof(inputPath));
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("请指定输出目录。", nameof(outputDirectory));
            var input = Path.GetFullPath(inputPath);
            var output = Path.GetFullPath(outputDirectory);
            var comparedOutput = output.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var sourceDirectory = Path.GetDirectoryName(input).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(comparedOutput, sourceDirectory, StringComparison.OrdinalIgnoreCase) ||
                comparedOutput.StartsWith(sourceDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("输出目录不能位于原始回放目录内。", nameof(outputDirectory));
            if (Directory.Exists(output) || File.Exists(output))
                throw new IOException("输出目录已存在，请指定新目录，避免覆盖已有结果。");
            for (var ancestor = new DirectoryInfo(output); ancestor != null; ancestor = ancestor.Parent)
                if (ancestor.Exists && (ancestor.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("输出路径经过目录链接，无法保证避开原始回放目录，请使用实际目录。");
            // 提前检查参数，避免无效版本选项留下输出文件。
            ReplayPacketCatalog.Classify(0, 0, 0, profile);

            // 同一个只读句柄负责摘要和解析，导出期间不允许文件被写入或替换。
            using (var stream = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                string hash;
                using (var sha = SHA256.Create()) hash = Hex(sha.ComputeHash(stream));
                stream.Position = 0;
                using (var reader = new ReplayReader(stream, true))
                {
                    Directory.CreateDirectory(output);
                    var result = new ReplayInspectionResult { OutputDirectory = output, Sha256 = hash };
                    var groups = new Dictionary<string, PacketGroup>();
                    var classifications = new Dictionary<ReplayPacketConfidence, long>();
                    var objects = new HashSet<uint>();
                    var chapterBoundaries = new HashSet<uint>();
                    var chapterOffsets = new HashSet<uint>(reader.Chapters.Select(c => c.Offset));
                    uint previousMs = 0;
                    long backwards = 0;
                    var files = new[] { "packets.csv", "opcodes.csv", "chapters.csv", "summary.md" };
                    try
                    {
                        using (var writer = NewWriter(Path.Combine(output, "packets.csv.partial")))
                        {
                            Csv(writer, "序号", "文件偏移", "时间毫秒", "时间", "对象ID", "opcode", "负载字节数", "类型", "识别等级", "候选类型", "依据", "负载前32字节");
                            foreach (var packet in reader.ReadPackets())
                            {
                                var key = packet.Opcode.ToString("X4", Culture) + "/" + packet.Payload.Length.ToString(Culture);
                                if (!groups.TryGetValue(key, out var group))
                                {
                                    group = new PacketGroup
                                    {
                                        Opcode = packet.Opcode, Length = packet.Payload.Length, FirstMs = packet.TimestampMs,
                                        Classification = ReplayPacketCatalog.Classify(reader.Header.GameVersion, packet.Opcode, packet.Payload.Length, profile)
                                    };
                                    groups.Add(key, group);
                                }
                                group.Count++;
                                group.LastMs = packet.TimestampMs;
                                var classification = group.Classification;
                                classifications.TryGetValue(classification.Confidence, out var classifiedCount);
                                classifications[classification.Confidence] = classifiedCount + 1;
                                if (result.PacketCount != 0 && packet.TimestampMs < previousMs) backwards++;
                                previousMs = packet.TimestampMs;
                                result.LastTimestampMs = packet.TimestampMs;
                                result.MaxTimestampMs = Math.Max(result.MaxTimestampMs, packet.TimestampMs);
                                result.PacketCount++;
                                objects.Add(packet.ObjectId);
                                var relativeOffset = packet.Offset - reader.DataOffset;
                                if (relativeOffset <= uint.MaxValue && chapterOffsets.Contains((uint)relativeOffset))
                                    chapterBoundaries.Add((uint)relativeOffset);
                                Csv(writer, packet.Index, "0x" + packet.Offset.ToString("X", Culture), packet.TimestampMs,
                                    FormatTime(packet.TimestampMs), "0x" + packet.ObjectId.ToString("X8", Culture),
                                    "0x" + packet.Opcode.ToString("X4", Culture), packet.Payload.Length,
                                    classification.Name ?? "", ConfidenceText(classification.Confidence),
                                    string.Join(" / ", classification.Candidates),
                                    string.IsNullOrEmpty(classification.Profile) ? "未指定版本；详见 opcode 汇总" : classification.Profile + "；详见 opcode 汇总",
                                    Hex(packet.Payload, Math.Min(32, packet.Payload.Length)));
                            }
                        }
                        result.OpcodeCount = groups.Values.Select(g => g.Opcode).Distinct().Count();
                        using (var writer = NewWriter(Path.Combine(output, "opcodes.csv.partial")))
                        {
                            Csv(writer, "opcode", "负载字节数", "条数", "首次时间", "末次时间", "类型", "识别等级", "候选类型", "依据", "来源");
                            foreach (var group in groups.Values.OrderByDescending(g => g.Count).ThenBy(g => g.Opcode).ThenBy(g => g.Length))
                                Csv(writer, "0x" + group.Opcode.ToString("X4", Culture), group.Length, group.Count,
                                    FormatTime(group.FirstMs), FormatTime(group.LastMs), group.Classification.Name ?? "",
                                    ConfidenceText(group.Classification.Confidence), string.Join(" / ", group.Classification.Candidates),
                                    group.Classification.Evidence, group.Classification.SourceUrl);
                        }
                        using (var writer = NewWriter(Path.Combine(output, "chapters.csv.partial")))
                        {
                            Csv(writer, "章节", "类型原值", "包流偏移", "时间毫秒", "时间", "指向记录边界");
                            foreach (var chapter in reader.Chapters)
                                Csv(writer, chapter.Index, chapter.Type, "0x" + chapter.Offset.ToString("X", Culture),
                                    chapter.TimestampMs, FormatTime(chapter.TimestampMs), chapterBoundaries.Contains(chapter.Offset) ? "是" : "否");
                        }
                        WriteSummary(Path.Combine(output, "summary.md.partial"), input, reader, result, profile,
                            groups.Values.ToArray(), classifications, objects.Count, backwards, chapterBoundaries);
                        // 只有整个包流验证结束，才发布没有 partial 后缀的清单。
                        foreach (var file in files) File.Move(Path.Combine(output, file + ".partial"), Path.Combine(output, file));
                        return result;
                    }
                    catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is ArgumentException || ex is UnauthorizedAccessException)
                    {
                        throw new IOException("回放清单生成失败；目录中的 .partial 文件不是完整结果：" + output + "。" + ex.Message, ex);
                    }
                }
            }
        }

        private static void WriteSummary(string path, string input, ReplayReader reader, ReplayInspectionResult result,
            string profile, PacketGroup[] groups, Dictionary<ReplayPacketConfidence, long> classifications,
            int objectCount, long backwards, HashSet<uint> chapterBoundaries)
        {
            using (var writer = NewWriter(path))
            {
                var header = reader.Header;
                writer.WriteLine("# 回放网络包清单\n");
                writer.WriteLine("- 输入：`" + Path.GetFileName(input) + "`");
                writer.WriteLine("- SHA-256：`" + result.Sha256 + "`");
                writer.WriteLine("- 文件大小：" + reader.Length.ToString(Culture) + " 字节；数据起点：`0x" + reader.DataOffset.ToString("X", Culture) + "`");
                writer.WriteLine("- 回放格式：" + header.FormatVersion + "；内部构建号：`0x" + header.GameVersion.ToString("X8", Culture) + "`（" + header.GameVersion + "）");
                writer.WriteLine("- 录制时间：" + header.TimestampUtc.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd HH:mm:ss zzz", Culture));
                writer.WriteLine("- 副本匹配条件 ID：" + header.ContentFinderConditionId);
                writer.WriteLine("- 记录：" + result.PacketCount + " 条；opcode：" + result.OpcodeCount + " 种；opcode／长度组合：" + groups.Length + " 种；对象 ID：" + objectCount + " 个");
                writer.WriteLine("- 包流最大时间：" + FormatTime(result.MaxTimestampMs) + "；末条时间：" + FormatTime(result.LastTimestampMs));
                writer.WriteLine("- 文件头总时长／显示时长：" + header.DeclaredDurationMs + " / " + header.DisplayedDurationMs + " 毫秒；声明数据长度：" + header.DeclaredDataLength);
                writer.WriteLine("- 章节：" + reader.Chapters.Count + "；时间回退：" + backwards + " 次（保持文件顺序）");
                writer.WriteLine("- opcode 版本选项：" + (string.IsNullOrEmpty(profile) ? "未指定，仅列出候选" : profile + "（指定版本，尚未验证回放内部构建号与版本表的对应关系）"));
                writer.WriteLine();
                if (header.DeclaredDataLength == 0 || header.DeclaredDurationMs == 0)
                    writer.WriteLine("文件头的长度或时长为零，可能未完成录制收尾；本次依据实际文件边界读取，不能用头部零值判断没有战斗。包流已完整扫描至文件末尾。\n");
                if (header.DeclaredDataLength != 0 && header.DeclaredDataLength != reader.Length - reader.DataOffset)
                    writer.WriteLine("文件头声明的数据长度与实际包流长度不同；本次保留全部记录，未依据声明长度截断。\n");
                if (reader.Chapters.Any(c => !chapterBoundaries.Contains(c.Offset)))
                    writer.WriteLine("部分章节未指向有效记录起点，详见 chapters.csv；不能直接使用这些章节跳转。\n");
                writer.WriteLine("## 识别结果\n");
                foreach (var item in classifications.OrderBy(p => p.Key))
                    writer.WriteLine("- " + ConfidenceText(item.Key) + "：" + item.Value + " 条");
                writer.WriteLine("\n版本表匹配仍以所选版本为前提。仅有相同长度不能确认类型；未知包和长度冲突保留原始 opcode、偏移和负载预览，不会生成可执行模拟动作。\n");
                writer.WriteLine("## 包类型分布\n");
                writer.WriteLine("| opcode | 负载字节 | 条数 | 类型或候选 | 依据 | 首次 | 末次 |\n| --- | ---: | ---: | --- | --- | --- | --- |");
                foreach (var group in groups.OrderByDescending(g => g.Count).ThenBy(g => g.Opcode).ThenBy(g => g.Length))
                    writer.WriteLine("| 0x" + group.Opcode.ToString("X4", Culture) + " | " + group.Length + " | " + group.Count + " | " +
                        (string.IsNullOrEmpty(group.Classification.Name) ? string.Join(" / ", group.Classification.Candidates) : group.Classification.Name).Replace("|", "\\|") + " | " +
                        ConfidenceText(group.Classification.Confidence) + " | " + FormatTime(group.FirstMs) + " | " + FormatTime(group.LastMs) + " |");
                writer.WriteLine("\n## 后续时间轴提取\n");
                writer.WriteLine("这一阶段产出包清单，不直接执行回放。后续需要按确认的包布局提取实体生命周期、位置与朝向、读条与技能结算、ActorControl 子事件、Director 与场景效果，再将原始实体 ID 映射为本次模拟实体。\n");
                writer.WriteLine("DirectorUpdate 可出现在 ActorControlSelf 的子类别中，也存在独立 Director 包；应检查子类别与参数，不能只按 opcode 名称过滤。ActionEffect 同时含动画、目标和结算信息，后续需要区分演出重现与伤害模拟。初始状态、状态效果和场地对象也可能是还原机制所必需的数据。\n");
                writer.WriteLine("- [逐条清单](packets.csv)：按文件顺序，含时间、对象、opcode、长度、识别依据与负载前 32 字节。\n- [opcode 汇总](opcodes.csv)：按 opcode 和长度分别计数，含来源链接。\n- [章节清单](chapters.csv)：保留原始章节类型并检查偏移。\n");
                writer.WriteLine("读取结构参考：[Hypostasis FFXIVReplay](https://github.com/UnknownX7/Hypostasis/blob/c230760c7bbc85a6e37b8477012dcd92ab700dc7/Game/Structures/FFXIVReplay.cs)。opcode 来源与证据保存在汇总清单中。原始文件仅以只读方式打开，未上传或修改。");
            }
        }

        private static string ConfidenceText(ReplayPacketConfidence confidence)
        {
            switch (confidence)
            {
                case ReplayPacketConfidence.LengthCandidate: return "仅长度候选";
                case ReplayPacketConfidence.OpcodeCandidate: return "版本未指定的 opcode 候选";
                case ReplayPacketConfidence.SelectedProfileCandidate: return "指定版本候选，长度待核对";
                case ReplayPacketConfidence.SelectedProfileMatch: return "指定版本 opcode 与长度匹配";
                case ReplayPacketConfidence.LengthConflict: return "版本表与长度冲突";
                case ReplayPacketConfidence.RecordMarker: return "回放扩展记录";
                default: return "未知";
            }
        }

        private static StreamWriter NewWriter(string path) => new StreamWriter(path, false, new UTF8Encoding(true));

        private static void Csv(TextWriter writer, params object[] values)
        {
            // 所有字段均加引号，避免多候选、说明和路径中的分隔符破坏列结构。
            writer.WriteLine(string.Join(",", values.Select(value => "\"" + Convert.ToString(value, Culture).Replace("\"", "\"\"") + "\"")));
        }

        private static string Hex(byte[] bytes, int count = -1)
            => bytes.Length == 0 || count == 0 ? "" : BitConverter.ToString(bytes, 0, count < 0 ? bytes.Length : count).Replace("-", "");

        private static string FormatTime(uint ms)
            => string.Format(Culture, "{0:D2}:{1:D2}:{2:D2}.{3:D3}", ms / 3600000, ms / 60000 % 60, ms / 1000 % 60, ms % 1000);
    }

    public sealed class ReplayInspectionResult
    {
        public string OutputDirectory { get; internal set; }
        public string Sha256 { get; internal set; }
        public long PacketCount { get; internal set; }
        public int OpcodeCount { get; internal set; }
        public uint MaxTimestampMs { get; internal set; }
        public uint LastTimestampMs { get; internal set; }
    }
}
