using System;
using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    public enum DetectorType
    {
        Basic = 0,
    }

    /// <summary>Merges one or more sensors. A target stays detected until the last sensor loses it.</summary>
    [DefaultExecutionOrder(-2)]
    public class Detector : MonoBehaviour, IEnabled, IResetable
    {
        [Header("References")]
        [SerializeField] private DetectorSettings _detectorSettings;

        [Header("Settings")]
        [SerializeField] protected bool EnableDetectorByDefault = true;
        [SerializeField] private List<Sensor> _sensors = new List<Sensor>();

        public event Action<Detection> OnNewDetectionFromSensorEvent;
        public event Action<Detection> OnLostDetectionFromSensorEvent;
        public event Action<Detection> OnNewDetectionFromDetectorEvent;
        public event Action<Detection> OnLostDetectionFromDetectorEvent;

        private DetectionsCollection _allDetectedObjects;
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

        /// <summary>The sensors this detector merges.</summary>
        public IReadOnlyList<Sensor> Sensors => _sensors;

        /// <summary>A copy of what this detector currently detects.</summary>
        public List<Detection> GetCurrentDetections()
        {
            return _allDetectedObjects != null ? new List<Detection>(_allDetectedObjects.Values) : new List<Detection>();
        }

        void IEnabled.SetEnable(bool isEnabled)
        {
            IsOn = isEnabled;
        }

        protected void SetEnable(bool isEnabled)
        {
            if (isEnabled)
            {
                RegisterSensorEvents();
            }
            else
            {
                UnregisterSensorEvents();
            }
        }

        public void ResetToDefault()
        {
            IsOn = EnableDetectorByDefault;
        }

        private void Awake()
        {
            if (_detectorSettings == null)
            {
                Debug.LogError($"Detector on {name} has no DetectorSettings assigned.", this);
                enabled = false;
                return;
            }

            _allDetectedObjects = new DetectionsCollection(_detectorSettings, OnNewDetectionFromDetector, OnLostDetectionFromDetector);
        }

        private void OnEnable()
        {
            if (!_hasStarted) return;

            ResetToDefault();
            SetSensorsEnabled(true);
        }

        private void Start()
        {
            _hasStarted = true;
            ResetToDefault();
        }

        private void OnDisable()
        {
            if (_allDetectedObjects == null) return;

            // Sensors report their losses while still subscribed, which empties the collection in order.
            SetSensorsEnabled(false);
            _allDetectedObjects.RemoveAllItems();
            IsOn = false;
        }

        private void SetSensorsEnabled(bool isEnabled)
        {
            foreach (Sensor sensor in _sensors)
            {
                if (sensor != null)
                {
                    sensor.enabled = isEnabled;
                }
            }
        }

        private void RegisterSensorEvents()
        {
            foreach (Sensor sensor in _sensors)
            {
                if (sensor == null) continue;

                sensor.OnNewDetectionEvent += OnNewDetectionFromSensor;
                sensor.OnLostDetectionEvent += OnLostDetectionFromSensor;

                // A sensor only reports changes, so catch up on what it already detects.
                foreach (Detection detection in sensor.GetCurrentDetections())
                {
                    OnNewDetectionFromSensor(detection);
                }
            }
        }

        private void UnregisterSensorEvents()
        {
            foreach (Sensor sensor in _sensors)
            {
                if (sensor == null) continue;

                sensor.OnNewDetectionEvent -= OnNewDetectionFromSensor;
                sensor.OnLostDetectionEvent -= OnLostDetectionFromSensor;
            }
        }

        private void OnNewDetectionFromSensor(Detection detection)
        {
            _allDetectedObjects.AddFromSensor(detection, detection.CurrentlyDetectingSensorInstance);
            OnNewDetectionFromSensorEvent?.Invoke(detection);
        }

        private void OnLostDetectionFromSensor(Detection detection)
        {
            OnLostDetectionFromSensorEvent?.Invoke(detection);
            _allDetectedObjects.RemoveFromSensor(detection, detection.CurrentlyDetectingSensorInstance);
        }

        private void OnNewDetectionFromDetector(KeyValuePair<IDetectable, Detection> keyValuePair)
        {
            OnNewDetectionFromDetectorEvent?.Invoke(keyValuePair.Value);
        }

        private void OnLostDetectionFromDetector(KeyValuePair<IDetectable, Detection> keyValuePair)
        {
            OnLostDetectionFromDetectorEvent?.Invoke(keyValuePair.Value);
        }
    }
}
