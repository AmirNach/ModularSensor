using System;
using UnityEngine;

namespace SensorSystem
{
    [Serializable, ConditionMenu("Spatial/Is angle clamped")]
    public class IsAngleClamped : VisualCondition
    {
        [Tooltip("Horizontal angle from the sensor's forward, in degrees, -180 to 180. Positive is to the right.")]
        [Range(-180f, 180f)] public float MinAngle = -45f;
        [Range(-180f, 180f)] public float MaxAngle = 45f;

        public IsAngleClamped() { VisualColor = new Color(1f, 0.8f, 0.1f); }

        public override string Summary => $"{MinAngle:0.#}° to {MaxAngle:0.#}°";

        public override bool Evaluate(IDetectable target, in SensorContext context)
        {
            Vector3 position = context.PositionOf(target);
            float yaw = AngleUtil.GetYaw(context.SensorPosition, context.SensorRotation, position);
            bool passed = AngleUtil.IsYawWithin(yaw, MinAngle, MaxAngle);

            // A small cross under the target: green when it is inside the angles, red when it is not.
            VisualsFor(context)?.Cross(position + Vector3.down * 0.6f, 0.3f, ColorOf(passed));
            return passed;
        }

        public override void DrawGizmos(Transform sensorTransform, Camera camera)
        {
            if (!ShowVisual) return;

            // The allowed wedge, flat at the sensor's height.
            const float Radius = 10f;
            const int Steps = 16;

            Vector3 position = sensorTransform.position;
            Vector3 Edge(float angle) => position + sensorTransform.rotation * Quaternion.Euler(0f, angle, 0f) * Vector3.forward * Radius;

            Vector3 previous = Edge(MinAngle);
            OverlayGizmoMask.DrawLine(position, previous);
            for (int i = 1; i <= Steps; i++)
            {
                Vector3 next = Edge(Mathf.Lerp(MinAngle, MaxAngle, i / (float)Steps));
                OverlayGizmoMask.DrawLine(previous, next);
                previous = next;
            }
            OverlayGizmoMask.DrawLine(previous, position);
        }
    }
}
