using System;
using UnityEngine;

namespace SensorSystem
{
    [Serializable, ConditionMenu("Spatial/In frustum")]
    public class InFrustum : VisualCondition
    {
        [Tooltip("Ignored when the sensor has a camera, which uses the camera's frustum instead.")]
        [Range(1f, 179f)] public float HorizontalFov = 60f;
        [Range(1f, 179f)] public float VerticalFov = 40f;
        [Min(0.01f)] public float MinDistance = 0.1f;
        [Min(0.01f)] public float MaxDistance = 100f;

        public InFrustum() { VisualColor = new Color(1f, 0.25f, 0.25f); }

        [NonSerialized] private Plane[] _planes;

        public override void Prepare(in SensorContext context)
        {
            _planes ??= new Plane[FrustumUtil.PlaneCount];

            if (context.Camera != null)
            {
                GeometryUtility.CalculateFrustumPlanes(context.Camera, _planes);
            }
            else
            {
                Transform sensor = context.SensorTransform;
                FrustumUtil.CalculatePlanes(sensor.position, sensor.rotation, HorizontalFov, VerticalFov, MinDistance, MaxDistance, _planes);
            }
        }

        public override string Summary => $"{HorizontalFov:0.#}° × {VerticalFov:0.#}°, {MinDistance:0.#}–{MaxDistance:0.#} m";

        public override bool Evaluate(IDetectable target, in SensorContext context)
        {
            Bounds bounds = target.Bounds;
            Vector3 center = context.PositionOf(target);
            if (bounds.center != center)
            {
                bounds.center = center;
                target.Bounds = bounds;
            }
            return GeometryUtility.TestPlanesAABB(_planes, bounds);
        }

        public override void DrawGizmos(Transform sensorTransform, Camera camera)
        {
            if (!ShowVisual) return;

            // Drawn through the mask, so the lines stay out from behind the QA overlay in the Game view.
            if (camera != null)
            {
                Transform cameraTransform = camera.transform;
                OverlayGizmoMask.DrawFrustum(cameraTransform.position, cameraTransform.rotation, camera.fieldOfView, camera.farClipPlane, camera.nearClipPlane, camera.aspect);
            }
            else
            {
                OverlayGizmoMask.DrawFrustum(sensorTransform.position, sensorTransform.rotation, VerticalFov, MaxDistance, MinDistance, FrustumUtil.GetAspect(HorizontalFov, VerticalFov));
            }
        }
    }
}
