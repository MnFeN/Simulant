using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Network;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Game.FFCS.Client.Network;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;

namespace Simulant.Core.Entity
{
    public sealed class EntitySpawner : IDisposable
    {
        private readonly PluginHost _host;

        public EntitySpawner(PluginHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        private readonly ConcurrentDictionary<IntPtr, Tuple<Character, ushort>> _localEntityIndexes =
            new ConcurrentDictionary<IntPtr, Tuple<Character, ushort>>();
        private readonly object _schedulerGate = new object();
        private SimScheduler _standaloneScheduler;
        private bool _disposed;

        // 调试工具等非会话调用也共用一个 worker，绝不为每次读条创建线程或 Task。
        internal SimScheduler StandaloneScheduler
        {
            get
            {
                SimScheduler retired = null;
                SimScheduler scheduler;
                lock (_schedulerGate)
                {
                    if (_disposed) throw new ObjectDisposedException(nameof(EntitySpawner));
                    if (_standaloneScheduler == null || !_standaloneScheduler.IsRunning)
                    {
                        retired = _standaloneScheduler;
                        _standaloneScheduler = new SimScheduler((ex, node) =>
                            _host.LogError($"实体延迟动作「{node}」失败：{ex}"));
                        _standaloneScheduler.Start();
                    }
                    scheduler = _standaloneScheduler;
                }
                retired?.Dispose();
                return scheduler;
            }
        }

        public void Dispose()
        {
            SimScheduler scheduler;
            lock (_schedulerGate)
            {
                _disposed = true;
                scheduler = _standaloneScheduler;
            }
            scheduler?.Dispose();
        }

        public Character CreateBattleCharacter()
        {
            var createdIndex = ClientObjectManager.Instance.CreateBattleCharacter();
            if (createdIndex > ushort.MaxValue)
                throw new InvalidOperationException($"生成实体失败：返回的实体索引 #{createdIndex} 超出 ushort 范围。");

            var native = ClientObjectManager.Instance.GetObjectByIndex((ushort)createdIndex);
            if (native.IsNull())
                throw new InvalidOperationException($"生成实体失败：返回的实体索引 #{createdIndex} 对应空指针。");

            var character = new Character(native, _host);
            lock (_localEntityIndexes)
                _localEntityIndexes[character.Address] = Tuple.Create(character, (ushort)createdIndex);

            return character;
        }

        private const CharacterSetupContainer.CopyFlags DefaultPlayerCopyFlags =
            CharacterSetupContainer.CopyFlags.ClassJob |
            CharacterSetupContainer.CopyFlags.Position |
            CharacterSetupContainer.CopyFlags.Name;

        public Character SpawnPlayer(
            Job job,
            Character source = null,
            CharacterSetupContainer.CopyFlags copyFlags = DefaultPlayerCopyFlags)
        {
            if (source != null && source.Native.ObjectKind != ObjectKind.Pc)
                throw new ArgumentException("实体源必须为玩家角色", nameof(source));

            source = source ?? _host.EntityProvider.GetMyself()
                ?? throw new ArgumentException("未找到自身实体", nameof(source));

            var player = CreateBattleCharacter();
            try
            {
                player.Native.CharacterSetup.CopyFromCharacter(source.Native, copyFlags);
                player.Native.ObjectKind.Set(ObjectKind.Pc);
                player.Id = GetNextPlayerId();
                player.Job = job;
                return player;
            }
            catch
            {
                DeleteAfterFailedSetup(player);
                throw;
            }
        }

        public Character SpawnBNpc(uint bNpcBaseId, uint bNpcNameId, byte level)
        {
            var bnpc = CreateBattleCharacter();
            try
            {
                bnpc.Native.CharacterSetup.SetupBNpc(bNpcBaseId, bNpcNameId);
                bnpc.Native.ObjectKind.Set(ObjectKind.BattleNpc);
                bnpc.Id = GetNextNonPlayerId();
                bnpc.Level = level;
                bnpc.Native.Battalion.Set(4);
                bnpc.Native.IsHostile.Set(true);
                bnpc.Native.InCombat.Set(true);
                return bnpc;
            }
            catch
            {
                DeleteAfterFailedSetup(bnpc);
                throw;
            }
        }

        private void DeleteAfterFailedSetup(Character character)
        {
            try { Delete(character); }
            catch (Exception ex) { _host.LogError("实体初始化失败后回收失败：" + ex); }
        }

        private static readonly object _idLock = new object();
        private static ushort _currentPlayerIdIndex = 0;
        private static ushort _currentNonPlayerIdIndex = 0;

        private uint GetNextPlayerId()
        {
            lock (_idLock)
            {
                var newIndex = unchecked(++_currentPlayerIdIndex);
                return 0x10FF0000u + newIndex;
            }
        }

        private uint GetNextNonPlayerId()
        {
            lock (_idLock)
            {
                var newIndex = unchecked(++_currentNonPlayerIdIndex);
                return 0x40FF0000u + newIndex;
            }
        }

        public void Delete(EntityBase entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (entity.Native.IsNull())
                throw new ArgumentException("实体的原生对象为空", nameof(entity));

            lock (_localEntityIndexes)
            {
                if (!_localEntityIndexes.TryGetValue(entity.Address, out var entry) ||
                    !ReferenceEquals(entry.Item1, entity))
                    return;

                entry.Item1.Invalidate();
                ClientObjectManager.Instance.DeleteObjectByIndex(entry.Item2, 0);
                _localEntityIndexes.TryRemove(entity.Address, out _);
            }
        }

        public EventObject SpawnEObj(int index, uint baseId, Vector3 pos, float heading, ushort animation = 0)
        {
            var data = new EObjData
            {
                Index = (byte)index,
                BaseId = baseId,
                Pos = pos,
                Heading = heading,
                SharedTimelineState = animation
            };
            return SpawnEObj(data);
        }

        public EventObject SpawnEObj(EObjData data)
        {
            if (data.Index >= 40)
                throw new ArgumentOutOfRangeException(nameof(data), $"EventObject 实体索引必须为 0-39，实际传入 {data.Index}。");
            var packet = (SpawnObjectPacket)data;
            packet.EntityId = GetNextNonPlayerId();
            _ = PacketDispatcher.HandleSpawnObjectPacket(packet.EntityId, packet);

            // 可以用 Index 优化，避免遍历查找
            return _host.EntityProvider.GetEventObjs().FirstOrDefault(e => e.Native.EntityId == packet.EntityId) 
                ?? throw new InvalidOperationException($"生成实体失败：未找到 EntityId={packet.EntityId:X8} 的 EObj 实体。");
        }
    }

    public class EObjData
    {
        /// <summary>
        /// EventObject 只有 40 个槽位，故最大为 39。
        /// </summary>
        public byte Index;

        /// <summary>
        /// 0x2 为 1 时不可见；至少也用到了 0x1 和 0x4，但暂时没观察到实际影响
        /// </summary>
        public byte TargetableFlags = 0x1 | 0x4;
        public bool Hidden = false;
        public uint BaseId;

        /// <summary>
        /// 似乎是绑定的场景实例 ID，缺少此值至少不影响 SetSharedTimelineState 动画
        /// </summary>
        public uint LayoutId;

        /// <summary>
        /// 似乎是 InstanceContentId，缺少此值至少不影响 SetSharedTimelineState 动画
        /// </summary>
        public uint EventId;

        public uint GimmickId;

        /// <summary>
        /// 影响生成实体的缩放比例，不是实际尺寸
        /// </summary>
        public float Scale = 1f;

        public ushort FateId;

        public byte EventState;

        /// <summary> 
        /// 对于使用 SetSharedTimelineState 控制动画的实体，代表生成时显现的动画。
        /// </summary>
        public ushort SharedTimelineState;
        public uint SharedGroupState;
        public Vector3 Pos;
        public float Heading;

        public static explicit operator SpawnObjectPacket(EObjData data)
        {
            return new SpawnObjectPacket
            {
                ObjectIndex = data.Index,
                ObjectKind = (byte)ObjectKind.EventObj,
                TargetableStatus = data.TargetableFlags,
                Visibility = (byte)(data.Hidden ? 1 : 0),
                BaseId = data.BaseId,
                LayoutId = data.LayoutId,
                EventId = data.EventId,
                OwnerId = 0xE0000000,
                GimmickId = data.GimmickId,
                Radius = data.Scale,
                Rotation = PacketCodec.EncodeUShortHeading(data.Heading),
                FateId = data.FateId,
                EventState = data.EventState,
                SharedTimelineState = data.SharedTimelineState,
                SharedGroupState = data.SharedGroupState,
                PositionX = data.Pos.X,
                PositionY = data.Pos.Z, // flip
                PositionZ = data.Pos.Y
            };
        }
    }
}
