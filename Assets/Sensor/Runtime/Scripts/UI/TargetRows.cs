using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// What one sensor saw in its last refresh. Plain data, so the row logic can be tested without running sensors.
    /// Targets are matched by ID, so a target that is a different instance in another sensor is still the same target.
    /// </summary>
    public sealed class SensorSnapshot
    {
        private readonly HashSet<string> _candidateIds;
        private readonly HashSet<string> _injectedIds;
        private readonly HashSet<string> _detectedIds;
        private readonly Dictionary<string, float> _distances = new Dictionary<string, float>();
        private readonly bool _hasDistances;

        public string Name { get; }
        public Vector3 Position { get; }
        public IReadOnlyCollection<IDetectable> Candidates { get; }
        public IReadOnlyCollection<IDetectable> Injected { get; }
        public IReadOnlyCollection<IDetectable> Detected { get; }

        /// <param name="distances">Distances measured at the sensor's last refresh. Null means measure from the current positions.</param>
        public SensorSnapshot(string name, Vector3 position, IReadOnlyCollection<IDetectable> candidates, IReadOnlyCollection<IDetectable> injected, IReadOnlyCollection<IDetectable> detected, IReadOnlyDictionary<IDetectable, float> distances = null)
        {
            Name = name;
            Position = position;
            Candidates = candidates;
            Injected = injected;
            Detected = detected;

            _candidateIds = ToIds(candidates);
            _injectedIds = ToIds(injected);
            _detectedIds = ToIds(detected);

            if (distances != null)
            {
                _hasDistances = true;
                foreach (KeyValuePair<IDetectable, float> entry in distances)
                {
                    if (entry.Key.TryGetId(out string id))
                    {
                        _distances[id] = entry.Value;
                    }
                }
            }
        }

        public static SensorSnapshot Of(Sensor sensor)
        {
            // The type only helps when it was set, and "NA" is just noise.
            string name = sensor.Type == SensorType.NA ? sensor.name : $"{sensor.name} ({sensor.Type})";
            return new SensorSnapshot(name, sensor.transform.position, sensor.LastCandidates, sensor.LastInjected, sensor.GetAllDetectedObjects(), sensor.LastDistances);
        }

        public bool IsCandidate(IDetectable target) => Has(_candidateIds, target);

        public bool IsInjected(IDetectable target) => Has(_injectedIds, target);

        public bool IsDetected(IDetectable target) => Has(_detectedIds, target);

        /// <summary>How far the target was at the sensor's last refresh, so it only changes when the sensor refreshes.</summary>
        public float DistanceTo(IDetectable target)
        {
            if (_hasDistances && target.TryGetId(out string id) && _distances.TryGetValue(id, out float distance))
            {
                return distance;
            }
            return Vector3.Distance(Position, target.GetCenterPosition());
        }

        private static HashSet<string> ToIds(IEnumerable<IDetectable> targets)
        {
            var ids = new HashSet<string>();
            foreach (IDetectable target in targets)
            {
                if (target.TryGetId(out string id))
                {
                    ids.Add(id);
                }
            }
            return ids;
        }

        private static bool Has(HashSet<string> ids, IDetectable target)
        {
            return target.TryGetId(out string id) && ids.Contains(id);
        }
    }

    /// <summary>Turns what sensors and detectors saw into rows for the live target lists.</summary>
    public static class TargetRows
    {
        /// <summary>Every object the sensor looked at in its last refresh, and where it landed.</summary>
        public static List<TargetRow> ForSensor(Sensor sensor)
        {
            var rows = new List<TargetRow>();
            var seen = new HashSet<string>();
            SensorSnapshot snapshot = SensorSnapshot.Of(sensor);

            var ids = new Dictionary<string, int>();
            foreach (Detection detection in sensor.GetCurrentDetections())
            {
                ids[detection.TargetId] = detection.ID;
            }

            foreach (IDetectable target in sensor.LastCandidates.Concat(sensor.LastInjected))
            {
                if (!target.TryGetId(out string id) || !seen.Add(id)) continue;

                TargetState state = snapshot.IsInjected(target)
                    ? TargetState.Injected
                    : snapshot.IsDetected(target) ? TargetState.Passed : TargetState.Failed;
                rows.Add(new TargetRow(target, TargetListModel.GetName(target), state, snapshot.DistanceTo(target), FormatId(ids, id), id, string.Empty));
            }
            return rows;
        }

        /// <summary>
        /// One row per object any sensor looked at. It passed if some sensor detects it, was injected if every
        /// sensor that detects it got it from an injector, and failed if no sensor detects it.
        /// </summary>
        /// <param name="detectionIds">The detector's detection ID of each target, by the target's identifier.</param>
        public static List<TargetRow> ForDetector(IReadOnlyList<SensorSnapshot> sensors, IReadOnlyDictionary<string, int> detectionIds)
        {
            var rows = new List<TargetRow>();
            var seen = new HashSet<string>();

            foreach (SensorSnapshot sensor in sensors)
            {
                foreach (IDetectable target in sensor.Candidates.Concat(sensor.Injected))
                {
                    if (!target.TryGetId(out string id) || !seen.Add(id)) continue;

                    rows.Add(CreateDetectorRow(target, id, sensors, detectionIds));
                }
            }
            return rows;
        }

        /// <summary>The names of the sensors that detect the target.</summary>
        public static List<string> GetDetectingSensors(IDetectable target, IReadOnlyList<SensorSnapshot> sensors)
        {
            return sensors.Where(sensor => sensor.IsDetected(target)).Select(sensor => sensor.Name).ToList();
        }

        /// <summary>What one sensor made of a target: Injected, Passed, Failed, or Not looked at.</summary>
        public static string DescribeState(IDetectable target, SensorSnapshot sensor)
        {
            if (sensor.IsInjected(target)) return "Injected";
            if (sensor.IsDetected(target)) return "Passed";
            return sensor.IsCandidate(target) ? "Failed" : "Not looked at";
        }

        private static TargetRow CreateDetectorRow(IDetectable target, string id, IReadOnlyList<SensorSnapshot> sensors, IReadOnlyDictionary<string, int> detectionIds)
        {
            var detectedBy = new List<string>();
            bool allInjected = true;
            float nearest = float.MaxValue;

            foreach (SensorSnapshot sensor in sensors)
            {
                bool isInjected = sensor.IsInjected(target);
                if (!isInjected && !sensor.IsCandidate(target)) continue;

                nearest = Mathf.Min(nearest, sensor.DistanceTo(target));
                if (sensor.IsDetected(target))
                {
                    detectedBy.Add(sensor.Name);
                    allInjected &= isInjected;
                }
            }

            TargetState state = detectedBy.Count == 0 ? TargetState.Failed : allInjected ? TargetState.Injected : TargetState.Passed;

            return new TargetRow(target, TargetListModel.GetName(target), state, nearest, FormatId(detectionIds, id), id, string.Join(", ", detectedBy));
        }

        private static string FormatId(IReadOnlyDictionary<string, int> ids, string targetId)
        {
            return ids.TryGetValue(targetId, out int id) ? $"#{id}" : string.Empty;
        }
    }
}
