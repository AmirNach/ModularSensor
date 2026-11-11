using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// Areas set from code, by name, for area conditions that have "Define In Inspector" off (the default). No reference to a
    /// sensor is needed, and every sensor whose condition uses that name sees the change from its next refresh.
    /// A name holds any number of polygons and any number of circles. A target passes when it is inside ANY of them (a union).
    /// All in X/Z: a Vector2's x is world X and its y is world Z (or right and front of the sensor when the condition is relative).
    /// <code>
    /// SensorAreas.SetPolygon("Zone A", points);     // Zone A is now exactly this one polygon
    /// SensorAreas.AddPolygon("Zone A", morePoints); // and this one too
    /// SensorAreas.SetCircle("Danger", new Vector2(10, 5), 8f);
    /// SensorAreas.AddCircle("Danger", new Vector2(-4, 2), 3f);
    /// </code>
    /// "In any polygon" reads the polygons of its name and "In any circle" the circles, so one name can hold both kinds.
    /// Points are copied, so changing your own list afterwards does nothing until you set it again.
    /// </summary>
    public static class SensorAreas
    {
        /// <summary>The polygons of one name. The inner lists are kept and refilled, so changing shapes often creates no garbage.</summary>
        internal sealed class PolygonSet
        {
            public readonly List<List<Vector2>> Shapes = new List<List<Vector2>>();
            public readonly List<string> Labels = new List<string>();
            public int Count;

            /// <summary>Adds a polygon and returns its label: the wanted one, or a unique one made from it.</summary>
            public string Add(IEnumerable<Vector2> points, string label = "")
            {
                label = AreaLabels.Unique(label, "Polygon", Count, this, static (set, l) => set.IndexOf(l) >= 0);

                if (Count == Shapes.Count)
                {
                    Shapes.Add(new List<Vector2>());
                    Labels.Add("");
                }
                Labels[Count] = label;
                List<Vector2> shape = Shapes[Count++];
                shape.Clear();
                if (points != null) shape.AddRange(points);
                return label;
            }

            /// <summary>The index of the first polygon with this label, -1 when none has it (or the label is empty).</summary>
            public int IndexOf(string label)
            {
                if (string.IsNullOrEmpty(label)) return -1;
                for (int i = 0; i < Count; i++)
                {
                    if (Labels[i] == label) return i;
                }
                return -1;
            }

            public void Replace(int index, IEnumerable<Vector2> points)
            {
                List<Vector2> shape = Shapes[index];
                shape.Clear();
                if (points != null) shape.AddRange(points);
            }

            public bool RemoveAt(int index)
            {
                if (index < 0 || index >= Count) return false;

                // The removed list goes to the end to be refilled later, so removing creates no garbage.
                List<Vector2> removed = Shapes[index];
                Shapes.RemoveAt(index);
                Shapes.Add(removed);
                Labels.RemoveAt(index);
                Labels.Add("");
                Count--;
                return true;
            }
        }

        private static readonly Dictionary<string, PolygonSet> Polygons = new Dictionary<string, PolygonSet>();
        private static readonly Dictionary<string, List<AreaCircle>> Circles = new Dictionary<string, List<AreaCircle>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clear();
        }

        // ---------- Polygons ----------

        /// <summary>
        /// Makes this name exactly one polygon: 3 or more points in order around the shape. Any earlier polygons of the name are dropped.
        /// Returns its label ("Polygon 1"), null when the name is empty.
        /// </summary>
        public static string SetPolygon(string name, IEnumerable<Vector2> points)
        {
            PolygonSet set = GetOrCreatePolygons(name);
            if (set == null) return null;
            set.Count = 0;
            return set.Add(points);
        }

        /// <summary>Makes this name exactly one polygon. Returns its label.</summary>
        public static string SetPolygon(string name, params Vector2[] points) => SetPolygon(name, (IEnumerable<Vector2>)points);

        /// <summary>
        /// Adds another polygon to this name. A target inside any of the name's polygons passes.
        /// Returns the label it was given ("Polygon 2", the first free number), null when the name is empty.
        /// </summary>
        public static string AddPolygon(string name, IEnumerable<Vector2> points) => GetOrCreatePolygons(name)?.Add(points);

        /// <summary>Adds another polygon to this name. Returns its label.</summary>
        public static string AddPolygon(string name, params Vector2[] points) => AddPolygon(name, (IEnumerable<Vector2>)points);

        /// <summary>Replaces all of this name's polygons with these. An empty list leaves the name set, with nothing inside.</summary>
        public static void SetPolygons(string name, IEnumerable<IEnumerable<Vector2>> polygons)
        {
            PolygonSet set = GetOrCreatePolygons(name);
            if (set == null) return;
            set.Count = 0;
            if (polygons == null) return;
            foreach (IEnumerable<Vector2> polygon in polygons)
            {
                set.Add(polygon);
            }
        }

        /// <summary>
        /// Removes one polygon, by its index in the order it was added (0 is the first). The ones after it move down by one.
        /// False when there is no polygon at that index. Removing the last one leaves the name set, with nothing inside.
        /// </summary>
        public static bool RemovePolygonAt(string name, int index) => GetPolygonSet(name)?.RemoveAt(index) ?? false;

        // ---------- Polygons by label ----------
        // A label is the name of one shape inside an area, so it can be changed or removed later without knowing its index.
        // Labels are unique inside an area: a shape added without one gets "Polygon 2" / "Circle 2", and one whose label is
        // already taken gets "Parking (2)". The Add and Set methods return the label actually used.

        /// <summary>Adds another polygon to this name with this label, or "label (2)" when it is taken. Returns the label used.</summary>
        public static string AddPolygon(string name, string label, IEnumerable<Vector2> points) => GetOrCreatePolygons(name)?.Add(points, label);

        /// <summary>Adds another polygon to this name with this label, or "label (2)" when it is taken. Returns the label used.</summary>
        public static string AddPolygon(string name, string label, params Vector2[] points) => AddPolygon(name, label, (IEnumerable<Vector2>)points);

        /// <summary>
        /// Changes the points of the polygon with this label, keeping its place. When the name has no polygon with this label, it is added.
        /// An empty label does nothing.
        /// </summary>
        public static void UpdatePolygon(string name, string label, IEnumerable<Vector2> points)
        {
            if (string.IsNullOrWhiteSpace(label)) return;
            PolygonSet set = GetOrCreatePolygons(name);
            if (set == null) return;

            int index = set.IndexOf(label);
            if (index < 0) set.Add(points, label);
            else set.Replace(index, points);
        }

        /// <summary>Changes the points of the polygon with this label, or adds it.</summary>
        public static void UpdatePolygon(string name, string label, params Vector2[] points) => UpdatePolygon(name, label, (IEnumerable<Vector2>)points);

        /// <summary>Removes the polygon with this label. False when the name has none with it.</summary>
        public static bool RemovePolygon(string name, string label)
        {
            PolygonSet set = GetPolygonSet(name);
            return set != null && set.RemoveAt(set.IndexOf(label));
        }

        /// <summary>The index of the polygon with this label, -1 when there is none.</summary>
        public static int IndexOfPolygon(string name, string label) => GetPolygonSet(name)?.IndexOf(label) ?? -1;

        /// <summary>A copy of the points of the polygon with this label.</summary>
        public static bool TryGetPolygon(string name, string label, List<Vector2> pointsOut) => TryGetPolygon(name, IndexOfPolygon(name, label), pointsOut);

        /// <summary>Removes this name's polygons. "In any polygon" conditions using it pass nothing until it is set again.</summary>
        public static void RemovePolygons(string name)
        {
            if (!string.IsNullOrEmpty(name)) Polygons.Remove(name);
        }

        public static bool HasPolygons(string name) => !string.IsNullOrEmpty(name) && Polygons.ContainsKey(name);

        /// <summary>How many polygons this name has, 0 when it has none.</summary>
        public static int GetPolygonCount(string name) => GetPolygonSet(name)?.Count ?? 0;

        /// <summary>A copy of one of this name's polygons.</summary>
        public static bool TryGetPolygon(string name, int index, List<Vector2> pointsOut)
        {
            PolygonSet set = GetPolygonSet(name);
            if (set == null || index < 0 || index >= set.Count) return false;

            pointsOut.Clear();
            pointsOut.AddRange(set.Shapes[index]);
            return true;
        }

        // ---------- Circles ----------

        /// <summary>Makes this name exactly one circle. Any earlier circles of the name are dropped. Returns its label ("Circle 1").</summary>
        public static string SetCircle(string name, Vector2 center, float radius)
        {
            List<AreaCircle> circles = GetOrCreateCircles(name);
            if (circles == null) return null;
            circles.Clear();
            return AddUniqueCircle(circles, "", center, radius);
        }

        /// <summary>
        /// Adds another circle to this name. A target inside any of the name's circles passes.
        /// Returns the label it was given ("Circle 2", the first free number), null when the name is empty.
        /// </summary>
        public static string AddCircle(string name, Vector2 center, float radius)
        {
            List<AreaCircle> circles = GetOrCreateCircles(name);
            return circles == null ? null : AddUniqueCircle(circles, "", center, radius);
        }

        /// <summary>
        /// Replaces all of this name's circles with copies of these. An empty list leaves the name set, with nothing inside.
        /// Their labels are kept, except copies and empty ones, which are made unique.
        /// </summary>
        public static void SetCircles(string name, IEnumerable<AreaCircle> circles)
        {
            List<AreaCircle> stored = GetOrCreateCircles(name);
            if (stored == null) return;
            stored.Clear();
            if (circles == null) return;
            foreach (AreaCircle circle in circles)
            {
                if (circle != null) AddUniqueCircle(stored, circle.Label, circle.Center, circle.Radius);
            }
        }

        /// <summary>Adds a circle with this label, or a unique one made from it, and returns the label used. Shared with "In any circle".</summary>
        internal static string AddUniqueCircle(List<AreaCircle> circles, string label, Vector2 center, float radius)
        {
            label = AreaLabels.Unique(label, "Circle", circles.Count, circles, static (list, l) => IndexOfLabel(list, l) >= 0);
            circles.Add(new AreaCircle(center, radius, label));
            return label;
        }

        /// <summary>
        /// Removes one circle, by its index in the order it was added (0 is the first). The ones after it move down by one.
        /// False when there is no circle at that index. Removing the last one leaves the name set, with nothing inside.
        /// </summary>
        public static bool RemoveCircleAt(string name, int index)
        {
            List<AreaCircle> circles = GetCircleList(name);
            if (circles == null || index < 0 || index >= circles.Count) return false;

            circles.RemoveAt(index);
            return true;
        }

        // ---------- Circles by label ----------

        /// <summary>Adds another circle to this name with this label, or "label (2)" when it is taken. Returns the label used.</summary>
        public static string AddCircle(string name, string label, Vector2 center, float radius)
        {
            List<AreaCircle> circles = GetOrCreateCircles(name);
            return circles == null ? null : AddUniqueCircle(circles, label, center, radius);
        }

        /// <summary>
        /// Changes the center and radius of the circle with this label, keeping its place. When the name has no circle with this label,
        /// it is added. An empty label does nothing.
        /// </summary>
        public static void UpdateCircle(string name, string label, Vector2 center, float radius)
        {
            if (string.IsNullOrWhiteSpace(label)) return;
            List<AreaCircle> circles = GetOrCreateCircles(name);
            if (circles == null) return;

            int index = IndexOfLabel(circles, label);
            if (index < 0)
            {
                circles.Add(new AreaCircle(center, radius, label));
                return;
            }
            circles[index].Center = center;
            circles[index].Radius = radius;
        }

        /// <summary>Changes only the radius of the circle with this label. False when the name has none with it.</summary>
        public static bool SetCircleRadius(string name, string label, float radius)
        {
            List<AreaCircle> circles = GetCircleList(name);
            int index = IndexOfLabel(circles, label);
            if (index < 0) return false;

            circles[index].Radius = radius;
            return true;
        }

        /// <summary>Moves the circle with this label. False when the name has none with it.</summary>
        public static bool SetCircleCenter(string name, string label, Vector2 center)
        {
            List<AreaCircle> circles = GetCircleList(name);
            int index = IndexOfLabel(circles, label);
            if (index < 0) return false;

            circles[index].Center = center;
            return true;
        }

        /// <summary>Removes the circle with this label. False when the name has none with it.</summary>
        public static bool RemoveCircle(string name, string label) => RemoveCircleAt(name, IndexOfCircle(name, label));

        /// <summary>The index of the circle with this label, -1 when there is none.</summary>
        public static int IndexOfCircle(string name, string label) => IndexOfLabel(GetCircleList(name), label);

        /// <summary>The center and radius of the circle with this label.</summary>
        public static bool TryGetCircle(string name, string label, out Vector2 center, out float radius) =>
            TryGetCircle(name, IndexOfCircle(name, label), out center, out radius);

        /// <summary>The index of the first circle with this label, -1 when none has it (or the label is empty). Shared with "In any circle".</summary>
        internal static int IndexOfLabel(List<AreaCircle> circles, string label)
        {
            if (circles == null || string.IsNullOrEmpty(label)) return -1;
            for (int i = 0; i < circles.Count; i++)
            {
                if (circles[i] != null && circles[i].Label == label) return i;
            }
            return -1;
        }

        /// <summary>Removes this name's circles. "In any circle" conditions using it pass nothing until it is set again.</summary>
        public static void RemoveCircles(string name)
        {
            if (!string.IsNullOrEmpty(name)) Circles.Remove(name);
        }

        public static bool HasCircles(string name) => !string.IsNullOrEmpty(name) && Circles.ContainsKey(name);

        /// <summary>How many circles this name has, 0 when it has none.</summary>
        public static int GetCircleCount(string name) => GetCircleList(name)?.Count ?? 0;

        public static bool TryGetCircle(string name, int index, out Vector2 center, out float radius)
        {
            List<AreaCircle> circles = GetCircleList(name);
            if (circles == null || index < 0 || index >= circles.Count)
            {
                center = default;
                radius = 0f;
                return false;
            }
            center = circles[index].Center;
            radius = circles[index].Radius;
            return true;
        }

        // ---------- Both ----------

        /// <summary>Removes this name's polygons and circles.</summary>
        public static void Remove(string name)
        {
            RemovePolygons(name);
            RemoveCircles(name);
        }

        public static void Clear()
        {
            Polygons.Clear();
            Circles.Clear();
        }

        /// <summary>The stored set itself, for the conditions to read without copying. Null when the name has no polygons.</summary>
        internal static PolygonSet GetPolygonSet(string name)
        {
            return !string.IsNullOrEmpty(name) && Polygons.TryGetValue(name, out PolygonSet set) ? set : null;
        }

        /// <summary>The stored list itself, for the conditions to read without copying. Null when the name has no circles.</summary>
        internal static List<AreaCircle> GetCircleList(string name)
        {
            return !string.IsNullOrEmpty(name) && Circles.TryGetValue(name, out List<AreaCircle> circles) ? circles : null;
        }

        private static PolygonSet GetOrCreatePolygons(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (!Polygons.TryGetValue(name, out PolygonSet set))
            {
                set = new PolygonSet();
                Polygons[name] = set;
            }
            return set;
        }

        private static List<AreaCircle> GetOrCreateCircles(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (!Circles.TryGetValue(name, out List<AreaCircle> circles))
            {
                circles = new List<AreaCircle>();
                Circles[name] = circles;
            }
            return circles;
        }
    }
}
