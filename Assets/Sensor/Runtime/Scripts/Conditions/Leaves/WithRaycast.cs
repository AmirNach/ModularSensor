using System;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>Passes when nothing blocks the line from the sensor to the target.</summary>
    [Serializable, ConditionMenu("World/With raycast")]
    public class WithRaycast : Condition
    {
        private const float MinDistance = 0.001f;

        [Tooltip("Layers the ray passes through.")]
        public LayerMask RaycastIgnoreLayerMask;

        public override string Summary => RaycastIgnoreLayerMask.value == 0 ? "Clear line of sight" : "Clear line of sight, some layers ignored";

        public override bool Evaluate(IDetectable target, in SensorContext context)
        {
            Vector3 start = context.SensorTransform.position;
            Vector3 direction = target.GetCenterPosition() - start;
            float distance = direction.magnitude;
            if (distance < MinDistance) return true;

            bool hasHit = Physics.Raycast(start, direction, out RaycastHit hit, distance, ~RaycastIgnoreLayerMask);
            if (target is Component component)
            {
                return hasHit && hit.collider.transform.IsChildOf(component.transform);
            }

            // Targets without a collider, such as fake detections, only need a clear line.
            return !hasHit;
        }
    }
}
