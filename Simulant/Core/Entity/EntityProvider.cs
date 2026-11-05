using Simulant.Game.FFCS.Client.Game.Object;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulant.Core.Entity
{
    public sealed class EntityProvider
    {
        private readonly EntityRegistry _registry;
        private readonly Func<uint> _currentPlayerId;
        public EntityProvider(EntityRegistry registry, Func<uint> currentPlayerId)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _currentPlayerId = currentPlayerId ?? throw new ArgumentNullException(nameof(currentPlayerId));
        }

        public Character GetMyself()
        {
            var playerId = _currentPlayerId();
            if (playerId == 0 || playerId == 0xE0000000) return null;
            // 常见槽位走快路径，但始终以宿主提供的本地玩家 ID 确认身份。
            var character = _registry.Get(0) as Character;
            if (character?.ObjectKind == ObjectKind.Pc && character.Id == playerId) return character;
            return GetCharacters().FirstOrDefault(e => e.ObjectKind == ObjectKind.Pc && e.Id == playerId);
        }

        public IEnumerable<Character> GetCharacters() => GetEntities().OfType<Character>();
        public IEnumerable<EventObject> GetEventObjs() => GetEntities().OfType<EventObject>();
        public IEnumerable<EntityBase> GetEntities() => _registry.GetEntities();
    }
}
