using UnityEngine;

namespace SensorSystem
{
    public static class FrustumUtil
    {
        public const int PlaneCount = 6;

        public static float GetAspect(float horizontalFov, float verticalFov)
        {
            return Mathf.Tan(horizontalFov * Mathf.Deg2Rad / 2f) / Mathf.Tan(verticalFov * Mathf.Deg2Rad / 2f);
        }

        /// <summary>Fills planes with a frustum centered on the forward direction of the pose.</summary>
        public static void CalculatePlanes(Vector3 position, Quaternion rotation, float horizontalFov, float verticalFov, float minDistance, float maxDistance, Plane[] planes)
        {
            Matrix4x4 projection = Matrix4x4.Perspective(verticalFov, GetAspect(horizontalFov, verticalFov), minDistance, maxDistance);
            // View space looks down -Z, while the pose looks down +Z.
            Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
            GeometryUtility.CalculateFrustumPlanes(projection * view, planes);
        }
    }
}
