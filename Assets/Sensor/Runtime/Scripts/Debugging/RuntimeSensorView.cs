using System.Collections.Generic;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>One sensor in the overlay: its objects grouped as passed, failed and injected, its condition tree with editable values, and its injectors.</summary>
    public sealed class RuntimeSensorView : VisualElement
    {
        private const long RefreshMilliseconds = 250;

        private readonly Sensor _sensor;
        private readonly TargetListView _targets = new TargetListView("Overlay.Sensor");
        private readonly Label _message = new Label();
        private readonly RuntimeConditionTree _tree;
        private readonly VisualElement _injectorArea = new VisualElement();
        private readonly List<NodeCard> _injectorCards = new List<NodeCard>();

        private object _builtInjectors;

        public RuntimeSensorView(Sensor sensor)
        {
            _sensor = sensor;
            AddToClassList("sensor-live");

            CollapsibleGroup targets = SensorStyles.AddFoldoutSection(this, "Targets", "Pick an object to see why it passes or fails. Filter by name to find it.");
            _message.AddToClassList("sensor-hint");
            targets.Add(_message);
            _targets.TargetPicked += target => _sensor.DebugTarget = target;
            targets.Add(_targets);

            CollapsibleGroup conditions = SensorStyles.AddFoldoutSection(this, "Condition tree", "Expand a condition to change its values. Changes apply to this sensor while the game runs.");
            _tree = new RuntimeConditionTree(sensor);
            conditions.Add(_tree);

            var reset = new Button(() => SensorDebug.Dispatch(new ResetSensorValuesCommand(_sensor)))
            {
                text = "Reset values",
                tooltip = "Put every value back to what the settings asset says",
            };
            reset.AddToClassList("sensor-reset-button");
            conditions.Add(reset);

            CollapsibleGroup injectors = SensorStyles.AddFoldoutSection(this, "Injected detections", null, false);
            injectors.Add(_injectorArea);

            RegisterCallback<AttachToPanelEvent>(_ => schedule.Execute(Refresh).Every(RefreshMilliseconds).StartingIn(0));
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (_sensor != null)
                {
                    _sensor.DebugTarget = null;
                }
            });
        }

        public RuntimeConditionTree Tree => _tree;

        public TargetListView Targets => _targets;

        public void Refresh()
        {
            using (SensorProfiler.OverlayRefresh.Measure())
            {
                RefreshCore();
            }
        }

        private void RefreshCore()
        {
            if (_sensor == null) return;

            _tree.Refresh();
            RefreshInjectors();

            List<TargetRow> rows;
            using (SensorProfiler.OverlayRows.Measure()) rows = TargetRows.ForSensor(_sensor);
            using (SensorProfiler.OverlayList.Measure()) _targets.SetRows(rows);
            _targets.SetSelected(_sensor.DebugTarget);

            _message.text = rows.Count > 0 ? string.Empty : _sensor.isActiveAndEnabled ? "No objects found yet." : "This sensor is off.";
            _message.style.display = rows.Count > 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void RefreshInjectors()
        {
            // Reset gives the sensor new injectors, so rebuild when the list is a different one.
            if (!ReferenceEquals(_builtInjectors, _sensor.RuntimeInjectors))
            {
                _builtInjectors = _sensor.RuntimeInjectors;
                _injectorArea.Clear();
                _injectorCards.Clear();

                foreach (DetectionInjector injector in _sensor.RuntimeInjectors)
                {
                    if (injector == null) continue;

                    DetectionInjector captured = injector;
                    var card = new NodeCard(injector.DisplayName, "INJECT", "sensor-card--injector", () => captured.Summary, ParameterEditor.Build(injector), false);
                    _injectorArea.Add(card);
                    _injectorCards.Add(card);
                }

                if (_injectorCards.Count == 0)
                {
                    var none = new Label("None");
                    none.AddToClassList("sensor-hint");
                    _injectorArea.Add(none);
                }
            }

            foreach (NodeCard card in _injectorCards)
            {
                card.Refresh();
            }
        }
    }
}
