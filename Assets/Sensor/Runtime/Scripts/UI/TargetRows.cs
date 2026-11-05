using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>What one sensor saw in its last refresh. Plain data, so the row logic can be tested without running sensors.</summary>
    public sealed class SensorSnapshot
    {
        public string Name { get; }
        public Vector3 Position { get; }
        public IReadOnlyCollection<IDetectable> Candidates { get; }
        public IReadOnlyCollection<IDetectable> Injected { get; }
        public IReadOnlyCollection<IDetectable> Detected { get; }

        public SensorSnapshot(string name, Vector3 position, IReadOnlyCollection<IDetectable> candidates, IReadOnlyCollection<IDetectable> injected, IReadOnlyCollection<IDetectable> detected)
        {
            Name = name;
            Position = position;
            Candidates = candidates;
            Injected = injected;
            Detected = detected;
        }

        public static SensorSnapshot Of(Sensor sensor)
        {
            // The type only helps when it was set, and "NA" is just noise.
            string name = sensor.Type == SensorType.NA ? sensor.name : $"{sensor.name} ({sensor.Type})";
            return new SensorSnapshot(name, sensor.transform.position, sensor.LastCandidates, sensor.LastInjected, sensor.GetAllDetectedObjects());
        }
    }

    /// <summary>Turns what sensors and detectors saw into rows for the live target lists.</summary>
    public static class TargetRows
    {
        /// <summary>Every object the sensor looked at in its last refresh, and where it landed.</summary>
        public static List<TargetRow> ForSensor(Sensor sensor)
        {
            var rows = new List<TargetRow>();
            var seen = new HashSet<IDetectable>();
            Vector3 position = sensor.transform.position;

            foreach (IDetectable target in sensor.LastCandidates.Concat(sensor.LastInjected))
            {
                if (target.IsDestroyed() || !seen.Add(target)) continue;

                TargetState state = sensor.LastInjected.Contains(target)
                    ? TargetState.Injected
                    : sensor.GetAllDetectedObjects().Contains(target) ? TargetState.Passed : TargetState.Failed;
                float distance = Vector3.Distance(position, target.GetCenterPosition());
                rows.Add(new TargetRow(target, TargetListModel.GetName(target), state, distance, $"{distance:0.#} m"));
            }
            return rows;
        }

        /// <summary>
        /// One row per object any sensor looked at. It passed if some sensor detects it, was injected if every
        /// sensor that detects it got it from an injector, and failed if no sensor detects it.
        /// </summary>
        public static List<TargetRow> ForDetector(IReadOnlyList<SensorSnapshot> sensors, IReadOnlyDictionary<IDetectable, int> detectionIds)
        {
            var rows = new List<TargetRow>();
            var seen = new HashSet<IDetectable>();

            foreach (SensorSnapshot sensor in sensors)
            {
                foreach (IDetectable target in sensor.Candidates.Concat(sensor.Injected))
                {
                    if (target.IsDestroyed() || !seen.Add(target)) continue;

                    rows.Add(CreateDetectorRow(target, sensors, detectionIds));
                }
            }
            return rows;
        }

        /// <summary>The names of the sensors that detect the target.</summary>
        public static List<string> GetDetectingSensors(IDetectable target, IReadOnlyList<SensorSnapshot> sensors)
        {
            return sensors.Where(sensor => sensor.Detected.Contains(target)).Select(sensor => sensor.Name).ToList();
        }

        /// <summary>What one sensor made of a target: Injected, Passed, Failed, or Not looked at.</summary>
        public static string DescribeState(IDetectable target, SensorSnapshot sensor)
        {
            if (sensor.Injected.Contains(target)) return "Injected";
            if (sensor.Detected.Contains(target)) return "Passed";
            return sensor.Candidates.Contains(target) ? "Failed" : "Not looked at";
        }

        private static TargetRow CreateDetectorRow(IDetectable target, IReadOnlyList<SensorSnapshot> sensors, IReadOnlyDictionary<IDetectable, int> detectionIds)
        {
            var detectedBy = new List<string>();
            bool allInjected = true;
            float nearest = float.MaxValue;

            foreach (SensorSnapshot sensor in sensors)
            {
                bool isInjected = sensor.Injected.Contains(target);
                if (!isInjected && !sensor.Candidates.Contains(target)) continue;

                nearest = Mathf.Min(nearest, Vector3.Distance(sensor.Position, target.GetCenterPosition()));
                if (sensor.Detected.Contains(target))
                {
                    detectedBy.Add(sensor.Name);
                    allInjected &= isInjected;
                }
            }

            TargetState state = detectedBy.Count == 0 ? TargetState.Failed : allInjected ? TargetState.Injected : TargetState.Passed;

            var parts = new List<string>();
            if (detectionIds.TryGetValue(target, out int id))
            {
                parts.Add($"#{id}");
            }
            parts.Add($"{nearest:0.#} m");
            if (detectedBy.Count > 0)
            {
                parts.Add(string.Join(", ", detectedBy));
            }
            return new TargetRow(target, TargetListModel.GetName(target), state, nearest, string.Join("  ", parts));
        }
    }
}
