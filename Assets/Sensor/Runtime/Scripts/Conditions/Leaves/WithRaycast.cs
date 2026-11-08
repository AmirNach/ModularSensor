using System;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>Passes when nothing blocks the line from the sensor to the target.</summary>
    [Serializable, ConditionMenu("World/With raycast")]
    public class WithRaycast : VisualCondition
    {
        private const float MinDistance = 0.001f;

        public WithRaycast() { VisualColor = new Color(0.25f, 0.9f, 0.35f); }
        private const float HitMarkerSize = 0.4f;

        [Tooltip("Layers the ray passes through.")]
        public LayerMask RaycastIgnoreLayerMask;

        public override string Summary => RaycastIgnoreLayerMask.value == 0 ? "Clear line of sight" : "Clear line of sight, some layers ignored";

        public override bool Evaluate(IDetectable target, in SensorContext context)
        {
            Vector3 start = context.SensorPosition;
            Vector3 end = context.PositionOf(target);
            Vector3 direction = end - start;
            float distance = direction.magnitude;
            if (distance < MinDistance) return true;

            bool hasHit = Physics.Raycast(start, direction, out RaycastHit hit, distance, ~RaycastIgnoreLayerMask);
            bool passed;
            if (!(target is Component component))
            {
                passed = !hasHit; // Targets without a collider, such as fake detections, only need a clear line.
            }
            else if (!hasHit)
            {
                passed = false;
            }
            else
            {
                // The usual pass is a hit on the target's own collider. Comparing IDs answers that without three calls into the engine.
                int ownCollider = target is IDetectableCollider withCollider ? withCollider.ColliderInstanceId : 0;
                passed = (ownCollider != 0 && hit.colliderInstanceID == ownCollider)
                    || hit.collider.transform.IsChildOf(component.transform);
            }

            Visualize(VisualsFor(context), start, end, hasHit, hit.point, passed);
            return passed;
        }

        /// <summary>
        /// A full-color line to the target when the way is clear. When blocked: a faint line to whatever blocked it,
        /// a full-color cross where it hit, and a fainter line on to the target.
        /// </summary>
        private void Visualize(ConditionVisuals visuals, Vector3 start, Vector3 end, bool hasHit, Vector3 hitPoint, bool passed)
        {
            if (visuals == null) return;

            if (passed)
            {
                visuals.Line(start, end, VisualColor);
                return;
            }

            Vector3 stop = hasHit ? hitPoint : end;
            visuals.Line(start, stop, ColorOf(false));
            visuals.Cross(stop, HitMarkerSize, VisualColor);
            if (hasHit)
            {
                visuals.Line(stop, end, Faint(ColorOf(false)));
            }
        }
    }
}
