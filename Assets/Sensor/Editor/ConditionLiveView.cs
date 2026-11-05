using System.Collections.Generic;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>Play mode view of a running sensor: its objects grouped as passed, failed and injected, and why the picked one is where it is.</summary>
    public class ConditionLiveView : VisualElement
    {
        private const long RefreshMilliseconds = 250;

        private readonly Sensor _sensor;
        private readonly TargetListView _targets = new TargetListView("Sensor", SessionStateStore.Instance);
        private readonly Label _message = new Label();
        private readonly ConditionStatusTree _status;

        public ConditionLiveView(Sensor sensor)
        {
            _sensor = sensor;
            SensorEditorStyles.Apply(this);
            AddToClassList("sensor-live");

            SensorEditorStyles.AddSection(this, "Targets", "Pick an object to see why it passes or fails. Filter by name to find it.");
            _message.AddToClassList("sensor-hint");
            Add(_message);
            _targets.TargetPicked += target => _sensor.DebugTarget = target;
            Add(_targets);

            SensorEditorStyles.AddSection(this, "Condition tree");
            _status = new ConditionStatusTree(sensor, true);
            Add(_status);

            RegisterCallback<AttachToPanelEvent>(_ => schedule.Execute(Refresh).Every(RefreshMilliseconds).StartingIn(0));
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (_sensor != null)
                {
                    _sensor.DebugTarget = null;
                }
            });
        }

        private void Refresh()
        {
            if (_sensor == null) return;

            _status.Refresh();

            List<TargetRow> rows = TargetRows.ForSensor(_sensor);
            _targets.SetRows(rows);
            _targets.SetSelected(_sensor.DebugTarget);

            _message.text = rows.Count > 0 ? string.Empty : _sensor.isActiveAndEnabled ? "No objects found yet." : "This sensor is off.";
            _message.style.display = rows.Count > 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
