using System;
using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    public class DetectionsCollection : DictionaryCollection<IDetectable, Detection>
    {
        private readonly LocalUniqueIdGenerator _idGenerator;
        private readonly List<IDetectable> _keys = new List<IDetectable>();

        public DetectionsCollection(IdSettings idSettings, Action<KeyValuePair<IDetectable, Detection>> onAddedItem, Action<KeyValuePair<IDetectable, Detection>> onRemovedItem)
            : base(onAddedItem, onRemovedItem)
        {
            _idGenerator = new LocalUniqueIdGenerator(idSettings.MinDetectionId, idSettings.MaxDetectionId, idSettings.ShuffleIdNumbers);
        }

        /// <summary>Sensor side: makes the collection match the current targets, reporting what changed.</summary>
        public void UpdateDetectionsCollection(Sensor sensor, HashSet<IDetectable> current, Dictionary<IDetectable, EvaluationTrace> changeTraces, List<IDetectable> gained, List<IDetectable> lost)
        {
            Vector3 sensorPosition = sensor.transform.position;
            CopyKeys();

            foreach (IDetectable key in _keys)
            {
                if (!Collection.TryGetValue(key, out Detection detection)) continue;

                if (current.Contains(key))
                {
                    detection.DistanceFromSensorToDetection = Vector3.Distance(sensorPosition, key.GetCenterPosition());
                    continue;
                }

                if (changeTraces.TryGetValue(key, out EvaluationTrace trace))
                {
                    detection.AllPassedConditions = trace.Passed;
                    detection.AllFailedConditions = trace.Failed;
                }
                lost.Add(key);
                RemoveItem(new KeyValuePair<IDetectable, Detection>(key, detection));
            }

            foreach (IDetectable key in current)
            {
                if (Collection.ContainsKey(key)) continue;

                changeTraces.TryGetValue(key, out EvaluationTrace trace);
                float distance = Vector3.Distance(sensorPosition, key.GetCenterPosition());
                var detection = new Detection(key, _idGenerator.GetAvailableId(), sensor, trace?.Passed ?? new HashSet<string>(), trace?.Failed ?? new HashSet<string>(), distance);
                gained.Add(key);
                AddItem(new KeyValuePair<IDetectable, Detection>(key, detection));
            }
        }

        /// <summary>Detector side: a sensor started detecting a target.</summary>
        public void AddFromSensor(Detection sensorDetection, Sensor sensor)
        {
            IDetectable key = sensorDetection.DetectionComponent;
            if (Collection.TryGetValue(key, out Detection existing))
            {
                existing.AddToDetectedBy(sensor);
                return;
            }

            var detection = new Detection(key, _idGenerator.GetAvailableId(), sensor, sensorDetection.AllPassedConditions, sensorDetection.AllFailedConditions, sensorDetection.DistanceFromSensorToDetection);
            AddItem(new KeyValuePair<IDetectable, Detection>(key, detection));
        }

        /// <summary>Detector side: a sensor stopped detecting a target. It is removed once no sensor detects it.</summary>
        public void RemoveFromSensor(Detection sensorDetection, Sensor sensor)
        {
            IDetectable key = sensorDetection.DetectionComponent;
            if (!Collection.TryGetValue(key, out Detection existing)) return;

            existing.RemoveFromDetectedBy(sensor);
            existing.AllPassedConditions = sensorDetection.AllPassedConditions;
            existing.AllFailedConditions = sensorDetection.AllFailedConditions;

            if (existing.AllDetectingSensors.Count == 0)
            {
                RemoveItem(new KeyValuePair<IDetectable, Detection>(key, existing));
            }
        }

        public void RemoveAllItems(List<IDetectable> lost = null)
        {
            CopyKeys();
            foreach (IDetectable key in _keys)
            {
                if (Collection.TryGetValue(key, out Detection detection))
                {
                    lost?.Add(key);
                    RemoveItem(new KeyValuePair<IDetectable, Detection>(key, detection));
                }
            }
        }

        public override void RemoveItem(KeyValuePair<IDetectable, Detection> item)
        {
            base.RemoveItem(item);
            _idGenerator.Release(item.Value.ID);
        }

        private void CopyKeys()
        {
            _keys.Clear();
            _keys.AddRange(Collection.Keys);
        }
    }
}
