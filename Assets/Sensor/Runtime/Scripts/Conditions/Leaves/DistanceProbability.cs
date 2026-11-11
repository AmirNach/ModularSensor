using System;
using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    [Serializable]
    public class DistanceProbability
    {
        [Tooltip("Meters. Applies from the previous entry's distance up to this one.")]
        [Min(0f)] public float Distance;
        [Range(0f, 100f)] public float Probability;

        /// <summary>The probability of the nearest entry reaching the distance, or 0 past the last one.</summary>
        public static float Find(List<DistanceProbability> entries, float distance)
        {
            if (entries == null) return 0f;

            bool found = false;
            float nearestDistance = 0f;
            float probability = 0f;
            foreach (DistanceProbability entry in entries)
            {
                if (distance <= entry.Distance && (!found || entry.Distance < nearestDistance))
                {
                    found = true;
                    nearestDistance = entry.Distance;
                    probability = entry.Probability;
                }
            }
            return probability;
        }

        public static bool Roll(float probability)
        {
            if (probability <= 0f) return false;
            return probability >= 100f || UnityEngine.Random.Range(0f, 100f) <= probability;
        }
    }

    /// <summary>A distance band (from the previous band's distance up to this one) and the percent of a fake type that falls in it.</summary>
    [Serializable]
    public class DistancePercent
    {
        [Tooltip("The band runs from the previous band's distance (0 for the first) up to this many meters from the sensor.")]
        [UnityEngine.Serialization.FormerlySerializedAs("Distance")]
        [Min(0f)] public float UpToMeters;

        [Tooltip("Of this type's fakes, the percent that appear in this band. The bands of a type add up to 100%.")]
        [UnityEngine.Serialization.FormerlySerializedAs("Percent")]
        [UnityEngine.Serialization.FormerlySerializedAs("Weight")]
        [Range(0f, 100f)] public float PercentOfThisType;
    }
}
