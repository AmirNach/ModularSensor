using System.Collections.Generic;

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
        /// <summary>
        /// The target as last seen by the sensor. It can be destroyed by the time you read it, for example in a lost event,
        /// so use Snapshot to read its data, and check IsTargetAlive before using it as an object.
        /// </summary>
        public IDetectable DetectionComponent { get; private set; }

        /// <summary>The target's own identifier. The sensor and detector track a target by this value.</summary>
        public string TargetId { get; }

        /// <summary>What the target looked like the last time it was seen. It always works, even when the target is gone.</summary>
        public DetectableSnapshot Snapshot { get; }

        /// <summary>Why the detection ended. None while it is still going.</summary>
        public DetectionLostReason LostReason { get; set; }

        public bool IsTargetAlive => DetectionComponent.IsAlive();

        public int ID { get; }
        public Sensor CurrentlyDetectingSensorInstance { get; set; }
        public SensorType CurrentlyDetectingSensor => CurrentlyDetectingSensorInstance != null ? CurrentlyDetectingSensorInstance.Type : SensorType.NA;
        public HashSet<Sensor> AllDetectingSensors { get; } = new HashSet<Sensor>();

        /// <summary>Leaf conditions the target passed at its last change. Short-circuited leaves are in neither set.</summary>
        public HashSet<string> AllPassedConditions { get; set; }
        public HashSet<string> AllFailedConditions { get; set; }
        public float DistanceFromSensorToDetection { get; set; }

        public Detection(IDetectable detectionComponent, string targetId, int id, Sensor currentlyDetectingSensor, HashSet<string> allPassedConditions, HashSet<string> allFailedConditions, float distanceFromSensorToDetection)
        {
            DetectionComponent = detectionComponent;
            TargetId = targetId;
            Snapshot = DetectableSnapshot.Of(detectionComponent, targetId);
            ID = id;
            CurrentlyDetectingSensorInstance = currentlyDetectingSensor;
            AllPassedConditions = allPassedConditions;
            AllFailedConditions = allFailedConditions;
            DistanceFromSensorToDetection = distanceFromSensorToDetection;
            AllDetectingSensors.Add(currentlyDetectingSensor);
        }

        /// <summary>Called by the sensor on every refresh while the target is detected: keeps the snapshot and the instance current.</summary>
        public void Refresh(IDetectable live, float distance)
        {
            DetectionComponent = live;
            DistanceFromSensorToDetection = distance;
            Snapshot.Update(live);
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
