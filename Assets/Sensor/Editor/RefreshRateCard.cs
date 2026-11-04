using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>The refresh rate, large and at the top, with what it means in plain words.</summary>
    public class RefreshRateCard : VisualElement
    {
        private const string RefreshRateField = "_refreshRate";

        private readonly Label _summary = new Label();

        public RefreshRateCard(SensorSettings settings)
        {
            SensorEditorStyles.Apply(this);
            AddToClassList("sensor-refresh-card");

            var serialized = new SerializedObject(settings);
            SerializedProperty property = serialized.FindProperty(RefreshRateField);

            var row = new VisualElement();
            row.AddToClassList("sensor-refresh-row");

            var title = new Label("Refresh rate");
            title.AddToClassList("sensor-refresh-title");
            title.tooltip = "How often the sensor checks its conditions.";
            row.Add(title);

            var field = new FloatField();
            field.AddToClassList("sensor-refresh-field");
            field.BindProperty(property);
            row.Add(field);

            var unit = new Label("seconds");
            unit.AddToClassList("sensor-refresh-unit");
            row.Add(unit);
            Add(row);

            _summary.AddToClassList("sensor-refresh-summary");
            Add(_summary);

            var hint = new Label($"Lower reacts faster but costs more. Minimum {SensorSettings.MinRefreshRate:0.#} seconds.");
            hint.AddToClassList("sensor-hint");
            Add(hint);

            _summary.text = Describe(property.floatValue);
            this.TrackPropertyValue(property, changed => _summary.text = Describe(changed.floatValue));
        }

        /// <summary>The rate in plain words, e.g. 0.5 seconds is "Checks 2 times per second".</summary>
        public static string Describe(float seconds)
        {
            if (seconds <= 0f) return string.Empty;
            if (Mathf.Approximately(seconds, 1f)) return "Checks once per second";
            if (seconds < 1f) return $"Checks {1f / seconds:0.##} times per second";
            return $"Checks once every {seconds:0.##} seconds";
        }
    }
}
