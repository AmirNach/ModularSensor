using System;
using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// Passes when the target is inside ANY of its circles on the X/Z plane (their union). Each circle is a center point and a
    /// radius, and its edge counts as inside. The number of circles can change at any time; the next refresh uses whatever is there.
    /// </summary>
    [Serializable, ConditionMenu("Area (XZ)/In any circle")]
    public class InCircleArea : AreaCondition
    {
        [Tooltip("The target passes when it is inside ANY of these circles (their union).")]
        [ShowIf(nameof(DefineInInspector))]
        public List<AreaCircle> Circles = new List<AreaCircle>
        {
            new AreaCircle(Vector2.zero, 5f, "Circle 1"),
        };

        public InCircleArea() { VisualColor = new Color(1f, 0.55f, 0.15f); }

        public override string DisplayName => "In any circle";

        private const int OutlineSegments = 48;

        // All circles of the current refresh in world X/Z.
        [NonSerialized] private Vector2[] _centers = Array.Empty<Vector2>();
        [NonSerialized] private float[] _radiiSquared = Array.Empty<float>();
        [NonSerialized] private int _count;

        public override string Summary
        {
            get
            {
                if (!DefineInInspector) return $"Circles {FromCodeSummary}";

                int count = Circles?.Count ?? 0;
                if (count == 1 && Circles[0] != null)
                {
                    AreaCircle circle = Circles[0];
                    return $"{NameSummary}radius {circle.Radius:0.#} m at ({circle.Center.x:0.#}, {circle.Center.y:0.#}), {SpaceSummary}";
                }
                return $"{NameSummary}{count} circles, inside any, {SpaceSummary}";
            }
        }

        // ---------- Changing this condition's own circles (Define In Inspector on) ----------

        /// <summary>Makes this condition exactly one circle. Used from the next refresh, while Define In Inspector is on. Returns its label.</summary>
        public string SetCircle(Vector2 center, float radius)
        {
            ClearCircles();
            return AddCircle(center, radius);
        }

        /// <summary>Adds another circle. The target passes when it is inside any of them. Returns its label ("Circle 2", the first free number).</summary>
        public string AddCircle(Vector2 center, float radius) => AddCircle("", center, radius);

        /// <summary>Replaces all circles with copies of these. Their labels are kept, except copies and empty ones, which are made unique.</summary>
        public void SetCircles(IEnumerable<AreaCircle> circles)
        {
            ClearCircles();
            if (circles == null) return;
            foreach (AreaCircle circle in circles)
            {
                if (circle != null) AddCircle(circle.Label, circle.Center, circle.Radius);
            }
        }

        /// <summary>Removes one circle by its index in the list (0 is the first). The ones after it move down by one. False when there is none there.</summary>
        public bool RemoveCircleAt(int index)
        {
            if (Circles == null || index < 0 || index >= Circles.Count) return false;

            Circles.RemoveAt(index);
            return true;
        }

        // ---------- By label (the Label field of each circle in the list) ----------
        // Labels are unique in this condition. Added from code, a taken label becomes "label (2)". Typed in the inspector, a copy is
        // flagged there and renamed in the running copy when the game starts (see AreaCondition.MakeLabelsUnique).

        /// <summary>Adds another circle with this label, or "label (2)" when it is taken. Returns the label used.</summary>
        public string AddCircle(string label, Vector2 center, float radius)
        {
            Circles ??= new List<AreaCircle>();
            return SensorAreas.AddUniqueCircle(Circles, label, center, radius);
        }

        /// <summary>The circle with this label, null when there is none. Its Center and Radius can be changed directly.</summary>
        public AreaCircle FindCircle(string label)
        {
            int index = IndexOfCircle(label);
            return index < 0 ? null : Circles[index];
        }

        /// <summary>The index of the circle with this label, -1 when there is none.</summary>
        public int IndexOfCircle(string label) => SensorAreas.IndexOfLabel(Circles, label);

        /// <summary>Changes the center and radius of the circle with this label, keeping its place, or adds it when there is none. An empty label does nothing.</summary>
        public void UpdateCircle(string label, Vector2 center, float radius)
        {
            if (string.IsNullOrWhiteSpace(label)) return;

            AreaCircle circle = FindCircle(label);
            if (circle == null)
            {
                AddCircle(label, center, radius);
                return;
            }
            circle.Center = center;
            circle.Radius = radius;
        }

        /// <summary>Removes the circle with this label. False when there is none.</summary>
        public bool RemoveCircle(string label) => RemoveCircleAt(IndexOfCircle(label));

        /// <summary>Removes all circles. Nothing passes until one is added.</summary>
        public void ClearCircles()
        {
            Circles ??= new List<AreaCircle>();
            Circles.Clear();
        }

        // ---------- Labels, for the checks in AreaCondition ----------

        protected override string ShapeKind => "Circle";
        protected override int LabelCount => Circles?.Count ?? 0;
        protected override string GetLabel(int index) => Circles[index] == null ? null : Circles[index].Label ?? "";
        protected override void SetLabel(int index, string label)
        {
            if (Circles[index] != null) Circles[index].Label = label;
        }

        // ---------- Evaluation ----------

        /// <summary>This condition's own circles, or the ones set from code. Null when set from code and not set yet.</summary>
        private List<AreaCircle> CurrentCircles => DefineInInspector ? Circles ?? new List<AreaCircle>() : SensorAreas.GetCircleList(AreaName);

        protected override bool PrepareArea(in Frame frame)
        {
            _count = 0;
            List<AreaCircle> circles = CurrentCircles;
            if (circles == null) return false;

            if (_centers == null || _centers.Length < circles.Count)
            {
                int size = Mathf.Max(circles.Count, (_centers?.Length ?? 0) * 2);
                Array.Resize(ref _centers, size);
                Array.Resize(ref _radiiSquared, size);
            }

            foreach (AreaCircle circle in circles)
            {
                if (circle == null) continue;

                float radius = Mathf.Max(0f, circle.Radius);
                _centers[_count] = frame.ToWorld(circle.Center);
                _radiiSquared[_count] = radius * radius;
                _count++;
            }
            return true;
        }

        public override bool Contains(Vector2 worldXZ)
        {
            for (int i = 0; i < _count; i++)
            {
                if ((worldXZ - _centers[i]).sqrMagnitude <= _radiiSquared[i]) return true;
            }
            return false;
        }

        // ---------- Drawing ----------

        protected override void DrawArea(in Frame frame, float height)
        {
            List<AreaCircle> circles = CurrentCircles;
            if (circles == null) return;

            foreach (AreaCircle circle in circles)
            {
                if (circle == null) continue;

                Vector2 center = frame.ToWorld(circle.Center);
                float radius = Mathf.Max(0f, circle.Radius);
                Vector3 previous = new Vector3(center.x + radius, height, center.y);
                for (int i = 1; i <= OutlineSegments; i++)
                {
                    float angle = i * (Mathf.PI * 2f / OutlineSegments);
                    var next = new Vector3(center.x + Mathf.Cos(angle) * radius, height, center.y + Mathf.Sin(angle) * radius);
                    OverlayGizmoMask.DrawLine(previous, next);
                    previous = next;
                }
            }
        }
    }
}
