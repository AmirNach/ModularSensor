using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>Hands out unique IDs from a range. Released IDs become available again.</summary>
    public class LocalUniqueIdGenerator
    {
        public const int NoId = -1;

        private readonly int _max;
        private readonly bool _shuffle;
        private readonly System.Random _random = new System.Random();
        private readonly List<int> _shuffledPool = new List<int>();
        private readonly Queue<int> _released = new Queue<int>();
        private int _next;

        public LocalUniqueIdGenerator(int min, int max, bool shuffle)
        {
            if (max < min)
            {
                (min, max) = (max, min);
            }

            _next = min;
            _max = max;
            _shuffle = shuffle;

            if (_shuffle)
            {
                for (int id = min; id <= max; id++)
                {
                    _shuffledPool.Add(id);
                }
                for (int i = _shuffledPool.Count - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    (_shuffledPool[i], _shuffledPool[j]) = (_shuffledPool[j], _shuffledPool[i]);
                }
            }
        }

        public int GetAvailableId()
        {
            if (_shuffle && _shuffledPool.Count > 0)
            {
                int last = _shuffledPool.Count - 1;
                int id = _shuffledPool[last];
                _shuffledPool.RemoveAt(last);
                return id;
            }

            if (!_shuffle && _next <= _max)
            {
                return _next++;
            }

            if (_released.Count > 0)
            {
                return _released.Dequeue();
            }

            Debug.LogError("LocalUniqueIdGenerator: all detection IDs are in use. Widen the ID range in the settings asset.");
            return NoId;
        }

        public void Release(int id)
        {
            if (id != NoId)
            {
                _released.Enqueue(id);
            }
        }
    }
}
