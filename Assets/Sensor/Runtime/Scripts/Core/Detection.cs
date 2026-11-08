using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>Why a detection ended.</summary>
    public enum DetectionLostReason
    {
        /// <summary>Not lost yet.</summary>
        None,
        /// <summary>The target is still around, but the conditions no longer accept it.</summary>
        ConditionsFailed,
        /// <summary>The target is gone: destroyed, no longer valid, or no longer found by the sensor.</summary>
        TargetDestroyed,
        /// <summary>The sensor or detector was switched off.</summary>
        SensorDisabled,
    }

    public class Detection
    {
        private readonly Sensor _firstSensor;
        private HashSet<Sensor> _allDetectingSensors;

        /// <summary>
        /// The target as last seen by the sensor. It can be destroyed by the time you read it, for example in a lost event,
        /// so use Snapshot to read its data, and check IsTargetAlive before using it as an object.
        /// </summary>
        public IDetectable DetectionComponent { get; private set; }

        /// <summary>The target's own identifier. The sensor and detector track a target by this value.</summary>
        public string TargetId { get; }

        /// <summary>
        /// What the target looked like the last time it was seen. It always works, even when the target is gone.
        /// A detector's detection shares this object with a sensor's detection instead of keeping a copy.
        /// </summary>
        public DetectableSnapshot Snapshot { get; private set; }

        /// <summary>Why the detection ended. None while it is still going.</summary>
        public DetectionLostReason LostReason { get; set; }

        public bool IsTargetAlive => DetectionComponent.IsAlive();

        public int ID { get; }
        public Sensor CurrentlyDetectingSensorInstance { get; set; }
        public SensorType CurrentlyDetectingSensor => CurrentlyDetectingSensorInstance != null ? CurrentlyDetectingSensorInstance.Type : SensorType.NA;

        /// <summary>Only a detector ever has more than one, so the set is created the first time something asks for it.</summary>
        public HashSet<Sensor> AllDetectingSensors
        {
            get
            {
                if (_allDetectingSensors == null)
                {
                    _allDetectingSensors = new HashSet<Sensor>();
                    _allDetectingSensors.Add(_firstSensor);
                }
                return _allDetectingSensors;
            }
        }

        /// <summary>Leaf conditions the target passed at its last change. Short-circuited leaves are in neither set.</summary>
        public HashSet<string> AllPassedConditions { get; set; }
        public HashSet<string> AllFailedConditions { get; set; }
        public float DistanceFromSensorToDetection { get; set; }

        public Detection(IDetectable detectionComponent, string targetId, int id, Sensor currentlyDetectingSensor, HashSet<string> allPassedConditions, HashSet<string> allFailedConditions, float distanceFromSensorToDetection)
            : this(detectionComponent, targetId, DetectableSnapshot.Of(detectionComponent, targetId), id, currentlyDetectingSensor, allPassedConditions, allFailedConditions, distanceFromSensorToDetection)
        {
        }

        /// <summary>A detector's detection of what a sensor already detects. It reuses the sensor's data instead of reading the target again.</summary>
        internal Detection(Detection sensorDetection, int id, Sensor sensor)
            : this(sensorDetection.DetectionComponent, sensorDetection.TargetId, sensorDetection.Snapshot, id, sensor, sensorDetection.AllPassedConditions, sensorDetection.AllFailedConditions, sensorDetection.DistanceFromSensorToDetection)
        {
        }

        private Detection(IDetectable detectionComponent, string targetId, DetectableSnapshot snapshot, int id, Sensor currentlyDetectingSensor, HashSet<string> allPassedConditions, HashSet<string> allFailedConditions, float distanceFromSensorToDetection)
        {
            DetectionComponent = detectionComponent;
            TargetId = targetId;
            Snapshot = snapshot;
            ID = id;
            _firstSensor = currentlyDetectingSensor;
            CurrentlyDetectingSensorInstance = currentlyDetectingSensor;
            AllPassedConditions = allPassedConditions;
            AllFailedConditions = allFailedConditions;
            DistanceFromSensorToDetection = distanceFromSensorToDetection;
        }

        /// <summary>Called by the sensor on every refresh while the target is detected: keeps the snapshot and the instance current.</summary>
        internal void Refresh(IDetectable live, float distance, Vector3 position, float now)
        {
            DetectionComponent = live;
            DistanceFromSensorToDetection = distance;
            Snapshot.Refresh(live, position, now);
        }

        /// <summary>The detector ends with the data of the last sensor that lost the target.</summary>
        internal void UseSnapshotOf(Detection other)
        {
            Snapshot = other.Snapshot;
        }

        public void AddToDetectedBy(Sensor nowDetectedBy)
        {
            AllDetectingSensors.Add(nowDetectedBy);
        }

        public void RemoveFromDetectedBy(Sensor noLongerDetectedBy)
        {
            AllDetectingSensors.Remove(noLongerDetectedBy);

            if (CurrentlyDetectingSensorInstance == noLongerDetectedBy)
            {
                foreach (Sensor remaining in AllDetectingSensors)
                {
                    CurrentlyDetectingSensorInstance = remaining;
                    break;
                }
            }
        }
    }
}
