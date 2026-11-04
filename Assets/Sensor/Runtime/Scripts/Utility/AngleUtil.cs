using UnityEngine;

namespace SensorSystem
{
    public static class AngleUtil
    {
        /// <summary>Signed horizontal angle from forward to the target in degrees, -180 to 180. Positive is to the right.</summary>
        public static float GetYaw(Vector3 position, Quaternion rotation, Vector3 target)
        {
            Vector3 local = Quaternion.Inverse(rotation) * (target - position);
            return Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        }

        /// <summary>When min is greater than max the arc wraps through 180 degrees behind the sensor.</summary>
        public static bool IsYawWithin(float yaw, float min, float max)
        {
            return min <= max ? yaw >= min && yaw <= max : yaw >= min || yaw <= max;
        }
    }
}
