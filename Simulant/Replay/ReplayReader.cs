using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Simulant.Replay
{
    /// <summary>
    /// 按文件顺序读取格式 5 的副本回放。只解析记录容器，不假设操作码在不同客户端版本间相同。
    /// </summary>
    /// <remarks>
    /// 布局来源：Hypostasis c230760c7bbc85a6e37b8477012dcd92ab700dc7 的 FFXIVReplay.cs，
    /// 并与 FFCS 的 ContentsReplayManager.FFXIVREPLAYHeader 核对。
    /// 文件头为 0x68 字节，章节区为 0x304 字节；记录头为 12 字节，负载没有额外对齐。
    /// </remarks>
    public sealed class ReplayReader : IDisposable
    {
        private const int HeaderSize = 0x68;
        private const int ChapterCapacity = 64;
        private const int ChapterSize = 0x0C;
        private const int PacketHeaderSize = 0x0C;
        private const int PacketDataOffset = HeaderSize + 4 + ChapterCapacity * ChapterSize;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("FFXIVREPLAY\0");
        private readonly Stream _stream;
        private readonly bool _leaveOpen;
        private bool _disposed;
        private int _enumerating;

        /// <summary>创建读取器。流必须可读、可定位；读取总是从文件起点开始。</summary>
        public ReplayReader(Stream stream, bool leaveOpen = false)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream), "回放输入流不能为空。");
            if (!stream.CanRead || !stream.CanSeek)
                throw new ArgumentException("回放输入流必须支持读取和定位。", nameof(stream));

            _stream = stream;
            _leaveOpen = leaveOpen;
            try
            {
                Length = stream.Length;
                stream.Position = 0;
                var prefix = ReadExactly(PacketDataOffset, 0, "回放文件头或章节区");
                for (var i = 0; i < Magic.Length; i++)
                {
                    if (prefix[i] != Magic[i])
                        throw InvalidAt(i, "文件标识不是 FFXIVREPLAY。");
                }

                var version = UInt16(prefix, 0x0C);
                if (version != 5)
                    throw InvalidAt(0x0C, $"暂不支持回放格式 {version}，当前仅支持格式 5。");

                Header = new ReplayHeader(prefix);
                var chapterCount = unchecked((int)UInt32(prefix, HeaderSize));
                if (chapterCount < 0 || chapterCount > ChapterCapacity)
                    throw InvalidAt(HeaderSize, $"章节数量 {chapterCount} 超出 0 至 {ChapterCapacity} 的范围。");

                var chapters = new ReplayChapter[chapterCount];
                for (var i = 0; i < chapterCount; i++)
                {
                    var offset = HeaderSize + 4 + i * ChapterSize;
                    var chapter = new ReplayChapter(i + 1, unchecked((int)UInt32(prefix, offset)),
                        UInt32(prefix, offset + 4), UInt32(prefix, offset + 8));
                    chapters[i] = chapter;
                }
                Chapters = Array.AsReadOnly(chapters);
            }
            catch
            {
                if (!leaveOpen) stream.Dispose();
                throw;
            }
        }

        public ReplayHeader Header { get; }
        public IReadOnlyList<ReplayChapter> Chapters { get; }

        /// <summary>打开时的文件长度；后续新追加的数据不属于本次读取。</summary>
        public long Length { get; }

        /// <summary>第一条记录在文件中的绝对偏移。</summary>
        public long DataOffset => PacketDataOffset;

        /// <summary>
        /// 流式读取所有完整记录，保留未知操作码、零长度负载和原始时间顺序。
        /// 同一读取器一次只能进行一次枚举，枚举结束后可以重新读取。
        /// </summary>
        /// <remarks>
        /// 部分录制文件未写回长度和章节信息，因此以打开时的实际文件末尾为边界。
        /// 声明长度仍保留在 Header 中，供调用方报告差异，不擅自补写原文件。
        /// </remarks>
        public IEnumerable<ReplayPacket> ReadPackets()
        {
            ThrowIfDisposed();
            return EnumeratePackets();
        }

        private IEnumerable<ReplayPacket> EnumeratePackets()
        {
            ThrowIfDisposed();
            if (Interlocked.CompareExchange(ref _enumerating, 1, 0) != 0)
                throw new InvalidOperationException("同一回放读取器不能同时枚举多份记录。");

            try
            {
                _stream.Position = PacketDataOffset;
                long index = 0;
                while (_stream.Position < Length)
                {
                    ThrowIfDisposed();
                    var offset = _stream.Position;
                    var header = ReadExactly(PacketHeaderSize, offset, "回放记录头");
                    var size = UInt16(header, 2);
                    var payload = ReadExactly(size, offset + PacketHeaderSize, "回放记录负载");
                    yield return new ReplayPacket(++index, offset, UInt16(header, 0),
                        UInt32(header, 4), UInt32(header, 8), payload);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _enumerating, 0);
            }
        }

        private byte[] ReadExactly(int count, long offset, string part)
        {
            if (count > Length - offset)
                throw InvalidAt(offset, $"{part}被截断：需要 {count} 字节，剩余 {Math.Max(0, Length - offset)} 字节。");

            var bytes = new byte[count];
            var read = 0;
            while (read < count)
            {
                var length = _stream.Read(bytes, read, count - read);
                if (length == 0)
                    throw InvalidAt(offset + read, $"{part}被截断：需要 {count} 字节，实际读到 {read} 字节。");
                read += length;
            }
            return bytes;
        }

        internal static ushort UInt16(byte[] bytes, int offset)
        {
            return (ushort)(bytes[offset] | bytes[offset + 1] << 8);
        }

        internal static uint UInt32(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24);
        }

        private static InvalidDataException InvalidAt(long offset, string message)
        {
            return new InvalidDataException($"回放文件偏移 0x{offset:X}：{message}");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ReplayReader), "回放读取器已释放。");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (!_leaveOpen) _stream.Dispose();
        }
    }

    /// <summary>回放文件头的原始信息；长度和时长为零时不自动推断或覆盖。</summary>
    public sealed class ReplayHeader
    {
        internal ReplayHeader(byte[] bytes)
        {
            FormatVersion = ReplayReader.UInt16(bytes, 0x0C);
            OperatingSystemType = ReplayReader.UInt16(bytes, 0x0E);
            GameVersion = ReplayReader.UInt32(bytes, 0x10);
            TimestampUnixSeconds = ReplayReader.UInt32(bytes, 0x14);
            TimestampUtc = DateTimeOffset.FromUnixTimeSeconds(TimestampUnixSeconds);
            DeclaredDurationMs = ReplayReader.UInt32(bytes, 0x18);
            DisplayedDurationMs = ReplayReader.UInt32(bytes, 0x1C);
            ContentFinderConditionId = ReplayReader.UInt16(bytes, 0x20);
            Flags = bytes[0x28];
            LocalContentId = ReplayReader.UInt32(bytes, 0x30) | (ulong)ReplayReader.UInt32(bytes, 0x34) << 32;
            var jobs = new byte[8];
            Array.Copy(bytes, 0x38, jobs, 0, jobs.Length);
            JobIds = Array.AsReadOnly(jobs);
            PlayerIndex = bytes[0x40];
            DeclaredDataLength = ReplayReader.UInt32(bytes, 0x48);
            var names = new ushort[7];
            for (var i = 0; i < names.Length; i++) names[i] = ReplayReader.UInt16(bytes, 0x4E + i * 2);
            BnpcNameIds = Array.AsReadOnly(names);
        }

        public ushort FormatVersion { get; }
        public ushort OperatingSystemType { get; }

        /// <summary>客户端内部构建号，不能直接当作 7.57 等公开版本号。</summary>
        public uint GameVersion { get; }
        public uint TimestampUnixSeconds { get; }
        public DateTimeOffset TimestampUtc { get; }
        public uint DeclaredDurationMs { get; }
        public uint DisplayedDurationMs { get; }
        public ushort ContentFinderConditionId { get; }
        public byte Flags { get; }
        public ulong LocalContentId { get; }
        public IReadOnlyList<byte> JobIds { get; }
        public byte PlayerIndex { get; }

        /// <summary>文件头声明的记录区长度，不含文件头和章节区。</summary>
        public uint DeclaredDataLength { get; }
        public IReadOnlyList<ushort> BnpcNameIds { get; }
    }

    /// <summary>一个章节的位置；未知类型原样保留。</summary>
    public sealed class ReplayChapter
    {
        internal ReplayChapter(int index, int type, uint offset, uint timestampMs)
        {
            Index = index;
            Type = type;
            Offset = offset;
            TimestampMs = timestampMs;
        }

        public int Index { get; }
        public int Type { get; }

        /// <summary>相对于第一条记录的字节偏移。</summary>
        public uint Offset { get; }
        public uint TimestampMs { get; }
    }

    /// <summary>回放中的一条记录。负载仅为包体，不包含实时网络传输头。</summary>
    public sealed class ReplayPacket
    {
        internal ReplayPacket(long index, long offset, ushort opcode, uint timestampMs, uint objectId, byte[] payload)
        {
            Index = index;
            Offset = offset;
            Opcode = opcode;
            TimestampMs = timestampMs;
            ObjectId = objectId;
            Payload = payload;
        }

        public long Index { get; }
        public long Offset { get; }
        public ushort Opcode { get; }
        public uint TimestampMs { get; }
        public uint ObjectId { get; }
        public byte[] Payload { get; }
    }
}
