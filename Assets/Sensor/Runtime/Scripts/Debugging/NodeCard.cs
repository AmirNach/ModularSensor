using System;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>
    /// One condition or injector in the runtime view: its name, a one-line summary, what happened to the
    /// picked object, and its values to edit when expanded. Children of an operator go inside it.
    /// </summary>
    public sealed class NodeCard : VisualElement
    {
        public VisualElement ChildArea { get; } = new VisualElement();
        public ParameterPanel Parameters { get; }
        public bool Expanded { get; private set; }
        public FoldArrow Arrow => _arrow;

        private readonly Func<string> _getSummary;
        private readonly FoldArrow _arrow = new FoldArrow();
        private readonly Label _summary = new Label();
        private readonly Label _status = new Label();
        private readonly VisualElement _body = new VisualElement();
        private Toggle _visualToggle;
        private VisualCondition _visualCondition;

        /// <param name="badge">Short tag such as AND. Empty for plain conditions.</param>
        /// <param name="kindClass">Picks the accent color, such as sensor-card--and.</param>
        public NodeCard(string title, string badge, string kindClass, Func<string> getSummary, ParameterPanel parameters, bool startExpanded)
        {
            _getSummary = getSummary;
            Parameters = parameters;

            AddToClassList("sensor-card");
            AddToClassList(kindClass);

            var header = new VisualElement();
            header.AddToClassList("sensor-card-header");

            header.Add(_arrow);

            if (!string.IsNullOrEmpty(badge))
            {
                var badgeLabel = new Label(badge);
                badgeLabel.AddToClassList("sensor-badge");
                header.Add(badgeLabel);
            }

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("sensor-card-title");
            header.Add(titleLabel);

            _summary.AddToClassList("sensor-card-summary");
            header.Add(_summary);

            var spacer = new VisualElement();
            spacer.AddToClassList("sensor-header-spacer");
            header.Add(spacer);

            _status.AddToClassList("sensor-status");
            header.Add(_status);

            _body.AddToClassList("sensor-card-body");
            if (parameters != null)
            {
                _body.Add(parameters);
            }
            ChildArea.AddToClassList("sensor-children");

            Add(header);
            Add(_body);
            Add(ChildArea);

            _arrow.clicked += () => SetExpanded(!Expanded);
            header.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target != _arrow && HasContent)
                {
                    SetExpanded(!Expanded);
                }
            });

            SetExpanded(startExpanded);
        }

        /// <summary>Adds a quick "Visual" switch to the header, so a condition's visual can be turned on and off without expanding it.</summary>
        public void AddVisualToggle(VisualCondition condition)
        {
            _visualCondition = condition;
            _visualToggle = new Toggle("Visual")
            {
                tooltip = "Show visual: draw what this condition did for each object, in the Scene view and in the Game view with Gizmos on.",
                value = condition.ShowVisual,
            };
            _visualToggle.AddToClassList("sensor-visual-toggle");
            _visualToggle.RegisterValueChangedCallback(change => _visualCondition.ShowVisual = change.newValue);
            _visualToggle.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            _status.parent.Insert(_status.parent.IndexOf(_status), _visualToggle);
        }

        private bool HasContent => (Parameters != null && Parameters.Paths.Count > 0) || ChildArea.childCount > 0;

        public void SetExpanded(bool expanded)
        {
            Expanded = expanded && HasContent;
            _arrow.SetState(HasContent, Expanded);
            _body.style.display = Expanded && Parameters != null && Parameters.Paths.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            ChildArea.style.display = Expanded && ChildArea.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            EnableInClassList("sensor-card--expanded", Expanded);
            RefreshSummary();
        }

        public void SetStatus(NodeStatusKind kind)
        {
            _status.text = NodeStatus.Text(kind);
            _status.EnableInClassList("sensor-status--passed", kind == NodeStatusKind.Passed);
            _status.EnableInClassList("sensor-status--failed", kind == NodeStatusKind.Failed);
            _status.EnableInClassList("sensor-status--skipped", kind == NodeStatusKind.NotReached);
        }

        /// <summary>Re-reads the summary and every value that is not being edited.</summary>
        public void Refresh()
        {
            RefreshSummary();
            Parameters?.Refresh();
            _visualToggle?.SetValueWithoutNotify(_visualCondition.ShowVisual);
        }

        private void RefreshSummary()
        {
            string summary = _getSummary?.Invoke();
            _summary.text = summary ?? string.Empty;
            _summary.tooltip = summary;
            _summary.style.display = !Expanded && !string.IsNullOrEmpty(summary) ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
