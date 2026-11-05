using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>Play mode view of a detector: everything its sensors look at, grouped as passed, failed and injected.</summary>
    public class DetectorLiveView : VisualElement
    {
        private const long RefreshMilliseconds = 250;
        private const string NothingPicked = "Pick an object to see why each sensor detects it or not.";

        private sealed class SensorSection
        {
            public Sensor Sensor;
            public CollapsibleGroup Foldout;
            public ConditionStatusTree Tree;
        }

        private readonly Detector _detector;
        private readonly TargetListView _targets = new TargetListView("Detector", SessionStateStore.Instance);
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

            SensorEditorStyles.AddSection(this, "Targets", "Everything this detector's sensors look at. Passed means at least one sensor detects it. Filter by name to find one.");
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

        private void Refresh()
        {
            if (_detector == null) return;

            List<Sensor> sensors = _detector.Sensors.Where(sensor => sensor != null).ToList();
            EnsureSections(sensors);

            List<SensorSnapshot> snapshots = sensors.Select(SensorSnapshot.Of).ToList();
            var ids = new Dictionary<IDetectable, int>();
            foreach (Detection detection in _detector.GetCurrentDetections())
            {
                ids[detection.DetectionComponent] = detection.ID;
            }

            List<TargetRow> rows = TargetRows.ForDetector(snapshots, ids);
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
                    Foldout = new CollapsibleGroup(sensor.name, true),
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
                List<string> detectedBy = TargetRows.GetDetectingSensors(_picked, snapshots);
                string idText = ids.TryGetValue(_picked, out int id) ? $" (detection #{id})" : string.Empty;
                _summary.text = TargetListModel.GetName(_picked)
                    + (detectedBy.Count > 0 ? $" is detected by {string.Join(", ", detectedBy)}" : " is not detected by any sensor")
                    + idText;
            }

            for (int i = 0; i < _sections.Count; i++)
            {
                SensorSection section = _sections[i];
                section.Foldout.text = _picked == null ? snapshots[i].Name : $"{snapshots[i].Name} - {TargetRows.DescribeState(_picked, snapshots[i])}";
                section.Tree.Refresh();
            }
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
