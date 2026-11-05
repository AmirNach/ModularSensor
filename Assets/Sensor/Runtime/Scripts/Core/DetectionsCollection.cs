using System;
using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// The detections of a sensor or detector, keyed by the target's ID. Keying by ID rather than by object means a target
    /// that comes back as a new instance is still the same detection, and a destroyed target never has to be touched to find it.
    /// </summary>
    public class DetectionsCollection : DictionaryCollection<string, Detection>
    {
        private readonly LocalUniqueIdGenerator _idGenerator;
        private readonly List<string> _keys = new List<string>();

        public DetectionsCollection(IdSettings idSettings, Action<KeyValuePair<string, Detection>> onAddedItem, Action<KeyValuePair<string, Detection>> onRemovedItem)
            : base(onAddedItem, onRemovedItem)
        {
            _idGenerator = new LocalUniqueIdGenerator(idSettings.MinDetectionId, idSettings.MaxDetectionId, idSettings.ShuffleIdNumbers);
        }

        /// <summary>
        /// Sensor side: makes the collection match the current targets, reporting what changed.
        /// </summary>
        /// <param name="current">The targets that are detected now, by ID.</param>
        /// <param name="seen">Every target the sensor found in this refresh, by ID. A lost target that is still in here failed the conditions, otherwise it is gone.</param>
        /// <param name="gained">Gets the live target of each new detection.</param>
        /// <param name="lost">Gets the snapshot of each lost detection, which still works after the target is gone.</param>
        public void UpdateDetectionsCollection(Sensor sensor, Dictionary<string, IDetectable> current, Dictionary<string, IDetectable> seen, Dictionary<string, EvaluationTrace> changeTraces, List<IDetectable> gained, List<IDetectable> lost)
        {
            Vector3 sensorPosition = sensor.transform.position;
            CopyKeys();

            foreach (string key in _keys)
            {
                if (!Collection.TryGetValue(key, out Detection detection)) continue;

                if (current.TryGetValue(key, out IDetectable live))
                {
                    detection.Refresh(live, Vector3.Distance(sensorPosition, live.GetCenterPosition()));
                    continue;
                }

                if (changeTraces.TryGetValue(key, out EvaluationTrace trace))
                {
                    detection.AllPassedConditions = trace.Passed;
                    detection.AllFailedConditions = trace.Failed;
                }
                detection.LostReason = seen.ContainsKey(key) ? DetectionLostReason.ConditionsFailed : DetectionLostReason.TargetDestroyed;
                lost.Add(detection.Snapshot);
                RemoveItem(new KeyValuePair<string, Detection>(key, detection));
            }

            foreach (KeyValuePair<string, IDetectable> entry in current)
            {
                if (Collection.ContainsKey(entry.Key)) continue;

                changeTraces.TryGetValue(entry.Key, out EvaluationTrace trace);
                float distance = Vector3.Distance(sensorPosition, entry.Value.GetCenterPosition());
                var detection = new Detection(entry.Value, entry.Key, _idGenerator.GetAvailableId(), sensor, trace?.Passed ?? new HashSet<string>(), trace?.Failed ?? new HashSet<string>(), distance);
                gained.Add(entry.Value);
                AddItem(new KeyValuePair<string, Detection>(entry.Key, detection));
            }
        }

        /// <summary>Detector side: a sensor started detecting a target.</summary>
        public void AddFromSensor(Detection sensorDetection, Sensor sensor)
        {
            string key = sensorDetection.TargetId;
            if (Collection.TryGetValue(key, out Detection existing))
            {
                existing.AddToDetectedBy(sensor);
                return;
            }

            var detection = new Detection(sensorDetection.DetectionComponent, key, _idGenerator.GetAvailableId(), sensor, sensorDetection.AllPassedConditions, sensorDetection.AllFailedConditions, sensorDetection.DistanceFromSensorToDetection);
            detection.Snapshot.CopyFrom(sensorDetection.Snapshot);
            AddItem(new KeyValuePair<string, Detection>(key, detection));
        }

        /// <summary>Detector side: a sensor stopped detecting a target. It is removed once no sensor detects it.</summary>
        public void RemoveFromSensor(Detection sensorDetection, Sensor sensor)
        {
            string key = sensorDetection.TargetId;
            if (!Collection.TryGetValue(key, out Detection existing)) return;

            existing.RemoveFromDetectedBy(sensor);
            existing.AllPassedConditions = sensorDetection.AllPassedConditions;
            existing.AllFailedConditions = sensorDetection.AllFailedConditions;

            if (existing.AllDetectingSensors.Count == 0)
            {
                // The detector sees the target through its sensors, so it ends for the same reason the last sensor's did.
                existing.Snapshot.CopyFrom(sensorDetection.Snapshot);
                existing.LostReason = sensorDetection.LostReason;
                RemoveItem(new KeyValuePair<string, Detection>(key, existing));
            }
        }

        public void RemoveAllItems(List<IDetectable> lost = null)
        {
            CopyKeys();
            foreach (string key in _keys)
            {
                if (Collection.TryGetValue(key, out Detection detection))
                {
                    detection.LostReason = DetectionLostReason.SensorDisabled;
                    lost?.Add(detection.Snapshot);
                    RemoveItem(new KeyValuePair<string, Detection>(key, detection));
                }
            }
        }

        public override void RemoveItem(KeyValuePair<string, Detection> item)
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
