using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// Light-weight timing for the sensor's own methods. Every section is also a Unity Profiler marker, which costs almost nothing
    /// when the Profiler is not recording. The numbers are only collected while Enabled is on (off by default),
    /// so the sensor pays for a bool check and nothing more. Times include the sections inside them.
    /// </summary>
    public static class SensorProfiler
    {
        /// <summary>When true, sections record their time.</summary>
        public static bool Enabled;

        /// <summary>
        /// Also time each condition call. Off by default: there are thousands of these per refresh and the timing itself takes real
        /// time, which makes EvaluateTree and everything around it look slower than it is. Switch it on to compare conditions with each other.
        /// </summary>
        public static bool MeasureConditions;

        private static readonly List<Section> Sections = new List<Section>();
        private static readonly Dictionary<Type, Section> ConditionSections = new Dictionary<Type, Section>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Enabled = false;
            MeasureConditions = false;
            ResetAll();
        }

        // The sensor's refresh, in order.
        public static readonly Section Refresh = Create("Sensor.Refresh");
        public static readonly Section Prepare = Create("Sensor.Prepare");
        public static readonly Section CollectCandidates = Create("Sensor.CollectCandidates");
        public static readonly Section EvaluateTree = Create("Sensor.EvaluateTree");
        public static readonly Section Injectors = Create("Sensor.Injectors");
        public static readonly Section RecordDistances = Create("Sensor.RecordDistances");
        public static readonly Section UpdateDetections = Create("Sensor.UpdateDetections");
        public static readonly Section NotifyConditions = Create("Sensor.NotifyConditions");
        public static readonly Section Events = Create("Sensor.Events (your handlers)");

        // Finding the objects.
        public static readonly Section Scan = Create("Collection.Scan");
        public static readonly Section ScanFindObjects = Create("Collection.Scan.FindObjectsByType");
        public static readonly Section ScanFilter = Create("Collection.Scan.LayerAndGetComponentInParent");

        // The overlay.
        public static readonly Section OverlayRows = Create("Overlay.BuildRows");
        public static readonly Section OverlayList = Create("Overlay.UpdateList");
        public static readonly Section OverlayRefresh = Create("Overlay.Refresh (whole view)");

        public static Section Create(string name)
        {
            var section = new Section(name);
            lock (Sections) Sections.Add(section);
            return section;
        }

        /// <summary>The section for one kind of condition, such as "Condition.WithRaycast". Used by Condition.Run.</summary>
        public static Section ForCondition(Condition condition)
        {
            Type type = condition.GetType();
            if (!ConditionSections.TryGetValue(type, out Section section))
            {
                section = Create("Condition." + type.Name);
                ConditionSections[type] = section;
            }
            return section;
        }

        public static void ResetAll()
        {
            lock (Sections)
            {
                foreach (Section section in Sections) section.Reset();
            }
        }

        /// <summary>Everything measured so far, slowest total first.</summary>
        public static List<SectionStats> Snapshot()
        {
            var result = new List<SectionStats>();
            lock (Sections)
            {
                foreach (Section section in Sections)
                {
                    if (section.Calls > 0) result.Add(section.ToStats());
                }
            }
            result.Sort((a, b) => b.TotalMs.CompareTo(a.TotalMs));
            return result;
        }

        public static string Report()
        {
            var text = new StringBuilder();
            text.AppendLine("section | calls | avg ms | max ms | total ms");
            foreach (SectionStats s in Snapshot())
            {
                text.AppendLine($"{s.Name} | {s.Calls} | {s.AverageMs:0.000} | {s.MaxMs:0.000} | {s.TotalMs:0.0}");
            }
            return text.ToString();
        }

        public sealed class Section
        {
            public readonly string Name;
            internal long Calls;
            internal long TotalTicks;
            internal long MaxTicks;
            private readonly ProfilerMarker _marker;

            internal Section(string name)
            {
                Name = name;
                _marker = new ProfilerMarker(name);
            }

            /// <summary>Use as: using (SensorProfiler.Refresh.Measure()) { ... }</summary>
            public Scope Measure() => new Scope(this, _marker.Auto());

            internal void Add(long ticks)
            {
                Calls++;
                TotalTicks += ticks;
                if (ticks > MaxTicks) MaxTicks = ticks;
            }

            internal void Reset()
            {
                Calls = 0;
                TotalTicks = MaxTicks = 0;
            }

            internal SectionStats ToStats()
            {
                double msPerTick = 1000.0 / Stopwatch.Frequency;
                return new SectionStats
                {
                    Name = Name,
                    Calls = Calls,
                    TotalMs = TotalTicks * msPerTick,
                    AverageMs = Calls > 0 ? TotalTicks * msPerTick / Calls : 0.0,
                    MaxMs = MaxTicks * msPerTick,
                };
            }
        }

        public readonly struct Scope : IDisposable
        {
            private readonly Section _section;
            private readonly ProfilerMarker.AutoScope _auto;
            private readonly long _startTicks;
            private readonly bool _record;

            internal Scope(Section section, ProfilerMarker.AutoScope auto)
            {
                _section = section;
                _auto = auto;
                _record = Enabled;
                _startTicks = _record ? Stopwatch.GetTimestamp() : 0;
            }

            public void Dispose()
            {
                if (_record)
                {
                    _section.Add(Stopwatch.GetTimestamp() - _startTicks);
                }
                _auto.Dispose();
            }
        }
    }

    public struct SectionStats
    {
        public string Name;
        public long Calls;
        public double TotalMs;
        public double AverageMs;
        public double MaxMs;
    }
}
