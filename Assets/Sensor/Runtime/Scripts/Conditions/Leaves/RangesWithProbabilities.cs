using System;
using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>Rolls by distance. The same list of distances and probabilities applies to every object.</summary>
    [Serializable]
    public abstract class RangesWithProbabilities : ProbabilityRollCondition
    {
        public List<DistanceProbability> DistanceProbabilities = new List<DistanceProbability>();

        protected abstract float GetDistance(Vector3 sensorPosition, Vector3 targetPosition);

        public override string Summary => (DistanceProbabilities == null || DistanceProbabilities.Count == 0 ? "No ranges set" : $"{DistanceProbabilities.Count} ranges") + AttemptsSummary;

        protected override float GetProbability(IDetectable target, in SensorContext context)
        {
            float distance = GetDistance(context.SensorPosition, context.PositionOf(target));
            return DistanceProbability.Find(DistanceProbabilities, distance);
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
