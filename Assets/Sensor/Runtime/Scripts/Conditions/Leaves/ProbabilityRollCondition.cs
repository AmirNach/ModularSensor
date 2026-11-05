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
    public abstract class ProbabilityRollCondition : VisualCondition
    {
        [Tooltip("On: once an object has been detected, it stays detected without rolling again, until it goes out of range or the sensor loses it. Off: the dice are rolled again on every refresh.")]
        [FormerlySerializedAs("DoesSaveDetectionAttempts")]
        public bool StopRollingOnceDetected = true;

        [Tooltip("Give each object only a limited number of rolls.")]
        public bool HasLimitedDetectionAttempts;

        [Tooltip("How many rolls each object gets before it can no longer be detected by this condition.")]
        [FormerlySerializedAs("MaxDetectionAttemptsPerEntity")]
        [Min(0)] public int MaxDetectionAttempts;

        protected ProbabilityRollCondition() { VisualColor = new Color(0.75f, 0.4f, 1f); }

        [NonSerialized] private HashSet<string> _passedRoll;
        [NonSerialized] private Dictionary<string, int> _attempts;

        private HashSet<string> PassedRoll => _passedRoll ??= new HashSet<string>();
        private Dictionary<string, int> Attempts => _attempts ??= new Dictionary<string, int>();

        /// <summary>The chance, 0 to 100, that this target is detected right now.</summary>
        protected abstract float GetProbability(IDetectable target, in SensorContext context);

        protected string AttemptsSummary => HasLimitedDetectionAttempts ? $", max {MaxDetectionAttempts} attempts" : "";

        public override bool Evaluate(IDetectable target, in SensorContext context)
        {
            bool passed = Roll(target, in context);

            // A small cross above the target: green when the roll let it through, red when it did not.
            VisualsFor(context)?.Cross(target.GetCenterPosition() + Vector3.up * 1.2f, 0.4f, ColorOf(passed));
            return passed;
        }

        private bool Roll(IDetectable target, in SensorContext context)
        {
            // Remembered by ID, so a target that comes back as a new instance keeps its state.
            string id = target.GetIdentifier();
            float probability = GetProbability(target, in context);
            if (probability <= 0f)
            {
                PassedRoll.Remove(id);
                return false;
            }

            if (StopRollingOnceDetected && PassedRoll.Contains(id)) return true;

            if (HasLimitedDetectionAttempts)
            {
                Attempts.TryGetValue(id, out int attempts);
                if (attempts >= MaxDetectionAttempts)
                {
                    PassedRoll.Remove(id);
                    return false;
                }
                Attempts[id] = attempts + 1;
            }

            if (DistanceProbability.Roll(probability))
            {
                PassedRoll.Add(id);
                return true;
            }

            PassedRoll.Remove(id);
            return false;
        }

        public override void OnTargetLost(IDetectable target)
        {
            string id = target.GetIdentifier();
            PassedRoll.Remove(id);
            Attempts.Remove(id);
        }
    }
}
