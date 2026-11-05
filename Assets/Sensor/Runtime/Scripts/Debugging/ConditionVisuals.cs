using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// What conditions draw for the targets of one sensor refresh, such as the raycast line from the sensor to a target.
    /// A condition adds shapes while it evaluates, through SensorContext.Visuals, which is null when the condition's "Show visual" is off. So a condition that draws nothing,
    /// or has it switched off, costs nothing.
    /// The sensor draws the shapes as gizmos every frame until the next refresh replaces them.
    /// </summary>
    public sealed class ConditionVisuals
    {
        private const int MaxShapes = 2000;

        private struct Shape
        {
            public bool IsCross;
            public Vector3 From;
            public Vector3 To;
            public float Size;
            public Color Color;
        }

        private readonly List<Shape> _shapes = new List<Shape>();

        public int Count => _shapes.Count;

        public void Clear()
        {
            _shapes.Clear();
        }

        /// <summary>A line, for example from the sensor to a target.</summary>
        public void Line(Vector3 from, Vector3 to, Color color)
        {
            if (_shapes.Count >= MaxShapes) return;
            _shapes.Add(new Shape { From = from, To = to, Color = color });
        }

        /// <summary>A small three-axis cross, to mark a point such as where a ray hit.</summary>
        public void Cross(Vector3 at, float size, Color color)
        {
            if (_shapes.Count >= MaxShapes) return;
            _shapes.Add(new Shape { IsCross = true, From = at, Size = size, Color = color });
        }

        /// <summary>Draws every shape. Call it from OnDrawGizmos. Lines stay out from behind the QA overlay.</summary>
        public void Draw()
        {
            foreach (Shape shape in _shapes)
            {
                Gizmos.color = shape.Color;
                if (!shape.IsCross)
                {
                    OverlayGizmoMask.DrawLine(shape.From, shape.To);
                    continue;
                }

                Vector3 at = shape.From;
                float half = shape.Size * 0.5f;
                OverlayGizmoMask.DrawLine(at - Vector3.right * half, at + Vector3.right * half);
                OverlayGizmoMask.DrawLine(at - Vector3.up * half, at + Vector3.up * half);
                OverlayGizmoMask.DrawLine(at - Vector3.forward * half, at + Vector3.forward * half);
            }
        }
    }
}
