using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SensorSystem
{
    public static class SensorEditorStyles
    {
        private const string StyleSheetName = "SensorEditor";

        /// <summary>Looks the sheet up by name, so the package can live in any folder.</summary>
        public static void Apply(VisualElement element)
        {
            string[] guids = AssetDatabase.FindAssets($"{StyleSheetName} t:StyleSheet");
            if (guids.Length == 0) return;

            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (sheet != null)
            {
                element.styleSheets.Add(sheet);
            }
        }

        public static void AddFields(VisualElement parent, SerializedProperty owner, params string[] skippedFields)
        {
            SerializedProperty field = owner.Copy();
            SerializedProperty end = owner.GetEndProperty();
            bool enterChildren = true;

            while (field.NextVisible(enterChildren) && !SerializedProperty.EqualContents(field, end))
            {
                enterChildren = false;
                if (Array.IndexOf(skippedFields, field.name) < 0)
                {
                    parent.Add(new PropertyField(field.Copy()));
                }
            }
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
        private const string ExpandedArrow = "▼";
        private const string CollapsedArrow = "▶";

        public VisualElement Header { get; } = new VisualElement();
        public VisualElement Body { get; } = new VisualElement();
        public VisualElement ChildArea { get; } = new VisualElement();
        public Label Handle { get; } = new Label("≡");
        public Label Badge { get; } = new Label();
        public Label TitleLabel { get; } = new Label();
        public Label SummaryLabel { get; } = new Label();
        public bool Expanded { get; private set; }

        public event Action<bool> ExpandedChanged;

        private readonly Button _arrow = new Button();
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
            _arrow.AddToClassList("sensor-arrow");
            _arrow.tooltip = "Expand or collapse";
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
            _arrow.text = expanded ? ExpandedArrow : CollapsedArrow;
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

        /// <summary>Hides the arrow on a card with nothing to expand.</summary>
        protected void RefreshArrow()
        {
            bool hasContent = Body.childCount > 0 || ChildArea.childCount > 0;
            _arrow.style.visibility = hasContent ? Visibility.Visible : Visibility.Hidden;
        }

        private void OnHeaderClicked(ClickEvent evt)
        {
            if (evt.target == _menuButton || evt.target == _arrow || evt.target == Handle) return;
            if (_arrow.style.visibility == Visibility.Hidden) return;

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
