using Simulant.Core;
using Simulant.Core.Entity;
using Simulant.Game;
using Simulant.Simulation.Timeline;
using System;
using System.Collections.Generic;
using System.Numerics;
using Job = Simulant.Game.FFCS.Client.Game.Character.Job;

namespace Simulant.Simulation.Runtime
{
    public sealed class SimEntityManager
    {
        private readonly EntitySpawner _spawner;
        private readonly Action<Exception> _onCleanupError;
        private readonly List<EntityBase> _localEntities = new List<EntityBase>();
        internal List<Character> Party = new List<Character>();
        internal SimScheduler Scheduler { get; set; }

        public SimEntityManager(EntitySpawner spawner, Action<Exception> onCleanupError = null)
        { 
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _onCleanupError = onCleanupError;
        }

        public Character SpawnPlayer(Job job)
            => Track(_spawner.SpawnPlayer(job));

        public Character SpawnBNpc(uint bNpcBaseId, uint bNpcNameId = 0, byte level = 0)
            => Track(_spawner.SpawnBNpc(bNpcBaseId, bNpcNameId, level));

        public EventObject SpawnEObj(ushort index, uint baseId, Vector3 pos, float heading = 0)
            => Track(_spawner.SpawnEObj(index, baseId, pos, heading));

        public T Track<T>(T entity) where T : EntityBase
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity is Character character)
                character.Scheduler = Scheduler;
            lock (_localEntities)
            {
                if (!_localEntities.Contains(entity))
                    _localEntities.Add(entity);
            }
            return entity;
        }

        public void Delete(EntityBase entity)
        {
            lock (_localEntities)
            {
                // 已清理的回收节点不能再操作被游戏复用的地址。
                if (!_localEntities.Contains(entity)) return;
                if (entity is EventObject)
                {
                    // EventObject 不属于 ClientObjectManager。现有封装没有销毁接口，
                    // 停止绘制，槽位由后续 SpawnObject 包或切换区域复用。
                    entity.DisableDraw();
                }
                else
                {
                    _spawner.Delete(entity);
                }
                _localEntities.Remove(entity);
            }
        }

        public void Clear()
        {
            lock (_localEntities)
            {
                foreach (var entity in _localEntities.ToArray())
                {
                    try
                    {
                        Delete(entity);
                    }
                    catch (Exception ex)
                    {
                        _onCleanupError?.Invoke(ex);
                    }
                }
                _localEntities.Clear();
            }
            Party.Clear();
        }

        internal Character GetPartyMember(int partyIndex)
        { 
            if (partyIndex < 1 || partyIndex > Party.Count)
                throw new ArgumentOutOfRangeException(nameof(partyIndex), $"小队成员索引必须在范围内（1-{Party.Count}）。");

            return Party[partyIndex - 1];
        }
    }
}
