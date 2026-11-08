using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// Optional. Tells the default object collection which layer to test against the sensor's layer mask.
    /// Without it, a Unity component uses its GameObject's layer, and anything else is always included.
    /// </summary>
    public interface IDetectableLayer
    {
        int DetectionLayer { get; }
    }

    /// <summary>
    /// Optional. The instance ID of a collider that belongs to this target, or 0 for none. A ray that hits it is known to have
    /// reached the target without asking the engine for the hit collider and its transform.
    /// </summary>
    public interface IDetectableCollider
    {
        int ColliderInstanceId { get; }
    }

    /// <summary>
    /// Every detectable object that is currently alive, kept up to date by the objects themselves.
    /// DetectableEntity registers in OnEnable and unregisters in OnDisable. Anything else that implements IDetectable
    /// (for example wrappers around objects from another program) calls Register and Unregister the same way.
    /// The default sensor collection reads this list, so it no longer has to search the whole scene.
    /// </summary>
    public static class DetectableRegistry
    {
        private static readonly HashSet<IDetectable> Registered = new HashSet<IDetectable>();

        /// <summary>
        /// Also look through every collider in the scene for IDetectable components that never registered, as sensors used to do.
        /// Slow with many colliders, so it is off by default. Only needed for IDetectable components that do not call Register.
        /// </summary>
        public static bool IncludeUnregisteredSceneObjects;

        /// <summary>Goes up whenever something is registered or unregistered.</summary>
        public static int Version { get; private set; }

        public static int Count => Registered.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Registered.Clear();
            IncludeUnregisteredSceneObjects = false;
            Version = 0;
        }

        /// <summary>Adds a detectable. Returns false if it was already there. Does nothing for null.</summary>
        public static bool Register(IDetectable detectable)
        {
            if (detectable == null || !Registered.Add(detectable)) return false;

            Version++;
            return true;
        }

        /// <summary>Removes a detectable. Returns false if it was not there.</summary>
        public static bool Unregister(IDetectable detectable)
        {
            if (detectable == null || !Registered.Remove(detectable)) return false;

            Version++;
            return true;
        }

        public static bool IsRegistered(IDetectable detectable) => detectable != null && Registered.Contains(detectable);

        /// <summary>
        /// Adds every registered detectable that is alive and on one of the layers of the mask to the set.
        /// The set is not cleared first and nothing is allocated.
        /// </summary>
        public static void CollectInto(HashSet<IDetectable> result, int layerMask)
        {
            foreach (IDetectable detectable in Registered)
            {
                if (!detectable.IsAlive()) continue;

                if (TryGetLayer(detectable, out int layer) && (layerMask & (1 << layer)) == 0) continue;

                result.Add(detectable);
            }
        }

        private static bool TryGetLayer(IDetectable detectable, out int layer)
        {
            if (detectable is IDetectableLayer layered)
            {
                layer = layered.DetectionLayer;
                return layer >= 0 && layer < 32;
            }
            if (detectable is Component component)
            {
                layer = component.gameObject.layer;
                return true;
            }

            layer = 0;
            return false;
        }
    }
}
