using System.Collections.Generic;

namespace SensorSystem
{
    public abstract class AbstractObjectCollection<T> : IAllObjectsCollection where T : class
    {
        public HashSet<IDetectable> AllObjects { get; protected set; } = new HashSet<IDetectable>();

        protected int LayerMask { get; private set; }

        protected AbstractObjectCollection(int layerMask)
        {
            LayerMask = layerMask;
            Init();
            SetEnable(true);
        }

        public virtual void SetEnable(bool isEnabled) { }

        protected virtual void Init() { }

        protected static bool IsLayerInLayerMask(int targetLayerMask, int layer)
        {
            return (targetLayerMask & (1 << layer)) != 0;
        }
    }
}
