using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>What a condition can see during one sensor refresh.</summary>
    public readonly struct SensorContext
    {
        public readonly Transform SensorTransform;
        public readonly Camera Camera;
        public readonly float Now;
        public readonly EvaluationTrace Trace;

        /// <summary>Where a condition can draw what it did for the target, or null when the sensor's condition visuals are off.</summary>
        public readonly ConditionVisuals Visuals;

        public SensorContext(Transform sensorTransform, Camera camera, float now, EvaluationTrace trace = null, ConditionVisuals visuals = null)
        {
            SensorTransform = sensorTransform;
            Camera = camera;
            Now = now;
            Trace = trace;
            Visuals = visuals;
        }
    }

    /// <summary>Which leaf conditions a target passed or failed. Short-circuited leaves appear in neither set.</summary>
    public sealed class EvaluationTrace
    {
        public HashSet<string> Passed { get; } = new HashSet<string>();
        public HashSet<string> Failed { get; } = new HashSet<string>();

        /// <summary>When true, NodeResults is filled in. A node missing from it was never reached.</summary>
        public bool RecordNodes { get; set; }
        public Dictionary<Condition, bool> NodeResults { get; } = new Dictionary<Condition, bool>();

        public void Record(Condition condition, bool result)
        {
            (result ? Passed : Failed).Add(condition.DisplayName);
        }

        public void RecordNode(Condition condition, bool result)
        {
            if (RecordNodes)
            {
                NodeResults[condition] = result;
            }
        }

        public void Clear()
        {
            Passed.Clear();
            Failed.Clear();
            NodeResults.Clear();
        }

        public EvaluationTrace Snapshot()
        {
            var copy = new EvaluationTrace();
            copy.Passed.UnionWith(Passed);
            copy.Failed.UnionWith(Failed);
            return copy;
        }
    }
}
