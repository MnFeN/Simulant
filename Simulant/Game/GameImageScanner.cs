using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Simulant.Game
{
    /// <summary>扫描磁盘上游戏可执行文件的 .text 节，并用运行中模块的基址换算命中地址。</summary>
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

        private readonly IntPtr _imageBase;
        private readonly byte[] _text;
        private readonly int _textRva;

        private GameImageScanner(string imagePath, IntPtr imageBase)
        {
            _imageBase = imageBase;
            _text = ReadText(imagePath, out _textRva);
        }

        internal static GameImageScanner Open(Process process)
        {
            var module = process.MainModule;
            return new GameImageScanner(module.FileName, module.BaseAddress);
        }

        /// <summary>定位 Framework 的 UI 更新回调，从磁盘提取入口的 10 字节并校验待搬运的指令。</summary>
        internal IntPtr FindFrameEntry(out byte[] original)
        {
            const int entryLength = 10;
            var rva = FindRva(GameEntryReference);
            if (!rva.HasValue || rva.Value < _textRva)
                throw new InvalidOperationException("未找到此客户端版本的框架 UI 更新入口。");

            original = _text.Skip(rva.Value - _textRva).Take(entryLength).ToArray();
            if (original.Length != entryLength || !original.Take(FrameEntryCopyBytes).SequenceEqual(FrameEntryInstruction))
                throw new InvalidOperationException("框架 UI 更新回调的入口指令已变化，无法按 5 字节搬运。");

            return _imageBase + rva.Value;
        }

        /// <summary>按顺序查找签名，返回首个唯一命中所解析的运行时地址。无唯一命中时返回零。</summary>
        internal IntPtr Find(params string[] patterns)
        {
            foreach (var pattern in patterns)
            {
                var rva = FindRva(pattern);
                if (rva.HasValue)
                    return _imageBase + rva.Value;
            }

            return IntPtr.Zero;
        }

        private static byte ParseByte(string value) => byte.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        private int? FindRva(string pattern)
        {
            // ? 只忽略匹配字节。连续的 * 也参与通配，并将其中的 rel32 按下一条指令地址解析。
            var parts = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var bytes = parts.Select(part => part == "*" || part == "**" ? -2 :
                part == "?" || part == "??" ? -1 : ParseByte(part)).ToArray();
            var firstLiteral = Array.FindIndex(bytes, value => value >= 0);
            if (firstLiteral < 0)
                throw new FormatException($"无效的地址签名 {pattern}。");

            var relative = Array.IndexOf(bytes, -2);
            var next = Array.LastIndexOf(bytes, -2) + 1;
            if (relative >= 0 && (next - relative < 4 || bytes.Skip(relative).Take(next - relative).Any(value => value != -2)))
                throw new FormatException($"无效的相对地址签名 {pattern}。");

            var literal = (byte)bytes[firstLiteral];
            int? result = null;

            for (var match = Array.IndexOf(_text, literal, firstLiteral);
                match >= 0 && match - firstLiteral <= _text.Length - bytes.Length;
                match = Array.IndexOf(_text, literal, match + 1))
            {
                var start = match - firstLiteral;
                var i = 0;
                while (i < bytes.Length && (bytes[i] < 0 || bytes[i] == _text[start + i]))
                    i++;

                if (i != bytes.Length)
                    continue;
                if (result.HasValue)
                    return null;

                result = checked(_textRva + start +
                    (relative < 0 ? 0 : next + BitConverter.ToInt32(_text, start + relative)));
            }

            return result;
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
}
