using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SensorSystem
{
    public static class SensorEditorStyles
    {
        /// <summary>The stylesheet is shared with the runtime overlay and lives in a Resources folder.</summary>
        public static void Apply(VisualElement element)
        {
            SensorStyles.Apply(element);
        }

        public static void AddFields(VisualElement parent, SerializedProperty owner, params string[] skippedFields)
        {
            SerializedProperty field = owner.Copy();
            SerializedProperty end = owner.GetEndProperty();
            Type ownerType = owner.propertyType == SerializedPropertyType.ManagedReference ? owner.managedReferenceValue?.GetType() : null;
            bool enterChildren = true;

            while (field.NextVisible(enterChildren) && !SerializedProperty.EqualContents(field, end))
            {
                enterChildren = false;
                if (Array.IndexOf(skippedFields, field.name) < 0)
                {
                    var propertyField = new PropertyField(field.Copy());
                    parent.Add(propertyField);
                    ApplyShowIf(propertyField, owner, field.name, ownerType);
                }
            }
        }

        /// <summary>Hides a field while the bool named by its [ShowIf] attribute is not the value that shows it.</summary>
        private static void ApplyShowIf(PropertyField propertyField, SerializedProperty owner, string fieldName, Type ownerType)
        {
            FieldInfo info = ownerType?.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            var attribute = info?.GetCustomAttribute<ShowIfAttribute>();
            if (attribute == null) return;

            SerializedProperty condition = owner.FindPropertyRelative(attribute.BoolField);
            if (condition == null || condition.propertyType != SerializedPropertyType.Boolean) return;

            void Update(SerializedProperty changed) => propertyField.style.display = changed.boolValue == attribute.ShowWhen ? DisplayStyle.Flex : DisplayStyle.None;
            Update(condition);
            propertyField.TrackPropertyValue(condition, Update);
        }

        public static Label AddSection(VisualElement parent, string title, string hint = null)
        {
            var label = new Label(title);
            label.AddToClassList("sensor-section-title");
            parent.Add(label);

            if (hint != null)
            {
                var hintLabel = new Label(hint);
                hintLabel.AddToClassList("sensor-hint");
                parent.Add(hintLabel);
            }
            return label;
        }
    }

    /// <summary>A menu row, usable by both the right-click menu and the "..." button.</summary>
    public readonly struct CardMenuEntry
    {
        public readonly string Label;
        public readonly Action Action;
        public readonly bool Enabled;

        public CardMenuEntry(string label, Action action, bool enabled = true)
        {
            Label = label;
            Action = action;
            Enabled = enabled;
        }
    }

    /// <summary>A collapsible row with a title, a one-line summary, warnings and a menu.</summary>
    public class TreeCard : VisualElement
    {
        public VisualElement Header { get; } = new VisualElement();
        public VisualElement Body { get; } = new VisualElement();
        public VisualElement ChildArea { get; } = new VisualElement();
        public Label Handle { get; } = new Label("≡");
        public Label Badge { get; } = new Label();
        public Label TitleLabel { get; } = new Label();
        public Label SummaryLabel { get; } = new Label();
        public bool Expanded { get; private set; }

        public event Action<bool> ExpandedChanged;

        private readonly FoldArrow _arrow = new FoldArrow();
        private readonly VisualElement _warning = new VisualElement();
        private readonly Label _warningLabel = new Label();
        private readonly Button _menuButton = new Button { text = "..." };

        protected TreeCard()
        {
            AddToClassList("sensor-card");
            Header.AddToClassList("sensor-card-header");
            Body.AddToClassList("sensor-card-body");
            ChildArea.AddToClassList("sensor-children");
            Handle.AddToClassList("sensor-handle");
            Handle.tooltip = "Drag to move";
            Badge.AddToClassList("sensor-badge");
            TitleLabel.AddToClassList("sensor-card-title");
            SummaryLabel.AddToClassList("sensor-card-summary");
            _warning.AddToClassList("sensor-warning");
            _warningLabel.AddToClassList("sensor-warning-text");
            _menuButton.AddToClassList("sensor-menu-button");
            _menuButton.tooltip = "More actions";

            var warningIcon = new Image { image = EditorGUIUtility.FindTexture("console.warnicon.sml") };
            warningIcon.AddToClassList("sensor-warning-icon");
            _warning.Add(warningIcon);
            _warning.Add(_warningLabel);

            Header.Add(Handle);
            Header.Add(_arrow);
            Header.Add(Badge);
            Header.Add(TitleLabel);
            Header.Add(SummaryLabel);
            var spacer = new VisualElement();
            spacer.AddToClassList("sensor-header-spacer");
            Header.Add(spacer);
            Header.Add(_warning);
            Header.Add(_menuButton);
            Add(Header);
            Add(Body);
            Add(ChildArea);

            _arrow.clicked += () => SetExpanded(!Expanded, true);
            _menuButton.clicked += ShowMenuFromButton;
            Header.RegisterCallback<ClickEvent>(OnHeaderClicked);
            Header.AddManipulator(new ContextualMenuManipulator(OnContextMenu));
            SetWarning(null);
        }

        protected virtual List<CardMenuEntry> BuildMenu() => new List<CardMenuEntry>();

        public void SetExpanded(bool expanded, bool notify)
        {
            Expanded = expanded;
            _arrow.SetState(HasContent, expanded);
            Body.style.display = expanded && Body.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            ChildArea.style.display = expanded && ChildArea.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            SummaryLabel.style.display = !expanded && !string.IsNullOrEmpty(SummaryLabel.text) ? DisplayStyle.Flex : DisplayStyle.None;
            EnableInClassList("sensor-card--expanded", expanded);

            if (notify)
            {
                ExpandedChanged?.Invoke(expanded);
            }
        }

        public void SetSummary(string summary)
        {
            SummaryLabel.text = summary ?? string.Empty;
            SummaryLabel.tooltip = summary;
            SetExpanded(Expanded, false);
        }

        public void SetWarning(string text)
        {
            _warningLabel.text = text ?? string.Empty;
            _warning.tooltip = text;
            _warning.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private bool HasContent => Body.childCount > 0 || ChildArea.childCount > 0;

        /// <summary>Hides the arrow on a card with nothing to expand.</summary>
        protected void RefreshArrow()
        {
            _arrow.SetState(HasContent, Expanded);
        }

        private void OnHeaderClicked(ClickEvent evt)
        {
            if (evt.target == _menuButton || evt.target == _arrow || evt.target == Handle) return;
            if (!HasContent) return;

            SetExpanded(!Expanded, true);
        }

        private void ShowMenuFromButton()
        {
            var menu = new GenericMenu();
            foreach (CardMenuEntry entry in BuildMenu())
            {
                if (entry.Enabled)
                {
                    Action action = entry.Action;
                    menu.AddItem(new GUIContent(entry.Label), false, () => action());
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent(entry.Label));
                }
            }
            menu.DropDown(_menuButton.worldBound);
        }

        private void OnContextMenu(ContextualMenuPopulateEvent evt)
        {
            foreach (CardMenuEntry entry in BuildMenu())
            {
                Action action = entry.Action;
                evt.menu.AppendAction(entry.Label, _ => action(), entry.Enabled ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }
        }
    }
}
