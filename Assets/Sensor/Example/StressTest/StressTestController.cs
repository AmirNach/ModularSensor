using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SensorSystem;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// Stress test for the sensor: up to 2000 moving detectable objects, changed with a slider while the game runs, and a table of
/// how long each part of the sensor takes. Build the scene with Tools > Sensor > Create Stress Test Scene.
/// "Run benchmark" steps through several object counts and writes a report.
/// </summary>
public class StressTestController : MonoBehaviour
{
    public const int MaxEntities = 2000;

    [Range(0, MaxEntities)] public int EntityCount = 500;
    public bool MoveTargets = true;
    [Tooltip("Objects are spread over a square this wide, centered on the sensors.")]
    public float AreaSize = 180f;
    public float MoveSpeed = 4f;
    public Detector Detector;
    public int[] BenchmarkCounts = { 0, 100, 250, 500, 1000, 1500, 2000 };
    public float WarmupSeconds = 3f;
    public float SampleSeconds = 8f;

    /// <summary>Where the benchmark report is also written, when set.</summary>
    public string ReportPath;

    private sealed class Entity
    {
        public Transform Transform;
        public Vector3 Direction;
    }

    private readonly List<Entity> _pool = new List<Entity>();
    private readonly List<float> _frameTimes = new List<float>();
    private readonly List<long> _gcBytesPerFrame = new List<long>();
    private ProfilerRecorder _gcRecorder;
    private readonly List<SectionStats> _tableRows = new List<SectionStats>();
    private Transform _container;
    private int _activeCount;
    private bool _isSampling;
    private bool _isBenchmarking;
    private bool _sensorsOn = true;
    private string _status = "";
    private string _lastReport = "";
    private float _tableRefreshAt;
    private float _shownFrameAverage;
    private float _shownFrameMax;
    private float _windowStart;
    private float _windowSum;
    private int _windowFrames;
    private float _windowMax;
    private int _newEvents;
    private int _lostEvents;
    private Vector2 _scroll;

    public bool IsBenchmarking => _isBenchmarking;
    public string LastReport => _lastReport;
    public int ActiveCount => _activeCount;

    private void Awake()
    {
        _container = new GameObject("Stress Entities").transform;
        _container.SetParent(transform, false);
    }

    private void OnEnable()
    {
        SensorProfiler.Enabled = true;
        _gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        if (Detector != null)
        {
            Detector.OnNewDetectionFromDetectorEvent += CountNew;
            Detector.OnLostDetectionFromDetectorEvent += CountLost;
        }
    }

    private void OnDisable()
    {
        SensorProfiler.Enabled = false;
        _gcRecorder.Dispose();
        if (Detector != null)
        {
            Detector.OnNewDetectionFromDetectorEvent -= CountNew;
            Detector.OnLostDetectionFromDetectorEvent -= CountLost;
        }
    }

    private void Start()
    {
        SetCount(EntityCount);
    }

    private void CountNew(Detection _) => _newEvents++;

    private void CountLost(Detection _) => _lostEvents++;

    /// <summary>Makes exactly this many objects active. Extra ones are switched off, so the sensor stops finding them.</summary>
    public void SetCount(int count)
    {
        count = Mathf.Clamp(count, 0, MaxEntities);
        EntityCount = count;

        while (_pool.Count < count)
        {
            _pool.Add(CreateEntity(_pool.Count));
        }
        for (int i = 0; i < _pool.Count; i++)
        {
            bool shouldBeActive = i < count;
            if (_pool[i].Transform.gameObject.activeSelf != shouldBeActive)
            {
                _pool[i].Transform.gameObject.SetActive(shouldBeActive);
            }
        }
        _activeCount = count;
    }

    private Entity CreateEntity(int index)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = $"Entity {index + 1}";
        cube.transform.SetParent(_container, false);
        cube.transform.position = RandomPosition();
        cube.AddComponent<DetectableEntity>();

        // A kinematic body is how moving objects are normally set up. Moving a bare collider makes physics rebuild its static data.
        var body = cube.AddComponent<Rigidbody>();
        body.isKinematic = true;

        Vector2 flat = UnityEngine.Random.insideUnitCircle.normalized;
        return new Entity { Transform = cube.transform, Direction = new Vector3(flat.x, 0f, flat.y) };
    }

    private Vector3 RandomPosition()
    {
        float half = AreaSize * 0.5f;
        return new Vector3(UnityEngine.Random.Range(-half, half), UnityEngine.Random.Range(0.5f, 4f), UnityEngine.Random.Range(-10f, half * 2f));
    }

    private void Update()
    {
        if (MoveTargets)
        {
            MoveEntities(Time.deltaTime);
        }
        SampleFrameTime();
    }

    private void MoveEntities(float deltaTime)
    {
        float half = AreaSize * 0.5f;
        for (int i = 0; i < _activeCount; i++)
        {
            Entity entity = _pool[i];
            Vector3 position = entity.Transform.position + entity.Direction * (MoveSpeed * deltaTime);
            if (Mathf.Abs(position.x) > half) entity.Direction.x = -entity.Direction.x;
            if (position.z < -10f || position.z > half * 2f) entity.Direction.z = -entity.Direction.z;
            entity.Transform.position = position;
        }
    }

    private void SampleFrameTime()
    {
        float frame = Time.unscaledDeltaTime;
        if (_isSampling)
        {
            _frameTimes.Add(frame);
            _gcBytesPerFrame.Add(_gcRecorder.Valid ? _gcRecorder.LastValue : 0L);
        }

        _windowSum += frame;
        _windowFrames++;
        if (frame > _windowMax) _windowMax = frame;
        if (Time.unscaledTime - _windowStart >= 1f)
        {
            _shownFrameAverage = _windowSum / Mathf.Max(1, _windowFrames) * 1000f;
            _shownFrameMax = _windowMax * 1000f;
            _windowStart = Time.unscaledTime;
            _windowSum = 0f;
            _windowFrames = 0;
            _windowMax = 0f;
        }
    }

    /// <summary>Steps through BenchmarkCounts, measuring each, and ends with a report in LastReport.</summary>
    public void RunBenchmark()
    {
        if (!_isBenchmarking)
        {
            StartCoroutine(BenchmarkRoutine());
        }
    }

    private IEnumerator BenchmarkRoutine()
    {
        _isBenchmarking = true;
        bool moveBefore = MoveTargets;
        MoveTargets = true;
        var report = new StringBuilder();
        report.AppendLine($"Sensor stress test, {Application.platform}, {SystemInfo.processorType}, {SystemInfo.processorCount} threads, Unity {Application.unityVersion}");
        report.AppendLine($"Sensors: {(Detector != null ? Detector.Sensors.Count : 0)}, sample {SampleSeconds}s after {WarmupSeconds}s warm-up, objects move at {MoveSpeed} m/s");
        report.AppendLine("All times in milliseconds. Sections include the sections inside them. KB is memory allocated per call.");

        foreach (int count in BenchmarkCounts)
        {
            yield return Measure(report, count, true);
        }

        // The same objects with the sensors off, to see what the objects themselves cost.
        int top = BenchmarkCounts[BenchmarkCounts.Length - 1];
        yield return Measure(report, top, false);

        MoveTargets = moveBefore;
        SetSensorsOn(true);
        _lastReport = report.ToString();
        _status = "Benchmark finished.";
        _isBenchmarking = false;

        Debug.Log(_lastReport);
        if (!string.IsNullOrEmpty(ReportPath))
        {
            File.WriteAllText(ReportPath, _lastReport);
        }
    }

    private IEnumerator Measure(StringBuilder report, int count, bool sensorsOn)
    {
        _status = $"Benchmark: {count} objects, sensors {(sensorsOn ? "on" : "off")}, warming up...";
        SetSensorsOn(sensorsOn);
        SetCount(count);
        yield return new WaitForSeconds(WarmupSeconds);

        _status = $"Benchmark: {count} objects, sensors {(sensorsOn ? "on" : "off")}, measuring...";
        SensorProfiler.ResetAll();
        _frameTimes.Clear();
        _gcBytesPerFrame.Clear();
        _newEvents = _lostEvents = 0;
        _isSampling = true;
        yield return new WaitForSeconds(SampleSeconds);
        _isSampling = false;

        _frameTimes.Sort();
        float sum = 0f;
        foreach (float frame in _frameTimes) sum += frame;
        int frames = Mathf.Max(1, _frameTimes.Count);
        float average = sum / frames * 1000f;
        float p99 = _frameTimes.Count > 0 ? _frameTimes[Mathf.Min(_frameTimes.Count - 1, (int)(_frameTimes.Count * 0.99f))] * 1000f : 0f;
        float max = _frameTimes.Count > 0 ? _frameTimes[_frameTimes.Count - 1] * 1000f : 0f;

        report.AppendLine();
        report.AppendLine($"## {count} objects, sensors {(sensorsOn ? "ON" : "OFF")}");
        report.AppendLine($"frames {_frameTimes.Count}, frame avg {average:0.00} ms ({1000f / Mathf.Max(0.001f, average):0} fps), p99 {p99:0.00} ms, worst {max:0.00} ms, detection events new {_newEvents} / lost {_lostEvents}");
        long gcTotal = 0, gcWorst = 0;
        foreach (long bytes in _gcBytesPerFrame) { gcTotal += bytes; if (bytes > gcWorst) gcWorst = bytes; }
        report.AppendLine($"GC allocated: {gcTotal / 1024f / SampleSeconds:0.0} KB per second, worst frame {gcWorst / 1024f:0.0} KB (whole game, not only the sensor)");
        if (sensorsOn)
        {
            report.AppendLine(SensorProfiler.Report());
        }
    }

    private void SetSensorsOn(bool isOn)
    {
        _sensorsOn = isOn;
        if (Detector != null)
        {
            Detector.enabled = isOn;
        }
    }

    private void OnGUI()
    {
        const float width = 640f;
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(10f, 10f, width, Screen.height - 20f), GUI.skin.box);

        GUILayout.Label($"Stress test: {_activeCount} objects active.  Frame {_shownFrameAverage:0.0} ms avg, {_shownFrameMax:0.0} ms worst (last second)");

        GUILayout.BeginHorizontal();
        GUILayout.Label("Objects", GUILayout.Width(60f));
        int slider = Mathf.RoundToInt(GUILayout.HorizontalSlider(EntityCount, 0, MaxEntities));
        GUILayout.Label(EntityCount.ToString(), GUILayout.Width(50f));
        GUILayout.EndHorizontal();
        if (slider != EntityCount && !_isBenchmarking)
        {
            SetCount(slider);
        }

        GUILayout.BeginHorizontal();
        MoveTargets = GUILayout.Toggle(MoveTargets, "Objects move");
        bool sensors = GUILayout.Toggle(_sensorsOn, "Sensors on");
        if (sensors != _sensorsOn && !_isBenchmarking) SetSensorsOn(sensors);
        SensorProfiler.Enabled = GUILayout.Toggle(SensorProfiler.Enabled, "Measure");
        SensorProfiler.MeasureConditions = GUILayout.Toggle(SensorProfiler.MeasureConditions, "Time each condition (slows the sensor)");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset stats")) SensorProfiler.ResetAll();
        GUI.enabled = !_isBenchmarking;
        if (GUILayout.Button("Run benchmark")) RunBenchmark();
        GUI.enabled = true;
        if (GUILayout.Button("Copy table")) GUIUtility.systemCopyBuffer = SensorProfiler.Report();
        if (!string.IsNullOrEmpty(_lastReport) && GUILayout.Button("Copy benchmark report")) GUIUtility.systemCopyBuffer = _lastReport;
        GUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status);
        GUILayout.Label($"Detection events since reset: new {_newEvents}, lost {_lostEvents}");

        if (Time.unscaledTime >= _tableRefreshAt)
        {
            _tableRows.Clear();
            _tableRows.AddRange(SensorProfiler.Snapshot());
            _tableRefreshAt = Time.unscaledTime + 0.5f;
        }

        GUILayout.Label("section                                         calls    avg ms    max ms");
        _scroll = GUILayout.BeginScrollView(_scroll);
        foreach (SectionStats row in _tableRows)
        {
            GUILayout.Label($"{row.Name,-46} {row.Calls,6} {row.AverageMs,9:0.000} {row.MaxMs,9:0.000}");
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
