using UnityEngine;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>Loads the shared stylesheets. They live in a Resources folder, so the same code works in the Editor and in builds.</summary>
    public static class SensorStyles
    {
        private const string SharedSheet = "SensorSystem/SensorShared";
        private const string RuntimeVariablesSheet = "SensorSystem/SensorRuntimeSkin";

        /// <summary>
        /// Adds the shared stylesheet. Pass true outside the Editor UI: the overlay has no Editor theme,
        /// so it also needs the colors the Editor would supply.
        /// </summary>
        public static void Apply(VisualElement element, bool withRuntimeVariables = false)
        {
            if (withRuntimeVariables)
            {
                AddSheet(element, RuntimeVariablesSheet);
            }
            AddSheet(element, SharedSheet);
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

        /// <summary>A section that can be collapsed, so a long list does not push everything else off screen.</summary>
        public static CollapsibleGroup AddFoldoutSection(VisualElement parent, string title, string hint = null, bool open = true)
        {
            var group = new CollapsibleGroup(title, open);
            group.AddToClassList("sensor-section");
            parent.Add(group);

            if (hint != null)
            {
                var hintLabel = new Label(hint);
                hintLabel.AddToClassList("sensor-hint");
                group.Add(hintLabel);
            }
            return group;
        }

        private static void AddSheet(VisualElement element, string resourcePath)
        {
            var sheet = Resources.Load<StyleSheet>(resourcePath);
            if (sheet != null && !element.styleSheets.Contains(sheet))
            {
                element.styleSheets.Add(sheet);
            }
        }
    }
}
