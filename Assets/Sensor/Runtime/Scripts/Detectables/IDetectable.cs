using System;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>Anything a Sensor can detect.</summary>
    public interface IDetectable
    {
        Bounds Bounds { get; set; }
        bool IsFake { get; }

        /// <summary>
        /// False once the thing this stands for is gone. Unity objects that were destroyed are already treated as gone.
        /// Override it when your IDetectable wraps something else that can disappear.
        /// </summary>
        bool IsValid => !this.IsDestroyed();

        /// <summary>
        /// A stable ID, unique among everything the sensor can see. The sensor tracks targets by this value, so the same
        /// object can come back as a new IDetectable instance. Return a cached string, since it is read on every refresh.
        /// </summary>
        string GetIdentifier();
        Vector3 GetCenterPosition();
        string GetDetectionType();
        float GetVelocity();
    }

    public static class DetectableExtensions
    {
        public static void SetBoundsCenter(this IDetectable detectable)
        {
            Bounds bounds = detectable.Bounds;
            bounds.center = detectable.GetCenterPosition();
            detectable.Bounds = bounds;
        }

        /// <summary>True when the detectable is a Unity object that has been destroyed.</summary>
        public static bool IsDestroyed(this IDetectable detectable)
        {
            return detectable is UnityEngine.Object unityObject && unityObject == null;
        }

        /// <summary>
        /// True when it is safe to call the detectable: not null, not a destroyed Unity object, and still valid.
        /// Never throws.
        /// </summary>
        public static bool IsAlive(this IDetectable detectable)
        {
            if (detectable == null || detectable.IsDestroyed()) return false;

            try
            {
                return detectable.IsValid;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Reads the ID of a live detectable. False when it is gone, throws, or has no ID. Never throws.</summary>
        public static bool TryGetId(this IDetectable detectable, out string id)
        {
            id = null;
            if (!detectable.IsAlive()) return false;

            try
            {
                id = detectable.GetIdentifier();
            }
            catch (Exception)
            {
                id = null;
            }
            return !string.IsNullOrEmpty(id);
        }
    }
}
