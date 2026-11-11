using System.Collections.Generic;
using SensorSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Builds the stress test scene: two sensors with different condition trees, a detector merging them, walls to block rays, and the controller.</summary>
public static class SensorStressSceneBuilder
{
    private const string Folder = "Assets/Sensor/Example/StressTest";
    private const string ScenePath = Folder + "/SensorStressTestScene.unity";
    private const string FrontSettingsPath = Folder + "/StressFrontSensorSettings.asset";
    private const string WideSettingsPath = Folder + "/StressWideSensorSettings.asset";
    private const string DetectorSettingsPath = Folder + "/StressDetectorSettings.asset";

    [MenuItem("Tools/Sensor/Create Stress Test Scene")]
    public static void CreateStressScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        SensorSettings front = CreateSettings(FrontSettingsPath, new And
        {
            Children = new List<Condition> { new InFrustum { MaxDistance = 150f }, new WithRaycast() },
        });
        SensorSettings wide = CreateSettings(WideSettingsPath, new And
        {
            Children = new List<Condition>
            {
                new IsAngleClamped { MinAngle = -90f, MaxAngle = 90f },
                new InAbsoluteRangesWithProbabilities
                {
                    DistanceProbabilities = new List<DistanceProbability>
                    {
                        new DistanceProbability { Distance = 40f, Probability = 100f },
                        new DistanceProbability { Distance = 100f, Probability = 60f },
                        new DistanceProbability { Distance = 200f, Probability = 20f },
                    },
                },
            },
        });
        var detectorSettings = LoadOrCreate<DetectorSettings>(DetectorSettingsPath);
        AssetDatabase.SaveAssets();

        // The old scene's objects are about to be destroyed. A selected one would leave the Inspector drawing a dead object.
        Selection.activeObject = null;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Making a new scene can unload assets that nothing in the scene uses yet, so load them again from disk.
        front = AssetDatabase.LoadAssetAtPath<SensorSettings>(FrontSettingsPath);
        wide = AssetDatabase.LoadAssetAtPath<SensorSettings>(WideSettingsPath);
        detectorSettings = AssetDatabase.LoadAssetAtPath<DetectorSettings>(DetectorSettingsPath);
        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = new Vector3(0f, 120f, -60f);
            camera.transform.LookAt(new Vector3(0f, 0f, 70f));
        }

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, 0f, 80f);
        ground.transform.localScale = new Vector3(30f, 1f, 30f);

        // Walls in front of the sensors, so some rays are blocked and the raycast has real work to do.
        for (int i = 0; i < 6; i++)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"Wall {i + 1}";
            wall.transform.position = new Vector3(-60f + i * 24f, 4f, 30f + (i % 2) * 40f);
            wall.transform.localScale = new Vector3(14f, 8f, 2f);
        }

        Sensor frontSensor = CreateSensor("Front Sensor", front, new Vector3(0f, 1.5f, 0f));
        Sensor wideSensor = CreateSensor("Wide Sensor", wide, new Vector3(0f, 1.5f, 5f));

        var detectorObject = new GameObject("Detector");
        Detector detector = detectorObject.AddComponent<Detector>();
        Set(detector, "_detectorSettings", detectorSettings);
        var serializedDetector = new SerializedObject(detector);
        SerializedProperty sensors = serializedDetector.FindProperty("_sensors");
        sensors.arraySize = 2;
        sensors.GetArrayElementAtIndex(0).objectReferenceValue = frontSensor;
        sensors.GetArrayElementAtIndex(1).objectReferenceValue = wideSensor;
        serializedDetector.ApplyModifiedPropertiesWithoutUndo();

        var controllerObject = new GameObject("Stress Test");
        controllerObject.AddComponent<StressTestController>().Detector = detector;
        controllerObject.AddComponent<SensorDebugOverlay>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"Stress test scene saved to {ScenePath}. Press Play, move the Objects slider, or press Run benchmark.");
    }

    private static Sensor CreateSensor(string name, SensorSettings settings, Vector3 position)
    {
        var owner = new GameObject(name);
        owner.transform.position = position;
        Sensor sensor = owner.AddComponent<Sensor>();
        Set(sensor, "_settings", settings);
        return sensor;
    }

    private static SensorSettings CreateSettings(string path, Condition root)
    {
        var settings = LoadOrCreate<SensorSettings>(path);
        var serialized = new SerializedObject(settings);
        serialized.FindProperty("_refreshRate").floatValue = SensorSettings.MinRefreshRate;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        settings.Root = root;
        EditorUtility.SetDirty(settings);
        return settings;
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder("Assets/Sensor/Example", "StressTest");
        }
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    private static void Set(Object owner, string field, Object value)
    {
        var serialized = new SerializedObject(owner);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
