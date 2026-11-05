using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>A sensor's condition tree, with Passed, Failed or Not reached beside each node for the picked target.</summary>
    public class ConditionStatusTree : VisualElement
    {
        private readonly Sensor _sensor;
        private readonly Label _note = new Label();
        private readonly VisualElement _tree = new VisualElement();
        private readonly Dictionary<Condition, Label> _statusLabels = new Dictionary<Condition, Label>();

        private Condition _builtRoot;
        private bool _isBuilt;

        public ConditionStatusTree(Sensor sensor, bool showNote)
        {
            _sensor = sensor;
            SensorEditorStyles.Apply(this);

            _note.AddToClassList("sensor-hint");
            if (showNote)
            {
                Add(_note);
            }
            Add(_tree);
        }

        public void Refresh()
        {
            if (_sensor == null) return;

            if (!_isBuilt || _builtRoot != _sensor.RuntimeRoot)
            {
                BuildTree();
            }
            RefreshStatuses();
        }

        private void BuildTree()
        {
            _tree.Clear();
            _statusLabels.Clear();
            _builtRoot = _sensor.RuntimeRoot;
            _isBuilt = true;

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

            if (node is VisualCondition visual)
            {
                var toggle = new Toggle("Visual")
                {
                    tooltip = "Show visual: draw what this condition did for each object, in the Scene view and in the Game view with Gizmos on.",
                    value = visual.ShowVisual,
                };
                toggle.RegisterValueChangedCallback(change => visual.ShowVisual = change.newValue);
                toggle.AddToClassList("sensor-visual-toggle");
                row.Add(toggle);
            }

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

        private void RefreshStatuses()
        {
            IDetectable target = _sensor.DebugTarget;
            if (target != null && !target.IsAlive())
            {
                _sensor.DebugTarget = target = null;
            }

            bool isInjected = target != null && _sensor.WasInjected(target);
            string name = target == null ? null : TargetListModel.GetName(target);
            _note.text = target == null
                ? "No object picked."
                : isInjected
                    ? $"{name} is injected, so it bypasses the tree."
                    : $"Showing {name}. Not reached means an earlier result already decided the operator.";

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
    }
}
