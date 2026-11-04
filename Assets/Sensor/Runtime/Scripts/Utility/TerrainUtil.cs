using UnityEngine;

namespace SensorSystem
{
    public static class TerrainUtil
    {
        private const float RayStartHeight = 1000f;

        /// <summary>Terrain height at the position, else the first physics hit below it, else its own height.</summary>
        public static float GetGroundHeight(Vector3 position)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                return terrain.SampleHeight(position) + terrain.transform.position.y;
            }

            Vector3 origin = new Vector3(position.x, position.y + RayStartHeight, position.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit))
            {
                return hit.point.y;
            }

            return position.y;
        }
    }
}
