using Simulant.Replay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Simulant.Tests
{
    internal static class ReplayTests
    {
        private const int DataOffset = 0x36C;

        public static void ReplayReadsHeaderChaptersAndPackets()
        {
            var bytes = CreateReplay(new Packet(0x123, 1500, 0x10203040, new byte[] { 1, 2, 3 }),
                new Packet(0x456, 2000, 0x50607080, new byte[] { 4, 5 }));
            SetUInt16(bytes, 0x0E, 2);
            SetUInt32(bytes, 0x10, 987654321);
            SetUInt32(bytes, 0x14, 1700000000);
            SetUInt32(bytes, 0x18, 5000);
            SetUInt32(bytes, 0x1C, 4000);
            SetUInt16(bytes, 0x20, 1234);
            bytes[0x28] = 0xA5;
            SetUInt32(bytes, 0x30, 0x55667788);
            SetUInt32(bytes, 0x34, 0x11223344);
            for (var i = 0; i < 8; i++) bytes[0x38 + i] = (byte)(19 + i);
            bytes[0x40] = 3;
            SetUInt32(bytes, 0x48, (uint)(bytes.Length - DataOffset));
            for (var i = 0; i < 7; i++) SetUInt16(bytes, 0x4E + i * 2, (ushort)(1000 + i));
            SetUInt32(bytes, 0x68, 2);
            SetChapter(bytes, 0, 1, 0, 1500);
            SetChapter(bytes, 1, -7, 15, 2000);

            using (var reader = Open(bytes))
            {
                var header = reader.Header;
                Assert(reader.Length == bytes.Length && reader.DataOffset == DataOffset, "应保留文件长度和记录区起点。");
                Assert(header.FormatVersion == 5 && header.OperatingSystemType == 2 && header.GameVersion == 987654321,
                    "文件格式、平台和内部构建号应分别读取。");
                Assert(header.TimestampUnixSeconds == 1700000000 &&
                    header.TimestampUtc == DateTimeOffset.FromUnixTimeSeconds(1700000000), "时间戳应按 UTC 秒数读取。");
                Assert(header.DeclaredDurationMs == 5000 && header.DisplayedDurationMs == 4000 &&
                    header.ContentFinderConditionId == 1234 && header.Flags == 0xA5 && header.PlayerIndex == 3,
                    "时长、副本标识和队员位置应保留原始头信息。");
                Assert(header.LocalContentId == 0x1122334455667788UL &&
                    header.JobIds.SequenceEqual(Enumerable.Range(19, 8).Select(value => (byte)value)) &&
                    header.BnpcNameIds.SequenceEqual(Enumerable.Range(1000, 7).Select(value => (ushort)value)),
                    "64 位身份与职业、名称编号数组应完整读取。");
                Assert(header.DeclaredDataLength == bytes.Length - DataOffset && reader.Chapters.Count == 2,
                    "声明长度与章节数应匹配合成回放。");
                Assert(reader.Chapters[0].Index == 1 && reader.Chapters[0].Type == 1 && reader.Chapters[0].Offset == 0 &&
                    reader.Chapters[1].Index == 2 && reader.Chapters[1].Type == -7 && reader.Chapters[1].Offset == 15 &&
                    reader.Chapters[1].TimestampMs == 2000, "章节编号、未知类型、相对偏移和时间应保持原样。");
                var packets = reader.ReadPackets().ToArray();
                Assert(packets.Length == 2 && packets[0].Index == 1 && packets[1].Index == 2 &&
                    packets[0].Offset == DataOffset && packets[1].Offset == DataOffset + 15,
                    "记录序号应从 1 开始，包体后不能增加对齐字节。");
                Assert(packets[0].Opcode == 0x123 && packets[0].TimestampMs == 1500 && packets[0].ObjectId == 0x10203040 &&
                    packets[0].Payload.SequenceEqual(new byte[] { 1, 2, 3 }) && packets[1].Payload.SequenceEqual(new byte[] { 4, 5 }),
                    "每条记录的操作码、时间、来源与包体必须正确分离。");
            }
        }

        public static void ReplayPreservesUnknownPacketsAndOriginalOrdering()
        {
            var bytes = CreateReplay(new Packet(0xFFFF, 90, uint.MaxValue, new byte[] { 8 }),
                new Packet(0xF123, 90, 0, new byte[] { 9 }), new Packet(0, 10, 1, new byte[] { 10 }));
            using (var reader = Open(bytes))
            {
                var packets = reader.ReadPackets().ToArray();
                Assert(packets.Select(packet => packet.Opcode).SequenceEqual(new ushort[] { 0xFFFF, 0xF123, 0 }) &&
                    packets.Select(packet => packet.TimestampMs).SequenceEqual(new uint[] { 90, 90, 10 }) &&
                    packets[0].ObjectId == uint.MaxValue, "未知包、同刻顺序和时间回退不能被过滤或重排。");
                packets[0].Payload[0] = 99;
                Assert(packets[1].Payload[0] == 9 && reader.ReadPackets().First().Payload[0] == 8,
                    "记录之间不能共用可变包体，也不能将调用方修改写回输入流。");
            }
        }

        public static void ReplayReadsZeroAndMaximumPayloads()
        {
            var large = Enumerable.Range(0, ushort.MaxValue).Select(index => (byte)(index * 17)).ToArray();
            using (var reader = Open(CreateReplay(new Packet(1, 0, 0, new byte[0]), new Packet(2, uint.MaxValue, 0, large))))
            {
                var packets = reader.ReadPackets().ToArray();
                Assert(packets.Length == 2 && packets[0].Payload.Length == 0 &&
                    packets[1].Offset == DataOffset + 12 && packets[1].Payload.SequenceEqual(large) &&
                    packets[1].TimestampMs == uint.MaxValue, "零长包、最大 ushort 包长与无符号时间戳均应被完整读取。");
            }
        }

        public static void ReplayAcceptsUnfinalizedAndMismatchedDeclaredLengths()
        {
            foreach (var declaredLength in new uint[] { 0, 1, uint.MaxValue })
            {
                var bytes = CreateReplay(new Packet(7, 10, 11, new byte[] { 3, 4 }));
                SetUInt32(bytes, 0x48, declaredLength);
                using (var reader = Open(bytes))
                    Assert(reader.Header.DeclaredDataLength == declaredLength && reader.Chapters.Count == 0 &&
                        reader.ReadPackets().Count() == 1, "尚未收尾的头信息应保留，实际文件边界不能被声明长度截断。");
            }
            using (var reader = Open(CreateReplay()))
                Assert(!reader.ReadPackets().Any(), "没有任何包的合法回放应返回空列表。");

            using (var stream = new MemoryStream())
            {
                var initial = CreateReplay(new Packet(1, 0, 0, new byte[0]));
                stream.Write(initial, 0, initial.Length);
                using (var reader = new ReplayReader(stream, true))
                {
                    stream.Position = stream.Length;
                    stream.Write(new byte[12], 0, 12);
                    Assert(reader.Length == initial.Length && reader.ReadPackets().Count() == 1,
                        "读取开始之后追加的记录不能混入当前文件快照。");
                }
            }
        }

        public static void ReplayRejectsInvalidMagicVersionAndChapterCount()
        {
            var magic = CreateReplay();
            magic[4] ^= 0xFF;
            RejectAt(() => Open(magic), 4);
            foreach (var version in new ushort[] { 0, 4, 6, ushort.MaxValue })
            {
                var bytes = CreateReplay();
                SetUInt16(bytes, 0x0C, version);
                RejectAt(() => Open(bytes), 0x0C);
            }
            foreach (var count in new uint[] { 65, int.MaxValue, uint.MaxValue })
            {
                var bytes = CreateReplay();
                SetUInt32(bytes, 0x68, count);
                RejectAt(() => Open(bytes), 0x68);
            }
        }

        public static void ReplayReportsTruncatedHeaderAndChapterTableOffsets()
        {
            var bytes = CreateReplay();
            foreach (var length in new[] { 0, 10, 0x67, 0x68, 0x6B, DataOffset - 1 })
                RejectAt(() => Open(bytes.Take(length).ToArray()), 0);
        }

        public static void ReplayReportsTruncatedPacketHeaderAndPayloadOffsets()
        {
            var complete = CreateReplay(new Packet(1, 0, 1, new byte[] { 1, 2, 3 }));
            for (var remaining = 1; remaining < 12; remaining++)
            {
                using (var reader = Open(complete.Take(DataOffset + remaining).ToArray()))
                    RejectAt(() => reader.ReadPackets().ToArray(), DataOffset);
            }
            for (var payloadLength = 0; payloadLength < 3; payloadLength++)
            {
                using (var reader = Open(complete.Take(DataOffset + 12 + payloadLength).ToArray()))
                    RejectAt(() => reader.ReadPackets().ToArray(), DataOffset + 12);
            }
            var two = CreateReplay(new Packet(1, 0, 1, new byte[] { 1, 2, 3 }),
                new Packet(2, 0, 1, new byte[] { 4, 5 }));
            using (var reader = Open(two.Take(two.Length - 1).ToArray()))
            using (var enumerator = reader.ReadPackets().GetEnumerator())
            {
                Assert(enumerator.MoveNext() && enumerator.Current.Index == 1, "完整的前序记录必须能够流式交付。");
                RejectAt(() => enumerator.MoveNext(), DataOffset + 15 + 12);
            }
        }

        public static void ReplayPreservesUnfinalizedChapterOffsets()
        {
            var bytes = CreateReplay(new Packet(1, 0, 0, new byte[0]));
            SetUInt32(bytes, 0x68, 64);
            for (var i = 0; i < 64; i++) SetChapter(bytes, i, i, 12, (uint)i);
            using (var reader = Open(bytes))
                Assert(reader.Chapters.Count == 64 && reader.Chapters[63].Offset == 12,
                    "章节容量边界和指向记录区末尾的章节应被接受。");
            SetChapter(bytes, 63, 1, 13, 0);
            using (var reader = Open(bytes))
                Assert(reader.Chapters[63].Offset == 13 && reader.ReadPackets().Count() == 1,
                    "未收尾章节偏移应保留供上层报告，不能据此跳过或截断完整记录。");
            SetChapter(bytes, 63, 1, uint.MaxValue, 0);
            using (var reader = Open(bytes))
                Assert(reader.Chapters[63].Offset == uint.MaxValue && reader.ReadPackets().Count() == 1,
                    "不可信的章节偏移不能被直接用作读取位置，也不能产生整数溢出。");
        }

        public static void ReplayHandlesShortReads()
        {
            var bytes = CreateReplay(new Packet(1, 12, 34, new byte[] { 5, 6, 7, 8, 9 }));
            using (var stream = new LimitedReadStream(bytes, 3))
            using (var reader = new ReplayReader(stream))
                Assert(reader.ReadPackets().Single().Payload.SequenceEqual(new byte[] { 5, 6, 7, 8, 9 }),
                    "一次 Read 未填满缓冲区时应继续读取，而不能把短读误当成文件结束。");
            using (var stream = new LimitedReadStream(bytes, 3, DataOffset + 13))
            using (var reader = new ReplayReader(stream))
                RejectAt(() => reader.ReadPackets().ToArray(), DataOffset + 13);
        }

        public static void ReplayCanEnumerateAgainAfterCompletionOrAbandonment()
        {
            using (var reader = Open(CreateReplay(new Packet(1, 0, 0, new byte[0]), new Packet(2, 0, 0, new byte[0]))))
            {
                Assert(reader.ReadPackets().Count() == 2 && reader.ReadPackets().Count() == 2,
                    "正常结束后应允许重新扫描同一文件。");
                using (var enumerator = reader.ReadPackets().GetEnumerator())
                    Assert(enumerator.MoveNext() && enumerator.Current.Opcode == 1, "提前结束枚举前应读取第一条记录。");
                Assert(reader.ReadPackets().Count() == 2, "提前释放枚举器后应释放独占标记，并从头重新读取。");
            }
        }

        public static void ReplayRejectsConcurrentEnumerationAndDisposedAccess()
        {
            var reader = Open(CreateReplay(new Packet(1, 0, 0, new byte[0]), new Packet(2, 0, 0, new byte[0])));
            using (reader)
            using (var first = reader.ReadPackets().GetEnumerator())
            {
                Assert(first.MoveNext(), "首次枚举应能启动。");
                using (var second = reader.ReadPackets().GetEnumerator())
                    Throws<InvalidOperationException>(() => second.MoveNext());
                Assert(first.MoveNext() && first.Current.Index == 2, "拒绝并行枚举不能破坏原枚举的文件位置。");
            }
            Throws<ObjectDisposedException>(() => reader.ReadPackets());
            var delayedReader = Open(CreateReplay(new Packet(1, 0, 0, new byte[0])));
            var delayed = delayedReader.ReadPackets();
            delayedReader.Dispose();
            using (var enumerator = delayed.GetEnumerator())
                Throws<ObjectDisposedException>(() => enumerator.MoveNext());
        }

        public static void ReplayHonorsStreamOwnershipAndCapabilities()
        {
            foreach (var leaveOpen in new[] { false, true })
            {
                var stream = new MemoryStream(CreateReplay());
                var reader = new ReplayReader(stream, leaveOpen);
                reader.Dispose();
                reader.Dispose();
                Assert(stream.CanRead == leaveOpen, "释放读取器必须遵循输入流的所有权约定。");
                stream.Dispose();
                var invalidStream = new MemoryStream(new byte[10]);
                Throws<InvalidDataException>(() => new ReplayReader(invalidStream, leaveOpen));
                Assert(invalidStream.CanRead == leaveOpen, "构造失败也必须遵循输入流的所有权约定。");
                invalidStream.Dispose();
            }
            Throws<ArgumentNullException>(() => new ReplayReader(null));
            using (var stream = new LimitedReadStream(CreateReplay(), 3, canSeek: false))
                Throws<ArgumentException>(() => new ReplayReader(stream));
            var closedStream = new MemoryStream(CreateReplay());
            closedStream.Dispose();
            Throws<ArgumentException>(() => new ReplayReader(closedStream));
        }

        public static void ReplayOpcodeRecognitionRequiresExplicitProfile()
        {
            foreach (var version in new uint[] { 0, 757, uint.MaxValue })
            {
                var unspecified = ReplayPacketCatalog.Classify(version, 0x217, 16);
                Assert(string.IsNullOrEmpty(unspecified.Name) && unspecified.Confidence == ReplayPacketConfidence.OpcodeCandidate &&
                    unspecified.Candidates.Contains("ActorMove"), "内部构建号不能被误当成已核实的国服版本。");
                var selected = ReplayPacketCatalog.Classify(version, 0x217, 16, ReplayPacketCatalog.China757Profile);
                Assert(selected.Name == "ActorMove" && selected.Confidence == ReplayPacketConfidence.SelectedProfileMatch &&
                    selected.Profile == ReplayPacketCatalog.China757Profile && !string.IsNullOrWhiteSpace(selected.SourceUrl),
                    "显式选择版本且操作码与长度一致时，应记录来源和识别依据。");
            }
        }

        public static void ReplayOpcodeLengthConflictAndAmbiguityRemainUnconfirmed()
        {
            var conflict = ReplayPacketCatalog.Classify(0, 0x217, 24, ReplayPacketCatalog.China757Profile);
            Assert(string.IsNullOrEmpty(conflict.Name) && conflict.Confidence == ReplayPacketConfidence.LengthConflict,
                "已选版本的操作码与已知长度冲突时，不能给出确定包名。");
            var ambiguous = ReplayPacketCatalog.Classify(0, 0xFFFF, 24);
            Assert(string.IsNullOrEmpty(ambiguous.Name) && ambiguous.Confidence == ReplayPacketConfidence.LengthCandidate &&
                ambiguous.Candidates.Contains("ActorControl") && ambiguous.Candidates.Contains("ActorSetPos") &&
                ambiguous.Candidates.Count > 1, "同长度可能对应多个包，必须保留歧义。");
            var unknown = ReplayPacketCatalog.Classify(0, 0xFFFF, 65535);
            Assert(string.IsNullOrEmpty(unknown.Name) && unknown.Confidence == ReplayPacketConfidence.Unknown,
                "未知操作码和未知长度不得被猜成已知包。");
        }

        public static void ReplayRecordMarkersAndInvalidClassificationInputs()
        {
            var marker = ReplayPacketCatalog.Classify(uint.MaxValue, 0xF003, 4);
            Assert(marker.Name == "IDScramble" && marker.Confidence == ReplayPacketConfidence.RecordMarker,
                "回放专用记录应与依赖版本的网络操作码区分。");
            var mismatchedMarker = ReplayPacketCatalog.Classify(0, 0xF003, 5);
            Assert(string.IsNullOrEmpty(mismatchedMarker.Name), "回放标记长度冲突时不能仍给出确定名称。");
            Throws<ArgumentException>(() => ReplayPacketCatalog.Classify(0, 0, -1));
            Throws<ArgumentException>(() => ReplayPacketCatalog.Classify(0, 0, 65536));
            Throws<ArgumentException>(() => ReplayPacketCatalog.Classify(0, 0, 0, "不支持的版本"));
        }

        public static void ReplayExportPreservesInputAndListsEveryPacket()
        {
            using (var directory = new ExportTestDirectory())
            {
                var bytes = CreateReplay(new Packet(0x217, 20, 1, new byte[16]),
                    new Packet(0xFFFF, 30, 2, new byte[] { 1, 2, 3 }), new Packet(0, 10, 3, new byte[0]));
                File.WriteAllBytes(directory.InputPath, bytes);
                var originalHash = FileHash(directory.InputPath);
                var result = ReplayInspector.Export(directory.InputPath, directory.OutputPath, ReplayPacketCatalog.China757Profile);
                Assert(result.PacketCount == 3 && result.OpcodeCount == 3 && result.MaxTimestampMs == 30 &&
                    result.LastTimestampMs == 10, "导出应记录全部包，并区分末条时间与最大时间。");
                Assert(result.Sha256 == originalHash && FileHash(directory.InputPath) == originalHash &&
                    File.ReadAllBytes(directory.InputPath).SequenceEqual(bytes), "导出前后输入摘要与内容必须完全一致。");
                var rows = File.ReadAllLines(Path.Combine(directory.OutputPath, "packets.csv"));
                Assert(rows.Length == 4 && rows[1].Contains("ActorMove") && rows[2].Contains("0xFFFF") &&
                    rows[3].Contains("\"0x0000\",\"0\""), "逐条清单必须包含完整包、未知包与零负载包，不能过滤或改变顺序。");
                Assert(new[] { "packets.csv", "opcodes.csv", "chapters.csv", "summary.md" }
                    .All(name => File.Exists(Path.Combine(directory.OutputPath, name))) &&
                    Directory.GetFiles(directory.OutputPath, "*.partial").Length == 0,
                    "完整扫描成功后应发布四份结果，不能遗留临时清单。");
            }
        }

        public static void ReplayExportRejectsExistingAndSourceDirectoryOutputs()
        {
            using (var directory = new ExportTestDirectory())
            {
                File.WriteAllBytes(directory.InputPath, CreateReplay(new Packet(1, 0, 0, new byte[0])));
                var originalHash = FileHash(directory.InputPath);
                Directory.CreateDirectory(directory.OutputPath);
                var existing = Path.Combine(directory.OutputPath, "packets.csv");
                File.WriteAllText(existing, "已有结果，不能覆盖。", Encoding.UTF8);
                var existingHash = FileHash(existing);
                Throws<IOException>(() => ReplayInspector.Export(directory.InputPath, directory.OutputPath));
                Assert(FileHash(existing) == existingHash && Directory.GetFiles(directory.OutputPath).Length == 1,
                    "已有输出目录必须完整保留，不能修改原结果或添加半成品。");
                var source = Path.GetDirectoryName(directory.InputPath);
                var nested = Path.Combine(source, "新输出");
                Throws<ArgumentException>(() => ReplayInspector.Export(directory.InputPath, source));
                Throws<ArgumentException>(() => ReplayInspector.Export(directory.InputPath, nested));
                Assert(!Directory.Exists(nested) && Directory.GetFiles(source).Length == 1 &&
                    FileHash(directory.InputPath) == originalHash, "原始回放目录及其子目录均不能成为输出位置。");
            }
        }

        public static void ReplayExportNeverPublishesTruncatedPacketLists()
        {
            using (var directory = new ExportTestDirectory())
            {
                var complete = CreateReplay(new Packet(1, 10, 1, new byte[] { 1 }),
                    new Packet(2, 20, 1, new byte[] { 2, 3, 4 }));
                File.WriteAllBytes(directory.InputPath, complete.Take(complete.Length - 1).ToArray());
                var originalHash = FileHash(directory.InputPath);
                var error = Throws<IOException>(() => ReplayInspector.Export(directory.InputPath, directory.OutputPath));
                Assert(error.InnerException is InvalidDataException, "导出失败应保留记录被截断的具体原因。");
                var files = Directory.GetFiles(directory.OutputPath);
                Assert(files.All(path => path.EndsWith(".partial", StringComparison.Ordinal)) &&
                    !File.Exists(Path.Combine(directory.OutputPath, "summary.md")) &&
                    !File.Exists(Path.Combine(directory.OutputPath, "packets.csv")),
                    "完整记录之后出现截断时，也不能发布看似完整的包清单或汇总。");
                Assert(FileHash(directory.InputPath) == originalHash, "失败的导出同样不能修改损坏的原始文件。");
            }
        }

        public static void ReplayExportReportsBothUnfinalizedAndMismatchedHeader()
        {
            using (var directory = new ExportTestDirectory())
            {
                var bytes = CreateReplay(new Packet(1, 10, 1, new byte[0]));
                SetUInt32(bytes, 0x48, 1);
                File.WriteAllBytes(directory.InputPath, bytes);
                ReplayInspector.Export(directory.InputPath, directory.OutputPath);
                var summary = File.ReadAllText(Path.Combine(directory.OutputPath, "summary.md"));
                Assert(summary.Contains("未完成录制收尾") && summary.Contains("声明的数据长度与实际包流长度不同"),
                    "时长为零与非零声明长度失配可以同时发生，汇总必须同时说明两种情况。");
            }
        }

        private static string FileHash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }

        private static ReplayReader Open(byte[] bytes) => new ReplayReader(new MemoryStream(bytes, false));

        private static byte[] CreateReplay(params Packet[] packets)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                var prefix = new byte[DataOffset];
                Encoding.ASCII.GetBytes("FFXIVREPLAY\0").CopyTo(prefix, 0);
                SetUInt16(prefix, 0x0C, 5);
                writer.Write(prefix);
                foreach (var packet in packets)
                {
                    writer.Write(packet.Opcode);
                    writer.Write((ushort)packet.Payload.Length);
                    writer.Write(packet.Timestamp);
                    writer.Write(packet.ObjectId);
                    writer.Write(packet.Payload);
                }
                return stream.ToArray();
            }
        }

        private static void SetChapter(byte[] bytes, int index, int type, uint offset, uint timestamp)
        {
            SetUInt32(bytes, 0x6C + index * 12, unchecked((uint)type));
            SetUInt32(bytes, 0x70 + index * 12, offset);
            SetUInt32(bytes, 0x74 + index * 12, timestamp);
        }

        private static void SetUInt16(byte[] bytes, int offset, ushort value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
        }

        private static void SetUInt32(byte[] bytes, int offset, uint value)
        {
            for (var i = 0; i < 4; i++) bytes[offset + i] = (byte)(value >> (i * 8));
        }

        private static void RejectAt(Action action, long offset)
        {
            var error = Throws<InvalidDataException>(action);
            Assert(error.Message.Contains("0x" + offset.ToString("X") + "："),
                "损坏文件应指出预期偏移 0x" + offset.ToString("X") + "，实际错误：" + error.Message);
        }

        private static T Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T error) { return error; }
            throw new Exception("预期抛出 " + typeof(T).Name + "。");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private sealed class Packet
        {
            internal Packet(ushort opcode, uint timestamp, uint objectId, byte[] payload)
            {
                Opcode = opcode;
                Timestamp = timestamp;
                ObjectId = objectId;
                Payload = payload;
            }

            internal readonly ushort Opcode;
            internal readonly uint Timestamp;
            internal readonly uint ObjectId;
            internal readonly byte[] Payload;
        }

        private sealed class LimitedReadStream : MemoryStream
        {
            private readonly int _maxRead;
            private readonly int _end;
            private readonly bool _canSeek;

            internal LimitedReadStream(byte[] bytes, int maxRead, int end = int.MaxValue, bool canSeek = true) : base(bytes, false)
            {
                _maxRead = maxRead;
                _end = end;
                _canSeek = canSeek;
            }

            public override bool CanSeek => _canSeek && base.CanSeek;

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (Position >= _end) return 0;
                return base.Read(buffer, offset, Math.Min(Math.Min(count, _maxRead), (int)Math.Min(int.MaxValue, _end - Position)));
            }
        }

        private sealed class ExportTestDirectory : IDisposable
        {
            private readonly string _root;
            private const string DirectoryPrefix = "Simulant.ReplayTests.";

            internal ExportTestDirectory()
            {
                _root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DirectoryPrefix + Guid.NewGuid().ToString("N")));
                var source = Path.Combine(_root, "source");
                Directory.CreateDirectory(source);
                InputPath = Path.Combine(source, "synthetic.dat");
                OutputPath = Path.Combine(_root, "result");
            }

            internal string InputPath { get; }
            internal string OutputPath { get; }

            public void Dispose()
            {
                var resolved = Path.GetFullPath(_root);
                var temporaryRoot = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!resolved.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) ||
                    !Path.GetFileName(resolved).StartsWith(DirectoryPrefix, StringComparison.Ordinal) ||
                    !string.Equals(resolved, _root, StringComparison.Ordinal))
                    throw new InvalidOperationException("测试清理路径不在本次创建的临时目录内。");
                if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            }
        }
    }
}
