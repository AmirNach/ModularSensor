using SensorSystem;
using UnityEngine;

public class testSensorDetectorEvents : MonoBehaviour
{
    public Detector Detector;

    private void Awake()
    {
        Detector.OnNewDetectionFromDetectorEvent += OnNewDetectionFromDetector;
        Detector.OnLostDetectionFromDetectorEvent += OnLostDetectionFromDetector;
        Detector.OnNewDetectionFromSensorEvent += OnNewDetectionFromSensor;
        Detector.OnLostDetectionFromSensorEvent += OnLostNewDetectionFromSensor;
    }

    // Read the target's data from the snapshot. It still works in a lost event, even when the target was destroyed.
    private static string Describe(Detection detection)
    {
        DetectableSnapshot target = detection.Snapshot;
        return $"{target.Name} ({target.GetDetectionType()}, {target.Identifier}) at {target.Position}";
    }

    private void OnNewDetectionFromSensor(Detection detection)
    {
        Debug.Log($"Sensor {detection.CurrentlyDetectingSensor} - New Detection {detection.ID} {Describe(detection)}");
    }

    private void OnLostNewDetectionFromSensor(Detection detection)
    {
        Debug.Log($"Sensor {detection.CurrentlyDetectingSensor} - Lost Detection {detection.ID} {Describe(detection)} because {detection.LostReason}");
    }

    private void OnNewDetectionFromDetector(Detection detection)
    {
        Debug.Log($"Detector - New Detection {detection.ID} {Describe(detection)}");
    }

    private void OnLostDetectionFromDetector(Detection detection)
    {
        Debug.Log($"Detector - Lost Detection {detection.ID} {Describe(detection)} because {detection.LostReason}");
    }
}
