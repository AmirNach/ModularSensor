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

        [Header("Gizmos")]
        [SerializeField] protected bool ShowSensorFrustum = true;
        [SerializeField] protected Color SensorFrustumColor = Color.red;

        public event Action<Detection> OnNewDetectionEvent;
        public event Action<Detection> OnLostDetectionEvent;

        public SensorType Type => SensorType;
        public SensorSettings Settings => _settings;

        /// <summary>This sensor's own copy of the condition tree, safe to change at runtime.</summary>
        public Condition RuntimeRoot { get; private set; }

        /// <summary>This sensor's own copies of the injectors.</summary>
        public IReadOnlyList<DetectionInjector> RuntimeInjectors => _runtimeInjectors;

        /// <summary>Objects evaluated by the tree in the last refresh. Treat it as read-only.</summary>
        public IReadOnlyCollection<IDetectable> LastCandidates => _candidates;

        /// <summary>Detections added by injectors in the last refresh. Treat it as read-only.</summary>
        public IReadOnlyCollection<IDetectable> LastInjected => _injected;

        /// <summary>
        /// When set, the next refreshes record every node's result for this target in DebugResults.
        /// Nothing is evaluated extra, so debugging can't change rolls or attempts.
        /// </summary>
        public IDetectable DebugTarget { get; set; }

        /// <summary>Node results for DebugTarget from the last refresh. A node missing from it was never reached.</summary>
        public IReadOnlyDictionary<Condition, bool> DebugResults => _debugResults;

        protected virtual Camera ContextCamera => null;

        private readonly HashSet<IDetectable> _detectedObjectsBlackList = new HashSet<IDetectable>();
        private readonly HashSet<IDetectable> _candidates = new HashSet<IDetectable>();
        private readonly HashSet<IDetectable> _injected = new HashSet<IDetectable>();
        private readonly HashSet<IDetectable> _allDetectedObjects = new HashSet<IDetectable>();
        private readonly List<IDetectable> _injectorOutput = new List<IDetectable>();
        private readonly Dictionary<IDetectable, EvaluationTrace> _changeTraces = new Dictionary<IDetectable, EvaluationTrace>();
        private readonly Dictionary<Condition, bool> _debugResults = new Dictionary<Condition, bool>();
        private readonly List<IDetectable> _gained = new List<IDetectable>();
        private readonly List<IDetectable> _lost = new List<IDetectable>();
        private readonly EvaluationTrace _trace = new EvaluationTrace();

        private List<Condition> _runtimeNodes = new List<Condition>();
        private List<DetectionInjector> _runtimeInjectors = new List<DetectionInjector>();
        private IAllObjectsCollection _allObjectsCollection;
        private DetectionsCollection _detectionsCollection;
        private Coroutine _refreshLoop;
        private bool _hasStarted;
        private bool _isOn;

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

            ConditionTree.Clone(_settings.Root, _settings.Injectors, out Condition root, out _runtimeInjectors);
            RuntimeRoot = root;
            _runtimeNodes = ConditionTree.Flatten(RuntimeRoot);

            if (RuntimeRoot == null && _runtimeInjectors.Count == 0)
            {
                Debug.LogWarning($"Sensor on {name} has an empty condition tree and no injectors, so it detects nothing.", this);
            }

            _allObjectsCollection = CreateAllObjectsCollection();
            _detectionsCollection = new DetectionsCollection(_settings, OnNewDetection, OnLostDetection);
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
            _debugResults.Clear();

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

        /// <summary>Everything currently detected. Treat it as read-only.</summary>
        public HashSet<IDetectable> GetAllDetectedObjects()
        {
            return _allDetectedObjects;
        }

        public void AddDetectionToBlackList(IDetectable detection)
        {
            _detectedObjectsBlackList.Add(detection);
        }

        public void RemoveDetectionToBlackList(IDetectable detection)
        {
            _detectedObjectsBlackList.Remove(detection);
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

            foreach (Condition node in _runtimeNodes)
            {
                node.Prepare(context);
            }

            CollectCandidates();
            _allDetectedObjects.Clear();
            _changeTraces.Clear();
            _debugResults.Clear();

            foreach (IDetectable candidate in _candidates)
            {
                bool isDebugTarget = ReferenceEquals(candidate, DebugTarget);
                _trace.Clear();
                _trace.RecordNodes = isDebugTarget;

                bool passed = Condition.Run(RuntimeRoot, candidate, context);
                if (passed)
                {
                    _allDetectedObjects.Add(candidate);
                }
                if (passed != _detectionsCollection.ContainsKey(candidate))
                {
                    _changeTraces[candidate] = _trace.Snapshot();
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

            _gained.Clear();
            _lost.Clear();
            _detectionsCollection.UpdateDetectionsCollection(this, _allDetectedObjects, _changeTraces, _gained, _lost);
            NotifyConditions();
        }

        private void CollectCandidates()
        {
            _candidates.Clear();

            if (_allObjectsCollection != null)
            {
                _candidates.UnionWith(_allObjectsCollection.AllObjects);
            }

            _candidates.RemoveWhere(candidate => candidate.IsDestroyed() || _detectedObjectsBlackList.Contains(candidate));
        }

        private void AddInjectedDetections(in SensorContext context)
        {
            _injected.Clear();

            foreach (DetectionInjector injector in _runtimeInjectors)
            {
                if (injector == null) continue;

                injector.Prepare(context);
                _injectorOutput.Clear();
                injector.Collect(_injectorOutput);

                foreach (IDetectable detection in _injectorOutput)
                {
                    if (detection == null || detection.IsDestroyed() || _detectedObjectsBlackList.Contains(detection)) continue;

                    _injected.Add(detection);
                    _allDetectedObjects.Add(detection);

                    if (!_detectionsCollection.ContainsKey(detection) && !_changeTraces.ContainsKey(detection))
                    {
                        var trace = new EvaluationTrace();
                        trace.Passed.Add(injector.DisplayName);
                        _changeTraces[detection] = trace;
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
                foreach (IDetectable target in _lost)
                {
                    node.OnTargetLost(target);
                }
            }
            _gained.Clear();
            _lost.Clear();
        }

        private void OnNewDetection(KeyValuePair<IDetectable, Detection> keyValuePair)
        {
            OnNewDetectionEvent?.Invoke(keyValuePair.Value);
        }

        private void OnLostDetection(KeyValuePair<IDetectable, Detection> keyValuePair)
        {
            OnLostDetectionEvent?.Invoke(keyValuePair.Value);
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmos()
        {
            if (!ShowSensorFrustum || _settings == null) return;

            Gizmos.color = SensorFrustumColor;
            foreach (Condition node in ConditionTree.Flatten(RuntimeRoot ?? _settings.Root))
            {
                node.DrawGizmos(transform, ContextCamera);
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
