using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>One detector in the overlay: everything its sensors look at, and for the picked object, each sensor's tree with its values.</summary>
    public sealed class RuntimeDetectorView : VisualElement
    {
        private const long RefreshMilliseconds = 250;
        private const string NothingPicked = "Pick an object to see why each sensor detects it or not.";

        private sealed class SensorSection
        {
            public Sensor Sensor;
            public CollapsibleGroup Foldout;
            public RuntimeConditionTree Tree;
        }

        private readonly Detector _detector;
        private readonly TargetListView _targets = new TargetListView("Overlay.Detector");
        private readonly Label _message = new Label();
        private readonly Label _summary = new Label();
        private readonly VisualElement _sectionArea = new VisualElement();
        private readonly List<SensorSection> _sections = new List<SensorSection>();
        private IDetectable _picked;

        public RuntimeDetectorView(Detector detector)
        {
            _detector = detector;
            AddToClassList("sensor-live");

            CollapsibleGroup detections = SensorStyles.AddFoldoutSection(this, "Detections", "Everything this detector's sensors look at. Passed means at least one sensor detects it.");
            _message.AddToClassList("sensor-hint");
            detections.Add(_message);
            _targets.TargetPicked += target => _picked = target;
            detections.Add(_targets);

            CollapsibleGroup details = SensorStyles.AddFoldoutSection(this, "Details");
            _summary.AddToClassList("sensor-detector-summary");
            details.Add(_summary);
            details.Add(_sectionArea);

            RegisterCallback<AttachToPanelEvent>(_ => schedule.Execute(Refresh).Every(RefreshMilliseconds).StartingIn(0));
            RegisterCallback<DetachFromPanelEvent>(_ => ClearDebugTargets());
        }

        public TargetListView Targets => _targets;

        public void Refresh()
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
                    Tree = new RuntimeConditionTree(sensor),
                };
                section.Foldout.AddToClassList("sensor-target-group");
                section.Foldout.Add(section.Tree);

                Sensor captured = sensor;
                var reset = new Button(() => SensorDebug.Dispatch(new ResetSensorValuesCommand(captured)))
                {
                    text = "Reset values",
                    tooltip = "Put every value of this sensor back to what its settings asset says",
                };
                reset.AddToClassList("sensor-reset-button");
                section.Foldout.Add(reset);

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
