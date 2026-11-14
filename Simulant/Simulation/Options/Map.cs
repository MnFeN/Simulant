using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Simulant.Simulation.Options
{
    public class Map<TKey> : IEnumerable<KeyValuePair<TKey, string>>
    {
        private readonly List<KeyValuePair<TKey, string>> _items = new List<KeyValuePair<TKey, string>>();
        private readonly Dictionary<TKey, int> _indexMap = new Dictionary<TKey, int>();

        public int Count => _items.Count;
        public IEnumerable<TKey> Keys => _items.Select(x => x.Key);
        public IEnumerable<string> Values => _items.Select(x => x.Value);

        public Map()
        {
        }

        public string this[TKey key]
        {
            get
            {
                if (!_indexMap.TryGetValue(key, out int index))
                    throw new KeyNotFoundException("Key not found: " + key);

                return _items[index].Value;
            }

            set
            {
                if (_indexMap.TryGetValue(key, out int index))
                {
                    _items[index] = new KeyValuePair<TKey, string>(key, value);
                    return;
                }

                _indexMap[key] = _items.Count;
                _items.Add(new KeyValuePair<TKey, string>(key, value));
            }
        }

        public bool ContainsKey(TKey key) => _indexMap.ContainsKey(key);
        public bool TryGetIndex(TKey key, out int index) => _indexMap.TryGetValue(key, out index);
        public bool TryGetValue(TKey key, out string value)
        {
            if (TryGetIndex(key, out int index))
            {
                value = _items[index].Value;
                return true;
            }

            value = null;
            return false;
        }

        public TKey GetKeyAt(int index)
        {
            if (index < 0 || index >= _items.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _items[index].Key;
        }

        public bool TryGetKeyAt(int index, out TKey key)
        {
            if (index >= 0 && index < _items.Count)
            {
                key = _items[index].Key;
                return true;
            }

            key = default(TKey);
            return false;
        }

        public IEnumerator<KeyValuePair<TKey, string>> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
