using Simulant.Core.Entity;
using Simulant.Game.FFCS.Client.Game;
using Simulant.Game.FFCS.Client.Game.Object;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulant.Simulation.Runtime
{
    public sealed partial class SimEntityManager
    {
        private sealed class OwnedStatus
        {
            internal Character Actor;
            internal Character Source;
            internal int Slot;
            internal ushort StatusId;
        }

        private readonly List<OwnedStatus> _statuses = new List<OwnedStatus>();

        /// <summary>在空状态槽中施加本会话的点名；重复调用只更新自己持有的同种状态。</summary>
        public void SetStatus(SimActor actor, ushort statusId, float seconds, ushort param, SimActor source)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (statusId == 0) throw new ArgumentOutOfRangeException(nameof(statusId), "状态编号不能为零。");
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(seconds), "状态持续时间必须为有限正数。");
            lock (_gate)
            {
                EnsureAcceptingEntities();
                EnsureStatusActor(actor);
                EnsureStatusActor(source);
                if (StatusManager.SetStatusFuncPtr == IntPtr.Zero || StatusManager.RemoveStatusFuncPtr == IntPtr.Zero)
                    throw new InvalidOperationException("状态设置或移除函数尚未定位，请重新初始化。");
                var owned = _statuses.FirstOrDefault(s => s.Actor.Identity == actor.Entity.Identity && s.StatusId == statusId);
                var statuses = actor.Entity.Native.StatusManager;
                if (owned != null)
                {
                    var current = statuses.GetStatus(owned.Slot);
                    if (current.StatusId.Get() == 0)
                    {
                        _statuses.Remove(owned);
                        owned = null;
                    }
                    else if (!MatchesStatus(current, owned))
                    {
                        _statuses.Remove(owned);
                        throw new InvalidOperationException("模拟状态槽已被其他状态替换，拒绝覆盖。");
                    }
                    else if (owned.Source.Identity != source.Entity.Identity)
                        throw new InvalidOperationException("同一模拟状态不能在仍生效时更换来源实体。");
                }
                if (owned == null)
                {
                    var slot = -1;
                    for (var i = 0; i < 30; i++)
                        if (statuses.GetStatus(i).StatusId.Get() == 0) { slot = i; break; }
                    if (slot < 0) throw new InvalidOperationException("角色前 30 个状态槽已满，无法添加模拟点名。");
                    owned = new OwnedStatus { Actor = actor.Entity, Source = source.Entity, Slot = slot, StatusId = statusId };
                    // 原生调用可能部分成功后抛错；写入之前即登记恢复责任。
                    _statuses.Add(owned);
                }
                // 返回值只表示编号或来源有变化；同一状态刷新时间和参数会正常返回 false。
                var identityChanged = statuses.SetStatus(owned.Slot, statusId, seconds, param,
                    new GameObjectId { ObjectId = source.Id }, true);
                var actual = actor.Entity.Native.StatusManager.GetStatus(owned.Slot);
                var actualId = actual.StatusId.Get();
                var actualSource = actual.SourceObject.Get().Id;
                var actualParam = actual.Param.Get();
                if (actualId != statusId || actualSource != source.Id || actualParam != param)
                    throw new InvalidOperationException("原生状态设置后未读到预期点名，已保留清理责任。" +
                        $"目标 0x{actor.Id:X8}，槽 {owned.Slot}；" +
                        $"期望编号 0x{statusId:X4}、参数 {param}、来源 0x{source.Id:X16}、时间 {seconds:F3} 秒；" +
                        $"实际编号 0x{actualId:X4}、参数 {actualParam}、来源 0x{actualSource:X16}、剩余 {actual.RemainingTime.Get():F3} 秒；" +
                        $"原生编号或来源变化标志：{(identityChanged ? "是" : "否")}。");
            }
        }

        /// <summary>只移除本会话施加的同种状态，不触碰原有或后来替换的状态。</summary>
        public void RemoveStatus(SimActor actor, ushort statusId)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            lock (_gate)
                foreach (var owned in _statuses.Where(s => s.Actor.Identity == actor.Entity.Identity && s.StatusId == statusId).ToArray())
                    RemoveOwnedStatus(owned);
        }

        private void EnsureStatusActor(SimActor actor)
        {
            if (!actor.IsActive || !_actors.TryGetValue(actor.Entity.Identity, out var current) || !ReferenceEquals(current, actor))
                throw new InvalidOperationException("状态的目标和来源必须是本会话的活动角色。");
        }

        private static bool MatchesStatus(Status status, OwnedStatus owned)
            => status.StatusId.Get() == owned.StatusId && status.SourceObject.Get().Id == owned.Source.Id;

        private void RemoveOwnedStatus(OwnedStatus owned)
        {
            // 实体身份包含世界与生命周期；失效后绝不触碰被复用的地址。
            if (!owned.Actor.IsAlive) { _statuses.Remove(owned); return; }
            var statuses = owned.Actor.Native.StatusManager;
            var current = statuses.GetStatus(owned.Slot);
            if (!MatchesStatus(current, owned)) { _statuses.Remove(owned); return; }
            if (StatusManager.RemoveStatusFuncPtr == IntPtr.Zero)
                throw new InvalidOperationException("状态移除函数尚未定位，已保留模拟点名以便重试清理。");
            statuses.RemoveStatus(owned.Slot);
            if (!owned.Actor.IsAlive) { _statuses.Remove(owned); return; }
            if (MatchesStatus(owned.Actor.Native.StatusManager.GetStatus(owned.Slot), owned))
                throw new InvalidOperationException("原生状态移除后模拟点名仍存在，请重试停止。");
            _statuses.Remove(owned);
        }

        private void ClearStatuses(EntityBase dependency = null)
        {
            lock (_gate)
            {
                List<Exception> errors = null;
                foreach (var owned in _statuses.Where(s => dependency == null ||
                    s.Actor.Identity == dependency.Identity || s.Source.Identity == dependency.Identity).ToArray())
                {
                    try { RemoveOwnedStatus(owned); }
                    catch (Exception error)
                    {
                        if (errors == null) errors = new List<Exception>();
                        errors.Add(error);
                    }
                }
                if (errors != null) throw new AggregateException("部分模拟点名清理失败，已保留实体与清理责任。", errors);
            }
        }

        private bool HasStatusDependency(EntityBase entity)
            => _statuses.Any(s => s.Actor.Identity == entity.Identity || s.Source.Identity == entity.Identity);
    }
}
