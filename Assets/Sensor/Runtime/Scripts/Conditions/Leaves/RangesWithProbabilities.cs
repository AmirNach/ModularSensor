using System;
using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>Rolls every refresh, using the probability for the target's distance.</summary>
    [Serializable]
    public abstract class RangesWithProbabilities : Condition
    {
        public List<DistanceProbability> DistanceProbabilities = new List<DistanceProbability>();

        protected abstract float GetDistance(Vector3 sensorPosition, Vector3 targetPosition);

        public override string Summary => DistanceProbabilities == null || DistanceProbabilities.Count == 0 ? "No ranges set" : $"{DistanceProbabilities.Count} ranges";

        public override bool Evaluate(IDetectable target, in SensorContext context)
        {
            float distance = GetDistance(context.SensorTransform.position, target.GetCenterPosition());
            return DistanceProbability.Roll(DistanceProbability.Find(DistanceProbabilities, distance));
        }
    }

    [Serializable, ConditionMenu("Probability/In absolute ranges")]
    public class InAbsoluteRangesWithProbabilities : RangesWithProbabilities
    {
        protected override float GetDistance(Vector3 sensorPosition, Vector3 targetPosition)
        {
            return Vector3.Distance(sensorPosition, targetPosition);
        }
    }

    [Serializable, ConditionMenu("Probability/In horizontal ranges")]
    public class InHorizontalRangesWithProbabilities : RangesWithProbabilities
    {
        protected override float GetDistance(Vector3 sensorPosition, Vector3 targetPosition)
        {
            return (targetPosition - sensorPosition).XZ().magnitude;
        }
    }
}
