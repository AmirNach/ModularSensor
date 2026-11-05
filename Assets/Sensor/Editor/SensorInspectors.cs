using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SensorSystem
{
    [CustomEditor(typeof(SensorSettings))]
    public class SensorSettingsEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var settings = (SensorSettings)target;
            var root = new VisualElement();
            SensorEditorStyles.Apply(root);
            root.AddToClassList("sensor-inspector");

            root.Add(new RefreshRateCard(settings));

            SensorEditorStyles.AddSection(root, "Condition tree", "Evaluated top to bottom. Put cheap conditions first, because later ones may be skipped.");
            root.Add(new ConditionTreeView(settings, false));

            var openWindow = new Button(() => ConditionTreeWindow.Open(settings)) { text = "Open in window" };
            openWindow.AddToClassList("sensor-open-window");
            root.Add(openWindow);

            SensorEditorStyles.AddSection(root, "Injected detections", "Added directly to the result, bypassing the tree. Used for fake detections.");
            root.Add(new InjectorListView(settings));

            var idFoldout = new CollapsibleGroup("Detection IDs", false);
            idFoldout.AddToClassList("sensor-advanced");
            var idHint = new Label("Each detection gets an ID from this range.");
            idHint.AddToClassList("sensor-hint");
            idFoldout.Add(idHint);
            idFoldout.Add(new PropertyField(serializedObject.FindProperty("_firstDetectionId")));
            idFoldout.Add(new PropertyField(serializedObject.FindProperty("_lastDetectionId")));
            idFoldout.Add(new PropertyField(serializedObject.FindProperty("_shuffledNumbers")));
            root.Add(idFoldout);

            return root;
        }
    }

    [CustomEditor(typeof(Sensor), true)]
    public class SensorEditor : Editor
    {
        private const string SettingsField = "_settings";

        private VisualElement _treeArea;
        private SensorSettings _shownSettings;
        private bool _shownPlaying;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            SensorEditorStyles.Apply(root);
            root.AddToClassList("sensor-inspector");

            // Looks the same as the [Header] titles of the fields below.
            var headerContainer = new VisualElement();
            headerContainer.AddToClassList("unity-decorator-drawers-container");
            var header = new Label("References");
            header.AddToClassList("unity-header-drawer__label");
            headerContainer.Add(header);
            root.Add(headerContainer);

            var settingsRow = new VisualElement();
            settingsRow.AddToClassList("sensor-settings-row");
            var settingsField = new PropertyField(serializedObject.FindProperty(SettingsField), "Sensor settings");
            settingsField.AddToClassList("sensor-settings-field");
            settingsField.RegisterValueChangeCallback(_ => RefreshTreeArea());
            settingsRow.Add(settingsField);
            settingsRow.Add(new Button(CreateSettingsAsset) { text = "New", tooltip = "Create a settings asset and assign it" });
            root.Add(settingsRow);

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name != "m_Script" && iterator.name != SettingsField)
                {
                    root.Add(new PropertyField(iterator.Copy()));
                }
            }

            _treeArea = new VisualElement();
            _treeArea.AddToClassList("sensor-tree-area");
            root.Add(_treeArea);

            root.RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.playModeStateChanged += OnPlayModeChanged);
            root.RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.playModeStateChanged -= OnPlayModeChanged);
            RefreshTreeArea();
            return root;
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            RefreshTreeArea();
        }

        private void RefreshTreeArea()
        {
            if (_treeArea == null || target == null) return;

            var sensor = (Sensor)target;
            SensorSettings settings = sensor.Settings;
            bool isPlaying = EditorApplication.isPlaying;
            if (settings == _shownSettings && isPlaying == _shownPlaying && _treeArea.childCount > 0) return;

            _shownSettings = settings;
            _shownPlaying = isPlaying;
            _treeArea.Clear();

            if (settings == null)
            {
                _treeArea.Add(new HelpBox("Assign a settings asset, or press New to create one. The condition tree is stored there.", HelpBoxMessageType.Info));
                return;
            }

            if (isPlaying)
            {
                _treeArea.Add(new ConditionLiveView(sensor));
                return;
            }

            SensorEditorStyles.AddSection(_treeArea, "Condition tree", $"Edits are saved to {settings.name}, not the scene, and affect every sensor using it.");
            _treeArea.Add(new ConditionTreeView(settings, !(sensor is SensorWithCamera)));

            var openWindow = new Button(() => ConditionTreeWindow.Open(settings)) { text = "Open in window" };
            openWindow.AddToClassList("sensor-open-window");
            _treeArea.Add(openWindow);
        }

        private void CreateSettingsAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("New sensor settings", "SensorSettings", "asset", "Choose where to save the settings.");
            if (string.IsNullOrEmpty(path)) return;

            var settings = CreateInstance<SensorSettings>();
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();

            serializedObject.Update();
            serializedObject.FindProperty(SettingsField).objectReferenceValue = settings;
            serializedObject.ApplyModifiedProperties();
            RefreshTreeArea();
        }
    }

    [CustomEditor(typeof(Detector), true)]
    public class DetectorEditor : Editor
    {
        private VisualElement _liveArea;
        private bool _shownPlaying;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            SensorEditorStyles.Apply(root);
            root.AddToClassList("sensor-inspector");

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name != "m_Script")
                {
                    root.Add(new PropertyField(iterator.Copy()));
                }
            }

            _liveArea = new VisualElement();
            _liveArea.AddToClassList("sensor-tree-area");
            root.Add(_liveArea);

            root.RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.playModeStateChanged += OnPlayModeChanged);
            root.RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.playModeStateChanged -= OnPlayModeChanged);
            RefreshLiveArea();
            return root;
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            RefreshLiveArea();
        }

        private void RefreshLiveArea()
        {
            if (_liveArea == null || target == null) return;

            bool isPlaying = EditorApplication.isPlaying;
            if (isPlaying == _shownPlaying && _liveArea.childCount > 0) return;

            _shownPlaying = isPlaying;
            _liveArea.Clear();

            if (isPlaying)
            {
                _liveArea.Add(new DetectorLiveView((Detector)target));
            }
            else
            {
                _liveArea.Add(new HelpBox("Enter Play mode to see what this detector detects, grouped as passed, failed and injected.", HelpBoxMessageType.Info));
            }
        }
    }

    /// <summary>A roomy, dockable view of the selected sensor's, detector's or settings asset's tree.</summary>
    public class ConditionTreeWindow : EditorWindow
    {
        [SerializeField] private SensorSettings _settings;
        [SerializeField] private Sensor _sensor;
        [SerializeField] private Detector _detector;

        [MenuItem("Tools/Sensor/Condition Tree")]
        public static void Open()
        {
            GetWindow<ConditionTreeWindow>("Condition Tree").minSize = new Vector2(420f, 300f);
        }

        public static void Open(SensorSettings settings)
        {
            ConditionTreeWindow window = GetWindow<ConditionTreeWindow>("Condition Tree");
            window._settings = settings;
            window._sensor = null;
            window._detector = null;
            window.Rebuild();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            Rebuild();
        }

        private void CreateGUI()
        {
            if (_settings == null && _sensor == null && _detector == null)
            {
                ReadSelection();
            }
            Rebuild();
        }

        private void OnSelectionChange()
        {
            if (ReadSelection())
            {
                Rebuild();
            }
        }

        /// <summary>Keeps showing the last tree when something unrelated is selected.</summary>
        private bool ReadSelection()
        {
            if (Selection.activeObject is SensorSettings settings)
            {
                _settings = settings;
                _sensor = null;
                _detector = null;
                return true;
            }

            GameObject selected = Selection.activeGameObject;
            Detector detector = selected != null ? selected.GetComponent<Detector>() : null;
            if (detector != null)
            {
                _detector = detector;
                _sensor = null;
                _settings = null;
                return true;
            }

            Sensor sensor = selected != null ? selected.GetComponent<Sensor>() : null;
            if (sensor != null)
            {
                _sensor = sensor;
                _settings = sensor.Settings;
                _detector = null;
                return true;
            }
            return false;
        }

        private void Rebuild()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.AddToClassList("sensor-window");
            SensorEditorStyles.Apply(root);

            var scroll = new ScrollView();
            root.Add(scroll);

            if (_detector != null)
            {
                if (EditorApplication.isPlaying)
                {
                    scroll.Add(new DetectorLiveView(_detector));
                }
                else
                {
                    scroll.Add(new HelpBox("Enter Play mode to see what this detector detects, grouped as passed, failed and injected.", HelpBoxMessageType.Info));
                }
            }
            else if (_sensor != null && EditorApplication.isPlaying)
            {
                scroll.Add(new ConditionLiveView(_sensor));
            }
            else if (_settings != null)
            {
                SensorEditorStyles.AddSection(scroll, _settings.name);
                scroll.Add(new RefreshRateCard(_settings));
                scroll.Add(new ConditionTreeView(_settings, _sensor != null && !(_sensor is SensorWithCamera)));
                SensorEditorStyles.AddSection(scroll, "Injected detections", "Added directly to the result, bypassing the tree.");
                scroll.Add(new InjectorListView(_settings));
            }
            else
            {
                scroll.Add(new HelpBox("Select a Sensor or a SensorSettings asset.", HelpBoxMessageType.Info));
            }
        }
    }
}
