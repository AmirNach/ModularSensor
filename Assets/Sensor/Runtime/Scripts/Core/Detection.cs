using System.Collections.Generic;

namespace SensorSystem
{
    public class Detection
    {
        public IDetectable DetectionComponent { get; }
        public int ID { get; }
        public Sensor CurrentlyDetectingSensorInstance { get; set; }
        public SensorType CurrentlyDetectingSensor => CurrentlyDetectingSensorInstance != null ? CurrentlyDetectingSensorInstance.Type : SensorType.NA;
        public HashSet<Sensor> AllDetectingSensors { get; } = new HashSet<Sensor>();

        /// <summary>Leaf conditions the target passed at its last change. Short-circuited leaves are in neither set.</summary>
        public HashSet<string> AllPassedConditions { get; set; }
        public HashSet<string> AllFailedConditions { get; set; }
        public float DistanceFromSensorToDetection { get; set; }

        public Detection(IDetectable detectionComponent, int id, Sensor currentlyDetectingSensor, HashSet<string> allPassedConditions, HashSet<string> allFailedConditions, float distanceFromSensorToDetection)
        {
            DetectionComponent = detectionComponent;
            ID = id;
            CurrentlyDetectingSensorInstance = currentlyDetectingSensor;
            AllPassedConditions = allPassedConditions;
            AllFailedConditions = allFailedConditions;
            DistanceFromSensorToDetection = distanceFromSensorToDetection;
            AllDetectingSensors.Add(currentlyDetectingSensor);
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
