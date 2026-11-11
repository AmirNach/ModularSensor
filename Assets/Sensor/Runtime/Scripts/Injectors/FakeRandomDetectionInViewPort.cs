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

        public override string DisplayName => "Random fake detections in viewport";

        protected override Vector3 GetDetectionPosition(float distance)
        {
            if (Camera == null) return base.GetDetectionPosition(distance);

            var viewportPoint = new Vector3(
                UnityEngine.Random.Range(TopLeftCorner.x, BottomRightCorner.x),
                UnityEngine.Random.Range(BottomRightCorner.y, TopLeftCorner.y),
                distance);
            Vector3 worldPoint = Camera.ViewportToWorldPoint(viewportPoint);
            Vector3 cameraPosition = Camera.transform.position;
            Vector3 toPoint = worldPoint - cameraPosition;

            // Something in the way, nearer than the chosen distance, is where the fake shows. Without the limit, a hill far
            // behind would win and the distance weights would mean nothing. Triggers are not surfaces.
            if (Physics.Raycast(cameraPosition, toPoint.normalized, out RaycastHit hit, toPoint.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            worldPoint.y = TerrainUtil.GetGroundHeight(worldPoint);
            return worldPoint;
        }
    }
}
