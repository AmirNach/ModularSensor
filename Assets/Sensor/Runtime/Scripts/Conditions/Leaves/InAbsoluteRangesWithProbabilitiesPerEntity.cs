using System;
using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// Rolls by entity type and distance. Attempts only count when the tree reaches this node,
    /// so an earlier failing condition in an And saves attempts.
    /// </summary>
    [Serializable, ConditionMenu("Probability/In absolute ranges per entity")]
    public class InAbsoluteRangesWithProbabilitiesPerEntity : Condition
    {
        [Serializable]
        public class EntityRanges
        {
            public EntityType EntityType;
            public List<DistanceProbability> DistanceProbabilities = new List<DistanceProbability>();
        }

        public List<EntityRanges> EntityTypeToDistanceProbabilities = new List<EntityRanges>();
        public bool HasLimitedDetectionAttempts;
        [Min(0)] public int MaxDetectionAttemptsPerEntity;
        [Tooltip("Once a roll succeeds, stop rolling until the sensor loses the target.")]
        public bool DoesSaveDetectionAttempts = true;

        [NonSerialized] private HashSet<IDetectable> _passedRoll;
        [NonSerialized] private Dictionary<IDetectable, int> _attempts;

        private HashSet<IDetectable> PassedRoll => _passedRoll ??= new HashSet<IDetectable>();
        private Dictionary<IDetectable, int> Attempts => _attempts ??= new Dictionary<IDetectable, int>();

        public override string Summary => $"{EntityTypeToDistanceProbabilities.Count} entity types" + (HasLimitedDetectionAttempts ? $", max {MaxDetectionAttemptsPerEntity} attempts" : "");

        public override bool Evaluate(IDetectable target, in SensorContext context)
        {
            if (!(target is IEntityTyped entity)) return false;

            float distance = Vector3.Distance(context.SensorTransform.position, target.GetCenterPosition());
            float probability = DistanceProbability.Find(GetDistanceProbabilities(entity.EntityType), distance);
            if (probability <= 0f)
            {
                PassedRoll.Remove(target);
                return false;
            }

            if (DoesSaveDetectionAttempts && PassedRoll.Contains(target)) return true;

            if (HasLimitedDetectionAttempts)
            {
                Attempts.TryGetValue(target, out int attempts);
                if (attempts >= MaxDetectionAttemptsPerEntity)
                {
                    PassedRoll.Remove(target);
                    return false;
                }
                Attempts[target] = attempts + 1;
            }

            if (DistanceProbability.Roll(probability))
            {
                PassedRoll.Add(target);
                return true;
            }

            PassedRoll.Remove(target);
            return false;
        }

        public override void OnTargetLost(IDetectable target)
        {
            PassedRoll.Remove(target);
            Attempts.Remove(target);
        }

        /// <summary>Changes probabilities at runtime, 0 to 100. A null type or distance means all of them.</summary>
        public void SetProbability(EntityType? entityType, float? distance, float probability)
        {
            foreach (EntityRanges ranges in EntityTypeToDistanceProbabilities)
            {
                if (entityType != null && !ranges.EntityType.Matches(entityType.Value)) continue;

                foreach (DistanceProbability entry in ranges.DistanceProbabilities)
                {
                    if (distance == null || Mathf.Approximately(entry.Distance, distance.Value))
                    {
                        entry.Probability = probability;
                    }
                }
            }
        }

        private List<DistanceProbability> GetDistanceProbabilities(EntityType entityType)
        {
            foreach (EntityRanges ranges in EntityTypeToDistanceProbabilities)
            {
                if (ranges.EntityType.Matches(entityType)) return ranges.DistanceProbabilities;
            }
            return null;
        }
    }
}
