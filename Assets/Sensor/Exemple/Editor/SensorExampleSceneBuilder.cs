using System.Collections.Generic;
using SensorSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Builds a working example scene and default settings assets for this project's render pipeline.</summary>
public static class SensorExampleSceneBuilder
{
    private const string ScenePath = "Assets/Sensor/Exemple/Scenes/SensorExempleScene.unity";
    private const string SensorSettingsPath = "Assets/Sensor/Runtime/ScriptableObjects/SensorSettingsSO.asset";
    private const string DetectorSettingsPath = "Assets/Sensor/Runtime/ScriptableObjects/DetectorSettingsSO.asset";
    private const int TargetCount = 5;

    [MenuItem("Tools/Sensor/Create Example Scene")]
    public static void CreateExampleScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        SensorSettings sensorSettings = LoadOrCreateSensorSettings();
        DetectorSettings detectorSettings = LoadOrCreateAsset<DetectorSettings>(DetectorSettingsPath);
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = new Vector3(0f, 25f, -15f);
            camera.transform.LookAt(new Vector3(0f, 0f, 15f));
        }

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(10f, 1f, 10f);

        for (int i = 0; i < TargetCount; i++)
        {
            CreateTarget(i);
        }

        var sensorObject = new GameObject("Sensor");
        sensorObject.transform.position = Vector3.up;
        Sensor sensor = sensorObject.AddComponent<Sensor>();
        SetReference(sensor, "_settings", sensorSettings);

        var detectorObject = new GameObject("Detector");
        Detector detector = detectorObject.AddComponent<Detector>();
        SetReference(detector, "_detectorSettings", detectorSettings);
        var serializedDetector = new SerializedObject(detector);
        SerializedProperty sensors = serializedDetector.FindProperty("_sensors");
        sensors.arraySize = 1;
        sensors.GetArrayElementAtIndex(0).objectReferenceValue = sensor;
        serializedDetector.ApplyModifiedPropertiesWithoutUndo();

        var listenerObject = new GameObject("Detection Log");
        listenerObject.AddComponent<testSensorDetectorEvents>().Detector = detector;

        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeObject = sensorSettings;
        Debug.Log($"Sensor example scene saved to {ScenePath}. Press Play and watch the console.");
    }

    private static void CreateTarget(int index)
    {
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.name = $"Target {index + 1}";
        target.transform.position = new Vector3(-25f, 1f, 10f + index * 8f);
        target.AddComponent<DetectableEntity>();

        PingPongMover mover = target.AddComponent<PingPongMover>();
        mover.Offset = new Vector3(50f, 0f, 0f);
        mover.Speed = 0.05f + index * 0.02f;
    }

    private static SensorSettings LoadOrCreateSensorSettings()
    {
        var existing = AssetDatabase.LoadAssetAtPath<SensorSettings>(SensorSettingsPath);
        if (existing != null) return existing;

        SensorSettings settings = LoadOrCreateAsset<SensorSettings>(SensorSettingsPath);
        settings.Root = new And
        {
            Children = new List<Condition> { new InFrustum(), new WithRaycast() },
        };
        EditorUtility.SetDirty(settings);
        return settings;
    }

    private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    private static void SetReference(Object owner, string field, Object value)
    {
        var serialized = new SerializedObject(owner);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
