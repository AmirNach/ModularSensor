using UnityEngine;

namespace SensorSystem
{
    public static class VectorExtensions
    {
        public static Vector2 XZ(this Vector3 vector) => new Vector2(vector.x, vector.z);
    }
}
