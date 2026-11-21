using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Simulant.ActionNpcTool
{
    internal sealed class ActNpcScanner
    {
        internal const uint DummyBaseId = 9020;

        private readonly byte[] _buffer = new byte[1024 * 1024];
        private readonly bool[] _wantedActions;
        private readonly HashSet<uint> _validNameIds;
        private readonly Dictionary<uint, uint> _entities = new Dictionary<uint, uint>();
        internal Dictionary<uint, HashSet<uint>> Casters { get; } = new Dictionary<uint, HashSet<uint>>();
        internal Dictionary<uint, HashSet<uint>> Names { get; } = new Dictionary<uint, HashSet<uint>>();

        internal ActNpcScanner(bool[] wantedActions, HashSet<uint> validNameIds)
        {
            _wantedActions = wantedActions;
            _validNameIds = validNameIds;
        }

        internal void Scan(string path, int entryIndex)
        {
            try
            {
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                    4096, entryIndex < 0 ? FileOptions.SequentialScan : FileOptions.None))
                {
                    if (entryIndex < 0)
                    {
                        Scan(file);
                        return;
                    }

                    using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
                    using (var log = archive.Entries[entryIndex].Open())
                        Scan(log);
                }
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException)
            {
                throw new IOException($"读取日志失败：{path}", error);
            }
        }

        private void Scan(Stream stream)
        {
            _entities.Clear();
            var buffered = 0;
            var skippingLongLine = false;
            int read;

            while ((read = stream.Read(_buffer, buffered, _buffer.Length - buffered)) != 0)
            {
                var remaining = new ReadOnlySpan<byte>(_buffer, 0, buffered + read);
                int end;

                while ((end = remaining.IndexOf((byte)'\n')) >= 0)
                {
                    if (!skippingLongLine)
                        ReadLine(remaining.Slice(0, end));

                    skippingLongLine = false;
                    remaining = remaining.Slice(end + 1);
                }

                buffered = remaining.Length;

                if (buffered == _buffer.Length)
                {
                    buffered = 0;
                    skippingLongLine = true;
                }
                else
                    remaining.CopyTo(_buffer);
            }

            if (buffered != 0 && !skippingLongLine)
                ReadLine(new ReadOnlySpan<byte>(_buffer, 0, buffered));
        }

        private void ReadLine(ReadOnlySpan<byte> line)
        {
            if (line.Length >= 3 && line[0] == 0xEF && line[1] == 0xBB && line[2] == 0xBF)
                line = line.Slice(3);

            if (line.Length < 4 || line[2] != '|')
                return;

            var type = ParseType(line[0], line[1]);
            if (type == 0)
                return;

            if (type == 1)
            {
                _entities.Clear();
                return;
            }

            line = line.Slice(3);
            TakeField(ref line); // 日期
            var entity = TakeField(ref line);
            if (entity.Length != 8 || entity[0] != '4' || !TryParse(entity, 16, out var actor))
                return;

            if (type == 4)
                _entities.Remove(actor);
            else if (type == 3)
            {
                // Name、Job、Level、Owner、World、WorldName、BNpcName、BNpcBase。
                for (var i = 0; i < 6; i++)
                    TakeField(ref line);

                TryParse(TakeField(ref line), 10, out var name);
                if (!TryParse(TakeField(ref line), 10, out var baseId) || baseId == 0 || baseId == DummyBaseId)
                {
                    _entities.Remove(actor);
                    return;
                }

                _entities[actor] = baseId;
                if (_validNameIds.Contains(name))
                    Add(Names, baseId, name);
            }
            else
            {
                TakeField(ref line); // 来源名
                if (TryParse(TakeField(ref line), 16, out var action) && action < _wantedActions.Length &&
                    _wantedActions[action] && _entities.TryGetValue(actor, out var casterBaseId))
                    Add(Casters, action, casterBaseId);
            }
        }

        private static byte ParseType(byte high, byte low)
        {
            if (high == '0' && (low == '1' || low == '3' || low == '4'))
                return (byte)(low - '0');

            return high == '2' && low >= '0' && low <= '2' ? (byte)20 : (byte)0;
        }

        private static ReadOnlySpan<byte> TakeField(ref ReadOnlySpan<byte> line)
        {
            var end = line.IndexOf((byte)'|');
            if (end < 0)
            {
                var final = line.Length != 0 && line[line.Length - 1] == '\r' ? line.Slice(0, line.Length - 1) : line;
                line = default;
                return final;
            }

            var field = line.Slice(0, end);
            line = line.Slice(end + 1);
            return field;
        }

        private static bool TryParse(ReadOnlySpan<byte> field, uint radix, out uint value)
        {
            value = 0;
            if (field.IsEmpty || field.Length > (radix == 16 ? 8 : 10))
                return false;

            ulong parsed = 0;
            foreach (var c in field)
            {
                var digit = (uint)(c - '0');
                if (digit > 9)
                {
                    var alpha = (uint)((c | 0x20) - 'a');
                    digit = alpha < 6 ? alpha + 10 : uint.MaxValue;
                }
                if (digit >= radix)
                    return false;

                parsed = parsed * radix + digit;
            }

            if (parsed > uint.MaxValue)
                return false;

            value = (uint)parsed;
            return true;
        }

        private static void Add(Dictionary<uint, HashSet<uint>> index, uint id, uint item)
        {
            if (!index.TryGetValue(id, out var set))
                index.Add(id, set = new HashSet<uint>());
            set.Add(item);
        }
    }
}
