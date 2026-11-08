using System;
using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// A node in a sensor's condition tree. To add a condition, inherit from this class and implement Evaluate.
    /// Public fields become its settings in the inspector, and it appears in the add menu automatically.
    /// Mark runtime state [NonSerialized].
    /// </summary>
    [Serializable]
    public abstract class Condition
    {
        [NonSerialized] private string _displayName;

        public virtual string DisplayName => _displayName ??= ConditionTree.GetDisplayName(GetType());

        /// <summary>Optional one-line description of the settings, shown on collapsed inspector cards.</summary>
        public virtual string Summary => null;

        /// <summary>True when this condition only works on a sensor with a camera. The inspector warns about it.</summary>
        public virtual bool RequiresCamera => false;

        /// <summary>True when this condition wants to draw what it did. The sensor then collects visuals for the refresh.</summary>
        public virtual bool WantsVisuals => false;

        /// <summary>Called once per refresh, before any target is evaluated.</summary>
        public virtual void Prepare(in SensorContext context) { }

        /// <summary>Does this target pass? Called at most once per target per refresh.</summary>
        public abstract bool Evaluate(IDetectable target, in SensorContext context);

        /// <summary>The sensor started detecting this target.</summary>
        public virtual void OnTargetGained(IDetectable target) { }

        /// <summary>The sensor stopped detecting this target.</summary>
        public virtual void OnTargetLost(IDetectable target) { }

        /// <summary>Draws this condition in the scene view when the sensor's gizmos are on.</summary>
        public virtual void DrawGizmos(Transform sensorTransform, Camera camera) { }

        /// <summary>Operators add their children here. Leaves have none.</summary>
        public virtual void CollectChildren(List<Condition> children) { }

        protected virtual bool IsOperator => false;

        /// <summary>Evaluates a node and records the result in the trace. Operators use this for their children.</summary>
        public static bool Run(Condition condition, IDetectable target, in SensorContext context)
        {
            if (condition == null) return false;

            bool result;
            if (SensorProfiler.MeasureConditions && SensorProfiler.Enabled)
            {
                using (SensorProfiler.ForCondition(condition).Measure())
                {
                    result = condition.Evaluate(target, context);
                }
            }
            else
            {
                result = condition.Evaluate(target, context);
            }

            EvaluationTrace trace = context.Trace;
            if (trace != null)
            {
                trace.RecordNode(condition, result);
                if (!condition.IsOperator)
                {
                    trace.Record(condition, result);
                }
            }
            return result;
        }
    }
}
