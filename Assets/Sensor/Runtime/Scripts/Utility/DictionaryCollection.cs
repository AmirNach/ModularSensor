using System;
using System.Collections.Generic;

namespace SensorSystem
{
    /// <summary>A dictionary that reports added and removed items through callbacks.</summary>
    public class DictionaryCollection<TKey, TValue>
    {
        private readonly Action<KeyValuePair<TKey, TValue>> _onAddedItem;
        private readonly Action<KeyValuePair<TKey, TValue>> _onRemovedItem;

        protected Dictionary<TKey, TValue> Collection { get; } = new Dictionary<TKey, TValue>();

        public int Count => Collection.Count;
        public IEnumerable<TValue> Values => Collection.Values;

        public DictionaryCollection(Action<KeyValuePair<TKey, TValue>> onAddedItem, Action<KeyValuePair<TKey, TValue>> onRemovedItem)
        {
            _onAddedItem = onAddedItem;
            _onRemovedItem = onRemovedItem;
        }

        public bool ContainsKey(TKey key) => Collection.ContainsKey(key);
        public bool TryGetValue(TKey key, out TValue value) => Collection.TryGetValue(key, out value);

        public virtual void AddItem(KeyValuePair<TKey, TValue> item)
        {
            Collection.Add(item.Key, item.Value);
            _onAddedItem?.Invoke(item);
        }

        public virtual void RemoveItem(KeyValuePair<TKey, TValue> item)
        {
            if (Collection.Remove(item.Key))
            {
                _onRemovedItem?.Invoke(item);
            }
        }
    }
}
