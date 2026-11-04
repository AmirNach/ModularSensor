using System;
using UnityEngine;

namespace SensorSystem
{
    [Serializable, ConditionMenu("Spatial/Is angle clamped")]
    public class IsAngleClamped : Condition
    {
        [Tooltip("Horizontal angle from the sensor's forward, in degrees, -180 to 180. Positive is to the right.")]
        [Range(-180f, 180f)] public float MinAngle = -45f;
        [Range(-180f, 180f)] public float MaxAngle = 45f;

        public override string Summary => $"{MinAngle:0.#}° to {MaxAngle:0.#}°";

        public override bool Evaluate(IDetectable target, in SensorContext context)
        {
            Transform sensor = context.SensorTransform;
            float yaw = AngleUtil.GetYaw(sensor.position, sensor.rotation, target.GetCenterPosition());
            return AngleUtil.IsYawWithin(yaw, MinAngle, MaxAngle);
        }
    }
}
