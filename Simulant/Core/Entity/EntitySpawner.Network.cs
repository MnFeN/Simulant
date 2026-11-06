using Simulant.Game;
using Simulant.Game.FFCS.Client.Game.Character;
using Simulant.Game.FFCS.Client.Game.Object;
using Simulant.Game.FFCS.Client.Network;
using System;
using System.Collections.Generic;

namespace Simulant.Core.Entity
{
    /// <summary>一次网络角色生成的预留记录。原生角色可能在发包后的若干帧才出现。</summary>
    internal sealed class NetworkCharacterSpawnRequest
    {
        internal NetworkCharacterSpawnRequest(EntitySpawner owner, long generation, byte index, uint entityId,
            ObjectKind objectKind, bool targetable)
        {
            Owner = owner;
            Generation = generation;
            Index = index;
            EntityId = entityId;
            ObjectKind = objectKind;
            Targetable = targetable;
        }

        public uint EntityId { get; }
        public byte Index { get; }
        internal EntitySpawner Owner { get; }
        internal long Generation { get; }
        internal ObjectKind ObjectKind { get; }
        internal bool Targetable { get; }
        internal Character Entity { get; set; }
        internal bool CancellationRequested { get; set; }
        internal bool Released { get; set; }
    }

    public sealed partial class EntitySpawner
    {
        private const int NetworkCharacterCapacity = 100;
        private readonly Dictionary<byte, NetworkCharacterSpawnRequest> _networkSpawns =
            new Dictionary<byte, NetworkCharacterSpawnRequest>();
        private long _networkWorldGeneration = 1;

        internal void BeginSpawnNetworkBNpc(NetworkNpcSpawnSpec spec, out NetworkCharacterSpawnRequest request)
        {
            request = null;
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            BeginSpawnNetworkCharacter(ObjectKind.BattleNpc, spec.Targetable,
                spec.ToPacket, PacketDispatcher.HandleSpawnNpcPacket, out request);
        }

        internal void BeginSpawnNetworkPlayer(NetworkPlayerSpawnSpec spec, out NetworkCharacterSpawnRequest request)
        {
            request = null;
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            BeginSpawnNetworkCharacter(ObjectKind.Pc, spec.Targetable, index =>
            {
                var source = spec.AppearanceSource ?? _provider.GetMyself()
                    ?? throw new InvalidOperationException("未找到用于复制外观的玩家角色。");
                return spec.ToPacket(index, PlayerAppearanceSnapshot.Capture(source));
            }, PacketDispatcher.HandleSpawnPlayerPacket, out request);
        }

        private void BeginSpawnNetworkCharacter<TPacket>(ObjectKind objectKind,
            bool targetable, Func<byte, TPacket> buildPacket, Action<uint, TPacket> dispatchPacket,
            out NetworkCharacterSpawnRequest reservedRequest)
        {
            reservedRequest = null;
            lock (_gate)
            {
                EnsureWorldReady();
                if (_networkSpawnAllowed == null || !_networkSpawnAllowed())
                    throw new InvalidOperationException("网络实体模拟需要先开启防火墙。");
                EnsureNetworkDespawnFunction();
                var spawnFunction = objectKind == ObjectKind.Pc
                    ? PacketDispatcher.HandleSpawnPlayerPacketFuncPtr
                    : PacketDispatcher.HandleSpawnNpcPacketFuncPtr;
                if (spawnFunction == IntPtr.Zero)
                    throw new InvalidOperationException("对应的网络角色生成函数尚未定位。");
                if (PacketDispatcher.HandleActorControlFuncPtr == IntPtr.Zero)
                    throw new InvalidOperationException("角色控制函数尚未定位，无法初始化网络角色的可选中状态。");
                var manager = CharacterManager.Instance();
                var objects = GameObjectManager.Instance().Objects.IndexSorted;
                var index = FindFreeNetworkIndex(manager, objects);
                var entityId = NextAvailableId(manager, objects, objectKind == ObjectKind.Pc);
                var packet = buildPacket(index);
                // 构造包和扫描 ID 期间游戏仍在运行，发包前再确认选中的两个槽仍为空。
                if (manager.GetBattleCharaPtr(index) != IntPtr.Zero ||
                    !objects[index * 2].IsNull() || !objects[index * 2 + 1].IsNull())
                    throw new InvalidOperationException("预留的网络角色槽位已经被占用，本次未发送生成包。");
                var request = new NetworkCharacterSpawnRequest(this, _networkWorldGeneration, index, entityId,
                    objectKind, targetable);
                // 必须在发包前预留；生成包按槽排队，不能在等待期间用另一种路径占用同槽。
                _networkSpawns.Add(index, request);
                // 即使发包及回滚都失败，调用会话也必须能拿到请求并继续承担回收责任。
                reservedRequest = request;
                try
                {
                    dispatchPacket(entityId, packet);
                }
                catch (Exception spawnError)
                {
                    try { CancelNetworkCore(request); }
                    catch (Exception cleanupError)
                    {
                        LogCleanupError("网络角色生成失败后取消失败：", cleanupError);
                        throw new AggregateException("网络角色生成及取消均失败，清理记录已保留。", spawnError, cleanupError);
                    }
                    throw;
                }
            }
        }

        internal Character TryCompleteNetworkCharacter(NetworkCharacterSpawnRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            lock (_gate)
            {
                EnsureWorldReady();
                ValidateNetworkRequestOwner(request);
                if (!IsCurrentNetworkRequest(request) || request.CancellationRequested)
                    throw new InvalidOperationException("网络角色生成请求已取消或不属于当前世界。");
                EnsureNetworkTables();
                var manager = CharacterManager.Instance();
                var objects = GameObjectManager.Instance().Objects.IndexSorted;
                var pointer = manager.GetBattleCharaPtr(request.Index);
                var objectSlot = (ushort)(request.Index * 2);
                var indexed = objects[objectSlot];
                ValidateNetworkOccupant(request, pointer);
                ValidateNetworkOccupant(request, indexed.Ptr);
                if (pointer == IntPtr.Zero || indexed.IsNull())
                {
                    if (pointer == IntPtr.Zero && indexed.IsNull() && !objects[objectSlot + 1].IsNull())
                        throw new InvalidOperationException($"网络角色槽位 {request.Index} 的附属槽被其他对象占用。");
                    return null;
                }
                if (pointer != indexed.Ptr)
                    throw new InvalidOperationException($"网络角色槽位 {request.Index} 的两个对象表指针不一致。");
                if (request.Entity != null)
                {
                    if (!_registry.MatchesCurrent(request.Entity))
                        throw new InvalidOperationException("网络角色已经失效，不能重新登记旧生成请求。");
                    return request.Entity;
                }
                var entity = _registry.RegisterCreated(objectSlot) as Character;
                if (entity == null || entity.Address != pointer || entity.Id != request.EntityId)
                    throw new InvalidOperationException("网络角色登记失败：对象表中的角色与生成请求不匹配。");
                request.Entity = entity;
                _owned.Add(entity.Identity, new OwnedEntity
                {
                    Entity = entity,
                    NetworkRequest = request
                });
                // 先登记回收责任，再初始化；控制调用失败时，取消请求仍能回收本次实体。
                try
                {
                    // AnoMech 1646a706 的 SimPartyNpc.OnPacketSpawned 用 0xC8 完成玩家入场。
                    if (request.ObjectKind == ObjectKind.Pc) entity.ActorControl(0xC8);
                    entity.SetTargetable(request.Targetable);
                }
                catch
                {
                    request.CancellationRequested = true;
                    throw;
                }
                return entity;
            }
        }

        internal void CancelNetworkCharacter(NetworkCharacterSpawnRequest request)
        {
            if (request == null) return;
            lock (_gate)
            {
                ValidateNetworkRequestOwner(request);
                CancelNetworkCore(request);
            }
        }

        private void CancelNetworkCore(NetworkCharacterSpawnRequest request)
        {
            // 换图后的请求只作废，绝不读取或销毁新世界同槽的对象。
            if (!IsCurrentNetworkRequest(request)) return;
            request.CancellationRequested = true;
            EnsureNetworkDespawnFunction();
            var manager = CharacterManager.Instance();
            var objects = GameObjectManager.Instance().Objects.IndexSorted;
            var objectSlot = request.Index * 2;
            var pointer = manager.GetBattleCharaPtr(request.Index);
            var indexed = objects[objectSlot];
            ValidateNetworkOccupant(request, pointer);
            ValidateNetworkOccupant(request, indexed.Ptr);
            if (pointer != IntPtr.Zero && indexed.Ptr != IntPtr.Zero && pointer != indexed.Ptr)
                throw new InvalidOperationException($"无法回收网络角色：槽位 {request.Index} 的两个对象表指针不一致。");
            if (pointer == IntPtr.Zero && indexed.IsNull() && !objects[objectSlot + 1].IsNull())
                throw new InvalidOperationException($"无法回收网络角色：槽位 {request.Index} 只剩无法确认归属的附属对象。");
            if ((pointer != IntPtr.Zero || !indexed.IsNull()) && request.Entity != null &&
                !_registry.MatchesCurrent(request.Entity))
                throw new InvalidOperationException($"无法回收网络角色：槽位 {request.Index} 的生命周期已变化，清理记录已保留。");
            // 空槽也要发取消包，否则尚未消费的生成包仍会在后续帧创建角色。
            if (request.Entity != null) _registry.Invalidate(request.Entity);
            PacketDispatcher.HandleDespawnCharacterPacket(request.Index);
            if (manager.GetBattleCharaPtr(request.Index) != IntPtr.Zero ||
                !objects[objectSlot].IsNull() || !objects[objectSlot + 1].IsNull())
                throw new InvalidOperationException($"网络角色槽位 {request.Index} 尚未清空，保留记录以便重试回收。");
            if (request.Entity != null) _owned.Remove(request.Entity.Identity);
            _networkSpawns.Remove(request.Index);
            request.Released = true;
        }

        private byte FindFreeNetworkIndex(CharacterManager manager, GameObjectPtrs objects)
        {
            // 0 是自身玩家的网络槽，不能用于模拟角色。
            for (byte index = 1; index < NetworkCharacterCapacity; index++)
            {
                if (_networkSpawns.ContainsKey(index) || manager.GetBattleCharaPtr(index) != IntPtr.Zero)
                    continue;
                if (objects[index * 2].IsNull() && objects[index * 2 + 1].IsNull()) return index;
            }
            throw new InvalidOperationException("没有可用的网络角色槽位，不能生成模拟角色。");
        }

        private void ValidateNetworkOccupant(NetworkCharacterSpawnRequest request, IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return;
            var native = pointer.As<GameObject>();
            if (native.EntityId != request.EntityId || native.ObjectKind != request.ObjectKind ||
                native.ObjectIndex != request.Index * 2 ||
                (request.Entity != null && request.Entity.Address != pointer))
                throw new InvalidOperationException($"网络角色槽位 {request.Index} 已被其他实体占用，不能按槽位删除；清理记录已保留。");
        }

        private bool IsCurrentNetworkRequest(NetworkCharacterSpawnRequest request)
            => !request.Released && request.Generation == _networkWorldGeneration &&
                _networkSpawns.TryGetValue(request.Index, out var current) && ReferenceEquals(current, request);

        private void ValidateNetworkRequestOwner(NetworkCharacterSpawnRequest request)
        {
            if (!ReferenceEquals(request.Owner, this))
                throw new ArgumentException("网络角色生成请求属于另一个资源管理器。", nameof(request));
        }

        private static void EnsureNetworkTables()
        {
            if (CharacterManager.InstancePtr == IntPtr.Zero || GameObjectManager.InstancePtr == IntPtr.Zero)
                throw new InvalidOperationException("网络角色对象表尚未初始化。");
        }

        private static void EnsureNetworkDespawnFunction()
        {
            EnsureNetworkTables();
            if (PacketDispatcher.HandleDespawnCharacterPacketFuncPtr == IntPtr.Zero)
                throw new InvalidOperationException("网络角色回收函数尚未定位。");
        }

    }
}
