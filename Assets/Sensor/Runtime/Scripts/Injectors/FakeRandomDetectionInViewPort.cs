using System;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>Spawns fake targets inside a viewport rectangle. Needs a SensorWithCamera to use the rectangle.</summary>
    [Serializable, ConditionMenu("Fake/Random detections in viewport")]
    public class FakeRandomDetectionInViewPort : FakeRandomReflectedEntitiesDetections
    {
        [Tooltip("Viewport coordinates, 0 to 1.")]
        public Vector2 TopLeftCorner = new Vector2(0f, 1f);
        public Vector2 BottomRightCorner = new Vector2(1f, 0f);

        protected override Vector3 GetDetectionPosition(float distance)
        {
            if (Camera == null) return base.GetDetectionPosition(distance);

            var viewportPoint = new Vector3(
                UnityEngine.Random.Range(TopLeftCorner.x, BottomRightCorner.x),
                UnityEngine.Random.Range(BottomRightCorner.y, TopLeftCorner.y),
                distance);
            Vector3 worldPoint = Camera.ViewportToWorldPoint(viewportPoint);
            Vector3 cameraPosition = Camera.transform.position;

            if (Physics.Raycast(cameraPosition, (worldPoint - cameraPosition).normalized, out RaycastHit hit))
            {
                return hit.point;
            }

            worldPoint.y = TerrainUtil.GetGroundHeight(worldPoint);
            return worldPoint;
        }
    }
}
