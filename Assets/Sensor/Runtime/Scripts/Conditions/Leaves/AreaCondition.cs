using System;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// Base for flat areas on the ground. Everything is in X/Z: a Vector2's x is world X and its y is world Z, and height is ignored.
    /// The area is either fixed in the world (the default), or relative to the sensor, where it moves and turns with it (turning around Y only).
    /// The shape is set in the inspector, or, with "Define In Inspector" off, from code by name through SensorAreas.
    /// Any change, including a different number of points, is used from the next refresh.
    /// </summary>
    [Serializable]
    public abstract class AreaCondition : VisualCondition
    {
        [Tooltip("The area's name. With Define In Inspector off, the shape is the one set from code under this name with SensorAreas. With it on, the name is only for finding this condition with sensor.FindArea<T>(name).")]
        public string AreaName = "";

        [Tooltip("Off (default): the shape is set from code, with SensorAreas.SetPolygon / SetCircle and this Area Name, and every sensor using the name shares it. Until it is set the condition passes nothing. On: the shape is set here.")]
        public bool DefineInInspector;

        [Tooltip("Off: the points are world X/Z. On: they are relative to the sensor, X to its right and Y to its front, and the area moves and turns with it.")]
        public bool RelativeToSensor;

        private const float CrossSize = 0.5f;
        private const float MissingAreaWarningSeconds = 5f;

        // An area set from code may come a little after the sensor starts, so a missing one is only reported once it has been missing a while.
        [NonSerialized] private bool _isMissing;
        [NonSerialized] private float _missingSince;
        [NonSerialized] private bool _warnedMissing;

        /// <summary>How sensor-relative points become world X/Z: the sensor's X/Z position and its yaw.</summary>
        protected readonly struct Frame
        {
            public readonly bool Relative;
            public readonly Vector2 Origin;
            public readonly float Sin;
            public readonly float Cos;

            public Frame(bool relative, Vector3 sensorPosition, Quaternion sensorRotation)
            {
                Relative = relative;
                Origin = new Vector2(sensorPosition.x, sensorPosition.z);

                // The yaw from the forward direction. A sensor looking straight down uses its up direction instead.
                Vector3 forward = sensorRotation * Vector3.forward;
                var flat = new Vector2(forward.x, forward.z);
                if (flat.sqrMagnitude < 1e-8f)
                {
                    Vector3 up = sensorRotation * Vector3.up;
                    flat = new Vector2(up.x, up.z);
                }
                float length = flat.magnitude;
                Sin = length > 1e-4f ? flat.x / length : 0f;
                Cos = length > 1e-4f ? flat.y / length : 1f;
            }

            /// <summary>A point of the area in world X/Z.</summary>
            public Vector2 ToWorld(Vector2 point)
            {
                if (!Relative) return point;
                return Origin + new Vector2(point.x * Cos + point.y * Sin, -point.x * Sin + point.y * Cos);
            }
        }

        /// <summary>
        /// Called once per refresh with the sensor's position, before any target. Turn the area into world X/Z here.
        /// Return false when there is no shape to use (set from code and not set yet); Contains must then pass nothing.
        /// </summary>
        protected abstract bool PrepareArea(in Frame frame);

        /// <summary>Is this world X/Z point inside the area, as it was at the start of the last refresh?</summary>
        public abstract bool Contains(Vector2 worldXZ);

        /// <summary>Draws the area's outline at this height.</summary>
        protected abstract void DrawArea(in Frame frame, float height);

        public sealed override void Prepare(in SensorContext context)
        {
            bool hasShape = PrepareArea(new Frame(RelativeToSensor, context.SensorPosition, context.SensorRotation));
            if (hasShape || DefineInInspector)
            {
                _isMissing = false;
                return;
            }

            if (!_isMissing)
            {
                _isMissing = true;
                _missingSince = context.Now;
            }
            else if (!_warnedMissing && context.Now - _missingSince >= MissingAreaWarningSeconds)
            {
                _warnedMissing = true;
                Debug.LogWarning(string.IsNullOrEmpty(AreaName)
                    ? $"{DisplayName} is set to come from code but has no Area Name, so it passes nothing."
                    : $"{DisplayName}: no area named \"{AreaName}\" has been set with SensorAreas, so it passes nothing.");
            }
        }

        public sealed override bool Evaluate(IDetectable target, in SensorContext context)
        {
            Vector3 position = context.PositionOf(target);
            bool passed = Contains(new Vector2(position.x, position.z));

            // A cross on the target: full color inside the area, faint outside.
            VisualsFor(context)?.Cross(position, CrossSize, ColorOf(passed));
            return passed;
        }

        public override void DrawGizmos(Transform sensorTransform, Camera camera)
        {
            if (!ShowVisual || sensorTransform == null) return;

            // Drawn at the sensor's height, since the area itself has none.
            Vector3 position = sensorTransform.position;
            DrawArea(new Frame(RelativeToSensor, position, sensorTransform.rotation), position.y);
        }

        /// <summary>
        /// Why this area's name clashes with another area of the same kind in the tree, or null when it does not.
        /// Two areas that both read the shape set from code may share a name, since they mean the same shape. Any other pair
        /// with the same name is ambiguous: sensor.FindArea finds only the first, and the two shapes can silently differ.
        /// </summary>
        public static string FindNameClash(Condition root, AreaCondition area)
        {
            if (area == null || string.IsNullOrEmpty(area.AreaName)) return null;

            foreach (Condition node in ConditionTree.Flatten(root))
            {
                if (ReferenceEquals(node, area) || !(node is AreaCondition other) || other.GetType() != area.GetType()) continue;
                if (!string.Equals(other.AreaName, area.AreaName, StringComparison.Ordinal)) continue;
                if (!area.DefineInInspector && !other.DefineInInspector) continue;

                return area.DefineInInspector == other.DefineInInspector
                    ? $"Name \"{area.AreaName}\" is used by another {area.DisplayName}"
                    : $"Name \"{area.AreaName}\" is used by another {area.DisplayName} that is {(other.DefineInInspector ? "defined in the inspector" : "set from code")}";
            }
            return null;
        }

        // ---------- Shape labels (this condition's own list, used with Define In Inspector on) ----------

        /// <summary>"Polygon" or "Circle": the start of an automatic label.</summary>
        protected virtual string ShapeKind => "Shape";
        protected virtual int LabelCount => 0;
        /// <summary>The label of the shape at this index: "" when it has none, null when the list entry itself is empty.</summary>
        protected virtual string GetLabel(int index) => null;
        protected virtual void SetLabel(int index, string label) { }

        /// <summary>How many shapes this condition's own list has.</summary>
        public int ShapeCount => LabelCount;

        /// <summary>
        /// Why two of this condition's own shapes share a label, or null when they do not. Only checked with Define In Inspector on,
        /// since the list is not used otherwise. Shown in the inspector, and logged when the game starts.
        /// </summary>
        public string FindLabelClash()
        {
            if (!DefineInInspector) return null;

            int count = LabelCount;
            for (int i = 1; i < count; i++)
            {
                string label = GetLabel(i);
                if (string.IsNullOrWhiteSpace(label) || !IsTakenBefore(i, label)) continue;

                string copy = AreaLabels.Unique(label, ShapeKind, i, (this, i), static (s, l) => s.Item1.IsTakenByOther(s.Item2, l));
                return $"Label \"{label}\" is used by more than one {ShapeKind.ToLowerInvariant()}. The copy becomes \"{copy}\" when the game starts";
            }
            return null;
        }

        /// <summary>
        /// Renames the shapes from this index on whose label is empty or already used by an earlier one, so every label is unique.
        /// An empty one gets "Polygon 3" (its own number if free), a copy gets "Parking (2)", and a copy of an automatic label gets
        /// the next free number. The first of two keeps its label. Returns how many were renamed.
        /// The sensor does this to its running copy when it starts; the inspector does it to shapes as they are added to the list.
        /// </summary>
        public int MakeLabelsUnique(int start = 0)
        {
            int renamed = 0;
            int count = LabelCount;
            for (int i = Mathf.Max(0, start); i < count; i++)
            {
                string label = GetLabel(i);
                if (label == null) continue;
                if (!string.IsNullOrWhiteSpace(label) && !IsTakenBefore(i, label)) continue;

                SetLabel(i, AreaLabels.Unique(label, ShapeKind, i, (this, i), static (s, l) => s.Item1.IsTakenByOther(s.Item2, l)));
                renamed++;
            }
            return renamed;
        }

        private bool IsTakenBefore(int index, string label)
        {
            for (int j = 0; j < index; j++)
            {
                if (GetLabel(j) == label) return true;
            }
            return false;
        }

        private bool IsTakenByOther(int index, string label)
        {
            int count = LabelCount;
            for (int j = 0; j < count; j++)
            {
                if (j != index && GetLabel(j) == label) return true;
            }
            return false;
        }

        protected string NameSummary => string.IsNullOrEmpty(AreaName) ? "" : $"\"{AreaName}\", ";

        protected string SpaceSummary => RelativeToSensor ? "relative to sensor" : "world";

        protected string FromCodeSummary => string.IsNullOrEmpty(AreaName) ? "set from code, needs an Area Name" : $"set from code as \"{AreaName}\", {SpaceSummary}";
    }
}
