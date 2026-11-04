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

    private static string GetName(Detection detection)
    {
        return detection.DetectionComponent is MonoBehaviour behaviour && behaviour != null ? behaviour.gameObject.name : "(fake or destroyed)";
    }

    private void OnNewDetectionFromSensor(Detection detection)
    {
        Debug.Log($"Sensor {detection.CurrentlyDetectingSensor} - New Detection {detection.ID} {GetName(detection)}");
    }

    private void OnLostNewDetectionFromSensor(Detection detection)
    {
        Debug.Log($"Sensor {detection.CurrentlyDetectingSensor} - Lost Detection {detection.ID} {GetName(detection)}");
    }

    private void OnNewDetectionFromDetector(Detection detection)
    {
        Debug.Log($"Detector - New Detection {detection.ID} {GetName(detection)}");
    }

    private void OnLostDetectionFromDetector(Detection detection)
    {
        Debug.Log($"Detector - Lost Detection {detection.ID} {GetName(detection)}");
    }
}
