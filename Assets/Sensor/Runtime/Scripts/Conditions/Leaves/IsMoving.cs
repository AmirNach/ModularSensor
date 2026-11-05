using System;
using UnityEngine;

namespace SensorSystem
{
    [Serializable, ConditionMenu("World/Is moving")]
    public class IsMoving : VisualCondition
    {
        [Tooltip("Meters per second.")]
        [Min(0f)] public float MinMovingVelocity = 0.1f;

        public IsMoving() { VisualColor = new Color(1f, 0.55f, 0.1f); }

        public override string Summary => $"Faster than {MinMovingVelocity:0.##} m/s";

        public override bool Evaluate(IDetectable target, in SensorContext context)
        {
            bool passed = target.GetVelocity() > MinMovingVelocity;

            // A short tick above the target: green when it moves fast enough, red when it does not.
            Vector3 top = target.GetCenterPosition() + Vector3.up * 0.8f;
            VisualsFor(context)?.Line(top, top + Vector3.up * 0.6f, ColorOf(passed));
            return passed;
        }
    }
}
