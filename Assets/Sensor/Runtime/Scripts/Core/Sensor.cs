using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    public enum SensorType
    {
        NA,
        A,
        B
    }

    public enum DetectionsType
    {
        Simple,
    }

    /// <summary>
    /// Evaluates its settings' condition tree on every object in its collection, once per refresh,
    /// adds any injected detections, and reports targets it gains and loses.
    /// </summary>
    [DefaultExecutionOrder(-1)]
    public class Sensor : MonoBehaviour, IEnabled, IResetable
    {
        [SerializeField] private SensorSettings _settings;

        [Header("Settings")]
        [SerializeField] protected bool EnableSensorByDefault = true;
        [Tooltip("Never detect the IDetectable on this object or its parents.")]
        [SerializeField] protected bool IgnoreSelf = true;
        [SerializeField] protected SensorType SensorType;
        [SerializeField] protected DetectionsType DetectionsType;
        [Tooltip("Layers this sensor looks for objects on.")]
        [SerializeField] protected LayerMask DetectableLayers = ~0;


        public event Action<Detection> OnNewDetectionEvent;
        public event Action<Detection> OnLostDetectionEvent;

        public SensorType Type => SensorType;
        public SensorSettings Settings => _settings;

        /// <summary>What the conditions with "Show visual" on drew in the last refresh. Empty when none has it on.</summary>
        public ConditionVisuals Visuals => _visuals;

        /// <summary>This sensor's own copy of the condition tree, safe to change at runtime.</summary>
        public Condition RuntimeRoot { get; private set; }

        /// <summary>This sensor's own copies of the injectors.</summary>
        public IReadOnlyList<DetectionInjector> RuntimeInjectors => _runtimeInjectors;

        /// <summary>Objects evaluated by the tree in the last refresh. Treat it as read-only.</summary>
        public IReadOnlyCollection<IDetectable> LastCandidates
        {
            get
            {
                BuildViews();
                return _candidatesView;
            }
        }

        /// <summary>Detections added by injectors in the last refresh. Treat it as read-only.</summary>
        public IReadOnlyCollection<IDetectable> LastInjected
        {
            get
            {
                BuildViews();
                return _injectedView;
            }
        }

        /// <summary>
        /// How far each object in LastCandidates and LastInjected was from this sensor when the last refresh ran.
        /// It changes only with the sensor's refresh rate, like the detections it sits next to. Treat it as read-only.
        /// </summary>
        public IReadOnlyDictionary<IDetectable, float> LastDistances
        {
            get
            {
                BuildViews();
                return _distancesView;
            }
        }

        /// <summary>
        /// When set, the next refreshes record every node's result for this target in DebugResults.
        /// Nothing is evaluated extra, so debugging can't change rolls or attempts.
        /// </summary>
        public IDetectable DebugTarget { get; set; }

        /// <summary>Node results for DebugTarget from the last refresh. A node missing from it was never reached.</summary>
        public IReadOnlyDictionary<Condition, bool> DebugResults => _debugResults;

        protected virtual Camera ContextCamera => null;

        /// <summary>Everything one refresh knows about one object, in one place, so the refresh walks an array and does not look anything up.</summary>
        private struct RefreshEntry
        {
            public IDetectable Target;
            public string Id;
            /// <summary>The detection this target already had when the refresh began. Null when it has none yet.</summary>
            public Detection Detection;
            /// <summary>Only set when the target was gained or lost in this refresh.</summary>
            public EvaluationTrace Trace;
            public Vector3 Position;
            public float Distance;
            public bool Passed;
            public bool Injected;
        }

        private readonly HashSet<string> _detectedObjectsBlackList = new HashSet<string>();
        private readonly List<IDetectable> _injectorOutput = new List<IDetectable>();
        private RefreshEntry[] _entries = new RefreshEntry[64];
        private int _entryCount;
        // Targets are tracked by ID. This finds a target's entry by ID, and is the only ID lookup that grows with the object count.
        private readonly Dictionary<string, int> _indexById = new Dictionary<string, int>();
        // The public lists are only for the debug views and tests, so they are built from the entries when someone reads them.
        private readonly List<IDetectable> _candidatesView = new List<IDetectable>();
        private readonly List<IDetectable> _injectedView = new List<IDetectable>();
        private readonly HashSet<IDetectable> _detectedView = new HashSet<IDetectable>();
        private readonly Dictionary<IDetectable, float> _distancesView = new Dictionary<IDetectable, float>();
        private bool _viewsAreStale;
        private readonly Dictionary<Condition, bool> _debugResults = new Dictionary<Condition, bool>();
        private readonly List<IDetectable> _gained = new List<IDetectable>();
        private readonly List<IDetectable> _lost = new List<IDetectable>();
        private readonly EvaluationTrace _trace = new EvaluationTrace();
        private readonly ConditionVisuals _visuals = new ConditionVisuals();

        private List<Condition> _runtimeNodes = new List<Condition>();
        private List<DetectionInjector> _runtimeInjectors = new List<DetectionInjector>();
        private IAllObjectsCollection _allObjectsCollection;
        private DetectionsCollection _detectionsCollection;
        private Coroutine _refreshLoop;
        private bool _hasStarted;
        private bool _isOn;
        private bool _warnedAboutMissingId;
        private bool _warnedAboutDuplicateId;

        protected virtual bool IsOn
        {
            get => _isOn;
            set
            {
                if (_isOn != value)
                {
                    _isOn = value;
                    SetEnable(_isOn);
                }
            }
        }

        protected virtual void Awake()
        {
            if (_settings == null)
            {
                Debug.LogError($"Sensor on {name} has no SensorSettings assigned.", this);
                enabled = false;
                return;
            }

            LoadRuntimeTree();

            _allObjectsCollection = CreateAllObjectsCollection();
            _detectionsCollection = new DetectionsCollection(_settings, OnNewDetection, OnLostDetection);
        }

        /// <summary>
        /// Takes fresh copies of the settings' tree and injectors, dropping any change made to this
        /// sensor's running copy, such as values edited in the QA overlay. The settings asset is never touched.
        /// </summary>
        public void ResetToSettings()
        {
            if (_settings == null) return;

            foreach (DetectionInjector injector in _runtimeInjectors)
            {
                injector?.ResetState();
            }
            LoadRuntimeTree();
        }

        private void LoadRuntimeTree()
        {
            ConditionTree.Clone(_settings.Root, _settings.Injectors, out Condition root, out _runtimeInjectors);
            RuntimeRoot = root;
            _runtimeNodes = ConditionTree.Flatten(RuntimeRoot);

            if (RuntimeRoot == null && _runtimeInjectors.Count == 0)
            {
                Debug.LogWarning($"Sensor on {name} has an empty condition tree and no injectors, so it detects nothing.", this);
            }

            // Once per clashing name, for trees that never went through the inspector.
            HashSet<string> reported = null;
            foreach (Condition node in _runtimeNodes)
            {
                if (!(node is AreaCondition area)) continue;

                string clash = AreaCondition.FindNameClash(RuntimeRoot, area);
                if (clash != null && (reported ??= new HashSet<string>()).Add(area.GetType().Name + "/" + area.AreaName))
                {
                    Debug.LogWarning($"Sensor on {name}: {clash}. FindArea returns only the first one.", this);
                }

                // Shape labels must be unique for UpdatePolygon / RemoveCircle and the like. Copies typed in the inspector are
                // renamed in this running copy only (the asset is left as it is); empty labels get "Polygon 2" and the like.
                if (!area.DefineInInspector) continue;
                string labelClash = area.FindLabelClash();
                area.MakeLabelsUnique();
                if (labelClash != null)
                {
                    Debug.LogWarning($"Sensor on {name}, {area.DisplayName} \"{area.AreaName}\": {labelClash}.", this);
                }
            }
        }

        protected virtual void OnEnable()
        {
            if (_hasStarted)
            {
                ResetToDefault();
            }
        }

        protected virtual void Start()
        {
            _hasStarted = true;
            ResetToDefault();
        }

        protected virtual void OnDisable()
        {
            IsOn = false;
        }

        public virtual void ResetToDefault()
        {
            if (IgnoreSelf)
            {
                IDetectable self = GetComponentInParent<IDetectable>();
                if (self != null)
                {
                    AddDetectionToBlackList(self);
                }
            }

            IsOn = EnableSensorByDefault;
        }

        void IEnabled.SetEnable(bool isEnabled)
        {
            IsOn = isEnabled;
        }

        protected virtual void SetEnable(bool isEnabled)
        {
            if (isEnabled)
            {
                if (isActiveAndEnabled)
                {
                    _refreshLoop = StartCoroutine(RefreshLoop());
                }
                else
                {
                    _isOn = false;
                }
                return;
            }

            if (_refreshLoop != null)
            {
                StopCoroutine(_refreshLoop);
                _refreshLoop = null;
            }

            _gained.Clear();
            _lost.Clear();
            _detectionsCollection.RemoveAllItems(_lost);
            NotifyConditions();
            ClearEntries();
            _viewsAreStale = true;
            _debugResults.Clear();
            _visuals.Clear();

            foreach (DetectionInjector injector in _runtimeInjectors)
            {
                injector?.ResetState();
            }
        }

        /// <summary>A copy of the current detections, for listeners that start after this sensor.</summary>
        public List<Detection> GetCurrentDetections()
        {
            return _detectionsCollection != null ? new List<Detection>(_detectionsCollection.Values) : new List<Detection>();
        }

        /// <summary>True when the object was added by an injector in the last refresh. Matched by ID.</summary>
        public bool WasInjected(IDetectable target)
        {
            return target.TryGetId(out string id) && _indexById.TryGetValue(id, out int index) && _entries[index].Injected;
        }

        /// <summary>
        /// Finds an area condition by its AreaName in this sensor's running tree, or null. Change it to change what this
        /// sensor detects, from the next refresh: sensor.FindArea&lt;InPolygonArea&gt;("Zone A").SetPolygon(points).
        /// Only this sensor is affected, even when other sensors use the same settings asset.
        /// </summary>
        public T FindArea<T>(string areaName) where T : AreaCondition
        {
            foreach (Condition node in _runtimeNodes)
            {
                if (node is T area && string.Equals(area.AreaName, areaName, StringComparison.Ordinal)) return area;
            }
            return null;
        }

        /// <summary>The first condition of this type in this sensor's running tree, or null. Only this sensor is affected by changing it.</summary>
        public T FindCondition<T>() where T : Condition
        {
            foreach (Condition node in _runtimeNodes)
            {
                if (node is T match) return match;
            }
            return null;
        }

        /// <summary>Adds every condition of this type in this sensor's running tree to the list.</summary>
        public void FindConditions<T>(List<T> results) where T : Condition
        {
            foreach (Condition node in _runtimeNodes)
            {
                if (node is T match) results.Add(match);
            }
        }

        /// <summary>Everything currently detected. Treat it as read-only.</summary>
        public HashSet<IDetectable> GetAllDetectedObjects()
        {
            BuildViews();
            return _detectedView;
        }

        private void BuildViews()
        {
            if (!_viewsAreStale) return;

            _viewsAreStale = false;
            _candidatesView.Clear();
            _injectedView.Clear();
            _detectedView.Clear();
            _distancesView.Clear();

            for (int i = 0; i < _entryCount; i++)
            {
                ref RefreshEntry entry = ref _entries[i];
                (entry.Injected ? _injectedView : _candidatesView).Add(entry.Target);
                _distancesView[entry.Target] = entry.Distance;
                if (entry.Passed)
                {
                    _detectedView.Add(entry.Target);
                }
            }
        }

        private void ClearEntries()
        {
            // Lets go of the objects too, so a destroyed target is not kept alive by the array.
            Array.Clear(_entries, 0, _entryCount);
            _entryCount = 0;
            _indexById.Clear();
        }

        /// <summary>The sensor will ignore this object from now on. It is remembered by ID.</summary>
        public void AddDetectionToBlackList(IDetectable detection)
        {
            if (detection.TryGetId(out string id))
            {
                _detectedObjectsBlackList.Add(id);
            }
        }

        public void RemoveDetectionFromBlackList(IDetectable detection)
        {
            if (detection.TryGetId(out string id))
            {
                _detectedObjectsBlackList.Remove(id);
            }
        }

        /// <summary>Override to supply your own IAllObjectsCollection.</summary>
        protected virtual IAllObjectsCollection CreateAllObjectsCollection()
        {
            switch (DetectionsType)
            {
                case DetectionsType.Simple:
                default:
                    return ObjectsProvider.Instance.CreateNewCollection<AllSimpleObjectsCollection>(DetectableLayers);
            }
        }

        private IEnumerator RefreshLoop()
        {
            float waitSeconds = _settings.RefreshRate;
            var wait = new WaitForSeconds(waitSeconds);

            while (true)
            {
                try
                {
                    Refresh();
                }
                catch (Exception e)
                {
                    // One bad condition must not stop the sensor for good.
                    Debug.LogException(e, this);
                }

                if (!Mathf.Approximately(waitSeconds, _settings.RefreshRate))
                {
                    waitSeconds = _settings.RefreshRate;
                    wait = new WaitForSeconds(waitSeconds);
                }
                yield return wait;
            }
        }

        private void Refresh()
        {
            using (SensorProfiler.Refresh.Measure())
            {
                RefreshCore();
            }
        }

        private void RefreshCore()
        {
            var context = new SensorContext(transform, ContextCamera, Time.time, _trace);
            // The same context with somewhere to draw. Used for the targets that should be drawn.
            bool anyVisuals = _runtimeNodes.Exists(node => node.WantsVisuals);
            var visualContext = anyVisuals ? new SensorContext(transform, ContextCamera, Time.time, _trace, _visuals) : context;
            _visuals.Clear();

            using (SensorProfiler.Prepare.Measure())
            foreach (Condition node in _runtimeNodes)
            {
                node.Prepare(context);
            }

            using (SensorProfiler.CollectCandidates.Measure()) CollectCandidates();
            _debugResults.Clear();

            string debugTargetId = null;
            DebugTarget?.TryGetId(out debugTargetId);

            using (SensorProfiler.EvaluateTree.Measure())
            {
                for (int i = 0; i < _entryCount; i++)
                {
                    ref RefreshEntry entry = ref _entries[i];
                    bool isDebugTarget = debugTargetId != null && entry.Id == debugTargetId;
                    _trace.Clear();
                    _trace.RecordNodes = isDebugTarget;

                    bool passed = false;
                    try
                    {
                        // With a picked object only that one is drawn, which keeps the scene readable and cheap.
                        // The context carries the position and ID already read for this target, so conditions do not ask the target again.
                        SensorContext targetContext = (debugTargetId == null || isDebugTarget ? visualContext : context)
                            .ForTarget(entry.Target, entry.Position, entry.Id);
                        passed = Condition.Run(RuntimeRoot, entry.Target, targetContext);
                    }
                    catch (Exception e)
                    {
                        // A target that vanished while it was being evaluated just fails. Anything else is a real bug in a condition.
                        if (entry.Target.IsAlive())
                        {
                            Debug.LogException(e, this);
                        }
                    }

                    entry.Passed = passed;
                    if (passed != (entry.Detection != null))
                    {
                        entry.Trace = _trace.Snapshot();
                    }
                    if (isDebugTarget)
                    {
                        foreach (KeyValuePair<Condition, bool> result in _trace.NodeResults)
                        {
                            _debugResults[result.Key] = result.Value;
                        }
                    }
                }
            }
            _trace.RecordNodes = false;

            using (SensorProfiler.Injectors.Measure()) AddInjectedDetections(context);
            using (SensorProfiler.RecordDistances.Measure()) RecordDistances(context.SensorPosition);
            _viewsAreStale = true;

            _gained.Clear();
            _lost.Clear();
            using (SensorProfiler.UpdateDetections.Measure()) UpdateDetections();
            using (SensorProfiler.NotifyConditions.Measure()) NotifyConditions();
        }

        // The positions were read when the entries were made, so this is arithmetic only.
        private void RecordDistances(Vector3 sensorPosition)
        {
            for (int i = 0; i < _entryCount; i++)
            {
                ref RefreshEntry entry = ref _entries[i];
                entry.Distance = Vector3.Distance(sensorPosition, entry.Position);
            }
        }

        /// <summary>
        /// Makes the detections match this refresh. Each entry already holds its detection, so nothing is looked up by ID unless
        /// something changed. An event handler can switch this sensor off part way, which empties the entries, so the
        /// loops re-read the count.
        /// </summary>
        private void UpdateDetections()
        {
            float now = Time.time;
            int kept = 0;

            for (int i = 0; i < _entryCount; i++)
            {
                ref RefreshEntry entry = ref _entries[i];
                Detection detection = entry.Detection;
                if (detection == null) continue;

                if (entry.Passed)
                {
                    detection.Refresh(entry.Target, entry.Distance, entry.Position, now);
                    kept++;
                }
                else
                {
                    _detectionsCollection.RemoveFailed(detection, entry.Trace, _lost);
                }
            }

            // Anything more than what was kept is a detection whose target was not found at all.
            if (_detectionsCollection.Count > kept)
            {
                _detectionsCollection.RemoveMissing(_indexById, _lost);
            }

            for (int i = 0; i < _entryCount; i++)
            {
                ref RefreshEntry entry = ref _entries[i];
                if (!entry.Passed || entry.Detection != null) continue;

                _gained.Add(entry.Target);
                _detectionsCollection.AddNew(entry.Target, entry.Id, this, entry.Trace, entry.Distance);
            }
        }

        private void CollectCandidates()
        {
            ClearEntries();

            if (_allObjectsCollection == null) return;

            // Usually far fewer objects are detected than seen, so the detections are matched to the entries from the
            // detections' side: one ID lookup per detection instead of one per object.
            bool fromDetections = _detectionsCollection.Count < _allObjectsCollection.AllObjects.Count;

            foreach (IDetectable candidate in _allObjectsCollection.AllObjects)
            {
                TryRegister(candidate, !fromDetections, out _);
            }

            if (fromDetections && _detectionsCollection.Count > 0)
            {
                foreach (KeyValuePair<string, Detection> pair in _detectionsCollection.Items)
                {
                    if (_indexById.TryGetValue(pair.Key, out int index))
                    {
                        _entries[index].Detection = pair.Value;
                    }
                }
            }
        }

        private bool IsBlackListed(string id)
        {
            int count = _detectedObjectsBlackList.Count;
            if (count == 0) return false;
            if (count > 4) return _detectedObjectsBlackList.Contains(id);

            // A sensor usually ignores only itself. Comparing a few strings is cheaper than hashing the ID.
            foreach (string ignored in _detectedObjectsBlackList)
            {
                if (string.Equals(ignored, id)) return true;
            }
            return false;
        }

        /// <summary>
        /// Adds an entry for a live target for this refresh. Gone targets, targets without an ID, ignored ones
        /// and a second target with an ID that is already taken are skipped.
        /// </summary>
        private bool TryRegister(IDetectable target, bool findDetection, out int index)
        {
            index = -1;
            if (!target.TryGetId(out string id))
            {
                if (target.IsAlive() && !_warnedAboutMissingId)
                {
                    _warnedAboutMissingId = true;
                    Debug.LogWarning($"Sensor on {name} found an object with no identifier, so it cannot be tracked and is ignored.", this);
                }
                return false;
            }

            if (IsBlackListed(id)) return false;

            if (!_indexById.TryAdd(id, _entryCount))
            {
                if (!_warnedAboutDuplicateId)
                {
                    _warnedAboutDuplicateId = true;
                    Debug.LogWarning($"Sensor on {name} found two objects with the identifier \"{id}\". Only the first is used. Identifiers must be unique.", this);
                }
                return false;
            }

            if (_entryCount == _entries.Length)
            {
                Array.Resize(ref _entries, _entries.Length * 2);
            }

            index = _entryCount++;
            ref RefreshEntry entry = ref _entries[index];
            entry.Target = target;
            entry.Id = id;
            // Read once here for the whole refresh: the conditions, the distance and the detection all use it.
            entry.Position = target.GetCenterPosition();
            if (findDetection && _detectionsCollection.Count > 0)
            {
                _detectionsCollection.TryGetValue(id, out entry.Detection);
            }
            return true;
        }

        private void AddInjectedDetections(in SensorContext context)
        {
            foreach (DetectionInjector injector in _runtimeInjectors)
            {
                if (injector == null) continue;

                injector.Prepare(context);
                _injectorOutput.Clear();
                injector.Collect(_injectorOutput);

                foreach (IDetectable detection in _injectorOutput)
                {
                    if (!TryRegister(detection, true, out int index)) continue;

                    ref RefreshEntry entry = ref _entries[index];
                    entry.Injected = true;
                    entry.Passed = true;

                    if (entry.Detection == null)
                    {
                        var trace = new EvaluationTrace();
                        trace.Passed.Add(injector.DisplayName);
                        entry.Trace = trace;
                    }
                }
            }
        }

        private void NotifyConditions()
        {
            foreach (Condition node in _runtimeNodes)
            {
                foreach (IDetectable target in _gained)
                {
                    node.OnTargetGained(target);
                }
                // Lost targets are snapshots, which are safe to use even though the real target may be gone.
                foreach (IDetectable target in _lost)
                {
                    node.OnTargetLost(target);
                }
            }
            _gained.Clear();
            _lost.Clear();
        }

        private void OnNewDetection(KeyValuePair<string, Detection> keyValuePair)
        {
            using (SensorProfiler.Events.Measure()) OnNewDetectionEvent?.Invoke(keyValuePair.Value);
        }

        private void OnLostDetection(KeyValuePair<string, Detection> keyValuePair)
        {
            using (SensorProfiler.Events.Measure()) OnLostDetectionEvent?.Invoke(keyValuePair.Value);
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmos()
        {
            if (_settings == null) return;

            if (_visuals.Count > 0)
            {
                _visuals.Draw();
                Gizmos.matrix = Matrix4x4.identity;
            }

            // Each condition that has its visual on draws its own shape, in its own color.
            // This runs on every repaint, so while playing it uses the list the sensor already keeps.
            foreach (Condition node in RuntimeRoot != null ? _runtimeNodes : ConditionTree.Flatten(_settings.Root))
            {
                Gizmos.color = node is VisualCondition visual ? visual.GizmoColor : Color.white;
                node.DrawGizmos(transform, ContextCamera);
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
