using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>What one sensor saw in its last refresh. Plain data, so the row logic can be tested without running sensors.</summary>
    public sealed class SensorSnapshot
    {
        public string Name { get; }
        public Vector3 Position { get; }
        public IReadOnlyCollection<IDetectable> Candidates { get; }
        public IReadOnlyCollection<IDetectable> Injected { get; }
        public IReadOnlyCollection<IDetectable> Detected { get; }

        public SensorSnapshot(string name, Vector3 position, IReadOnlyCollection<IDetectable> candidates, IReadOnlyCollection<IDetectable> injected, IReadOnlyCollection<IDetectable> detected)
        {
            Name = name;
            Position = position;
            Candidates = candidates;
            Injected = injected;
            Detected = detected;
        }
    }

    /// <summary>Play mode view of a detector: everything its sensors look at, grouped as passed, failed and injected.</summary>
    public class DetectorLiveView : VisualElement
    {
        private const long RefreshMilliseconds = 250;
        private const string NothingPicked = "Pick an object to see why each sensor detects it or not.";

        private sealed class SensorSection
        {
            public Sensor Sensor;
            public Foldout Foldout;
            public ConditionStatusTree Tree;
        }

        private readonly Detector _detector;
        private readonly TargetListView _targets = new TargetListView("Detector");
        private readonly Label _message = new Label();
        private readonly Label _summary = new Label();
        private readonly VisualElement _sectionArea = new VisualElement();
        private readonly List<SensorSection> _sections = new List<SensorSection>();
        private IDetectable _picked;

        public DetectorLiveView(Detector detector)
        {
            _detector = detector;
            SensorEditorStyles.Apply(this);
            AddToClassList("sensor-live");

            SensorEditorStyles.AddSection(this, "Detections", "Everything this detector's sensors look at. Passed means at least one sensor detects it.");
            _message.AddToClassList("sensor-hint");
            Add(_message);
            _targets.TargetPicked += target => _picked = target;
            Add(_targets);

            SensorEditorStyles.AddSection(this, "Details");
            _summary.AddToClassList("sensor-detector-summary");
            Add(_summary);
            Add(_sectionArea);

            RegisterCallback<AttachToPanelEvent>(_ => schedule.Execute(Refresh).Every(RefreshMilliseconds).StartingIn(0));
            RegisterCallback<DetachFromPanelEvent>(_ => ClearDebugTargets());
        }

        /// <summary>
        /// One row per object any sensor looked at. It passed if some sensor detects it, was injected if every
        /// sensor that detects it got it from an injector, and failed if no sensor detects it.
        /// </summary>
        public static List<TargetRow> BuildRows(IReadOnlyList<SensorSnapshot> sensors, IReadOnlyDictionary<IDetectable, int> detectionIds)
        {
            var rows = new List<TargetRow>();
            var seen = new HashSet<IDetectable>();

            foreach (SensorSnapshot sensor in sensors)
            {
                foreach (IDetectable target in sensor.Candidates.Concat(sensor.Injected))
                {
                    if (target.IsDestroyed() || !seen.Add(target)) continue;

                    rows.Add(CreateRow(target, sensors, detectionIds));
                }
            }
            return rows;
        }

        /// <summary>The names of the sensors that detect the target.</summary>
        public static List<string> GetDetectingSensors(IDetectable target, IReadOnlyList<SensorSnapshot> sensors)
        {
            return sensors.Where(sensor => sensor.Detected.Contains(target)).Select(sensor => sensor.Name).ToList();
        }

        private static TargetRow CreateRow(IDetectable target, IReadOnlyList<SensorSnapshot> sensors, IReadOnlyDictionary<IDetectable, int> detectionIds)
        {
            var detectedBy = new List<string>();
            bool allInjected = true;
            float nearest = float.MaxValue;

            foreach (SensorSnapshot sensor in sensors)
            {
                bool isInjected = sensor.Injected.Contains(target);
                if (!isInjected && !sensor.Candidates.Contains(target)) continue;

                nearest = Mathf.Min(nearest, Vector3.Distance(sensor.Position, target.GetCenterPosition()));
                if (sensor.Detected.Contains(target))
                {
                    detectedBy.Add(sensor.Name);
                    allInjected &= isInjected;
                }
            }

            TargetState state = detectedBy.Count == 0 ? TargetState.Failed : allInjected ? TargetState.Injected : TargetState.Passed;

            var parts = new List<string>();
            if (detectionIds.TryGetValue(target, out int id))
            {
                parts.Add($"#{id}");
            }
            parts.Add($"{nearest:0.#} m");
            if (detectedBy.Count > 0)
            {
                parts.Add(string.Join(", ", detectedBy));
            }
            return new TargetRow(target, TargetListModel.GetName(target), state, nearest, string.Join("  ", parts));
        }

        private static SensorSnapshot CreateSnapshot(Sensor sensor)
        {
            return new SensorSnapshot($"{sensor.name} ({sensor.Type})", sensor.transform.position, sensor.LastCandidates, sensor.LastInjected, sensor.GetAllDetectedObjects());
        }

        private void Refresh()
        {
            if (_detector == null) return;

            List<Sensor> sensors = _detector.Sensors.Where(sensor => sensor != null).ToList();
            EnsureSections(sensors);

            List<SensorSnapshot> snapshots = sensors.Select(CreateSnapshot).ToList();
            var ids = new Dictionary<IDetectable, int>();
            foreach (Detection detection in _detector.GetCurrentDetections())
            {
                ids[detection.DetectionComponent] = detection.ID;
            }

            List<TargetRow> rows = BuildRows(snapshots, ids);
            if (_picked != null && _picked.IsDestroyed())
            {
                _picked = null;
            }
            _targets.SetRows(rows);
            _targets.SetSelected(_picked);

            _message.text = rows.Count > 0 ? string.Empty : _detector.isActiveAndEnabled ? "No objects found yet." : "This detector is off.";
            _message.style.display = rows.Count > 0 ? DisplayStyle.None : DisplayStyle.Flex;

            foreach (SensorSection section in _sections)
            {
                section.Sensor.DebugTarget = _picked;
            }
            RefreshDetails(snapshots, ids);
        }

        private void EnsureSections(List<Sensor> sensors)
        {
            if (_sections.Select(section => section.Sensor).SequenceEqual(sensors)) return;

            ClearDebugTargets();
            _sections.Clear();
            _sectionArea.Clear();

            foreach (Sensor sensor in sensors)
            {
                var section = new SensorSection
                {
                    Sensor = sensor,
                    Foldout = new Foldout { text = sensor.name, value = true },
                    Tree = new ConditionStatusTree(sensor, false),
                };
                section.Foldout.AddToClassList("sensor-target-group");
                section.Foldout.Add(section.Tree);
                _sectionArea.Add(section.Foldout);
                _sections.Add(section);
            }
        }

        private void RefreshDetails(List<SensorSnapshot> snapshots, Dictionary<IDetectable, int> ids)
        {
            if (_picked == null)
            {
                _summary.text = NothingPicked;
            }
            else
            {
                List<string> detectedBy = GetDetectingSensors(_picked, snapshots);
                string idText = ids.TryGetValue(_picked, out int id) ? $" (detection #{id})" : string.Empty;
                _summary.text = TargetListModel.GetName(_picked)
                    + (detectedBy.Count > 0 ? $" is detected by {string.Join(", ", detectedBy)}" : " is not detected by any sensor")
                    + idText;
            }

            for (int i = 0; i < _sections.Count; i++)
            {
                SensorSection section = _sections[i];
                section.Foldout.text = _picked == null ? snapshots[i].Name : $"{snapshots[i].Name} - {DescribeState(_picked, snapshots[i])}";
                section.Tree.Refresh();
            }
        }

        private static string DescribeState(IDetectable target, SensorSnapshot sensor)
        {
            if (sensor.Injected.Contains(target)) return "Injected";
            if (sensor.Detected.Contains(target)) return "Passed";
            return sensor.Candidates.Contains(target) ? "Failed" : "Not looked at";
        }

        private void ClearDebugTargets()
        {
            foreach (SensorSection section in _sections)
            {
                if (section.Sensor != null)
                {
                    section.Sensor.DebugTarget = null;
                }
            }
        }
    }
}
