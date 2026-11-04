using System;
using UnityEngine;

namespace SensorSystem
{
    [Serializable, ConditionMenu("Spatial/In frustum")]
    public class InFrustum : Condition
    {
        [Tooltip("Ignored when the sensor has a camera, which uses the camera's frustum instead.")]
        [Range(1f, 179f)] public float HorizontalFov = 60f;
        [Range(1f, 179f)] public float VerticalFov = 40f;
        [Min(0.01f)] public float MinDistance = 0.1f;
        [Min(0.01f)] public float MaxDistance = 100f;

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
            target.SetBoundsCenter();
            return GeometryUtility.TestPlanesAABB(_planes, target.Bounds);
        }

        public override void DrawGizmos(Transform sensorTransform, Camera camera)
        {
            if (camera != null)
            {
                Gizmos.matrix = camera.transform.localToWorldMatrix;
                Gizmos.DrawFrustum(Vector3.zero, camera.fieldOfView, camera.farClipPlane, camera.nearClipPlane, camera.aspect);
            }
            else
            {
                Gizmos.matrix = Matrix4x4.TRS(sensorTransform.position, sensorTransform.rotation, Vector3.one);
                Gizmos.DrawFrustum(Vector3.zero, VerticalFov, MaxDistance, MinDistance, FrustumUtil.GetAspect(HorizontalFov, VerticalFov));
            }
        }
    }
}
