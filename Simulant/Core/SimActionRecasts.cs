using Simulant.ACT;
using Simulant.Core.Entity;
using Simulant.Game;
using Simulant.Game.Data;
using Simulant.Game.FFCS.Client.Game;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Simulant.Core
{
    /// <summary>同步游戏快捷栏冷却；退出防火墙时恢复原有冷却的自然流逝结果。</summary>
    internal sealed class SimActionRecasts : IDisposable
    {
        private readonly Character _player;
        private readonly IntPtr _process;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly Dictionary<uint, float> _durations = new Dictionary<uint, float>();
        private readonly Dictionary<IntPtr, byte[]> _original = new Dictionary<IntPtr, byte[]>();

        internal SimActionRecasts(Character player)
        {
            _player = player;
            _process = NamazuInterop.Plugin.Memory.ProcessHandle;
            foreach (var action in new uint[] { 3, 7548, 7559 })
            {
                _durations[action] = GameDataService.Instance.GetSheet<Lumina.Excel.Sheets.Action>().GetRow(action).Recast100ms / 10f;
                var manager = ActionManager.Instance;
                var detail = manager.GetRecastGroupDetail(manager.GetRecastGroup(action));
                if (!_original.ContainsKey(detail))
                    _original.Add(detail, detail.ReadBytes(0x14));
            }
        }

        internal float Duration(uint action) => _durations[action];
        internal void Start(uint action) => ActionManager.Instance.SetCooldown(action, 0, Duration(action));

        internal void Reset()
        {
            foreach (var action in _durations.Keys)
                ActionManager.Instance.SetCooldown(action, Duration(action), Duration(action));
        }

        public void Dispose()
        {
            var memory = NamazuInterop.Plugin.Memory;
            if (memory == null || !memory.IsProcessOpen || memory.ProcessHandle != _process || !_player.IsAlive)
                return;
            foreach (var entry in _original)
            {
                var bytes = entry.Value;
                if (bytes[0] != 0)
                {
                    var total = BitConverter.ToSingle(bytes, 12);
                    var elapsed = Math.Min(total, BitConverter.ToSingle(bytes, 8) + (float)_clock.Elapsed.TotalSeconds);
                    Array.Copy(BitConverter.GetBytes(elapsed), 0, bytes, 8, 4);
                    if (elapsed >= total)
                        bytes[0] = 0;
                }
                entry.Key.WriteBytes(bytes);
            }
        }
    }
}
