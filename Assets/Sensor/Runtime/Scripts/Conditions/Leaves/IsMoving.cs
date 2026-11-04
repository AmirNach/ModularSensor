using System;
using UnityEngine;

namespace SensorSystem
{
    [Serializable, ConditionMenu("World/Is moving")]
    public class IsMoving : Condition
    {
        [Tooltip("Meters per second.")]
        [Min(0f)] public float MinMovingVelocity = 0.1f;

        public override string Summary => $"Faster than {MinMovingVelocity:0.##} m/s";

        public override bool Evaluate(IDetectable target, in SensorContext context)
        {
            return target.GetVelocity() > MinMovingVelocity;
        }
    }
}
