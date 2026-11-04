using System;
using System.Collections.Generic;

namespace SensorSystem
{
    /// <summary>Shares object collections between sensors that use the same collection type and layer mask.</summary>
    public class ObjectsProvider : SingletonMonoBehaviour<ObjectsProvider>
    {
        public Dictionary<(Type type, int layerMask), IAllObjectsCollection> ObjectsCollections { get; } = new Dictionary<(Type type, int layerMask), IAllObjectsCollection>();

        public IAllObjectsCollection CreateNewCollection<T>(int layerMask) where T : AbstractObjectCollection<T>
        {
            var key = (typeof(T), layerMask);
            if (!ObjectsCollections.TryGetValue(key, out IAllObjectsCollection collection))
            {
                collection = (T)Activator.CreateInstance(typeof(T), layerMask);
                ObjectsCollections.Add(key, collection);
            }
            return collection;
        }

        public void SetEnable(Type typeOfElements, bool isEnabled)
        {
            foreach (KeyValuePair<(Type type, int layerMask), IAllObjectsCollection> pair in ObjectsCollections)
            {
                if (pair.Key.type == typeOfElements)
                {
                    pair.Value.SetEnable(isEnabled);
                }
            }
        }

        private void OnDestroy()
        {
            foreach (IAllObjectsCollection collection in ObjectsCollections.Values)
            {
                collection.SetEnable(false);
            }
        }
    }
}
