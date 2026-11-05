using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>
    /// The QA overlay. Add one to a scene and it shows a small button on screen that opens a panel with every sensor and
    /// detector: their objects, their condition trees, and the values of each condition, which can be changed while the game runs.
    /// It does nothing in release builds. Open and close it by dispatching commands, from any code.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sensor/Sensor Debug Overlay")]
    public class SensorDebugOverlay : MonoBehaviour, ISensorDebugReceiver
    {
        private const string ThemePath = "SensorSystem/SensorRuntimeTheme";

        /// <summary>Decides whether overlays switch on. The default is the Editor and development builds. Replace it to change that.</summary>
        public static Func<bool> AvailabilityCheck { get; set; } = DefaultAvailability;

        [SerializeField, Tooltip("Open the panel as soon as the game starts.")]
        private bool _startOpen;

        [SerializeField, Min(0), Tooltip("Higher draws on top of other UI.")]
        private int _sortingOrder = 1000;

        private PanelSettings _settings;
        private GameObject _documentObject;
        private UIDocument _document;
        private SensorDebugPanel _panel;
        private Button _toggle;
        private UnityEngine.Object _pendingFocus;
        private bool _isOpen;

        /// <summary>Optional. Draw the overlay into this texture instead of the screen. Used for screenshots and tests.</summary>
        public RenderTexture Target { get; set; }

        public bool IsOpen => _isOpen;

        /// <summary>True when the overlay switched itself on. False in release builds.</summary>
        public bool IsRunning => _document != null;

        public SensorDebugPanel Panel => _panel;

        public VisualElement Root => _document != null ? _document.rootVisualElement : null;

        public Button ToggleButton => _toggle;

        private static bool DefaultAvailability() => Debug.isDebugBuild;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AvailabilityCheck = DefaultAvailability;
        }

        private void OnEnable()
        {
            if (!(AvailabilityCheck?.Invoke() ?? false)) return;

            SensorDebugEventSystem.EnsureExists();
            CreateDocument();
            SensorDebug.Register(this);
            _isOpen = _startOpen;
        }

        private void OnDisable()
        {
            SensorDebug.Unregister(this);
            _panel?.SetActive(false);
            _panel = null;
            _toggle = null;

            if (_documentObject != null)
            {
                if (Application.isPlaying) Destroy(_documentObject); else DestroyImmediate(_documentObject);
            }
            if (_settings != null)
            {
                if (Application.isPlaying) Destroy(_settings); else DestroyImmediate(_settings);
            }
            _document = null;
            _documentObject = null;
            _settings = null;
        }

        void ISensorDebugReceiver.SetOpen(bool open) => SetOpen(open);

        public void SetOpen(bool open)
        {
            _isOpen = open;
            ApplyOpenState();
        }

        public void Focus(Sensor sensor) => FocusOn(sensor);

        public void Focus(Detector detector) => FocusOn(detector);

        public void ResetValues(Sensor sensor)
        {
            if (sensor != null)
            {
                sensor.ResetToSettings();
            }
        }

        private void FocusOn(UnityEngine.Object target)
        {
            if (_panel == null)
            {
                _pendingFocus = target;
                return;
            }
            _panel.Focus(target);
        }

        private void CreateDocument()
        {
            _settings = ScriptableObject.CreateInstance<PanelSettings>();
            _settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ThemePath);
            _settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _settings.referenceResolution = new Vector2Int(1280, 720);
            _settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _settings.match = 0.5f;
            _settings.sortingOrder = _sortingOrder;
            _settings.targetTexture = Target;

            // Created while playing and never saved, so it cannot dirty the scene.
            _documentObject = new GameObject("Sensor Debug UI") { hideFlags = HideFlags.HideAndDontSave };
            _documentObject.transform.SetParent(transform, false);
            _document = _documentObject.AddComponent<UIDocument>();
            _document.panelSettings = _settings;

            StartCoroutine(BuildUiWhenReady());
        }

        private IEnumerator BuildUiWhenReady()
        {
            while (_document != null && _document.rootVisualElement == null)
            {
                yield return null;
            }
            if (_document == null) yield break;

            VisualElement root = _document.rootVisualElement;
            SensorStyles.Apply(root, true);
            root.AddToClassList("sensor-overlay");
            // Only the button and the panel take the mouse. Everything else reaches the game.
            root.pickingMode = PickingMode.Ignore;

            _toggle = new Button(() => SensorDebug.Dispatch(new ToggleOverlayCommand())) { text = "Sensors" };
            _toggle.AddToClassList("sensor-overlay-toggle");
            root.Add(_toggle);

            _panel = new SensorDebugPanel();
            root.Add(_panel);

            ApplyOpenState();
            if (_pendingFocus != null)
            {
                _panel.Focus(_pendingFocus);
                _pendingFocus = null;
            }
        }

        private void ApplyOpenState()
        {
            if (_panel == null || _toggle == null) return;

            _panel.style.display = _isOpen ? DisplayStyle.Flex : DisplayStyle.None;
            _panel.SetActive(_isOpen);
            _toggle.text = _isOpen ? "Sensors ✕" : "Sensors";
            _toggle.EnableInClassList("sensor-overlay-toggle--open", _isOpen);
        }
    }
}
