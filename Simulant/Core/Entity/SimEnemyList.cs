using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Network;
using Simulant.Game.FFCS.Client.Game.UI;
using Simulant.Game.FFCS.Client.Network;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulant.Core.Entity
{
    /// <summary>经原生收包入口发布模拟敌人，并在停止时移除本会话的行。</summary>
    internal sealed class SimEnemyList
    {
        private readonly PluginHost _host;
        private readonly HashSet<uint> _published = new HashSet<uint>();
        private readonly Dictionary<int, (uint EntityId, uint Previous)> _letters =
            new Dictionary<int, (uint EntityId, uint Previous)>();
        private int _nextLetter;
        private GameConnection _connection;
        private IntPtr _uiState;
        private uint _playerId;
        private int _world;
        private bool _warned;

        internal SimEnemyList(PluginHost host) => _host = host;

        internal bool HasPendingCleanup => _published.Count != 0 || _letters.Count != 0;

        internal void StartLettersAt(char letter)
        {
            if (letter < 'A' || letter > 'Z')
                throw new ArgumentOutOfRangeException(nameof(letter));

            _nextLetter = letter - 'A';
        }

        internal void Refresh(uint playerId, IReadOnlyList<(uint EntityId, byte Enmity)> enemies)
        {
            if (HasPendingCleanup && !IsCurrent())
            {
                ForgetPublishedTargets();
                return;
            }

            if (enemies.Count == 0 && !HasPendingCleanup)
                return;

            try
            {
                if (!GameInterop.IsReady || UIState.InstancePtr == IntPtr.Zero ||
                    PacketDispatcher.HandleUpdateHaterPacketFuncPtr == IntPtr.Zero)
                    throw new InvalidOperationException("原生敌对列表尚未就绪。");

                _connection = GameInterop.Connection;
                _uiState = UIState.InstancePtr;
                _playerId = playerId;
                _world = _host.WorldGeneration;
                Sync(enemies);
                _warned = false;
            }
            catch (Exception error)
            {
                if (!_warned)
                {
                    _warned = true;
                    _host.LogWarning("更新模拟敌对列表失败：" + error.Message);
                }
            }
        }

        internal void Restore()
        {
            if (HasPendingCleanup && IsCurrent())
                Sync(Array.Empty<(uint, byte)>());

            ForgetPublishedTargets();
            _nextLetter = 0;
            _connection = null;
            _warned = false;
        }

        private void ForgetPublishedTargets()
        {
            _published.Clear();
            _letters.Clear();
        }

        private bool IsCurrent()
            => _world == _host.WorldGeneration && ReferenceEquals(_connection, GameInterop.Connection)
                && _uiState == UIState.InstancePtr && GameInterop.IsReady;

        private void Sync(IReadOnlyList<(uint EntityId, byte Enmity)> enemies)
        {
            var current = _uiState.As<UIState>().Hater.ReadEntries();
            var desiredIds = new HashSet<uint>(enemies.Select(enemy => enemy.EntityId));
            var packet = new UpdateHaterPacket();

            foreach (var entry in current)
            {
                if (_published.Contains(entry.EntityId) || desiredIds.Contains(entry.EntityId))
                    continue;

                packet.SetEntry(packet.EnemyCount, new UpdateHatePacketMember
                {
                    EntityId = entry.EntityId,
                    Enmity = checked((byte)entry.Enmity)
                });
                packet.EnemyCount++;
            }

            var included = new List<uint>();
            foreach (var enemy in enemies)
            {
                if (packet.EnemyCount == UpdateHaterPacket.Capacity)
                    break;

                packet.SetEntry(packet.EnemyCount, new UpdateHatePacketMember
                {
                    EntityId = enemy.EntityId,
                    Enmity = enemy.Enmity
                });
                packet.EnemyCount++;
                included.Add(enemy.EntityId);
            }

            if (!IsCurrent())
            {
                ForgetPublishedTargets();
                return;
            }

            SyncLetters(included);
            if (!IsCurrent())
            {
                ForgetPublishedTargets();
                return;
            }

            // 调用可能写入部分行后抛错，须先登记清理责任。
            _published.UnionWith(included);
            if (!Matches(current, packet))
            {
                PacketDispatcher.HandleUpdateHaterPacket(_playerId, packet);
                if (!IsCurrent())
                {
                    ForgetPublishedTargets();
                    return;
                }

                if (_uiState.As<UIState>().Hater.ReadEntries().Any(entry =>
                    _published.Contains(entry.EntityId) && !included.Contains(entry.EntityId)))
                    throw new InvalidOperationException("仍可读到应移除的模拟敌对条目。");
            }

            _published.IntersectWith(included);
        }

        private void SyncLetters(List<uint> included)
        {
            if (included.Count == 0 && _letters.Count == 0)
                return;

            var controller = MarkingController.Instance;
            if (controller.Ptr == IntPtr.Zero && _letters.Count == 0)
                return;

            var markers = controller.LetterMarkers;
            var current = markers.Get();
            foreach (var assignment in _letters.ToArray())
            {
                var slot = assignment.Key;
                if (current[slot] == assignment.Value.EntityId)
                {
                    if (included.Contains(assignment.Value.EntityId))
                        continue;

                    if (!SetLetter(markers, slot, assignment.Value.Previous))
                        return;

                    current[slot] = assignment.Value.Previous;
                }

                _letters.Remove(slot);
            }

            // 客户端按字母槽排序后截取前八行，未分配字母的条目保留名单包里的顺序。
            foreach (var entityId in included)
            {
                if (Array.IndexOf(current, entityId) >= 0)
                    continue;

                for (var offset = 0; offset < current.Length; offset++)
                {
                    var slot = (_nextLetter + offset) % current.Length;
                    if (current[slot] != 0 && current[slot] != MarkingController.NoTarget)
                        continue;

                    // 原生控制有可能写入后抛错，先登记清理责任。
                    _letters[slot] = (entityId, current[slot]);
                    if (!SetLetter(markers, slot, entityId))
                        return;

                    current[slot] = entityId;
                    _nextLetter = (slot + 1) % current.Length;
                    break;
                }
            }
        }

        private bool SetLetter(MemoryArray<uint> markers, int slot, uint entityId)
        {
            if (!IsCurrent())
                return false;

            PacketDispatcher.HandleActorControl(_playerId, ActorControlCategory.SetHateLetter,
                (uint)slot, entityId, 0, 0, 0, 0, 0, 0, MarkingController.NoTarget, 0);

            if (!IsCurrent())
                return false;

            if (markers.Ptr.Read<uint>(slot * sizeof(uint)) != entityId)
                throw new InvalidOperationException("未能更新原生敌对列表字母。");

            return true;
        }

        private static bool Matches(HaterInfo[] entries, UpdateHaterPacket packet)
        {
            if (entries.Length != packet.EnemyCount)
                return false;

            for (var i = 0; i < entries.Length; i++)
            {
                var target = packet.GetEntry(i);
                if (entries[i].EntityId != target.EntityId || entries[i].Enmity != target.Enmity)
                    return false;
            }

            return true;
        }
    }
}
