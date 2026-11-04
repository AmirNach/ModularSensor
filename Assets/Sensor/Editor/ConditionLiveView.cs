using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>Play mode view of a running sensor: its targets, and why the chosen one passed or failed.</summary>
    public class ConditionLiveView : VisualElement
    {
        private const long RefreshMilliseconds = 250;

        private readonly Sensor _sensor;
        private readonly VisualElement _targets = new VisualElement();
        private readonly Label _note = new Label();
        private readonly VisualElement _tree = new VisualElement();
        private readonly Dictionary<Condition, Label> _statusLabels = new Dictionary<Condition, Label>();
        private readonly Dictionary<IDetectable, Button> _targetButtons = new Dictionary<IDetectable, Button>();

        private string _targetListKey;
        private Condition _builtRoot;

        public ConditionLiveView(Sensor sensor)
        {
            _sensor = sensor;
            SensorEditorStyles.Apply(this);
            AddToClassList("sensor-live");

            SensorEditorStyles.AddSection(this, "Targets", "Pick a target to see why it passes or fails.");
            _targets.AddToClassList("sensor-live-targets");
            Add(_targets);

            SensorEditorStyles.AddSection(this, "Condition tree");
            _note.AddToClassList("sensor-hint");
            Add(_note);
            Add(_tree);

            RegisterCallback<AttachToPanelEvent>(_ => schedule.Execute(Refresh).Every(RefreshMilliseconds));
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

            if (_builtRoot != _sensor.RuntimeRoot || (_builtRoot == null && _tree.childCount == 0))
            {
                BuildTree();
            }

            RefreshTargets();
            RefreshStatuses();
        }

        private void BuildTree()
        {
            _tree.Clear();
            _statusLabels.Clear();
            _builtRoot = _sensor.RuntimeRoot;

            if (_builtRoot == null)
            {
                _tree.Add(new Label("This sensor's condition tree is empty."));
                return;
            }
            AddNode(_tree, _builtRoot);
        }

        private void AddNode(VisualElement parent, Condition node)
        {
            var row = new VisualElement();
            row.AddToClassList("sensor-live-row");
            row.Add(new Label(node.DisplayName) { name = "title" });

            var status = new Label();
            status.AddToClassList("sensor-status");
            row.Add(status);
            parent.Add(row);
            _statusLabels[node] = status;

            var children = new List<Condition>();
            node.CollectChildren(children);
            if (children.Count == 0) return;

            var childArea = new VisualElement();
            childArea.AddToClassList("sensor-children");
            parent.Add(childArea);
            foreach (Condition child in children)
            {
                AddNode(childArea, child);
            }
        }

        private void RefreshTargets()
        {
            List<IDetectable> targets = _sensor.LastCandidates
                .Concat(_sensor.LastInjected)
                .Where(target => !target.IsDestroyed())
                .Distinct()
                .OrderBy(GetName)
                .ToList();

            string key = string.Join("|", targets.Select(target => target.GetIdentifier()));
            if (key != _targetListKey)
            {
                _targetListKey = key;
                _targets.Clear();
                _targetButtons.Clear();

                foreach (IDetectable target in targets)
                {
                    IDetectable captured = target;
                    var button = new Button(() => _sensor.DebugTarget = captured);
                    button.AddToClassList("sensor-live-target");
                    _targets.Add(button);
                    _targetButtons[target] = button;
                }

                if (targets.Count == 0)
                {
                    _targets.Add(new Label(_sensor.isActiveAndEnabled ? "No objects found yet." : "This sensor is off."));
                }
            }

            foreach (KeyValuePair<IDetectable, Button> pair in _targetButtons)
            {
                IDetectable target = pair.Key;
                pair.Value.text = $"{GetName(target)}  -  {GetState(target)}";
                pair.Value.EnableInClassList("sensor-live-target--selected", ReferenceEquals(target, _sensor.DebugTarget));
            }
        }

        private void RefreshStatuses()
        {
            IDetectable target = _sensor.DebugTarget;
            if (target != null && target.IsDestroyed())
            {
                _sensor.DebugTarget = target = null;
            }

            bool isInjected = target != null && _sensor.LastInjected.Contains(target);
            _note.text = target == null
                ? "No target picked."
                : isInjected
                    ? $"{GetName(target)} is injected, so it bypasses the tree."
                    : $"Showing {GetName(target)}. Not reached means an earlier result already decided the operator.";

            foreach (KeyValuePair<Condition, Label> pair in _statusLabels)
            {
                Label label = pair.Value;
                bool hasResult = target != null && !isInjected;
                bool wasReached = hasResult && _sensor.DebugResults.TryGetValue(pair.Key, out bool _);
                _sensor.DebugResults.TryGetValue(pair.Key, out bool result);

                label.text = !hasResult ? string.Empty : !wasReached ? "Not reached" : result ? "✓ Passed" : "✗ Failed";
                label.EnableInClassList("sensor-status--passed", wasReached && result);
                label.EnableInClassList("sensor-status--failed", wasReached && !result);
                label.EnableInClassList("sensor-status--skipped", hasResult && !wasReached);
            }
        }

        private string GetState(IDetectable target)
        {
            if (_sensor.LastInjected.Contains(target)) return "Injected";
            return _sensor.GetAllDetectedObjects().Contains(target) ? "Detected" : "Not detected";
        }

        private static string GetName(IDetectable target)
        {
            if (target is Component component) return component.name;
            return target.IsFake ? $"Fake {target.GetDetectionType()}" : target.GetIdentifier();
        }
    }
}
