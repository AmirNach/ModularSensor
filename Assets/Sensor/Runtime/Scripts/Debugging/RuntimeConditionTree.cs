using System.Collections.Generic;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>
    /// A running sensor's condition tree as cards. Each shows what happened to the picked object and,
    /// when expanded, its values, which can be edited. Edits go to this sensor's own copy of the tree.
    /// </summary>
    public sealed class RuntimeConditionTree : VisualElement
    {
        private readonly Sensor _sensor;
        private readonly List<(Condition node, NodeCard card)> _cards = new List<(Condition, NodeCard)>();

        private Condition _builtRoot;
        private bool _isBuilt;

        public RuntimeConditionTree(Sensor sensor)
        {
            _sensor = sensor;
            AddToClassList("sensor-runtime-tree");
        }

        public int CardCount => _cards.Count;

        public NodeCard GetCard(Condition node)
        {
            foreach ((Condition candidate, NodeCard card) in _cards)
            {
                if (candidate == node) return card;
            }
            return null;
        }

        /// <summary>Rebuilds if the sensor got a new tree (after Reset), then updates statuses and values.</summary>
        public void Refresh()
        {
            if (_sensor == null) return;

            if (!_isBuilt || _builtRoot != _sensor.RuntimeRoot)
            {
                Build();
            }

            foreach ((Condition node, NodeCard card) in _cards)
            {
                card.SetStatus(NodeStatus.Get(_sensor, node));
                card.Refresh();
            }
        }

        private void Build()
        {
            Clear();
            _cards.Clear();
            _builtRoot = _sensor.RuntimeRoot;
            _isBuilt = true;

            if (_builtRoot == null)
            {
                var empty = new Label("This sensor's condition tree is empty.");
                empty.AddToClassList("sensor-hint");
                Add(empty);
                return;
            }
            AddNode(this, _builtRoot);
        }

        private void AddNode(VisualElement parent, Condition node)
        {
            string title = node.DisplayName;
            string badge = string.Empty;
            string kindClass = "sensor-card--leaf";
            bool isOperator = true;

            switch (node)
            {
                case And _:
                    badge = "AND";
                    kindClass = "sensor-card--and";
                    title = "All must pass";
                    break;
                case Or _:
                    badge = "OR";
                    kindClass = "sensor-card--or";
                    title = "Any must pass";
                    break;
                case Not _:
                    badge = "NOT";
                    kindClass = "sensor-card--not";
                    title = "Must not pass";
                    break;
                default:
                    isOperator = false;
                    break;
            }

            var card = new NodeCard(title, badge, kindClass, () => node.Summary, ParameterEditor.Build(node), isOperator);
            parent.Add(card);
            _cards.Add((node, card));

            var children = new List<Condition>();
            node.CollectChildren(children);
            foreach (Condition child in children)
            {
                AddNode(card.ChildArea, child);
            }
            card.SetExpanded(isOperator);
        }
    }
}
