using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>What a condition can see during one sensor refresh.</summary>
    public readonly struct SensorContext
    {
        public readonly Transform SensorTransform;

        /// <summary>The sensor's position for this refresh. Read this rather than SensorTransform: it is not a call into the engine.</summary>
        public readonly Vector3 SensorPosition;

        /// <summary>The sensor's rotation for this refresh. Read this rather than SensorTransform: it is not a call into the engine.</summary>
        public readonly Quaternion SensorRotation;

        public readonly Camera Camera;
        public readonly float Now;
        public readonly EvaluationTrace Trace;

        /// <summary>Where a condition can draw what it did for the target, or null when the sensor's condition visuals are off.</summary>
        public readonly ConditionVisuals Visuals;

        // The target being evaluated, with the values the sensor already read from it for this refresh.
        private readonly IDetectable _target;
        private readonly Vector3 _targetPosition;
        private readonly string _targetId;

        public SensorContext(Transform sensorTransform, Camera camera, float now, EvaluationTrace trace = null, ConditionVisuals visuals = null)
        {
            _target = null;
            _targetPosition = default;
            _targetId = null;
            SensorTransform = sensorTransform;
            if (sensorTransform != null)
            {
                SensorPosition = sensorTransform.position;
                SensorRotation = sensorTransform.rotation;
            }
            else
            {
                SensorPosition = Vector3.zero;
                SensorRotation = Quaternion.identity;
            }
            Camera = camera;
            Now = now;
            Trace = trace;
            Visuals = visuals;
        }

        private SensorContext(in SensorContext other, IDetectable target, Vector3 targetPosition, string targetId)
        {
            this = other;
            _target = target;
            _targetPosition = targetPosition;
            _targetId = targetId;
        }

        /// <summary>The same context for one target, carrying the position and ID the sensor read from it at the start of the refresh.</summary>
        internal SensorContext ForTarget(IDetectable target, Vector3 position, string id) => new SensorContext(this, target, position, id);

        /// <summary>
        /// The target's center. For the target being evaluated it is the value the sensor read for this refresh, so it is not
        /// another call into the target. Use this rather than target.GetCenterPosition() in a condition.
        /// </summary>
        public Vector3 PositionOf(IDetectable target)
        {
            return ReferenceEquals(target, _target) ? _targetPosition : target.GetCenterPosition();
        }

        /// <summary>The target's ID, the same way: read once by the sensor for the target being evaluated.</summary>
        public string IdOf(IDetectable target)
        {
            return ReferenceEquals(target, _target) ? _targetId : target.GetIdentifier();
        }
    }

    /// <summary>
    /// Which leaf conditions a target passed or failed. Short-circuited leaves appear in neither set.
    /// Results are kept as a plain list of conditions while the tree runs, because almost all of them are thrown away.
    /// The sets of names are only built when something reads them, which is when a target was gained or lost.
    /// </summary>
    public sealed class EvaluationTrace
    {
        // Created on the first Record, so the copies a detection keeps (which never record) do not carry two empty lists.
        private List<Condition> _pendingPassed;
        private List<Condition> _pendingFailed;
        private HashSet<string> _passed;
        private HashSet<string> _failed;
        private Dictionary<Condition, bool> _nodeResults;

        public HashSet<string> Passed => Resolve(ref _passed, _pendingPassed);

        public HashSet<string> Failed => Resolve(ref _failed, _pendingFailed);

        /// <summary>When true, NodeResults is filled in. A node missing from it was never reached.</summary>
        public bool RecordNodes { get; set; }

        public Dictionary<Condition, bool> NodeResults => _nodeResults ??= new Dictionary<Condition, bool>();

        public void Record(Condition condition, bool result)
        {
            if (result)
            {
                (_pendingPassed ??= new List<Condition>()).Add(condition);
            }
            else
            {
                (_pendingFailed ??= new List<Condition>()).Add(condition);
            }
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
            _pendingPassed?.Clear();
            _pendingFailed?.Clear();
            if (_passed != null && _passed.Count > 0) _passed.Clear();
            if (_failed != null && _failed.Count > 0) _failed.Clear();
            if (_nodeResults != null && _nodeResults.Count > 0) _nodeResults.Clear();
        }

        /// <summary>A copy holding only the names, for a detection to keep. The node results are for debugging and stay here.</summary>
        public EvaluationTrace Snapshot()
        {
            var copy = new EvaluationTrace();
            copy._passed = new HashSet<string>(Passed);
            copy._failed = new HashSet<string>(Failed);
            return copy;
        }

        private static HashSet<string> Resolve(ref HashSet<string> names, List<Condition> pending)
        {
            names ??= new HashSet<string>();
            if (pending != null && pending.Count > 0)
            {
                foreach (Condition condition in pending)
                {
                    names.Add(condition.DisplayName);
                }
                pending.Clear();
            }
            return names;
        }
    }
}
