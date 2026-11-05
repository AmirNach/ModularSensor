using System;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>A text box with a clear button. It works in the Editor and in builds, unlike the Editor's own search field.</summary>
    public class SensorSearchField : VisualElement
    {
        private readonly TextField _field = new TextField();
        private readonly Button _clear = new Button { text = "✕", tooltip = "Clear" };

        /// <summary>Raised as the user types or clears the box. Not raised by SetValueWithoutNotify.</summary>
        public event Action<string> ValueChanged;

        public string value => _field.value;

        public SensorSearchField(string placeholder)
        {
            AddToClassList("sensor-search");

            _field.AddToClassList("sensor-search-input");
            _field.textEdition.placeholder = placeholder;
            _field.RegisterValueChangedCallback(evt =>
            {
                RefreshClearButton();
                ValueChanged?.Invoke(evt.newValue);
            });

            _clear.AddToClassList("sensor-search-clear");
            _clear.clicked += () => _field.value = string.Empty;

            Add(_field);
            Add(_clear);
            RefreshClearButton();
        }

        public void SetValueWithoutNotify(string newValue)
        {
            _field.SetValueWithoutNotify(newValue ?? string.Empty);
            RefreshClearButton();
        }

        private void RefreshClearButton()
        {
            _clear.style.display = string.IsNullOrEmpty(_field.value) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
