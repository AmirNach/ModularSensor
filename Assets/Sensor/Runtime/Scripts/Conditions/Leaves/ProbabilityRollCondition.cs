using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SensorSystem
{
    /// <summary>
    /// Base for conditions that pass by a dice roll. It owns the two options every rolling condition has:
    /// keeping a detection once it has been rolled, and limiting how many rolls an object gets.
    /// A subclass only says what the probability is for a target.
    /// Rolls only happen when the tree reaches this node, so an earlier failing condition in an And saves rolls.
    /// </summary>
    [Serializable]
    public abstract class ProbabilityRollCondition : Condition
    {
        [Tooltip("On: once an object has been detected, it stays detected without rolling again, until it goes out of range or the sensor loses it. Off: the dice are rolled again on every refresh.")]
        [FormerlySerializedAs("DoesSaveDetectionAttempts")]
        public bool StopRollingOnceDetected = true;

        [Tooltip("Give each object only a limited number of rolls.")]
        public bool HasLimitedDetectionAttempts;

        [Tooltip("How many rolls each object gets before it can no longer be detected by this condition.")]
        [FormerlySerializedAs("MaxDetectionAttemptsPerEntity")]
        [Min(0)] public int MaxDetectionAttempts;

        [NonSerialized] private HashSet<IDetectable> _passedRoll;
        [NonSerialized] private Dictionary<IDetectable, int> _attempts;

        private HashSet<IDetectable> PassedRoll => _passedRoll ??= new HashSet<IDetectable>();
        private Dictionary<IDetectable, int> Attempts => _attempts ??= new Dictionary<IDetectable, int>();

        /// <summary>The chance, 0 to 100, that this target is detected right now.</summary>
        protected abstract float GetProbability(IDetectable target, in SensorContext context);

        protected string AttemptsSummary => HasLimitedDetectionAttempts ? $", max {MaxDetectionAttempts} attempts" : "";

        public override bool Evaluate(IDetectable target, in SensorContext context)
        {
            float probability = GetProbability(target, in context);
            if (probability <= 0f)
            {
                PassedRoll.Remove(target);
                return false;
            }

            if (StopRollingOnceDetected && PassedRoll.Contains(target)) return true;

            if (HasLimitedDetectionAttempts)
            {
                Attempts.TryGetValue(target, out int attempts);
                if (attempts >= MaxDetectionAttempts)
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
    }
}
