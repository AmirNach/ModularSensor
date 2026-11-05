using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>The open overlay: a source picker for every sensor and detector in the scene, and the view of the chosen one.</summary>
    public sealed class SensorDebugPanel : VisualElement
    {
        private const long ScanMilliseconds = 1000;

        private readonly DropdownField _source = new DropdownField("Source");
        private readonly ScrollView _content = new ScrollView(ScrollViewMode.Vertical);
        private readonly Label _empty = new Label("No sensors or detectors found in the scene.");
        private readonly List<Object> _sources = new List<Object>();
        private readonly List<string> _labels = new List<string>();

        private IVisualElementScheduledItem _scan;
        private bool _isActive;

        public SensorDebugPanel()
        {
            AddToClassList("sensor-overlay-panel");

            var header = new VisualElement();
            header.AddToClassList("sensor-overlay-header");
            var title = new Label("Sensor debug");
            title.AddToClassList("sensor-overlay-title");
            header.Add(title);
            var close = new Button(() => SensorDebug.Dispatch(new CloseOverlayCommand())) { text = "✕", tooltip = "Close" };
            close.AddToClassList("sensor-overlay-close");
            header.Add(close);
            Add(header);

            _source.AddToClassList("sensor-overlay-source");
            _source.RegisterValueChangedCallback(_ => OnSourcePicked());
            Add(_source);

            _empty.AddToClassList("sensor-hint");
            Add(_empty);

            _content.AddToClassList("sensor-overlay-content");
            // Long names shorten with an ellipsis, so there is never anything to scroll sideways.
            _content.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            Add(_content);
        }

        /// <summary>The sensor or detector being shown.</summary>
        public Object Selected { get; private set; }

        public IReadOnlyList<Object> Sources => _sources;

        public VisualElement ContentRoot => _content.contentContainer;

        /// <summary>While active, the panel keeps its list of sources up to date and shows the chosen one. While inactive it costs nothing.</summary>
        public void SetActive(bool active)
        {
            if (_isActive == active) return;

            _isActive = active;
            if (active)
            {
                Scan();
                // Closing clears the view but keeps the choice, and Scan only rebuilds when the choice
                // changes, so show the chosen one again here.
                if (Selected != null && _content.contentContainer.childCount == 0)
                {
                    Select(Selected);
                }
                _scan = schedule.Execute(Scan).Every(ScanMilliseconds);
            }
            else
            {
                _scan?.Pause();
                _scan = null;
                // Removing the views also clears the objects they were debugging.
                _content.contentContainer.Clear();
            }
        }

        /// <summary>Shows this sensor or detector.</summary>
        public void Focus(Object target)
        {
            if (target == null) return;

            Scan();
            if (_sources.Contains(target))
            {
                Select(target);
            }
        }

        /// <summary>Looks for sensors and detectors again and keeps the picker and the view in step.</summary>
        public void Scan()
        {
            var found = new List<Object>();
            found.AddRange(Object.FindObjectsByType<Sensor>(FindObjectsSortMode.None).OrderBy(sensor => sensor.name, System.StringComparer.OrdinalIgnoreCase));
            found.AddRange(Object.FindObjectsByType<Detector>(FindObjectsSortMode.None).OrderBy(detector => detector.name, System.StringComparer.OrdinalIgnoreCase));

            List<string> labels = MakeLabels(found);
            bool changed = !found.SequenceEqual(_sources);
            if (changed)
            {
                _sources.Clear();
                _sources.AddRange(found);
                _labels.Clear();
                _labels.AddRange(labels);
                _source.choices = new List<string>(_labels);
            }

            _empty.style.display = _sources.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _source.style.display = _sources.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;

            if (Selected == null || !_sources.Contains(Selected))
            {
                Select(_sources.FirstOrDefault());
            }
            else if (changed)
            {
                _source.SetValueWithoutNotify(_labels[_sources.IndexOf(Selected)]);
            }
        }

        private void OnSourcePicked()
        {
            int index = _source.index;
            if (index >= 0 && index < _sources.Count && _sources[index] != Selected)
            {
                Select(_sources[index]);
            }
        }

        private void Select(Object target)
        {
            Selected = target;
            _content.contentContainer.Clear();

            if (target == null) return;

            _source.SetValueWithoutNotify(_labels[_sources.IndexOf(target)]);
            switch (target)
            {
                case Sensor sensor:
                    _content.Add(new RuntimeSensorView(sensor));
                    break;
                case Detector detector:
                    _content.Add(new RuntimeDetectorView(detector));
                    break;
            }
        }

        private static List<string> MakeLabels(List<Object> objects)
        {
            var labels = new List<string>();
            var used = new Dictionary<string, int>();
            foreach (Object item in objects)
            {
                string baseLabel = $"{(item is Detector ? "Detector" : "Sensor")} - {item.name}";
                used.TryGetValue(baseLabel, out int count);
                used[baseLabel] = count + 1;
                labels.Add(count == 0 ? baseLabel : $"{baseLabel} ({count + 1})");
            }
            return labels;
        }
    }
}
