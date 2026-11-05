using System;
using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// Rolls by entity type and distance. Each entity type has its own list of distances and probabilities.
    /// </summary>
    [Serializable, ConditionMenu("Probability/In absolute ranges per entity")]
    public class InAbsoluteRangesWithProbabilitiesPerEntity : ProbabilityRollCondition
    {
        [Serializable]
        public class EntityRanges
        {
            public EntityType EntityType;
            public List<DistanceProbability> DistanceProbabilities = new List<DistanceProbability>();
        }

        public List<EntityRanges> EntityTypeToDistanceProbabilities = new List<EntityRanges>();

        public override string Summary => $"{EntityTypeToDistanceProbabilities.Count} entity types" + AttemptsSummary;

        protected override float GetProbability(IDetectable target, in SensorContext context)
        {
            if (!(target is IEntityTyped entity)) return 0f;

            float distance = Vector3.Distance(context.SensorTransform.position, target.GetCenterPosition());
            return DistanceProbability.Find(GetDistanceProbabilities(entity.EntityType), distance);
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
