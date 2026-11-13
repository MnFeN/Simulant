using Simulant.Core.Entity;
using Simulant.Game;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulant.Simulation.Runtime
{
    public sealed partial class SimEntityManager
    {
        private sealed class TemporaryName
        {
            internal Character Actor;
            internal byte[] Original;
            internal byte[] Applied;
        }

        private readonly List<TemporaryName> _names = new List<TemporaryName>();

        /// <summary>临时修改借用玩家的名字；手动停止或结束模拟时恢复，机制失败留场时继续保留。</summary>
        internal void SetTemporaryName(SimActor actor, string name)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("临时名字不能为空。", nameof(name));
            var applied = new byte[64];
            var encoded = PacketCodec.EncodeActorName(name);
            Array.Copy(encoded, applied, encoded.Length);
            lock (_gate)
            {
                EnsureAcceptingEntities();
                if (!actor.IsActive || !_actors.TryGetValue(actor.Entity.Identity, out var current) || !ReferenceEquals(current, actor))
                    throw new InvalidOperationException("只能修改本会话活动角色的名字。");
                if (_names.Any(n => n.Actor.Identity == actor.Entity.Identity))
                    throw new InvalidOperationException("该角色已经有临时名字，请先完成恢复。");
                var owned = new TemporaryName { Actor = actor.Entity, Original = actor.Entity.ReadNameBytes(), Applied = applied };
                // 原生调用可能已经生效后抛错，因此先登记恢复责任。
                _names.Add(owned);
                actor.Entity.SetNameBytes(applied);
                if (!SameName(actor.Entity.ReadNameBytes(), applied))
                    throw new InvalidOperationException("原生改名后未读到预期名字，已保留恢复责任。");
            }
        }

        private static bool SameName(byte[] left, byte[] right)
        {
            for (var i = 0; i < 64; i++)
            {
                if (left[i] != right[i]) return false;
                if (left[i] == 0) return true;
            }
            return false;
        }

        private void RestoreNames(EntityBase actor = null)
        {
            lock (_gate)
            {
                List<Exception> errors = null;
                foreach (var owned in _names.Where(n => actor == null || n.Actor.Identity == actor.Identity).ToArray())
                {
                    try
                    {
                        // 世界、槽位或地址复用后不写旧快照；外部改名也不覆盖。
                        if (owned.Actor.IsAlive && SameName(owned.Actor.ReadNameBytes(), owned.Applied))
                        {
                            owned.Actor.SetNameBytes(owned.Original);
                            if (owned.Actor.IsAlive && !SameName(owned.Actor.ReadNameBytes(), owned.Original))
                                throw new InvalidOperationException("原生名字恢复后未读到原名，请重试停止模拟。");
                        }
                        _names.Remove(owned);
                    }
                    catch (Exception error)
                    {
                        if (errors == null) errors = new List<Exception>();
                        errors.Add(error);
                    }
                }
                if (errors != null) throw new AggregateException("部分玩家原名尚未恢复，已保留恢复责任。", errors);
            }
        }
    }
}
