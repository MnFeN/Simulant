using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Simulant.Game
{
    // to-do: 以当前的签名数量，已经足够快，不需要随游戏版本缓存扫描结果。如果随着开发，签名多到扫描明显减慢，可添加缓存。

    /// <summary>扫描磁盘上游戏可执行文件的 .text 节，解析模块相对虚拟地址（RVA）。</summary>
    internal sealed class GameImageScanner
    {
        
        // 取自国服 ffxiv_dx11.exe 2026.11.03.0000.0000 的 Framework 初始化例程，下文地址均为该版本的 RVA。
        // 初始化例程（0xCF450）批量注册 Framework 任务。
        // 0xD0598 调用任务注册函数（0x61590），通过 RCX、EDX、R8 分别传入任务管理器、槽位 0x25、任务对象。
        // 前后的注册槽位为 0x09 和 0x27。
        // 0xD056E 的 LEA 取 UI 更新回调（0xD4330）的地址，0xD057C 将其存入任务对象的 +0x18 字段。
        // 四个 * 标记该 LEA 的 RIP 相对位移。末尾 E8 用于匹配注册调用，其位移用 ? 通配。
        // 版本更新后从 Framework 初始化时的任务注册调用重新追踪回调字段，并检查目标对 Framework.UIModule 的访问。
        /// <summary>
        /// 游戏入口函数，选用了与 PostNamazu 不同的以免冲突。
        /// </summary>
        private const string GameEntryReference =
            "48 8D 0D * * * * 48 89 18 48 89 70 20 48 89 48 18 48 89 70 28 48 89 70 30 EB ? 48 8B C6 4C 8B C0 BA 25 00 00 00 49 8B CE E8 ? ? ? ?";

        internal const int FrameEntryCopyBytes = 5;

        // SimuMagic 会原样搬运入口处的指令。当前入口的 mov [rsp+8], rbx 占 5 字节，可以直接搬运。
        private static readonly byte[] FrameEntryInstruction = { 0x48, 0x89, 0x5C, 0x24, 0x08 };

        private readonly byte[] _text;
        private readonly int _textRva;
        private Dictionary<string, ScanPattern> _preparedPatterns;

        private GameImageScanner(string imagePath)
        {
            _text = ReadText(imagePath, out _textRva);
        }

        /// <summary>预扫磁盘 EXE，保存地址相对模块基址的偏移及入口原始字节，供连接进程时使用。</summary>
        internal static GameImageScan Scan(GameImageFile file)
        {
            var scanner = new GameImageScanner(file.Path);
            scanner.PreparePatterns(SigAddressScanner.GetSignatures());
            var entryRva = scanner.FindFrameEntry(out var original);
            var addresses = SigAddressScanner.ResolveRvas(scanner);
            if (!file.Matches(new GameImageFile(file.Path)))
                throw new IOException("游戏 EXE 在扫描期间发生变化，请重试连接。");

            return new GameImageScan(file, entryRva, original, addresses);
        }

        /// <summary>定位 Framework 的 UI 更新回调，从磁盘提取入口的 10 字节并校验待搬运的指令。</summary>
        private int FindFrameEntry(out byte[] original)
        {
            const int entryLength = 10;
            var rva = FindRva(GameEntryReference);
            if (!rva.HasValue || rva.Value < _textRva)
                throw new InvalidOperationException("未找到此客户端版本的框架 UI 更新入口。");

            original = _text.Skip(rva.Value - _textRva).Take(entryLength).ToArray();
            if (original.Length != entryLength || !original.Take(FrameEntryCopyBytes).SequenceEqual(FrameEntryInstruction))
                throw new InvalidOperationException("框架 UI 更新回调的入口指令已变化，无法按 5 字节搬运。");

            return rva.Value;
        }

        /// <summary>按顺序查找备选签名，返回首个唯一命中所解析的 RVA。</summary>
        internal int? FindRva(IEnumerable<string> patterns)
        {
            foreach (var pattern in patterns)
            {
                var rva = FindRva(pattern);
                if (rva.HasValue)
                    return rva;
            }

            return null;
        }

        /// <summary>
        /// 按抽样选出的稀有字节对建立候选索引，再完整遍历一次 .text。<br />
        /// 缓存游戏入口和地址属性签名的匹配位置及唯一性，供后续查址使用。
        /// </summary>
        private void PreparePatterns(IEnumerable<string> patterns)
        {
            var prepared = new Dictionary<string, ScanPattern>(StringComparer.Ordinal);
            var candidatesByPair = new List<ScanPattern>[1 << 16];
            var pairFrequencies = SamplePairFrequencies();
            foreach (var pattern in patterns.Append(GameEntryReference).Distinct(StringComparer.Ordinal))
            {
                if (pattern == null)
                    continue;

                ScanPattern parsed;
                try
                {
                    parsed = new ScanPattern(pattern);
                }
                catch (Exception ex) when (ex is FormatException || ex is OverflowException)
                {
                    // 无效的备选签名仍在实际用到时才报错。
                    continue;
                }

                if (parsed.FirstLiteral > _text.Length)
                    continue;

                prepared.Add(pattern, parsed);
                var pair = parsed.SelectPairAnchor(pairFrequencies);
                if (pair >= 0)
                {
                    AddCandidate(candidatesByPair, pair, parsed);
                    continue;
                }

                // 没有连续固定字节时，只限定第一个字节，下一字节取任意值。
                for (var key = parsed.Bytes[parsed.AnchorOffset]; key < candidatesByPair.Length; key += 1 << 8)
                    AddCandidate(candidatesByPair, key, parsed);
            }

            var last = _text.Length - 1;
            for (var index = 0; index < last; index++)
            {
                var candidates = candidatesByPair[_text[index] | (_text[index + 1] << 8)];
                if (candidates != null)
                    CheckCandidates(candidates, index);
            }

            // 末尾用零补位查表，要求两个实际字节的锚点会在签名长度检查时被排除。
            if (last >= 0 && candidatesByPair[_text[last]] is List<ScanPattern> lastCandidates)
                CheckCandidates(lastCandidates, last);

            _preparedPatterns = prepared;
        }

        /// <summary>按固定间隔抽样 .text，统计相邻字节对的频率，供选择较少出现的锚点使用。</summary>
        private int[] SamplePairFrequencies()
        {
            const int sampleStride = 4096;
            const int sampleLength = 64;
            var frequencies = new int[1 << 16];
            var last = _text.Length - 1;
            for (var start = 0; start < last; start += sampleStride)
            {
                var end = Math.Min(start + sampleLength, last);
                for (var index = start; index < end; index++)
                    frequencies[_text[index] | (_text[index + 1] << 8)]++;
            }

            return frequencies;
        }

        private static void AddCandidate(List<ScanPattern>[] buckets, int key, ScanPattern pattern)
        {
            var bucket = buckets[key] ?? (buckets[key] = new List<ScanPattern>());
            bucket.Add(pattern);
        }

        /// <summary>从锚点位置反推签名起点，逐字节验证候选并记录首次命中和重复命中。</summary>
        private void CheckCandidates(List<ScanPattern> candidates, int index)
        {
            foreach (var candidate in candidates)
            {
                var start = index - candidate.AnchorOffset;
                if (candidate.HasMultipleMatches || start < 0 || start > _text.Length - candidate.Bytes.Length ||
                    !Matches(candidate, start))
                    continue;

                if (candidate.FirstMatch < 0)
                    candidate.FirstMatch = start;
                else
                    candidate.HasMultipleMatches = true;
            }
        }

        private bool Matches(ScanPattern pattern, int start)
        {
            var bytes = pattern.Bytes;
            for (var index = 0; index < bytes.Length; index++)
            {
                if (bytes[index] >= 0 && bytes[index] != _text[start + index])
                    return false;
            }

            return true;
        }

        private int? FindRva(string pattern)
        {
            if (_preparedPatterns != null && pattern != null && _preparedPatterns.TryGetValue(pattern, out var prepared))
            {
                if (prepared.FirstMatch < 0)
                    return null;

                var rva = ResolveRva(prepared, prepared.FirstMatch);
                return prepared.HasMultipleMatches ? (int?)null : rva;
            }

            var parsed = new ScanPattern(pattern);
            var literal = (byte)parsed.Bytes[parsed.FirstLiteral];
            int? result = null;
            for (var match = Array.IndexOf(_text, literal, parsed.FirstLiteral);
                match >= 0 && match - parsed.FirstLiteral <= _text.Length - parsed.Bytes.Length;
                match = Array.IndexOf(_text, literal, match + 1))
            {
                var start = match - parsed.FirstLiteral;
                if (!Matches(parsed, start))
                    continue;
                if (result.HasValue)
                    return null;

                result = ResolveRva(parsed, start);
            }

            return result;
        }

        /// <summary>将 .text 内的命中偏移转换为模块相对虚拟地址。签名含 * 时解析其标记的 rel32 目标。</summary>
        private int ResolveRva(ScanPattern pattern, int start)
            => checked(_textRva + start + (pattern.RelativeOffset < 0 ? 0 :
                pattern.RelativeEnd + BitConverter.ToInt32(_text, start + pattern.RelativeOffset)));

        /// <summary>保存解析出的签名字节、相对位移位置、候选锚点和命中状态。</summary>
        private sealed class ScanPattern
        {
            internal readonly int[] Bytes;
            internal readonly int FirstLiteral;
            internal readonly int RelativeOffset;
            internal readonly int RelativeEnd;
            internal int AnchorOffset;
            internal int FirstMatch = -1;
            internal bool HasMultipleMatches;

            internal ScanPattern(string pattern)
            {
                // ? 仅通配，连续的 * 同时标记需要解析的 rel32。
                var parts = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Bytes = parts.Select(part => part == "*" || part == "**" ? -2 :
                    part == "?" || part == "??" ? -1 : byte.Parse(part, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
                FirstLiteral = Array.FindIndex(Bytes, value => value >= 0);
                if (FirstLiteral < 0)
                    throw new FormatException($"无效的地址签名 {pattern}。");

                RelativeOffset = Array.IndexOf(Bytes, -2);
                RelativeEnd = Array.LastIndexOf(Bytes, -2) + 1;
                if (RelativeOffset >= 0 && (RelativeEnd - RelativeOffset < 4 ||
                    Bytes.Skip(RelativeOffset).Take(RelativeEnd - RelativeOffset).Any(value => value != -2)))
                    throw new FormatException($"无效的相对地址签名 {pattern}。");
            }

            /// <summary>
            /// 选取抽样中出现次数最少的相邻固定字节对，并保存它在签名中的偏移。<br />
            /// 没有相邻固定字节时，保留首个固定字节作为锚点并返回 -1。
            /// </summary>
            internal int SelectPairAnchor(int[] frequencies)
            {
                AnchorOffset = FirstLiteral;
                var bestPair = -1;
                var bestFrequency = int.MaxValue;
                for (var index = FirstLiteral; index < Bytes.Length - 1; index++)
                {
                    if (Bytes[index] < 0 || Bytes[index + 1] < 0)
                        continue;

                    var pair = Bytes[index] | (Bytes[index + 1] << 8);
                    if (frequencies[pair] >= bestFrequency)
                        continue;

                    AnchorOffset = index;
                    bestPair = pair;
                    bestFrequency = frequencies[pair];
                    if (bestFrequency == 0)
                        break;
                }

                return bestPair;
            }
        }

        private static byte[] ReadText(string path, out int rva)
        {
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new BinaryReader(stream))
            {
                stream.Position = 0x3C;
                stream.Position = reader.ReadInt32();
                if (reader.ReadUInt32() != 0x4550 || reader.ReadUInt16() != 0x8664)
                    throw new InvalidDataException("游戏客户端不是 x64 PE 文件。");
                var count = reader.ReadUInt16();
                stream.Position += 12;
                var optionalSize = reader.ReadUInt16();
                stream.Position += 2 + optionalSize;
                for (var i = 0; i < count; i++)
                {
                    var name = reader.ReadBytes(8);
                    var virtualSize = reader.ReadInt32();
                    rva = reader.ReadInt32();
                    var rawSize = reader.ReadInt32();
                    var offset = reader.ReadInt32();
                    stream.Position += 16;
                    if (Encoding.ASCII.GetString(name).TrimEnd('\0') != ".text")
                        continue;
                    stream.Position = offset;
                    var length = Math.Min(virtualSize, rawSize);
                    var text = reader.ReadBytes(length);
                    if (text.Length != length)
                        throw new InvalidDataException("游戏客户端的 .text 节读取不完整。");
                    return text;
                }
            }
            throw new InvalidDataException("游戏客户端缺少 .text 节。");
        }
    }

    /// <summary>记录磁盘 EXE 的路径和文件状态，用于判断是否需要重新准备客户端。</summary>
    internal readonly struct GameImageFile
    {
        internal string Path { get; }
        private readonly long _length;
        private readonly DateTime _lastWriteTime;

        internal GameImageFile(string path)
        {
            var file = new FileInfo(path);
            Path = file.FullName;
            _length = file.Length;
            _lastWriteTime = file.LastWriteTimeUtc;
        }

        internal bool Matches(GameImageFile other) =>
            string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase) &&
            _length == other._length && _lastWriteTime == other._lastWriteTime;
    }

    /// <summary>单次磁盘扫描的结果，可用于连接同一 EXE 的多个进程。</summary>
    internal sealed class GameImageScan
    {
        internal GameImageFile File { get; }
        internal int EntryRva { get; }
        internal byte[] EntryBytes { get; }
        internal IReadOnlyDictionary<PropertyInfo, int?> Addresses { get; }

        internal GameImageScan(GameImageFile file, int entryRva, byte[] entryBytes, IReadOnlyDictionary<PropertyInfo, int?> addresses)
        {
            File = file;
            EntryRva = entryRva;
            EntryBytes = entryBytes;
            Addresses = addresses;
        }
    }
}
