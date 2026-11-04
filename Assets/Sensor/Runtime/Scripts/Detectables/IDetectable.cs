using UnityEngine;

namespace SensorSystem
{
    /// <summary>Anything a Sensor can detect.</summary>
    public interface IDetectable
    {
        Bounds Bounds { get; set; }
        bool IsFake { get; }

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
            return detectable is Object unityObject && unityObject == null;
        }
    }
}
