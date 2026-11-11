using System;
using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// Passes when the target is inside ANY of its polygons on the X/Z plane (their union). Each polygon has 3 or more points in
    /// order around the shape, can be concave, and its last point joins back to the first. A polygon with fewer than 3 points is ignored.
    /// The number of polygons and of points can change at any time; the next refresh uses whatever is there.
    /// </summary>
    [Serializable, ConditionMenu("Area (XZ)/In any polygon")]
    public class InPolygonArea : AreaCondition
    {
        [Tooltip("The target passes when it is inside ANY of these polygons (their union). Each needs 3 or more points in order around its shape.")]
        [ShowIf(nameof(DefineInInspector))]
        public List<AreaPolygon> Polygons = new List<AreaPolygon>
        {
            new AreaPolygon(new[] { new Vector2(-5f, -5f), new Vector2(5f, -5f), new Vector2(5f, 5f), new Vector2(-5f, 5f) }, "Polygon 1"),
        };

        public InPolygonArea() { VisualColor = new Color(0.2f, 0.8f, 1f); }

        public override string DisplayName => "In any polygon";

        // All polygons of the current refresh in world X/Z, one after another. Each has a start, a length and a box around it,
        // and there is one box around them all, so most targets are turned away with four comparisons.
        [NonSerialized] private Vector2[] _points = Array.Empty<Vector2>();
        [NonSerialized] private int[] _starts = Array.Empty<int>();
        [NonSerialized] private int[] _lengths = Array.Empty<int>();
        [NonSerialized] private Vector2[] _mins = Array.Empty<Vector2>();
        [NonSerialized] private Vector2[] _maxes = Array.Empty<Vector2>();
        [NonSerialized] private int _shapeCount;
        [NonSerialized] private int _pointCount;
        [NonSerialized] private Vector2 _min;
        [NonSerialized] private Vector2 _max;

        public override string Summary
        {
            get
            {
                if (!DefineInInspector) return $"Polygons {FromCodeSummary}";

                int total = Polygons?.Count ?? 0;
                int usable = 0;
                if (Polygons != null)
                {
                    foreach (AreaPolygon polygon in Polygons)
                    {
                        if ((polygon?.Points?.Count ?? 0) >= 3) usable++;
                    }
                }

                string count = total == 1 ? "1 polygon" : $"{total} polygons, inside any";
                string ignored = usable < total ? $" ({total - usable} ignored: fewer than 3 points)" : "";
                return $"{NameSummary}{count}{ignored}, {SpaceSummary}";
            }
        }

        // ---------- Changing this condition's own polygons (Define In Inspector on) ----------

        /// <summary>Makes this condition exactly one polygon. Used from the next refresh, while Define In Inspector is on. Returns its label.</summary>
        public string SetPolygon(IEnumerable<Vector2> points)
        {
            ClearPolygons();
            return AddPolygon(points);
        }

        /// <summary>Makes this condition exactly one polygon. Returns its label.</summary>
        public string SetPolygon(params Vector2[] points) => SetPolygon((IEnumerable<Vector2>)points);

        /// <summary>Adds another polygon. The target passes when it is inside any of them. Returns its label ("Polygon 2", the first free number).</summary>
        public string AddPolygon(IEnumerable<Vector2> points) => AddPolygon("", points);

        /// <summary>Adds another polygon. Returns its label.</summary>
        public string AddPolygon(params Vector2[] points) => AddPolygon((IEnumerable<Vector2>)points);

        /// <summary>Replaces all polygons with these.</summary>
        public void SetPolygons(IEnumerable<IEnumerable<Vector2>> polygons)
        {
            ClearPolygons();
            if (polygons == null) return;
            foreach (IEnumerable<Vector2> polygon in polygons)
            {
                AddPolygon(polygon);
            }
        }

        /// <summary>Removes one polygon by its index in the list (0 is the first). The ones after it move down by one. False when there is none there.</summary>
        public bool RemovePolygonAt(int index)
        {
            if (Polygons == null || index < 0 || index >= Polygons.Count) return false;

            Polygons.RemoveAt(index);
            return true;
        }

        // ---------- By label (the Label field of each polygon in the list) ----------
        // Labels are unique in this condition. Added from code, a taken label becomes "label (2)". Typed in the inspector, a copy is
        // flagged there and renamed in the running copy when the game starts (see AreaCondition.MakeLabelsUnique).

        /// <summary>Adds another polygon with this label, or "label (2)" when it is taken. Returns the label used.</summary>
        public string AddPolygon(string label, IEnumerable<Vector2> points)
        {
            Polygons ??= new List<AreaPolygon>();
            label = AreaLabels.Unique(label, ShapeKind, Polygons.Count, this, static (self, l) => self.IndexOfPolygon(l) >= 0);
            Polygons.Add(new AreaPolygon(points, label));
            return label;
        }

        /// <summary>Adds another polygon with this label, or "label (2)" when it is taken. Returns the label used.</summary>
        public string AddPolygon(string label, params Vector2[] points) => AddPolygon(label, (IEnumerable<Vector2>)points);

        /// <summary>The polygon with this label, null when there is none. Its Points can be changed directly.</summary>
        public AreaPolygon FindPolygon(string label)
        {
            int index = IndexOfPolygon(label);
            return index < 0 ? null : Polygons[index];
        }

        /// <summary>The index of the polygon with this label, -1 when there is none.</summary>
        public int IndexOfPolygon(string label)
        {
            if (Polygons == null || string.IsNullOrEmpty(label)) return -1;
            for (int i = 0; i < Polygons.Count; i++)
            {
                if (Polygons[i] != null && Polygons[i].Label == label) return i;
            }
            return -1;
        }

        /// <summary>Changes the points of the polygon with this label, keeping its place, or adds it when there is none. An empty label does nothing.</summary>
        public void UpdatePolygon(string label, IEnumerable<Vector2> points)
        {
            if (string.IsNullOrWhiteSpace(label)) return;

            AreaPolygon polygon = FindPolygon(label);
            if (polygon == null)
            {
                AddPolygon(label, points);
                return;
            }
            polygon.Points ??= new List<Vector2>();
            polygon.Points.Clear();
            if (points != null) polygon.Points.AddRange(points);
        }

        /// <summary>Changes the points of the polygon with this label, or adds it.</summary>
        public void UpdatePolygon(string label, params Vector2[] points) => UpdatePolygon(label, (IEnumerable<Vector2>)points);

        /// <summary>Removes the polygon with this label. False when there is none.</summary>
        public bool RemovePolygon(string label) => RemovePolygonAt(IndexOfPolygon(label));

        /// <summary>Removes all polygons. Nothing passes until one is added.</summary>
        public void ClearPolygons()
        {
            Polygons ??= new List<AreaPolygon>();
            Polygons.Clear();
        }

        // ---------- Labels, for the checks in AreaCondition ----------

        protected override string ShapeKind => "Polygon";
        protected override int LabelCount => Polygons?.Count ?? 0;
        protected override string GetLabel(int index) => Polygons[index] == null ? null : Polygons[index].Label ?? "";
        protected override void SetLabel(int index, string label)
        {
            if (Polygons[index] != null) Polygons[index].Label = label;
        }

        // ---------- Evaluation ----------

        protected override bool PrepareArea(in Frame frame)
        {
            _shapeCount = 0;
            _pointCount = 0;
            _min = new Vector2(float.MaxValue, float.MaxValue);
            _max = new Vector2(float.MinValue, float.MinValue);

            if (DefineInInspector)
            {
                if (Polygons != null)
                {
                    foreach (AreaPolygon polygon in Polygons)
                    {
                        AddShape(frame, polygon?.Points);
                    }
                }
                return true;
            }

            SensorAreas.PolygonSet set = SensorAreas.GetPolygonSet(AreaName);
            if (set == null) return false;
            for (int i = 0; i < set.Count; i++)
            {
                AddShape(frame, set.Shapes[i]);
            }
            return true;
        }

        private void AddShape(in Frame frame, List<Vector2> points)
        {
            int length = points?.Count ?? 0;
            if (length < 3) return;

            EnsureCapacity(_shapeCount + 1, _pointCount + length);

            int start = _pointCount;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < length; i++)
            {
                Vector2 point = frame.ToWorld(points[i]);
                _points[start + i] = point;
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            _starts[_shapeCount] = start;
            _lengths[_shapeCount] = length;
            _mins[_shapeCount] = min;
            _maxes[_shapeCount] = max;
            _shapeCount++;
            _pointCount += length;
            _min = Vector2.Min(_min, min);
            _max = Vector2.Max(_max, max);
        }

        private void EnsureCapacity(int shapes, int points)
        {
            if (_points == null || _points.Length < points) Array.Resize(ref _points, Mathf.Max(points, (_points?.Length ?? 0) * 2));
            if (_starts == null || _starts.Length < shapes)
            {
                int size = Mathf.Max(shapes, (_starts?.Length ?? 0) * 2);
                Array.Resize(ref _starts, size);
                Array.Resize(ref _lengths, size);
                Array.Resize(ref _mins, size);
                Array.Resize(ref _maxes, size);
            }
        }

        public override bool Contains(Vector2 p)
        {
            if (_shapeCount == 0) return false;
            if (p.x < _min.x || p.x > _max.x || p.y < _min.y || p.y > _max.y) return false;

            for (int shape = 0; shape < _shapeCount; shape++)
            {
                Vector2 min = _mins[shape];
                Vector2 max = _maxes[shape];
                if (p.x < min.x || p.x > max.x || p.y < min.y || p.y > max.y) continue;

                if (IsInside(p, _starts[shape], _lengths[shape])) return true;
            }
            return false;
        }

        // Even-odd rule: count how many edges a line from the point to the right crosses.
        private bool IsInside(Vector2 p, int start, int length)
        {
            bool inside = false;
            int end = start + length;
            for (int i = start, j = end - 1; i < end; j = i++)
            {
                Vector2 a = _points[i];
                Vector2 b = _points[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        // ---------- Drawing ----------

        protected override void DrawArea(in Frame frame, float height)
        {
            if (DefineInInspector)
            {
                if (Polygons == null) return;
                foreach (AreaPolygon polygon in Polygons)
                {
                    DrawOutline(frame, polygon?.Points, height);
                }
                return;
            }

            SensorAreas.PolygonSet set = SensorAreas.GetPolygonSet(AreaName);
            if (set == null) return;
            for (int i = 0; i < set.Count; i++)
            {
                DrawOutline(frame, set.Shapes[i], height);
            }
        }

        private static void DrawOutline(in Frame frame, List<Vector2> points, float height)
        {
            int count = points?.Count ?? 0;
            if (count < 2) return;

            Vector3 previous = ToWorld3(frame, points[count - 1], height);
            for (int i = 0; i < count; i++)
            {
                Vector3 next = ToWorld3(frame, points[i], height);
                OverlayGizmoMask.DrawLine(previous, next);
                previous = next;
            }
        }

        private static Vector3 ToWorld3(in Frame frame, Vector2 point, float height)
        {
            Vector2 world = frame.ToWorld(point);
            return new Vector3(world.x, height, world.y);
        }
    }
}
