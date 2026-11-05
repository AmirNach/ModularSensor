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
        public IReadOnlyCollection<IDetectable> LastCandidates => _candidates;

        /// <summary>Detections added by injectors in the last refresh. Treat it as read-only.</summary>
        public IReadOnlyCollection<IDetectable> LastInjected => _injected;

        /// <summary>
        /// How far each object in LastCandidates and LastInjected was from this sensor when the last refresh ran.
        /// It changes only with the sensor's refresh rate, like the detections it sits next to. Treat it as read-only.
        /// </summary>
        public IReadOnlyDictionary<IDetectable, float> LastDistances => _distances;

        /// <summary>
        /// When set, the next refreshes record every node's result for this target in DebugResults.
        /// Nothing is evaluated extra, so debugging can't change rolls or attempts.
        /// </summary>
        public IDetectable DebugTarget { get; set; }

        /// <summary>Node results for DebugTarget from the last refresh. A node missing from it was never reached.</summary>
        public IReadOnlyDictionary<Condition, bool> DebugResults => _debugResults;

        protected virtual Camera ContextCamera => null;

        private readonly HashSet<string> _detectedObjectsBlackList = new HashSet<string>();
        private readonly HashSet<IDetectable> _candidates = new HashSet<IDetectable>();
        private readonly HashSet<IDetectable> _injected = new HashSet<IDetectable>();
        private readonly HashSet<string> _injectedIds = new HashSet<string>();
        private readonly HashSet<IDetectable> _allDetectedObjects = new HashSet<IDetectable>();
        private readonly Dictionary<IDetectable, float> _distances = new Dictionary<IDetectable, float>();
        private readonly List<IDetectable> _injectorOutput = new List<IDetectable>();
        private readonly Dictionary<string, EvaluationTrace> _changeTraces = new Dictionary<string, EvaluationTrace>();
        // Targets are tracked by ID. These hold the live instance of each ID for the refresh that is running.
        private readonly Dictionary<IDetectable, string> _ids = new Dictionary<IDetectable, string>();
        private readonly Dictionary<string, IDetectable> _seenById = new Dictionary<string, IDetectable>();
        private readonly Dictionary<string, IDetectable> _detectedById = new Dictionary<string, IDetectable>();
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
            _allDetectedObjects.Clear();
            _candidates.Clear();
            _injected.Clear();
            _injectedIds.Clear();
            _distances.Clear();
            _ids.Clear();
            _seenById.Clear();
            _detectedById.Clear();
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
            return target.TryGetId(out string id) && _injectedIds.Contains(id);
        }

        /// <summary>Everything currently detected. Treat it as read-only.</summary>
        public HashSet<IDetectable> GetAllDetectedObjects()
        {
            return _allDetectedObjects;
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
            var context = new SensorContext(transform, ContextCamera, Time.time, _trace);
            // The same context with somewhere to draw. Used for the targets that should be drawn.
            bool anyVisuals = _runtimeNodes.Exists(node => node.WantsVisuals);
            var visualContext = anyVisuals ? new SensorContext(transform, ContextCamera, Time.time, _trace, _visuals) : context;
            _visuals.Clear();

            foreach (Condition node in _runtimeNodes)
            {
                node.Prepare(context);
            }

            CollectCandidates();
            _allDetectedObjects.Clear();
            _detectedById.Clear();
            _changeTraces.Clear();
            _debugResults.Clear();

            string debugTargetId = null;
            DebugTarget?.TryGetId(out debugTargetId);

            foreach (IDetectable candidate in _candidates)
            {
                string id = _ids[candidate];
                bool isDebugTarget = id == debugTargetId;
                _trace.Clear();
                _trace.RecordNodes = isDebugTarget;

                bool passed = false;
                try
                {
                    // With a picked object only that one is drawn, which keeps the scene readable and cheap.
                    bool draw = debugTargetId == null || isDebugTarget;
                    passed = Condition.Run(RuntimeRoot, candidate, draw ? visualContext : context);
                }
                catch (Exception e)
                {
                    // A target that vanished while it was being evaluated just fails. Anything else is a real bug in a condition.
                    if (candidate.IsAlive())
                    {
                        Debug.LogException(e, this);
                    }
                }

                if (passed)
                {
                    _allDetectedObjects.Add(candidate);
                    _detectedById[id] = candidate;
                }
                if (passed != _detectionsCollection.ContainsKey(id))
                {
                    _changeTraces[id] = _trace.Snapshot();
                }
                if (isDebugTarget)
                {
                    foreach (KeyValuePair<Condition, bool> result in _trace.NodeResults)
                    {
                        _debugResults[result.Key] = result.Value;
                    }
                }
            }
            _trace.RecordNodes = false;

            AddInjectedDetections(context);
            RecordDistances();

            _gained.Clear();
            _lost.Clear();
            _detectionsCollection.UpdateDetectionsCollection(this, _detectedById, _seenById, _changeTraces, _gained, _lost);
            NotifyConditions();
        }

        private void RecordDistances()
        {
            _distances.Clear();
            Vector3 position = transform.position;

            foreach (IDetectable candidate in _candidates)
            {
                _distances[candidate] = Vector3.Distance(position, candidate.GetCenterPosition());
            }
            foreach (IDetectable detection in _injected)
            {
                _distances[detection] = Vector3.Distance(position, detection.GetCenterPosition());
            }
        }

        private void CollectCandidates()
        {
            _candidates.Clear();
            _ids.Clear();
            _seenById.Clear();

            if (_allObjectsCollection == null) return;

            foreach (IDetectable candidate in _allObjectsCollection.AllObjects)
            {
                if (TryRegister(candidate, out _))
                {
                    _candidates.Add(candidate);
                }
            }
        }

        /// <summary>
        /// Records a live target under its ID for this refresh. Gone targets, targets without an ID, ignored ones
        /// and a second target with an ID that is already taken are skipped.
        /// </summary>
        private bool TryRegister(IDetectable target, out string id)
        {
            if (!target.TryGetId(out id))
            {
                if (target.IsAlive() && !_warnedAboutMissingId)
                {
                    _warnedAboutMissingId = true;
                    Debug.LogWarning($"Sensor on {name} found an object with no identifier, so it cannot be tracked and is ignored.", this);
                }
                return false;
            }

            if (_detectedObjectsBlackList.Contains(id)) return false;

            if (_seenById.ContainsKey(id))
            {
                if (!_warnedAboutDuplicateId)
                {
                    _warnedAboutDuplicateId = true;
                    Debug.LogWarning($"Sensor on {name} found two objects with the identifier \"{id}\". Only the first is used. Identifiers must be unique.", this);
                }
                return false;
            }

            _seenById[id] = target;
            _ids[target] = id;
            return true;
        }

        private void AddInjectedDetections(in SensorContext context)
        {
            _injected.Clear();
            _injectedIds.Clear();

            foreach (DetectionInjector injector in _runtimeInjectors)
            {
                if (injector == null) continue;

                injector.Prepare(context);
                _injectorOutput.Clear();
                injector.Collect(_injectorOutput);

                foreach (IDetectable detection in _injectorOutput)
                {
                    if (!TryRegister(detection, out string id)) continue;

                    _injected.Add(detection);
                    _injectedIds.Add(id);
                    _allDetectedObjects.Add(detection);
                    _detectedById[id] = detection;

                    if (!_detectionsCollection.ContainsKey(id) && !_changeTraces.ContainsKey(id))
                    {
                        var trace = new EvaluationTrace();
                        trace.Passed.Add(injector.DisplayName);
                        _changeTraces[id] = trace;
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
            OnNewDetectionEvent?.Invoke(keyValuePair.Value);
        }

        private void OnLostDetection(KeyValuePair<string, Detection> keyValuePair)
        {
            OnLostDetectionEvent?.Invoke(keyValuePair.Value);
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
            foreach (Condition node in ConditionTree.Flatten(RuntimeRoot ?? _settings.Root))
            {
                Gizmos.color = node is VisualCondition visual ? visual.GizmoColor : Color.white;
                node.DrawGizmos(transform, ContextCamera);
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
