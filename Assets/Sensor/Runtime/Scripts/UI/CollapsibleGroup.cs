using System;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>
    /// A titled section that opens and closes. It replaces Unity's Foldout, whose arrow is drawn differently in
    /// the Inspector, in an Editor window and in a build. This one uses the same arrow as the condition cards,
    /// so it looks and lines up the same everywhere. Children added to it go into its content.
    /// </summary>
    public sealed class CollapsibleGroup : VisualElement
    {
        private readonly VisualElement _header = new VisualElement();
        private readonly FoldArrow _arrow = new FoldArrow();
        private readonly Label _title = new Label();
        private readonly VisualElement _content = new VisualElement();
        private bool _expanded = true;

        public CollapsibleGroup() : this(string.Empty, true)
        {
        }

        public CollapsibleGroup(string title, bool expanded)
        {
            AddToClassList("sensor-group");
            _header.AddToClassList("sensor-group-header");
            _title.AddToClassList("sensor-group-title");
            _content.AddToClassList("sensor-group-content");

            _header.Add(_arrow);
            _header.Add(_title);
            hierarchy.Add(_header);
            hierarchy.Add(_content);

            _title.text = title;
            // A click anywhere on the header, the arrow included, toggles the group.
            _header.RegisterCallback<ClickEvent>(_ => SetValue(!_expanded));
            SetValueWithoutNotify(expanded);
        }

        public override VisualElement contentContainer => _content;

        /// <summary>Raised when the user opens or closes the group. Not raised by SetValueWithoutNotify.</summary>
        public event Action<bool> ValueChanged;

        public string text
        {
            get => _title.text;
            set => _title.text = value;
        }

        public bool value => _expanded;

        public FoldArrow Arrow => _arrow;

        public VisualElement Header => _header;

        public void SetValueWithoutNotify(bool expanded)
        {
            _expanded = expanded;
            _arrow.SetState(true, expanded);
            _content.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Opens or closes the group as if the user had, and tells listeners.</summary>
        public void SetValue(bool expanded)
        {
            if (_expanded == expanded) return;

            SetValueWithoutNotify(expanded);
            ValueChanged?.Invoke(expanded);
        }
    }
}
