using System;
using System.Collections.Generic;

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

        /// <summary>The detections by ID, for the sensor to walk without copying. Do not change it through this.</summary>
        internal Dictionary<string, Detection> Items => Collection;

        /// <summary>Sensor side: a target is newly detected.</summary>
        internal Detection AddNew(IDetectable target, string id, Sensor sensor, EvaluationTrace trace, float distance)
        {
            var detection = new Detection(target, id, _idGenerator.GetAvailableId(), sensor, trace?.Passed ?? new HashSet<string>(), trace?.Failed ?? new HashSet<string>(), distance);
            AddItem(new KeyValuePair<string, Detection>(id, detection));
            return detection;
        }

        /// <summary>Sensor side: the sensor saw the target and the conditions no longer accept it.</summary>
        internal void RemoveFailed(Detection detection, EvaluationTrace trace, List<IDetectable> lost)
        {
            if (trace != null)
            {
                detection.AllPassedConditions = trace.Passed;
                detection.AllFailedConditions = trace.Failed;
            }
            detection.LostReason = DetectionLostReason.ConditionsFailed;
            lost.Add(detection.Snapshot);
            RemoveItem(new KeyValuePair<string, Detection>(detection.TargetId, detection));
        }

        /// <summary>Sensor side: removes every detection whose target the sensor did not find in this refresh. Those targets are gone.</summary>
        internal void RemoveMissing(Dictionary<string, int> seen, List<IDetectable> lost)
        {
            _keys.Clear();
            foreach (KeyValuePair<string, Detection> entry in Collection)
            {
                if (!seen.ContainsKey(entry.Key))
                {
                    _keys.Add(entry.Key);
                }
            }

            foreach (string key in _keys)
            {
                if (!Collection.TryGetValue(key, out Detection detection)) continue;

                detection.LostReason = DetectionLostReason.TargetDestroyed;
                lost.Add(detection.Snapshot);
                RemoveItem(new KeyValuePair<string, Detection>(key, detection));
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

            AddItem(new KeyValuePair<string, Detection>(key, new Detection(sensorDetection, _idGenerator.GetAvailableId(), sensor)));
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
                existing.UseSnapshotOf(sensorDetection);
                existing.LostReason = sensorDetection.LostReason;
                RemoveItem(new KeyValuePair<string, Detection>(key, existing));
            }
        }

        public void RemoveAllItems(List<IDetectable> lost = null)
        {
            _keys.Clear();
            _keys.AddRange(Collection.Keys);
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
    }
}
